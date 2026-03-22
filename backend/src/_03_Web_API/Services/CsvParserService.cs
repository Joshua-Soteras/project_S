// ============================================
// CsvParserService.cs
// ============================================
//
// RESPONSIBILITY:
// Reads a CSV file uploaded by a user, figures out what columns
// exist and what type of data they contain, then returns all of
// that as a structured object ready to be saved to the database.
//
// THIS CLASS DOES NOT TOUCH THE DATABASE.
// It only reads the file and returns data.
// The endpoint in Program.cs is responsible for saving.
//
// WHY A SEPARATE SERVICE AND NOT INLINE IN THE ENDPOINT?
// Parsing logic is complex enough to test independently.
// Keeping it here means we can write unit tests for ParseAsync()
// without needing a database or an HTTP request.
//
// WHERE THIS FITS:
//   IFormFile (HTTP request)
//       → CsvParserService.ParseAsync()   ← THIS FILE
//           → ParsedSurvey (structured data)
//               → Program.cs endpoint saves to DB
// ============================================

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ProjectS.Web.Services;

// ============================================
// RESULT TYPES
// ============================================
// These are simple immutable records that carry parsed data
// out of this service and into the endpoint.
//
// record = a C# type designed for holding data.
// It gives you equality, immutability, and a clean constructor
// without writing boilerplate. Equivalent to a DTO.
// ============================================

/// <summary>
/// Describes one column detected from the CSV header row.
/// </summary>
/// <param name="Name">The raw column header string from the CSV.</param>
/// <param name="Type">Detected data type: "text" | "numeric" | "date" | "boolean"</param>
/// <param name="Index">Zero-based position in the CSV (column 0, 1, 2...).</param>
/// <param name="AnalyzeSentiment">True if this column should be sent to the NLP service.</param>
public record ParsedColumn(string Name, string Type, int Index, bool AnalyzeSentiment);

/// <summary>
/// One cell value — the intersection of a row and a column.
/// </summary>
/// <param name="ColumnIndex">Which column this cell belongs to.</param>
/// <param name="Value">The raw string from the CSV. Nullable — cells can be empty.</param>
public record ParsedCell(int ColumnIndex, string? Value);

/// <summary>
/// One row from the CSV, containing all its cell values.
/// </summary>
/// <param name="RowIndex">Zero-based row number, preserving original CSV order.</param>
/// <param name="Cells">All cells in this row, one per column.</param>
public record ParsedRow(int RowIndex, List<ParsedCell> Cells);

/// <summary>
/// The full result of parsing a CSV file — columns and all rows.
/// This is what gets handed back to the endpoint to save to the DB.
/// </summary>
public record ParsedSurvey(List<ParsedColumn> Columns, List<ParsedRow> Rows);

// ============================================
// SERVICE
// ============================================
public class CsvParserService
{
    // How many non-empty values per column to inspect when detecting its type.
    // 20 is enough to be confident without reading the entire file first.
    private const int TypeDetectionSampleSize = 20;

    /// <summary>
    /// Parses an uploaded CSV file into a structured ParsedSurvey.
    /// Auto-detects column names from the header row and data types
    /// by sampling values in each column.
    /// </summary>
    /// <param name="file">The uploaded CSV file from the HTTP request.</param>
    /// <returns>A ParsedSurvey containing all columns and rows.</returns>
    public async Task<ParsedSurvey> ParseAsync(IFormFile file)
    {
        // ----------------------------------------
        // CSVHELPER CONFIGURATION
        // ----------------------------------------
        // CsvConfiguration controls how the parser behaves.
        //
        // HasHeaderRecord = true:
        //   The first row is column names, not data.
        //
        // MissingFieldFound = null:
        //   Don't throw an exception if a row has fewer columns
        //   than the header. Just return null for the missing cell.
        //
        // BadDataFound = null:
        //   Don't throw an exception on malformed input.
        //   Real-world CSVs are messy — this keeps the parser resilient.
        //
        // TrimOptions.Trim:
        //   Remove leading/trailing whitespace from every cell value.
        //   Prevents "  Yes  " and "Yes" from being treated differently.
        // ----------------------------------------
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, config);

        // ----------------------------------------
        // READ ALL ROWS INTO MEMORY
        // ----------------------------------------
        // We need to read the entire file before we can decide on
        // column types, because type detection requires sampling values
        // from each column across multiple rows.
        //
        // IFormFile gives us a stream that can only be read once,
        // so we collect everything upfront.
        //
        // TODO (Step 6 — Async Queue):
        // For very large CSVs this will be replaced with a streaming
        // approach where rows are saved to the DB in batches of 500
        // without loading the whole file into memory.
        // ----------------------------------------

        // Read the header row — this gives us the column names
        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        // rawRows[rowIndex][columnIndex] = the string value of that cell
        var rawRows = new List<string?[]>();

        while (await csv.ReadAsync())
        {
            var row = new string?[headers.Length];
            for (var i = 0; i < headers.Length; i++)
                row[i] = csv.GetField(i);

            rawRows.Add(row);
        }

        // ----------------------------------------
        // DETECT COLUMN TYPES
        // ----------------------------------------
        // For each column, collect a sample of non-empty values
        // and pass them to DetectColumnType().
        //
        // AnalyzeSentiment defaults to true for "text" columns —
        // those are the free-text responses we want the RoBERTa
        // model to evaluate. Numeric, date, and boolean columns
        // are skipped.
        // ----------------------------------------
        var columns = new List<ParsedColumn>();

        for (var i = 0; i < headers.Length; i++)
        {
            var samples = rawRows
                .Select(row => i < row.Length ? row[i] : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Take(TypeDetectionSampleSize);

            var type = DetectColumnType(samples);

            columns.Add(new ParsedColumn(
                Name: headers[i],
                Type: type,
                Index: i,
                AnalyzeSentiment: type == "text"
            ));
        }

        // ----------------------------------------
        // BUILD STRUCTURED ROWS
        // ----------------------------------------
        // Convert the raw string arrays into typed ParsedRow records
        // with explicit ColumnIndex references.
        // ----------------------------------------
        var rows = rawRows
            .Select((rawRow, rowIndex) =>
            {
                var cells = rawRow
                    .Select((value, colIndex) => new ParsedCell(colIndex, value))
                    .ToList();

                return new ParsedRow(rowIndex, cells);
            })
            .ToList();

        return new ParsedSurvey(columns, rows);
    }

    // ============================================
    // TYPE DETECTION
    // ============================================
    //
    // Inspects a sample of non-empty cell values from one column
    // and returns the most specific type that fits all of them.
    //
    // PRIORITY ORDER (most specific → least specific):
    //   numeric  → all values parse as a number (int or decimal)
    //   date     → all values parse as a date/datetime
    //   boolean  → all values are yes/no/true/false/1/0
    //   text     → fallback — anything that doesn't fit above
    //
    // WHY PRIORITY ORDER MATTERS:
    // "1" and "0" are valid numbers AND valid booleans. We check
    // numeric first so a column of 1s and 0s is treated as numeric,
    // not boolean, unless all values are in the boolean set.
    // ============================================
    private static string DetectColumnType(IEnumerable<string?> samples)
    {
        var values = samples.ToList();

        // No data — default to text
        if (values.Count == 0) return "text";

        // Numeric: every value parses as a double
        // InvariantCulture ensures "3.14" works regardless of OS locale
        if (values.All(v => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _)))
            return "numeric";

        // Date: every value parses as a DateTime
        if (values.All(v => DateTime.TryParse(v, out _)))
            return "date";

        // Boolean: every value is one of the known boolean representations
        var booleanValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "true", "false", "yes", "no", "1", "0" };

        if (values.All(v => booleanValues.Contains(v!)))
            return "boolean";

        // Default
        return "text";
    }
}
