# Implementation Log

A running record of every step implemented in this project.
Each section covers what was built, what design decisions were applied from
the planning documents, what was intentionally deferred, and how to test it.

---

## Step 1 — Database Schema (Entities + Migration)

### What Was Built

**6 new EF Core entities** added to `_01_Core/Entities/`:

- `Survey.cs` — top-level record for an uploaded CSV file. Tracks the file
  location, processing status, who uploaded it, and timestamps.
- `SurveyColumn.cs` — stores the detected column layout for a specific CSV.
  Includes `AnalyzeSentiment` flag to mark which columns go to the NLP service.
- `SurveyResponse.cs` — one record per CSV row. Groups all cell values for
  that row together and preserves original row order via `RowIndex`.
- `ResponseValue.cs` — one record per cell (row × column intersection).
  Stores the raw string value from the CSV.
- `SentimentResult.cs` — stores the RoBERTa model output for a single text
  cell. One-to-one with `ResponseValue`. Holds label and three probability scores.
- `KpiAggregate.cs` — pre-computed sentiment summary per survey per column.
  Written by the NLP worker when processing completes. Dashboard reads from
  this instead of running live aggregations.

**`ApplicationDbContext.cs`** updated in `_02_Infrastructure/Data/`:
- Registered all 6 new entities as `DbSet<T>` properties using the standard
  `{ get; set; } = null!` pattern
- Configured Fluent API for every entity: column constraints, indexes,
  foreign key relationships, and cascade vs. restrict delete behavior
- Refactored to C# 12 primary constructor syntax

**Migration `AddSurveySchema`** generated and ready to apply.

### Applied From Planning Documents

**`_5_ARCHITECTURE.md` — Database Schema section:**
The entity structure directly implements the ER diagram designed in the
architecture doc. The relationship chain (`Survey → SurveyColumn → SurveyResponse
→ ResponseValue → SentimentResult`) and the `KpiAggregate` pre-computation
pattern were both defined there before a single line of code was written.

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Database Design for Scale:**
- `KpiAggregate` was added specifically because the scalability doc identified
  that running live `AVG()`/`COUNT()` queries across millions of rows for
  thousands of concurrent dashboard users would be unsustainable.
- Composite indexes (e.g. `SurveyId + RowIndex` on `SurveyResponse`,
  `SurveyId + ColumnId` unique on `KpiAggregate`) were added based on the
  query patterns identified in the scalability design.
- `DeleteBehavior.Restrict` vs `DeleteBehavior.Cascade` decisions follow the
  data safety principles outlined in the scalability doc — child data should
  not silently disappear when a parent is removed.

**`_9_WHY_THESE_ENTITIES.md`:**
The rationale for every entity was documented before implementation to ensure
each one had a clear, non-redundant purpose. This also serves as onboarding
material for new developers.

### What Was Intentionally Left Out

- No `UploadedBy` foreign key to a Users table yet — authentication (Azure
  Entra ID) is a Step 8 concern. The field is a plain string for now.
- No Azure Blob Storage URL yet — `BlobUrl` is a string field ready to receive
  a real URL in Step 8.
- `KpiAggregate` rows are not yet written by anything — the NLP worker that
  populates them is Step 3/4.

### How to Test

```bash
# Start the database
docker-compose up -d postgres

# Run the API (migrations apply automatically on startup)
cd backend/src/_03_Web_API && dotnet run

# Verify migrations were applied
docker exec -it projects_db psql -U projects_admin -d projects_db
\dt   # should show all new tables
```

---

## Step 2 — CSV Upload + Parsing Endpoint

### What Was Built

**`CsvParserService.cs`** added to `_03_Web_API/Services/`:

A standalone service responsible solely for reading a CSV file and returning
structured data. Intentionally has no database dependency — it only parses.
This separation means it can be unit tested without a database or HTTP request.

Two internal phases:
1. **Read phase** — streams the entire file into memory as raw string arrays,
   capturing the header row as column names and every subsequent row as cells.
2. **Type detection phase** — for each column, samples up to 20 non-empty
   values and tests them in priority order: numeric → date → boolean → text.
   Falls back to `"text"` if no more specific type fits. Any column typed as
   `"text"` is automatically flagged `AnalyzeSentiment = true`.

Result types (`ParsedSurvey`, `ParsedColumn`, `ParsedRow`, `ParsedCell`) are
C# records — immutable, lightweight, and designed to carry data between layers.

**3 new endpoints** added to `Program.cs`:

- `POST /api/surveys` — accepts a `multipart/form-data` CSV upload, validates
  the file, runs it through `CsvParserService`, then persists to the database
  in strict order:
  1. `Survey` record saved first to obtain its `Id`
  2. `SurveyColumn` records saved to obtain column `Id`s
  3. `SurveyResponse` + `ResponseValue` records saved in batches of 500 rows
     using EF Core navigation properties to resolve foreign keys automatically
  4. `Survey.Status` updated to `"complete"` with `CompletedAt` timestamp
  Returns `201 Created` with survey metadata and detected columns.

- `GET /api/surveys` — returns all surveys ordered newest first (summary only,
  no row data).
- `GET /api/surveys/{id}` — returns survey metadata plus all detected column
  definitions ordered by `ColumnIndex`.

**CsvHelper 33.1.0** installed as the CSV parsing library (industry standard
for .NET).

### Applied From Planning Documents

**`_5_ARCHITECTURE.md` — Service Breakdown:**
`CsvParserService` directly implements the component defined in the architecture
diagram. The doc called for a dedicated parsing service decoupled from the API
layer, which is exactly the pattern used.

**`_5_ARCHITECTURE.md` — Data Flow Diagram:**
The sequence of saves (Survey → Columns → Responses → Values → mark complete)
follows the processing flow defined in the architecture's sequence diagram.

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Async Processing:**
The scalability doc identified that synchronous CSV processing is only
acceptable for Step 2. Two `TODO` comments are embedded in the endpoint code
marking exactly where to:
- Replace synchronous processing with Azure Service Bus enqueueing (Step 6)
- Add Azure Blob Storage upload for the raw CSV file (Step 8)

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Database Design:**
Batch saves of 500 rows at a time were implemented based on the scalability
doc's guidance that large single `SaveChangesAsync()` calls hold DB transactions
open too long and cause lock contention under load.

**`_6_TESTING_STRATEGY_AND_INTERVIEW_PREP.md` — Separation of Concerns:**
`CsvParserService` was kept pure (no DB) specifically to support the testing
strategy. The testing doc calls for unit tests on `CsvParserService` in
isolation — this is only possible if parsing is not tangled with DB operations.

### What Was Intentionally Left Out

- **No Azure Blob Storage** — `BlobUrl` is set to the filename as a placeholder.
  Marked `TODO (Step 8)`.
- **No authentication** — `UploadedBy` is hardcoded to `"anonymous"`.
  Marked `TODO (Step 8)`.
- **No async queue** — the user waits synchronously for the full response.
  Will be replaced with `202 Accepted` + Service Bus in Step 6.
- **No NLP calls** — `AnalyzeSentiment` columns are flagged but not yet
  processed. That begins in Step 3.
- **No streaming for large files** — the full CSV is loaded into memory.
  Marked `TODO (Step 6)` for replacement with chunked streaming.

### How to Test

```bash
# Start the database and API
docker-compose up -d postgres
cd backend/src/_03_Web_API && dotnet run

# Upload a CSV file
curl -X POST http://localhost:5120/api/surveys \
  -F "file=@/path/to/your/file.csv"

# Upload with a custom display name
curl -X POST http://localhost:5120/api/surveys \
  -F "file=@/path/to/your/file.csv" \
  --data-urlencode "name=Q1 Feedback"

# List all surveys
curl http://localhost:5120/api/surveys

# Get a specific survey with its detected columns
curl http://localhost:5120/api/surveys/1
```

Expected response from `POST /api/surveys`:
```json
{
  "id": 1,
  "name": "Q1 Feedback",
  "status": "complete",
  "totalRows": 500,
  "columnCount": 6,
  "columns": [
    { "columnName": "How was your experience?", "columnType": "text", "analyzeSentiment": true },
    { "columnName": "Rating", "columnType": "numeric", "analyzeSentiment": false }
  ]
}
```
