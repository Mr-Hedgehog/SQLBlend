namespace SQLBlend.Config.Models.Config;

public class OperationConfig
{
    /// <summary>
    /// Type of operation to be performed
    /// </summary>
    public OperationType Operation { get; set; }

    // Для Union и Filter
    /// <summary>
    /// Query names for operations that involve multiple queries
    /// Used for Union and Filter operations
    /// </summary>
    public List<string> QueryNames { get; set; }

    /// <summary>
    /// Condition for operation
    /// Used for Union and Filter operations
    /// </summary>
    public string Condition { get; set; }

    // Для Join:
    /// <summary>
    /// Name of the left query in a join operation
    /// Used for Join operation
    /// </summary>
    public string LeftQueryName { get; set; }

    /// <summary>
    /// Name of the right query in a join operation
    /// Used for Join operation
    /// </summary>
    public string RightQueryName { get; set; }

    /// <summary>
    /// List of join conditions for operations that involve joins
    /// Used for Join operation
    /// </summary>
    public List<JoinCondition> JoinConditions { get; set; }

    /// <summary>
    /// List of columns to select
    /// Used for Join operation
    /// </summary>
    public List<SelectColumn> SelectColumns { get; set; }
}
