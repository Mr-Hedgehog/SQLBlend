namespace SQLBlend.Config.Models.Config;

public class JoinCondition
{
    /// <summary>
    /// Column from the left table in the join operation
    /// </summary>
    public string LeftColumn { get; set; }

    /// <summary>
    /// Column from the right table in the join operation
    /// </summary>
    public string RightColumn { get; set; }

    /// <summary>
    /// Operator used for the join condition.
    /// Currently supports only "=" operation
    /// </summary>
    public string Operator { get; set; }
}
