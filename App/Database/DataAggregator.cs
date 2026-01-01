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
        if (string.IsNullOrWhiteSpace(condition)) 
            return data;

        try
        {
            var (columnName, op, value) = ParseCondition(condition);
            
            return data.Where(row => EvaluateCondition(row, columnName, op, value)).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to apply filter condition '{condition}': {ex.Message}", ex);
        }
    }

    private static (string columnName, string op, string value) ParseCondition(string condition)
    {
        var trimmedCondition = condition.Trim();
        
        // Supported operators (ordered by length to match longest first)
        var operators = new[] { ">=", "<=", "<>", "!=", ">", "<", "=", "LIKE", "IN" };
        
        foreach (var op in operators)
        {
            int opIndex = trimmedCondition.IndexOf(op, StringComparison.OrdinalIgnoreCase);
            if (opIndex > 0)
            {
                string columnName = trimmedCondition.Substring(0, opIndex).Trim();
                string value = trimmedCondition.Substring(opIndex + op.Length).Trim();
                
                // Validate column name (not empty, no special chars)
                if (string.IsNullOrWhiteSpace(columnName))
                    throw new ArgumentException("Column name cannot be empty");
                
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Filter value cannot be empty");
                
                return (columnName, op.ToUpperInvariant(), value);
            }
        }
        
        throw new ArgumentException($"Condition '{condition}' does not contain a valid operator. Supported: =, <>, !=, >, <, >=, <=, LIKE, IN");
    }

    private bool EvaluateCondition(Dictionary<string, object> row, string columnName, string op, string value)
    {
        if (!row.TryGetValue(columnName, out var rowValue))
        {
            return false; // Column not found - exclude row
        }

        return op switch
        {
            "=" => CompareEqual(rowValue, value),
            "<>" or "!=" => !CompareEqual(rowValue, value),
            ">" => CompareGreater(rowValue, value),
            "<" => CompareLess(rowValue, value),
            ">=" => CompareGreater(rowValue, value) || CompareEqual(rowValue, value),
            "<=" => CompareLess(rowValue, value) || CompareEqual(rowValue, value),
            "LIKE" => CompareLike(rowValue, value),
            "IN" => CompareIn(rowValue, value),
            _ => throw new NotSupportedException($"Operator '{op}' is not supported")
        };
    }

    private static bool CompareEqual(object rowValue, string filterValue)
    {
        if (rowValue == null)
            return filterValue == "NULL" || filterValue == "null";

        // Try numeric comparison
        if (decimal.TryParse(rowValue.ToString(), out decimal rowNum) &&
            decimal.TryParse(filterValue, out decimal filterNum))
        {
            return rowNum == filterNum;
        }

        // String comparison (case-insensitive)
        return rowValue.ToString()!.Equals(filterValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareGreater(object rowValue, string filterValue)
    {
        if (rowValue == null)
            return false;

        if (decimal.TryParse(rowValue.ToString(), out decimal rowNum) &&
            decimal.TryParse(filterValue, out decimal filterNum))
        {
            return rowNum > filterNum;
        }

        // String comparison
        return string.Compare(rowValue.ToString(), filterValue, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static bool CompareLess(object rowValue, string filterValue)
    {
        if (rowValue == null)
            return false;

        if (decimal.TryParse(rowValue.ToString(), out decimal rowNum) &&
            decimal.TryParse(filterValue, out decimal filterNum))
        {
            return rowNum < filterNum;
        }

        // String comparison
        return string.Compare(rowValue.ToString(), filterValue, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool CompareLike(object rowValue, string pattern)
    {
        if (rowValue == null)
            return false;

        string rowStr = rowValue.ToString()!;
        
        // Simple LIKE implementation: % as wildcard
        // "test%" matches "test", "testing", "tested"
        // "%test" matches "test", "mytest"
        // "%test%" matches anything containing "test"
        
        bool startsWith = pattern.StartsWith("%");
        bool endsWith = pattern.EndsWith("%");
        
        // Remove leading and trailing %
        string cleanPattern = pattern.Trim('%');
        
        if (startsWith && endsWith)
        {
            // %pattern% - contains
            return rowStr.Contains(cleanPattern, StringComparison.OrdinalIgnoreCase);
        }
        else if (startsWith)
        {
            // %pattern - ends with
            return rowStr.EndsWith(cleanPattern, StringComparison.OrdinalIgnoreCase);
        }
        else if (endsWith)
        {
            // pattern% - starts with
            return rowStr.StartsWith(cleanPattern, StringComparison.OrdinalIgnoreCase);
        }
        
        // No wildcards - exact match
        return rowStr.Equals(cleanPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareIn(object rowValue, string valueList)
    {
        if (rowValue == null)
            return false;

        // Parse comma-separated list: "value1,value2,value3"
        var values = valueList.Split(',')
            .Select(v => v.Trim())
            .ToList();

        string rowStr = rowValue.ToString()!;

        // Try numeric comparison first
        if (decimal.TryParse(rowStr, out decimal rowNum))
        {
            foreach (var val in values)
            {
                if (decimal.TryParse(val, out decimal compareNum) && rowNum == compareNum)
                    return true;
            }
        }

        // String comparison
        return values.Any(val => rowStr.Equals(val, StringComparison.OrdinalIgnoreCase));
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