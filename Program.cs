using SQLBlend.Config;
using SQLBlend.Config.Models.Config;
using SQLBlend.Database;

namespace SQLBlend;

internal class Program
{
    static async Task Main(string[] args)
    {
        var (config, resultsDir) = LoadConfiguration(args);

        var resultsByQueryName = new Dictionary<string, List<Dictionary<string, object>>>();
        var clients = new Dictionary<string, IDatabaseClient>();

        foreach (var connInfo in config.ConnectionStrings)
        {
            var client = new DatabaseClient(connInfo.ConnectionString, connInfo.Type);
            clients[connInfo.Name] = client;
        }

        foreach (var queryConfig in config.Queries)
        {
            var resultFileName = Path.Combine(resultsDir, $"{queryConfig.Name}.csv");

            if (File.Exists(resultFileName))
            {
                resultsByQueryName[queryConfig.Name] = CsvFileManager.ReadFromCsv(resultFileName);
                continue;
            }

            var sql = File.ReadAllText(queryConfig.QueryFilePath);

            // Substitute parameters if they exist
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

                    // Генерируем строку для подстановки
                    if (param.Format == QueryParameterFormatType.InClause)
                    {
                        string inClause = "(" + string.Join(",", values) + ")";
                        sql = sql.Replace($"@{param.Name}", inClause);
                    }

                    // Extend logic for additional parameter formats if needed
                }
            }

            var client = clients[queryConfig.DataSourceName];
            var queryResults = await client.ExecuteQueryAsync(sql);
            resultsByQueryName[queryConfig.Name] = queryResults;

            CsvFileManager.SaveToCsv(queryResults, resultFileName);
        }

        foreach (var filter in config.FiltersAndAggregations)
        {
            var resultFileName = Path.Combine(resultsDir, $"{filter.Name}.csv");

            if (File.Exists(resultFileName))
            {
                resultsByQueryName[filter.Name] = CsvFileManager.ReadFromCsv(resultFileName);
                continue;
            }

            var aggregator = new DataAggregator(resultsByQueryName);
            var aggregationResult = aggregator.ApplyOperations(filter.Operations);
            resultsByQueryName[filter.Name] = aggregationResult;
            CsvFileManager.SaveToCsv(aggregationResult, resultFileName);
        }
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
