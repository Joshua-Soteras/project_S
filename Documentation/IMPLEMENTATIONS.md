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

---

## Step 4 — SentimentClient + NLP Wiring into ASP.NET Core

### What Was Built

**`Services/SentimentClient.cs`** added to `_03_Web_API/Services/`:

A typed `HttpClient` wrapper responsible solely for communicating with the
Python NLP service. It has no database dependency — it only makes HTTP calls
and returns typed result objects. This separation means it can be unit tested
by mocking `HttpClient` without needing the NLP service running.

Two types defined alongside the client:
- `SentimentRequest(string Text)` — the JSON body sent to `POST /analyze`.
  `Text` serializes as `"text"` automatically via `JsonSerializerDefaults.Web`
  (camelCase policy applied by `PostAsJsonAsync`).
- `SentimentResponse(string Label, float Positive, float Neutral, float Negative)` —
  the deserialized response from the NLP service. Case-insensitive deserialization
  means `"label"` → `Label`, `"positive"` → `Positive`, etc. without any
  `[JsonPropertyName]` attributes.

`AnalyzeAsync(string text)` returns `null` on non-success HTTP status rather
than throwing — so a temporarily unavailable NLP service skips individual cells
instead of failing the entire upload.

Uses C# 12 primary constructor: `SentimentClient(HttpClient http)`.

**`Program.cs`** — three changes:

1. **`AddHttpClient<SentimentClient>` registration** — registers the client as
   a typed `HttpClient` with a pre-configured `BaseAddress` read from config.
   `AddHttpClient` manages the underlying `HttpClientHandler` lifetime (connection
   pooling, DNS refresh) so there is no manual `HttpClient` construction or disposal.
   `NlpService:BaseUrl` resolves to:
   - `http://localhost:8000` in Development (direct `uv run uvicorn` process)
   - `http://nlp:8000` in Docker (docker-compose service name resolution)

2. **`SentimentClient sentiment` parameter** injected into `POST /api/surveys` —
   DI automatically provides the pre-configured instance.

3. **NLP processing block** added after the CSV batch-save loop:
   - Identifies text column IDs (`AnalyzeSentiment = true`).
   - Queries all `ResponseValue` rows for those columns from the DB (after the
     batch save, so every row has a DB-assigned `Id` available as a FK).
   - Calls `AnalyzeAsync` in batches of 100 to avoid too many in-flight HTTP
     requests at once. Skips blank cells and failed NLP calls rather than
     aborting the upload.
   - Saves `SentimentResult` entities (one per non-blank text cell) in batches.
   - Computes one `KpiAggregate` per text column — average scores and label
     counts across all results for that column. Written once when processing
     completes; dashboard reads this flat row instead of running live aggregations.
   - `SentimentAnalyzed` count added to the `POST /api/surveys` 201 response
     so the caller knows how many cells were analyzed.

**`appsettings.json`** — added `NlpService.BaseUrl = "http://nlp:8000"` (Docker default).

**`appsettings.Development.json`** — added `NlpService.BaseUrl = "http://localhost:8000"` (local dev).

### Applied From Planning Documents

**`_5_ARCHITECTURE.md` — Service & Component Breakdown:**
`SentimentClient` directly implements the `SentimentClient (HttpClient wrapper)`
component defined in the ASP.NET Core service breakdown diagram. The architecture
specified it as a dedicated wrapper class, not inline HttpClient calls in the endpoint.

**`_5_ARCHITECTURE.md` — CSV Upload & Processing Flow:**
The sequence diagram shows the loop:
`For each row × text column → API: POST /analyze → NLP: { label, positive, neutral, negative }`.
The batch processing loop in `POST /api/surveys` implements exactly this, then
`INSERT SentimentResults` and `UPDATE Survey status=Complete` as shown.

**`_5_ARCHITECTURE.md` — ER Diagram:**
`SentimentResult` is one-to-one with `ResponseValue` (enforced by unique index
on `ResponseValueId` from the migration). `KpiAggregate` is one per survey×column
(enforced by unique composite index). Both constraints prevent duplicate processing
if the endpoint is accidentally called twice.

**`_6_TESTING_STRATEGY_AND_INTERVIEW_PREP.md` — Separation of Concerns:**
`SentimentClient` is kept pure (no DB dependency) so unit tests can inject a
mock `HttpMessageHandler` and test `AnalyzeAsync` without a running NLP service.
The testing doc calls this out explicitly as a required test target.

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Async Processing:**
Two `TODO (Step 6)` comments mark exactly where the synchronous NLP processing
loop will be replaced with an async queue pattern (Azure Service Bus / .NET Channel).
The endpoint currently blocks until all NLP calls complete — acceptable for Step 4
development, but will not scale for large CSVs with hundreds of text columns.

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Database Design for Scale:**
`KpiAggregate` was always the intended read target for the dashboard. This step
is where the writes finally happen — the NLP worker (inline for now, background
worker in Step 6) computes and persists the aggregates once per survey.

### What Was Intentionally Left Out

- **No retry policy** — if the NLP service returns a non-200, the cell is skipped
  silently. A Polly retry policy (exponential backoff) will be added when `HttpClient`
  registration is hardened in Step 6/7.
- **No partial failure tracking** — if the NLP service is down for half the cells,
  the survey still completes without flagging which cells were skipped. An
  `ErrorMessage` field on `Survey` is available for this; it will be wired in Step 6.
- **No async queue** — the user still waits synchronously for all NLP calls to
  complete. Marked `TODO (Step 6)`.

### How to Test

```bash
# Prerequisites: postgres running, NLP service running
docker-compose up -d postgres
cd nlp && uv run uvicorn app.main:app --reload &
cd backend/src/_03_Web_API && dotnet run

# Upload a CSV that has at least one text column
curl -X POST http://localhost:5120/api/surveys \
  -F "file=@/path/to/survey.csv"

# Expected response now includes SentimentAnalyzed count
# {
#   "id": 1,
#   "name": "survey",
#   "status": "complete",
#   "totalRows": 50,
#   "columnCount": 3,
#   "sentimentAnalyzed": 50,
#   "columns": [
#     { "columnName": "Feedback", "columnType": "text", "analyzeSentiment": true },
#     { "columnName": "Rating",   "columnType": "numeric", "analyzeSentiment": false }
#   ]
# }

# Verify SentimentResults were written to the database
docker exec -it projects_db psql -U projects_admin -d projects_db
SELECT label, COUNT(*) FROM "SentimentResults" GROUP BY label;

# Verify KpiAggregates were computed
SELECT * FROM "KpiAggregates";
```

---

## Step 5 — Angular Frontend MVP

### What Was Built

**`frontend-angular/`** — new Angular 21 project scaffolded alongside the existing
Nuxt frontend. Nuxt (`frontend/`) is kept untouched until Angular is confirmed.

---

**`src/app/models/survey.model.ts`** — TypeScript interfaces:

- `Survey` — summary shape from `GET /api/surveys` (id, name, status, totalRows,
  processedRows, uploadedBy, uploadedAt, completedAt).
- `SurveyDetail extends Survey` — adds `errorMessage` and `columns: SurveyColumn[]`,
  returned by `GET /api/surveys/:id`.
- `SurveyColumn` — id, columnName, columnType, analyzeSentiment, columnIndex.
- `UploadResult` — shape returned by `POST /api/surveys` (id, name, status,
  totalRows, columnCount, sentimentAnalyzed).

Centralizing these means every component gets the same type — no duplicated
interface definitions across files.

---

**`src/app/services/api.service.ts`** — HTTP wrapper:

- `getSurveys()` → `GET /api/surveys`
- `getSurvey(id)` → `GET /api/surveys/:id`
- `uploadSurvey(file, name?)` → `POST /api/surveys` as `multipart/form-data`
  via `FormData`. Angular sets `Content-Type` with the correct multipart boundary
  automatically — no manual header needed.

Uses Angular's `inject()` function (modern signal-style DI) instead of constructor
injection. Registered `providedIn: 'root'` so the same instance is shared across
all components.

---

**`src/app/components/survey-list/`** — Survey list page (`/`):

- Calls `getSurveys()` on `ngOnInit`, stores results in a `surveys` array.
- Shows a loading state, an error message if the API is unreachable, and an
  empty-state prompt to upload if no surveys exist.
- Renders a table with name, status badge (color-coded by status), row count,
  upload date, and a "View →" link to the dashboard.
- `statusClass(status)` helper maps status strings to Tailwind badge classes
  (`complete` → green, `processing` → yellow, `error` → red, `queued` → gray).

---

**`src/app/components/upload/`** — CSV upload form (`/upload`):

- File input filtered to `.csv` only. `onFileSelected()` stores the `File` object
  and clears any previous error.
- Optional survey name input via `[(ngModel)]` (two-way binding via `FormsModule`).
- `onSubmit()` calls `uploadSurvey()`, disables the button during upload, and
  on success navigates directly to `/surveys/:id` for the new survey.
- Displays the API error message (`err.error?.error`) if the upload fails,
  falling back to a generic message.
- Shows a note during upload that sentiment analysis is running (since the
  synchronous NLP loop can take several seconds for large files).

---

**`src/app/components/dashboard/`** — Survey detail page (`/surveys/:id`):

- Reads the `id` route parameter via `ActivatedRoute.snapshot.paramMap`, calls
  `getSurvey(id)` on init.
- Header card: survey name, upload timestamp, status badge, processed row count.
- Stats row: three metric cards — Total Rows, Processed, Columns.
- Columns table: index, column name, type badge, and a "✓ Analyzed" indicator
  for text columns flagged `analyzeSentiment = true`.
- `pct(value)` helper formats 0–1 floats as percentage strings for future
  KPI score display.
- Error message card shown if `survey.errorMessage` is set.

---

**`src/app/app.routes.ts`** — Routes:

| Path | Component |
|---|---|
| `/` | SurveyList |
| `/upload` | Upload |
| `/surveys/:id` | Dashboard |
| `**` | redirect to `/` |

---

**`src/app/app.config.ts`** — added `provideHttpClient(withFetch())`:
`withFetch()` uses the browser Fetch API instead of XMLHttpRequest — required
for Angular SSR compatibility and is the modern default in Angular 17+.

---

**`src/app/app.html`** — replaced the default Angular welcome template with a
top nav bar (dark background, Project S title, Surveys + Upload links with
`routerLinkActive` highlighting) and a `<router-outlet>` for page content.

---

**`src/proxy.conf.json`** — dev proxy config:
All requests to `/api/*` are forwarded to `http://localhost:5120` (the ASP.NET
Core API). This means Angular components use relative `/api/surveys` paths that
work in both development and production. No CORS headers needed.

**`angular.json`** — `proxyConfig: "src/proxy.conf.json"` added to the
`serve` options so the proxy activates automatically with `ng serve`.

**`tailwind.config.js`** — `content: ['./src/**/*.{html,ts}']` added so
Tailwind scans all Angular templates and component files for class names.

**`src/styles.scss`** — added the three Tailwind directives (`@tailwind base`,
`@tailwind components`, `@tailwind utilities`) replacing the empty default.

### Applied From Planning Documents

**`_5_ARCHITECTURE.md` — Service & Component Breakdown:**
The Angular frontend box lists exactly the components built here:
`CSV Upload Component`, `Survey List Component`, `API Service (HttpClient)`.
The `Dashboard Component` and `Response Detail Component` are partially
implemented — the dashboard shows column metadata for now; full KPI chart
rendering (ng2-charts / Chart.js) is deferred to after Step 6 provides the
KPI endpoint with real aggregate data.

**`_5_ARCHITECTURE.md` — Data Flow:**
`A5 (API Service) → B1 (Survey Endpoints)` is the only active data path in
this step. `A5 → B2 (KPI Endpoints)` will be active in a future step once
a `/api/kpis` endpoint is added to the backend.

**`_6_TESTING_STRATEGY_AND_INTERVIEW_PREP.md` — Separation of Concerns:**
`ApiService` is the only class that touches `HttpClient`. Components inject
`ApiService` only — they never construct URLs or call `HttpClient` directly.
This is the pattern the testing doc requires so components can be tested by
providing a mock `ApiService` without spinning up a real HTTP server.

### What Was Intentionally Left Out

- **No KPI charts** — `GET /api/kpis` does not exist on the backend yet.
  The dashboard shows column metadata and stats. Chart.js sentiment visualizations
  will be added once the KPI endpoint is built.
- **No polling for processing status** — if a large CSV takes time, the user
  stays on the upload page until the response comes back. A polling mechanism
  (periodic `GET /api/surveys/:id` checking `status`) will be added in Step 6
  alongside the async queue.
- **No form validation library** — upload validation is minimal (file presence
  check, `.csv` extension filter). A proper reactive form with `FormBuilder`
  will replace the template-driven form in a later step.
- **No authentication UI** — `UploadedBy` is hardcoded to `"anonymous"` on the
  backend. Azure Entra ID login is Step 8.

### How to Test

```bash
# Start all services
docker-compose up -d postgres
cd nlp && uv run uvicorn app.main:app --reload &
cd backend/src/_03_Web_API && dotnet run &

# Start Angular dev server
cd frontend-angular && ng serve

# Open http://localhost:4200
# - Survey list loads (empty on first run)
# - Click "Upload CSV", select a .csv file, submit
# - Redirects to /surveys/1 showing columns + stats
# - Click "← Back to Surveys" to see the survey in the list
```

---

## Step 6 — Async Queue Pattern (Channel\<int\> + BackgroundService)

### What Was Built

**`Workers/SurveyQueue.cs`** — singleton in-memory queue:

A thin wrapper around `System.Threading.Channels.Channel<int>`. Holds survey
IDs that are waiting for NLP processing. Registered as a singleton so the channel
lives for the full app lifetime (outlives any individual HTTP request or background job).

Key design choices:
- `Channel.CreateUnbounded<int>` — no upper limit on queued items.
- `UnboundedChannelOptions { SingleReader = true }` — enables internal channel
  optimisations because only one consumer (the worker) reads from it.
- `TryWrite(surveyId)` — non-blocking write from the HTTP endpoint.
- `ChannelReader<int> Reader` — exposed to the background worker for async reads.
  `ReadAllAsync()` suspends the worker thread when the queue is empty with no
  polling or busy-waiting.

**`Services/SurveyProcessingService.cs`** — scoped NLP + KPI pipeline:

Contains the full sentiment analysis and KPI computation logic that was
previously inline in `POST /api/surveys` (Step 4). Extracted into its own
class so the background worker can call it with a dedicated DI scope per job.

`ProcessAsync(int surveyId, CancellationToken ct)` pipeline:
1. Loads the survey by ID. Sets `status = "processing"`, saves immediately
   so the dashboard can show a "running" state while work happens.
2. Queries all `SurveyColumn` rows for the survey with `AnalyzeSentiment = true`.
3. Loads all `ResponseValue` rows for those columns.
4. Calls `SentimentClient.AnalyzeAsync()` in batches of 100. Skips blank cells
   and null results (NLP service down). Saves `SentimentResult` rows per batch.
5. Computes one `KpiAggregate` per text column: avg scores and label counts
   across all `SentimentResult` rows for that column.
6. Sets `status = "complete"`, `ProcessedRows`, `CompletedAt`. Saves.
7. On `Exception`: sets `status = "error"`, writes `ex.Message` to
   `survey.ErrorMessage`. Uses `CancellationToken.None` for this save so it
   completes even during app shutdown.
8. On `OperationCanceledException` (app shutdown): logs the cancellation,
   re-throws to stop the loop cleanly. Survey stays in `"processing"` state.

**`Workers/SurveyProcessingWorker.cs`** — hosted background service:

A `BackgroundService` (automatically singleton) that runs for the full app
lifetime alongside the HTTP server. Its `ExecuteAsync()` loop:
1. Awaits survey IDs from `SurveyQueue.Reader.ReadAllAsync(stoppingToken)`.
   The `await foreach` suspends here when the queue is empty — no CPU usage.
2. For each ID, creates a new `IServiceScope` via `IServiceScopeFactory`.
   The scope provides a fresh `ApplicationDbContext` and `SentimentClient`
   per job — essential because `DbContext` is scoped and cannot be shared
   across concurrent operations or retained between jobs.
3. Resolves `SurveyProcessingService` from the scope and calls `ProcessAsync()`.
4. Catches `OperationCanceledException` to exit the loop cleanly on shutdown.
5. Outer `catch (Exception)` is a safety net in case `ProcessAsync` itself
   throws unexpectedly (error is already written to the survey by the service).

**`Program.cs`** — three registration additions, endpoint simplified:

New service registrations (builder phase):
```csharp
builder.Services.AddSingleton<SurveyQueue>();
builder.Services.AddScoped<SurveyProcessingService>();
builder.Services.AddHostedService<SurveyProcessingWorker>();
```

`POST /api/surveys` changes:
- `SentimentClient sentiment` parameter removed (no longer needed at the endpoint level).
- `SurveyQueue queue` parameter added.
- `survey.Status` changed from `"processing"` to `"queued"` at creation.
- Entire NLP + KPI block removed — replaced with `queue.Enqueue(survey.Id)`.
- Return changed from `201 Created` to `202 Accepted` — semantically correct
  since the resource is created but processing is deferred.
- Response body simplified (no `SentimentAnalyzed` count since NLP hasn't run yet).

**`frontend-angular/src/app/components/dashboard/dashboard.ts`** — polling added:

The dashboard now implements `OnDestroy` alongside `OnInit`. After the initial
`getSurvey()` call:
- If status is `queued` or `processing`, `startPolling(id)` is called.
- Every 2 seconds, `getSurvey(id)` is called and `this.survey` is updated.
- When status transitions to `complete` or `error`, `stopPolling()` is called.
- `ngOnDestroy()` always calls `stopPolling()` to clear the `setInterval`
  timer and prevent memory leaks if the user navigates away before completion.
- Poll errors are swallowed — a transient 500 during polling just delays the
  next update without showing an error to the user.

**`frontend-angular/src/app/components/dashboard/dashboard.html`** — processing banner:

A yellow spinner banner is shown while `status` is `queued` or `processing`:
- "Waiting in queue…" when status = `queued`.
- "Running sentiment analysis — this may take a minute…" when status = `processing`.
- Spinner uses Tailwind's `animate-spin` on an inline SVG.
- Banner disappears automatically when polling updates the survey to `complete`.

### Applied From Planning Documents

**`_5_ARCHITECTURE.md` — Async Processing:**
The architecture document calls for a `.NET Channel<T>` as the local async
queue implementation (Step 6 scope), with Azure Service Bus as the production
swap in Step 8. `SurveyQueue` is designed with exactly this swap in mind —
only the registration in `Program.cs` and the `SurveyQueue` class itself change
to move to Service Bus. `SurveyProcessingWorker` and `SurveyProcessingService`
remain identical.

**`_7_SCALABILITY_AND_PRODUCTION_DESIGN.md` — Async Processing:**
The scalability doc identified that synchronous NLP processing in an HTTP
request handler doesn't scale. A 500-row CSV with 3 text columns = 1,500 NLP
calls during the request. At ~50ms per call that's 75 seconds of blocked HTTP.
The `202 Accepted + background worker` pattern removes this entirely.

**`_6_TESTING_STRATEGY_AND_INTERVIEW_PREP.md` — Separation of Concerns:**
`SurveyProcessingService` is a clean, injectable service that can be unit tested
by providing mock `ApplicationDbContext` and `SentimentClient`. The `CancellationToken`
threading through the method makes it testable for cancellation scenarios too.

### What Was Intentionally Left Out

- **No re-delivery on crash** — if the app crashes while a survey is processing,
  the survey stays stuck in `"processing"`. An in-memory `Channel<T>` does not
  survive app restarts. Azure Service Bus (Step 8) provides at-least-once delivery
  with message locking to solve this.
- **No concurrency** — the worker processes one survey at a time. Parallelism
  can be added by running multiple worker instances (`AddHostedService` called
  multiple times) but is not needed at current scale.
- **No Polly retry on NLP calls** — if the NLP service is down, cells are skipped
  silently. A retry policy with exponential backoff will be added in Step 7.
- **No progress updates** — `ProcessedRows` is only written at completion.
  Incremental progress (updating every batch) would make the dashboard more
  informative but adds write overhead.

### How to Test

```bash
# Start all services
docker-compose up -d postgres
cd nlp && uv run uvicorn app.main:app --reload &
cd backend/src/_03_Web_API && dotnet run &
cd frontend-angular && ng serve

# Upload a CSV via the API — now returns 202 immediately
curl -X POST http://localhost:5120/api/surveys \
  -F "file=@/path/to/survey.csv"
# Response: 202 Accepted
# { "id": 1, "name": "survey", "status": "queued", "totalRows": 500, "columnCount": 3 }

# Poll until complete
curl http://localhost:5120/api/surveys/1
# status transitions: queued → processing → complete

# Or open http://localhost:4200, upload a CSV, and watch the dashboard
# spinner update automatically as the worker processes in the background.

# Verify background processing in dotnet run console logs:
# info: SurveyProcessingWorker — Dequeued survey 1 for processing
# info: SurveyProcessingService — Survey 1: processed NLP batch 1/5
# info: SurveyProcessingService — Survey 1 complete — 50 sentiment results
```
