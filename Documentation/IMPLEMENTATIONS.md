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

---

## Step 3 — Python NLP Sentiment Service

### What Was Built

**`nlp/app/model.py`** — Model loader and inference function:

- `load_model()` — called once at FastAPI startup via the lifespan hook.
  Loads the RoBERTa pipeline into memory so every request reuses the
  same in-memory model. No reload cost per request.
- `analyze(text)` — runs the loaded pipeline on a single text string
  and returns a dict with `label`, `positive`, `neutral`, `negative` scores.
- Model used: `cardiffnlp/twitter-roberta-base-sentiment-latest` — trained
  on ~124M tweets, returns three labels (negative / neutral / positive) as
  softmax probability scores that always sum to ~1.0.

**`nlp/app/main.py`** — FastAPI application:

- `GET /health` — liveness check used by Docker healthcheck and the
  ASP.NET Core API on startup to confirm the NLP service is reachable.
- `POST /analyze` — accepts `{ "text": "..." }`, rejects empty strings,
  runs `analyze()`, returns `{ "label", "positive", "neutral", "negative" }`.
- FastAPI lifespan context manager used for startup (replaces deprecated
  `@app.on_event("startup")`). Model is warm before the first request.

**`nlp/pyproject.toml`** — uv project configuration:
- All dependencies declared (fastapi, uvicorn, transformers, torch, scipy, numpy).
- `[tool.uv.sources]` override routes `torch` to PyTorch's CPU wheel index,
  reducing the installed size from ~3.5GB (CUDA) to ~700MB (CPU-only).
- `[[tool.uv.index]]` registers the PyTorch CPU index as `pytorch-cpu`.

**`nlp/uv.lock`** — exact locked versions for all 47 packages, generated by
`uv sync`. Guarantees reproducible installs across all environments.

**`nlp/Dockerfile`** — container definition:
- Copies the `uv` binary from the official `ghcr.io/astral-sh/uv` image
  (no pip install of uv needed).
- Installs dependencies via `uv sync --frozen` using the lockfile.
- Pre-downloads the RoBERTa model weights (~500MB) into the Hugging Face
  cache at build time so the container starts instantly at runtime.
- Runs as a non-root user (`appuser`) for production security.

**`docker-compose.yml`** — `nlp` service added:
- Builds from `./nlp`, exposed on port 8000.
- Docker healthcheck polls `GET /health` every 30s.

**`.gitignore`** — added Python exclusions:
- `.venv/`, `__pycache__/`, `*.pyc`, `.pytest_cache/`, `.coverage`
- `nlp/.cache/` — Hugging Face model cache (large, never committed)

### Applied From Planning Documents

**`_5_ARCHITECTURE.md` — Service & Component Breakdown:**
The `Python Microservice` box in the architecture diagram calls for a
`FastAPI App` + `RoBERTa Pipeline` using `cardiffnlp/twitter-roberta-base-sentiment-latest`.
Both components are exactly what was built here.

**`_5_ARCHITECTURE.md` — CSV Upload & Processing Flow:**
The sequence diagram shows `API → NLP: POST /analyze { "text": "..." }` and
`NLP → API: { label, positive, neutral, negative }`. The `/analyze` endpoint
implements exactly this contract.

**`_6_TESTING_STRATEGY_AND_INTERVIEW_PREP.md` — Separation of Concerns:**
`model.py` is kept pure (no FastAPI dependency) so unit tests can mock or
call `analyze()` directly without starting the web server. The testing doc
specifically calls for `pytest` tests on the `analyze()` function in isolation.

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Scale-to-Zero NLP Workers:**
The scalability doc notes the NLP service is stateless and CPU-bound, making
it a good candidate for containerized scale-to-zero on Azure Container Apps.
The Dockerfile and lifespan-based model loading support this pattern —
the model loads once per container instance, not per request.

### What Was Intentionally Left Out

- **No batch endpoint** — `/analyze` processes one text at a time.
  A `POST /analyze/batch` endpoint accepting a list of texts would reduce
  HTTP overhead during CSV processing. Marked as a future optimization.
- **No GPU support** — CPU-only torch for now. Dockerfile can be updated
  by swapping the PyTorch index URL to the CUDA wheel index when GPU
  instances are available in Azure.
- **No auth on the NLP service** — it sits behind the ASP.NET Core API
  and is not exposed publicly. Network-level isolation (private Docker network,
  Azure VNet) is the security boundary. API key auth can be added in Step 8.
- **No ASP.NET Core wiring yet** — the `SentimentClient` that calls this
  service from the C# API is Step 4.

### How to Test

```bash
# Start the NLP service locally (from nlp/)
cd nlp
uv run uvicorn app.main:app --reload

# Health check
curl http://localhost:8000/health
# → {"status":"healthy"}

# Analyze a text
curl -X POST http://localhost:8000/analyze \
  -H "Content-Type: application/json" \
  -d '{"text": "The support team was incredibly helpful and fast."}'
# → {"label":"positive","positive":0.97,"neutral":0.02,"negative":0.01}

# Test rejection of empty text
curl -X POST http://localhost:8000/analyze \
  -H "Content-Type: application/json" \
  -d '{"text": ""}'
# → 400 {"detail":"text must not be empty or whitespace-only."}
```
