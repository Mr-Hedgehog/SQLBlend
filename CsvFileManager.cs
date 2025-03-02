using System.Text;

namespace SQLBlend;

public static class CsvFileManager
{
    /// <summary>
    /// Saves a list of dictionary-based rows to a CSV file.
    /// The first row of the file contains column headers.
    /// </summary>
    /// <param name="data">List of rows, where each row is a Dictionary columnName -> value.</param>
    /// <param name="filePath">Path to the CSV file to be written.</param>
    public static void SaveToCsv(List<Dictionary<string, object>> data, string filePath)
    {
        using var writer = new StreamWriter(filePath);

        if (data.Count == 0)
        {
            return;
        }

        // Get column names from the first row
        var columns = data[0].Keys.ToList();

        // Write header
        writer.WriteLine(string.Join(";", columns));

        // Write rows
        foreach (var row in data)
        {
            var values = columns.Select(colName =>
            {
                var cellValue = row[colName]?.ToString() ?? string.Empty;

                // Escape if contains comma or quote
                if (cellValue.Contains(";") || cellValue.Contains("\""))
                {
                    cellValue = $"\"{cellValue.Replace("\"", "\"\"")}\"";
                }
                return cellValue;
            });

            writer.WriteLine(string.Join(";", values));
        }
    }

    /// <summary>
    /// Reads a CSV file into a list of dictionary-based rows.
    /// The first line is assumed to be the header line with column names.
    /// </summary>
    /// <param name="filePath">Path to the CSV file to be read.</param>
    /// <returns>A list of rows, each row is a Dictionary columnName -> string value.</returns>
    public static List<Dictionary<string, object>> ReadFromCsv(string filePath)
    {
        var result = new List<Dictionary<string, object>>();

        if (!File.Exists(filePath))
            return result; // File not found => return empty list

        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
            return result; // Empty file => return empty list

        // First line - column headers
        var headers = ParseCsvLine(lines[0]);

        // Data lines
        for (int i = 1; i < lines.Length; i++)
        {
            var rowValues = ParseCsvLine(lines[i]);
            var rowDict = new Dictionary<string, object>();

            for (int colIndex = 0; colIndex < headers.Count; colIndex++)
            {
                // If the row has fewer columns than the header, 
                // the missing columns will be null
                var value = colIndex < rowValues.Count ? rowValues[colIndex] : null;

                // In this example, we keep values as strings; 
                // you can add type conversions as needed.
                rowDict[headers[colIndex]] = value;
            }

            result.Add(rowDict);
        }

        return result;
    }

    /// <summary>
    /// Parses a single CSV line respecting quoted fields ("...").
    /// Delimiter is assumed to be ';'.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line))
            return result;

        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                // If quote not in quotes => start quotes
                // If in quotes, need to check if next char is also a quote => escaped quote
                if (!inQuotes)
                {
                    inQuotes = true;
                }
                else
                {
                    // check next char
                    if (i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        // Escaped quote
                        current.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
            }
            else if (c == ';' && !inQuotes)
            {
                // Delimiter outside quotes
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                // Regular character
                current.Append(c);
            }
        }

        // Add the last accumulated token
        result.Add(current.ToString());

        return result;
    }
}