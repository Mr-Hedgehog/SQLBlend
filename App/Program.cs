using SQLBlend.Config;
using SQLBlend.Config.Models.Config;
using SQLBlend.Database;
using SQLBlend.Terminal;

namespace SQLBlend;

internal class Program
{
    private static readonly string DefaultConfigsBaseDir = Directory.GetCurrentDirectory();
    private const string ConfigFileName = "config.json";
    private const string OverrideConfigFileName = "config.override.json";
    private const string EnableCacheFlag = "--cache";

    static async Task Main(string[] args)
    {
        ProgressTracker? tracker = null;

        try
        {
            bool disableCache = !args.Any(IsEnableCacheArg);
            var positionalArgs = args.Where(arg => !IsEnableCacheArg(arg)).ToArray();

            string configsBaseDir = positionalArgs.Length > 0 ? positionalArgs[0] : DefaultConfigsBaseDir;
            string? configFolder = ConfigSelector.SelectConfigFolder(configsBaseDir);

            if (configFolder == null)
            {
                Console.WriteLine("Configuration selection cancelled.");
                return;
            }

            var configArgs = new[] { Path.Combine(configFolder, ConfigFileName) };

            tracker = new ProgressTracker();
            tracker.Initialize();

            tracker.AddStep("Select Configuration");
            tracker.CompleteStep("Select Configuration");
            tracker.UpdateProgress(5, "Loading configuration...");

            tracker.AddStep("Load Configuration");
            tracker.StartStep("Load Configuration");
            var (config, resultsDir) = LoadConfiguration(configArgs);
            tracker.CompleteStep("Load Configuration");

            if (disableCache)
            {
                ClearResultsDirectory(resultsDir);
            }

            tracker.UpdateProgress(15, "Initializing database clients...");

            var resultsByQueryName = new Dictionary<string, List<Dictionary<string, object>>>();
            var clients = new Dictionary<string, IDatabaseClient>();

            tracker.AddStep("Initialize Database Clients");
            tracker.StartStep("Initialize Database Clients", $"{config.ConnectionStrings.Count} connections");
            foreach (var connInfo in config.ConnectionStrings)
            {
                var client = new DatabaseClient(connInfo.ConnectionString, connInfo.Type);
                clients[connInfo.Name] = client;
            }
            tracker.CompleteStep("Initialize Database Clients");

            int totalQueries = config.Queries.Count;
            int queryIndex = 0;
            int progressPerQuery = (100 - 30) / (totalQueries + config.FiltersAndAggregations.Count);

            tracker.UpdateProgress(20, "Executing queries...");

            foreach (var queryConfig in config.Queries)
            {
                queryIndex++;
                var stepName = $"Query: {queryConfig.Name}";
                tracker.AddStep(stepName);
                tracker.StartStep(stepName);

                try
                {
                    var resultFileName = GetResultFilePath(resultsDir, queryConfig.Name, queryConfig.OutputFileName);

                    if (!disableCache && File.Exists(resultFileName))
                    {
                        var cachedResults = CsvFileManager.ReadFromCsv(resultFileName);
                        resultsByQueryName[queryConfig.Name] = cachedResults;
                        tracker.CompleteStep(stepName, "Loaded from cache");
                    }
                    else
                    {
                        var sql = File.ReadAllText(queryConfig.QueryFilePath);
                        sql = ApplyQueryVariables(sql, queryConfig.Variables);

                        if (queryConfig.Parameters != null)
                        {
                            foreach (var param in queryConfig.Parameters)
                            {
                                var prevResults = resultsByQueryName[param.FromQuery];
                                var values = new List<object>();
                                foreach (var row in prevResults)
                                {
                                    if (row.ContainsKey(param.Column))
                                        values.Add(row[param.Column]);
                                }

                                if (param.Format == QueryParameterFormatType.InClause)
                                {
                                    string inClause = "(" + string.Join(",", values) + ")";
                                    sql = sql.Replace($"@{param.Name}", inClause);
                                }
                            }
                        }

                        var client = clients[queryConfig.DataSourceName];
                        var queryResults = await client.ExecuteQueryAsync(sql);
                        resultsByQueryName[queryConfig.Name] = queryResults;

                        CsvFileManager.SaveToCsv(queryResults, resultFileName);

                        tracker.CompleteStep(stepName, $"{queryResults.Count} rows");
                    }

                    int progress = 20 + (queryIndex * progressPerQuery);
                    tracker.UpdateProgress(progress, $"Processing query {queryIndex}/{totalQueries}...");
                }
                catch (Exception ex)
                {
                    tracker.FailStep(stepName, ex.Message);
                    throw;
                }
            }

            tracker.UpdateProgress(50, "Applying filters and aggregations...");

            int filterIndex = 0;
            foreach (var filter in config.FiltersAndAggregations)
            {
                filterIndex++;
                var stepName = $"Filter: {filter.Name}";
                tracker.AddStep(stepName);
                tracker.StartStep(stepName);

                try
                {
                    var outputExtension = GetAggregationOutputExtension(filter.OutputFormat);
                    var resultFileName = GetResultFilePath(resultsDir, filter.Name, filter.OutputFileName, outputExtension);

                    if (filter.OutputFormat == AggregationOutputFormatType.Csv && !disableCache && File.Exists(resultFileName))
                    {
                        var cachedResults = CsvFileManager.ReadFromCsv(resultFileName);
                        resultsByQueryName[filter.Name] = cachedResults;
                        tracker.CompleteStep(stepName, "Loaded from cache");
                    }
                    else
                    {
                        var aggregator = new DataAggregator(resultsByQueryName);
                        var aggregationResult = aggregator.ApplyOperations(filter.Operations);
                        resultsByQueryName[filter.Name] = aggregationResult;

                        if (filter.OutputFormat == AggregationOutputFormatType.Excel)
                        {
                            ExcelFileManager.SaveToExcel(aggregationResult, resultFileName);
                        }
                        else
                        {
                            CsvFileManager.SaveToCsv(aggregationResult, resultFileName);
                        }

                        tracker.CompleteStep(stepName, $"{aggregationResult.Count} rows");
                    }

                    int progress = 50 + (filterIndex * progressPerQuery);
                    tracker.UpdateProgress(progress, $"Processing filter {filterIndex}/{config.FiltersAndAggregations.Count}...");
                }
                catch (Exception ex)
                {
                    tracker.FailStep(stepName, ex.Message);
                    throw;
                }
            }

            tracker.UpdateProgress(100, "Complete!");

            var completionMessage = FormatCompletionMessage(tracker, resultsDir, resultsByQueryName);
            tracker.ShowCompletionMessage(completionMessage, resultsDir);
        }
        catch (Exception ex)
        {
            if (tracker != null)
            {
                var errorMessage = $"Error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                tracker.ShowCompletionMessage(errorMessage);
            }
            else
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        finally
        {
            // Даем время пользователю посмотреть результаты перед закрытием
            System.Threading.Thread.Sleep(500);
            tracker?.Dispose();
        }
    }

    private static string FormatCompletionMessage(ProgressTracker tracker, string resultsDir, Dictionary<string, List<Dictionary<string, object>>> results)
    {
        var steps = tracker.GetSteps();
        var completedSteps = steps.Count(s => s.Status == ProgressTracker.ProgressStepStatus.Completed);

        var message = new System.Text.StringBuilder();
        message.AppendLine("═══════════════════════════════════════════════════════════");
        message.AppendLine("                  EXECUTION COMPLETED                      ");
        message.AppendLine("═══════════════════════════════════════════════════════════\n");

        message.AppendLine($"Results saved to: {resultsDir}\n");

        message.AppendLine("EXECUTION STEPS:");
        message.AppendLine("───────────────────────────────────────────────────────────");

        foreach (var step in steps)
        {
            message.AppendLine(step.ToString());
        }

        message.AppendLine("\n───────────────────────────────────────────────────────────");
        message.AppendLine($"Total steps completed: {completedSteps}/{steps.Count}");
        message.AppendLine($"Total results generated: {results.Count}");
        message.AppendLine("═══════════════════════════════════════════════════════════");

        return message.ToString();
    }

    /// <summary>
    /// Loads configuration by determining the file path from the command-line arguments (if provided),
    /// reads the JSON config, creates the "Results" folder in the same directory, and returns both
    /// the AppConfig object and the path to the results folder.
    /// </summary>
    private static (AppConfig config, string resultsDir) LoadConfiguration(string[] args)
    {
        const string defaultConfigPath = "appsettings.json";
        string configPath = args.Length > 0 ? args[0] : defaultConfigPath;

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"Config file not found: {configPath}");
            throw new FileNotFoundException($"Config file not found at {configPath}");
        }

        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath));

        var resultsDir = Path.Combine(configDir, "Results");
        Directory.CreateDirectory(resultsDir);

        var config = ConfigReader.Read(configPath);
        var overrideConfigPath = Path.Combine(AppContext.BaseDirectory, OverrideConfigFileName);

        if (File.Exists(overrideConfigPath))
        {
            var overrideConfig = ConfigReader.ReadOverride(overrideConfigPath);
            ApplyConnectionStringOverrides(config, overrideConfig);
        }

        return (config, resultsDir);
    }

    private static bool IsEnableCacheArg(string arg)
    {
        return string.Equals(arg, EnableCacheFlag, StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearResultsDirectory(string resultsDir)
    {
        if (!Directory.Exists(resultsDir))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(resultsDir))
        {
            File.Delete(filePath);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(resultsDir))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static string GetResultFilePath(string resultsDir, string defaultName, string? outputFileName, string extension = ".csv")
    {
        var fileBaseName = string.IsNullOrWhiteSpace(outputFileName)
            ? defaultName
            : outputFileName;

        fileBaseName = Path.GetFileNameWithoutExtension(fileBaseName);

        if (string.IsNullOrWhiteSpace(fileBaseName))
        {
            throw new InvalidOperationException("Output file name cannot be empty.");
        }

        return Path.Combine(resultsDir, $"{fileBaseName}{extension}");
    }

    private static string GetAggregationOutputExtension(AggregationOutputFormatType outputFormat)
    {
        return outputFormat == AggregationOutputFormatType.Excel ? ".xlsx" : ".csv";
    }

    private static void ApplyConnectionStringOverrides(AppConfig config, AppConfigOverride overrideConfig)
    {
        if (overrideConfig.ConnectionStrings == null || overrideConfig.ConnectionStrings.Count == 0)
        {
            return;
        }

        var sourceConnections = config.ConnectionStrings.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var overrideConnection in overrideConfig.ConnectionStrings)
        {
            if (string.IsNullOrWhiteSpace(overrideConnection.Name))
            {
                continue;
            }

            if (!sourceConnections.TryGetValue(overrideConnection.Name, out var existingConnection))
            {
                throw new InvalidOperationException($"Connection string override references unknown source '{overrideConnection.Name}'.");
            }

            existingConnection.ConnectionString = overrideConnection.ConnectionString;
        }
    }

    private static string ApplyQueryVariables(string sql, Dictionary<string, string>? variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return sql;
        }

        foreach (var (name, value) in variables)
        {
            sql = sql.Replace($"{{{{{name}}}}}", value ?? string.Empty, StringComparison.Ordinal);
        }

        return sql;
    }
}
