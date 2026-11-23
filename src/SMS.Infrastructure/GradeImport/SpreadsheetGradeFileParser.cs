using System.Text;
using ClosedXML.Excel;
using SMS.Application.GradeImport;

namespace SMS.Infrastructure.GradeImport;

/// <summary>Reads .xlsx (via ClosedXML) and .csv into a header row + data rows.
/// Modern replacement for the legacy Jet OLEDB 4.0 / "Excel 8.0" reader, which was 32-bit,
/// unreliable with .xlsx, and required hand-typed named ranges.</summary>
public class SpreadsheetGradeFileParser : IGradeFileParser
{
    public ParsedSheet Parse(Stream file, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ".csv" ? ParseCsv(file) : ParseXlsx(file);
    }

    static ParsedSheet ParseXlsx(Stream file)
    {
        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheets.First();
        var range = ws.RangeUsed();
        if (range is null) return new ParsedSheet(Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());

        var rows = range.RowsUsed().ToList();
        var headers = rows[0].Cells(1, range.ColumnCount()).Select(c => c.GetString().Trim()).ToList();
        var data = new List<IReadOnlyList<string>>();
        foreach (var r in rows.Skip(1))
            data.Add(r.Cells(1, range.ColumnCount()).Select(c => c.GetString()).ToList());
        return new ParsedSheet(headers, data);
    }

    static ParsedSheet ParseCsv(Stream file)
    {
        using var reader = new StreamReader(file, Encoding.UTF8);
        var lines = new List<List<string>>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 && reader.EndOfStream) break;
            lines.Add(SplitCsv(line));
        }
        if (lines.Count == 0) return new ParsedSheet(Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
        var headers = lines[0].Select(h => h.Trim()).ToList();
        var data = lines.Skip(1).Select(l => (IReadOnlyList<string>)l).ToList();
        return new ParsedSheet(headers, data);
    }

    /// <summary>Minimal RFC-4180-ish CSV splitter (handles quoted fields and escaped quotes).</summary>
    static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
