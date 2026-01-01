namespace SQLBlend.Config.Models.Config;

public class QueryConfig
{
    public string Name { get; set; }
    public string DataSourceName { get; set; }
    public string QueryFilePath { get; set; }
    public List<QueryParameterConfig> Parameters { get; set; }
}
