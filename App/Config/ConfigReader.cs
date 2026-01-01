using SQLBlend.Config.Models.Config;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SQLBlend.Config;

public static class ConfigReader
{
    private static JsonSerializerOptions jsonOptions;

    static ConfigReader()
    {
        jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static AppConfig Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file was not found: {filePath}");
        }

        string json = File.ReadAllText(filePath);

        AppConfig config;

        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, jsonOptions);

            if (config == null)
            {
                throw new Exception("Failed to deserialize the configuration file");
            }
        }
        catch (JsonException ex)
        {
            throw new Exception("Error while parsing the JSON configuration file", ex);
        }

        ValidateConfig(config);
        return config;
    }

    private static void ValidateConfig(AppConfig config)
    {
        if (config.ConnectionStrings == null || config.ConnectionStrings.Count == 0)
        {
            throw new Exception("Сonnection strings are not specified in the configuration");
        }

        if (config.Queries == null || config.Queries.Count == 0)
        {
            throw new Exception("Queries are not specified in the configuration");
        }
    }
}
