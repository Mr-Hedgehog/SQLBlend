namespace SQLBlend.Config.Models.Config;

public class QueryParameterConfig
{
    /// <summary>
    /// Name of the parameter that will be substituted in the SQL query
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Query from which data will be retrieved
    /// </summary>
    public string FromQuery { get; set; }

    /// <summary>
    /// Column from which values will be taken
    /// </summary>
    public string Column { get; set; }

    /// <summary>
    /// Format for substitution
    /// </summary>
    public QueryParameterFormatType Format { get; set; }
}
