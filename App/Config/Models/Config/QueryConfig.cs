namespace SQLBlend.Config.Models.Config;

public class QueryConfig
{
    public string Name { get; set; }
    public string? OutputFileName { get; set; }
    public string DataSourceName { get; set; }
    public string QueryFilePath { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
    public List<QueryParameterConfig> Parameters { get; set; }
}
