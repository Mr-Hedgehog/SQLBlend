namespace SQLBlend.Config.Models.Config;

public class AppConfig
{
    /// <summary>
    /// Database connection strings
    /// </summary>
    public List<DbConnectionInfo> ConnectionStrings { get; set; }

    /// <summary>
    /// Queries configuration
    /// </summary>
    public List<QueryConfig> Queries { get; set; }

    /// <summary>
    /// Filter and aggregation configuration
    /// </summary>
    public List<FilterAggregationConfig> FiltersAndAggregations { get; set; }
}