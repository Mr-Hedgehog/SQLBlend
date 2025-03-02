using SQLBlend.Config.Models.Config;

namespace SQLBlend.Database;

public class DataAggregator(Dictionary<string, List<Dictionary<string, object>>> resultsByQueryName)
{
    private readonly Dictionary<string, List<Dictionary<string, object>>> resultsByQueryName = resultsByQueryName;

    public List<Dictionary<string, object>> ApplyOperations(List<OperationConfig> operations)
    {
        // Assuming we start with an empty list of data and apply each operation sequentially.
        // In practice, the first operations, such as Union, take data from multiple queries.
        // We can store intermediate state in currentData.
        var currentData = new List<Dictionary<string, object>>();

        foreach (var op in operations)
        {
            switch (op.Operation)
            {
                case OperationType.Union:
                    currentData = UnionOperation(op.QueryNames);
                    break;
                case OperationType.Filter:
                    currentData = FilterOperation(currentData, op.Condition);
                    break;
                case OperationType.InnerJoin:
                    currentData = JoinOperation(op, innerJoin: true);
                    break;
                case OperationType.LeftJoin:
                    currentData = JoinOperation(op, innerJoin: false);
                    break;
                default:
                    throw new NotSupportedException($"Operation {op.Operation} is not supported.");
            }
        }

        return currentData;
    }

    private List<Dictionary<string, object>> UnionOperation(List<string> queryNames)
    {
        var result = new List<Dictionary<string, object>>();

        foreach (var queryName in queryNames)
        {
            result.AddRange(resultsByQueryName[queryName]);
        }

        return result;
    }

    private List<Dictionary<string, object>> FilterOperation(List<Dictionary<string, object>> data, string condition)
    {
        // A simple implementation for example: assuming a condition of the form "Value > 100".
        // In reality, you would need a condition parser or predefined filters
        if (string.IsNullOrWhiteSpace(condition)) return data;

        // Let's assume the simplest logic:
        // Searching for the pattern "Value > N"
        var parts = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts[1] == ">" && int.TryParse(parts[2], out int threshold))
        {
            string colName = parts[0];
            return data.Where(row =>
            {
                if (row.TryGetValue(colName, out var val) && val is int vInt)
                    return vInt > threshold;
                return false;
            }).ToList();
        }

        // If the condition is not recognized, return the data as is
        return data;
    }

    private List<Dictionary<string, object>> JoinOperation(OperationConfig operation, bool innerJoin)
    {
        var leftData = resultsByQueryName[operation.LeftQueryName];
        var rightData = resultsByQueryName[operation.RightQueryName];

        var result = new List<Dictionary<string, object>>();

        foreach (var leftRow in leftData)
        {
            // Find all matching rows in the right table
            var matchingRightRows = rightData
                .Where(rightRow => RowMatchesConditions(leftRow, rightRow, operation.JoinConditions))
                .ToList();

            if (matchingRightRows.Count > 0)
            {
                // For each match, form the resulting row
                foreach (var rightRow in matchingRightRows)
                {
                    result.Add(SelectColumnsFromRows(leftRow, rightRow, operation.SelectColumns));
                }
            }
            else
            {
                // If it is a LeftJoin and there are no matches, add a row with null for the right columns
                if (!innerJoin)
                {
                    result.Add(SelectColumnsFromRows(leftRow, null, operation.SelectColumns));
                }
            }
        }

        return result;
    }

    private static bool RowMatchesConditions(Dictionary<string, object> leftRow, Dictionary<string, object> rightRow, List<JoinCondition> conditions)
    {
        foreach (var cond in conditions)
        {
            // For now, we only support the "=" operator
            if (cond.Operator != "=")
                throw new NotSupportedException($"Operator {cond.Operator} is not supported yet.");

            object leftVal = leftRow.ContainsKey(cond.LeftColumn) ? leftRow[cond.LeftColumn] : null;
            object rightVal = rightRow.ContainsKey(cond.RightColumn) ? rightRow[cond.RightColumn] : null;

            // Compare the values. For example, if they are null or types do not match, we consider them unequal
            if (!AreValuesEqual(leftVal, rightVal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool AreValuesEqual(object leftVal, object rightVal)
    {
        // Primitive comparison. You can expand the logic as needed
        if (leftVal == null && rightVal == null) return true;
        if (leftVal == null || rightVal == null) return false;

        return leftVal.Equals(rightVal);
    }

    private static Dictionary<string, object> SelectColumnsFromRows(
        Dictionary<string, object> leftRow,
        Dictionary<string, object> rightRow,
        List<SelectColumn> selectColumns)
    {
        var newRow = new Dictionary<string, object>();

        foreach (var sc in selectColumns)
        {
            if (sc.Query == SelectColumnSide.Left)
            {
                newRow[sc.Column] = leftRow.ContainsKey(sc.Column) ? leftRow[sc.Column] : null;
            }
            else if (sc.Query == SelectColumnSide.Right)
            {
                newRow[sc.Column] = (rightRow != null && rightRow.ContainsKey(sc.Column)) ? rightRow[sc.Column] : null;
            }
            else
            {
                throw new Exception($"Unknown query side: {sc.Query}");
            }
        }

        return newRow;
    }
}