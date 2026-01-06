using SQLBlend.Config;
using SQLBlend.Config.Models.Config;
using SQLBlend.Database;
using SQLBlend.Terminal;

namespace SQLBlend;

internal class Program
{
    private static readonly string DefaultConfigsBaseDir = Directory.GetCurrentDirectory();
    private const string ConfigFileName = "config.json";

    static async Task Main(string[] args)
    {
        ProgressTracker? tracker = null;

        try
        {
            string configsBaseDir = args.Length > 0 ? args[0] : DefaultConfigsBaseDir;
            string? configFolder = ConfigSelector.SelectConfigFolder(configsBaseDir);

            if (configFolder == null)
            {
                Console.WriteLine("Configuration selection cancelled.");
                return;
            }

            args = [Path.Combine(configFolder, ConfigFileName)];

            tracker = new ProgressTracker();
            tracker.Initialize();

            tracker.AddStep("Select Configuration");
            tracker.CompleteStep("Select Configuration");
            tracker.UpdateProgress(5, "Loading configuration...");

            tracker.AddStep("Load Configuration");
            tracker.StartStep("Load Configuration");
            var (config, resultsDir) = LoadConfiguration(args);
            tracker.CompleteStep("Load Configuration");
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
                    var resultFileName = Path.Combine(resultsDir, $"{queryConfig.Name}.csv");

                    if (File.Exists(resultFileName))
                    {
                        resultsByQueryName[queryConfig.Name] = CsvFileManager.ReadFromCsv(resultFileName);
                        tracker.CompleteStep(stepName, "Loaded from cache");
                    }
                    else
                    {
                        var sql = File.ReadAllText(queryConfig.QueryFilePath);

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
                    var resultFileName = Path.Combine(resultsDir, $"{filter.Name}.csv");

                    if (File.Exists(resultFileName))
                    {
                        resultsByQueryName[filter.Name] = CsvFileManager.ReadFromCsv(resultFileName);
                        tracker.CompleteStep(stepName, "Loaded from cache");
                    }
                    else
                    {
                        var aggregator = new DataAggregator(resultsByQueryName);
                        var aggregationResult = aggregator.ApplyOperations(filter.Operations);
                        resultsByQueryName[filter.Name] = aggregationResult;
                        CsvFileManager.SaveToCsv(aggregationResult, resultFileName);
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
            tracker.ShowCompletionMessage(completionMessage);
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

        return (config, resultsDir);
    }
}
