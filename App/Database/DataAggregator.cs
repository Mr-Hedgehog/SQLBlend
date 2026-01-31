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

        // Validate that all join conditions use "=" operator (required for hash join)
        foreach (var cond in operation.JoinConditions)
        {
            if (cond.Operator != "=")
                throw new NotSupportedException($"Operator {cond.Operator} is not supported. Hash join only supports '=' operator.");
        }

        // Build hash index on right table - O(m)
        var rightIndex = BuildHashIndex(rightData, operation.JoinConditions, isLeft: false);

        var result = new List<Dictionary<string, object>>();

        // Probe with left table - O(n) with O(1) lookups
        foreach (var leftRow in leftData)
        {
            var key = BuildJoinKey(leftRow, operation.JoinConditions, isLeft: true);
            
            if (rightIndex.TryGetValue(key, out var matchingRightRows) && matchingRightRows.Count > 0)
            {
                // For each match, form the resulting row
                foreach (var rightRow in matchingRightRows)
                {
                    result.Add(SelectColumnsFromRows(leftRow, rightRow, operation.SelectColumns));
                }
            }
            else if (!innerJoin)
            {
                // LeftJoin: add row with null for the right columns
                result.Add(SelectColumnsFromRows(leftRow, null, operation.SelectColumns));
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a hash index for efficient join lookups. Complexity: O(n)
    /// </summary>
    private static Dictionary<string, List<Dictionary<string, object>>> BuildHashIndex(
        List<Dictionary<string, object>> data,
        List<JoinCondition> conditions,
        bool isLeft)
    {
        var index = new Dictionary<string, List<Dictionary<string, object>>>();

        foreach (var row in data)
        {
            var key = BuildJoinKey(row, conditions, isLeft);
            
            if (!index.TryGetValue(key, out var bucket))
            {
                bucket = new List<Dictionary<string, object>>();
                index[key] = bucket;
            }
            bucket.Add(row);
        }

        return index;
    }

    /// <summary>
    /// Builds a composite key from row values for join columns.
    /// Uses a separator that's unlikely to appear in data.
    /// </summary>
    private static string BuildJoinKey(Dictionary<string, object> row, List<JoinCondition> conditions, bool isLeft)
    {
        const string separator = "\x1F"; // Unit separator - unlikely in normal data
        const string nullMarker = "\x00NULL\x00";

        var keyParts = new string[conditions.Count];

        for (int i = 0; i < conditions.Count; i++)
        {
            var columnName = isLeft ? conditions[i].LeftColumn : conditions[i].RightColumn;
            
            if (row.TryGetValue(columnName, out var value) && value != null)
            {
                keyParts[i] = value.ToString() ?? nullMarker;
            }
            else
            {
                keyParts[i] = nullMarker;
            }
        }

        return string.Join(separator, keyParts);
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