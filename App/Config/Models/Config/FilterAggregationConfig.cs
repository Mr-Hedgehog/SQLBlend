namespace SQLBlend.Config.Models.Config;

public class FilterAggregationConfig
{
    public string Name { get; set; }
    public string? OutputFileName { get; set; }
    public AggregationOutputFormatType OutputFormat { get; set; } = AggregationOutputFormatType.Csv;
    public List<OperationConfig> Operations { get; set; }
}
