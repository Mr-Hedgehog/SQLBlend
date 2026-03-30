using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SQLBlend;

public static class ExcelFileManager
{
    public static void SaveToExcel(List<Dictionary<string, object>> data, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Results"
        });

        if (data.Count > 0)
        {
            var columns = data[0].Keys.ToList();

            var headerRow = new Row();
            foreach (var column in columns)
            {
                headerRow.Append(CreateTextCell(column));
            }
            sheetData.Append(headerRow);

            foreach (var row in data)
            {
                var excelRow = new Row();
                foreach (var column in columns)
                {
                    row.TryGetValue(column, out var rawValue);
                    var value = rawValue?.ToString() ?? string.Empty;
                    excelRow.Append(CreateTextCell(value));
                }

                sheetData.Append(excelRow);
            }
        }

        workbookPart.Workbook.Save();
    }

    private static Cell CreateTextCell(string value)
    {
        return new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })
        };
    }
}
