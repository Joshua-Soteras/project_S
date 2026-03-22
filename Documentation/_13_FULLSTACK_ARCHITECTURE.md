# How It All Connects — Full Stack Architecture

This document shows how Angular, ASP.NET Core, Python NLP, and PostgreSQL
connect to each other. It is the zoom-out view — read the layer-specific
docs (`_10`, `_11`, `_12`) for internal details of each service.

---

## 1. System Topology

All four services, their ports, and which processes communicate with which.

```mermaid
graph TB
    subgraph Browser["Browser"]
        A["Angular App\nlocalhost:4200 (dev)\nor same origin (prod)"]
    end

    subgraph Backend["Backend Services"]
        B["ASP.NET Core API\nlocalhost:5120 (dev)\nport 8080 (Docker)"]
        C["Python FastAPI NLP\nlocalhost:8000\nport 8000 (Docker)"]
    end

    subgraph Data["Data Layer"]
        D[("PostgreSQL\nlocalhost:5432\nDocker: projects_db")]
    end

    A  -- "HTTP /api/*\n(proxied in dev)" --> B
    B  -- "HTTP POST /analyze" --> C
    B  -- "EF Core / Npgsql\nTCP 5432" --> D

    style Browser fill:#e0f2fe
    style Backend fill:#fef9c3
    style Data   fill:#dcfce7
```

---

## 2. Port Reference

| Service | Local dev port | Docker port | How to start |
|---|---|---|---|
| Angular | 4200 | N/A (static files in prod) | `ng serve` |
| ASP.NET Core API | 5120 | 8080 | `dotnet run` |
| Python NLP | 8000 | 8000 | `uv run uvicorn app.main:app --reload` |
| PostgreSQL | 5432 | 5432 | `docker-compose up -d postgres` |

---

## 3. Docker Network

In Docker, services communicate by **service name** (not localhost). Docker
Compose creates an internal DNS so every container can reach every other
container by name.

```mermaid
graph LR
    subgraph docker-compose["docker-compose.yml internal network"]
        P["postgres\ncontainer_name: projects_db\nport 5432"]
        N["nlp\ncontainer_name: projects_nlp\nport 8000"]
        B["backend (future)\nport 8080"]
    end

    B -- "Host=postgres;Port=5432\n(connection string)" --> P
    B -- "http://nlp:8000\n(NlpService:BaseUrl in appsettings.json)" --> N

    H["Host machine\nlocalhost"] -- "5432 mapped" --> P
    H -- "8000 mapped" --> N
```

**`"nlp"` in `http://nlp:8000`** is resolved by Docker's internal DNS to the
NLP container's IP. This is why `appsettings.json` (used in Docker/Production)
has `http://nlp:8000` and `appsettings.Development.json` (used locally) has
`http://localhost:8000`.

---

## 4. Configuration Per Environment

How each service knows where to find the others:

```mermaid
flowchart TD
    subgraph Local["Local Development"]
        LA["Angular\nproxy.conf.json\n/api → localhost:5120"]
        LB["ASP.NET Core\nappsettings.Development.json\nNlpService:BaseUrl = http://localhost:8000\nDB = localhost:5432"]
        LC["Python NLP\nuv run uvicorn\n--reload (hot reload on)"]
        LD["PostgreSQL\ndocker-compose up -d postgres\nlocalhost:5432"]
    end

    subgraph Docker["Docker Compose"]
        DA["Angular\nnot containerized yet\n(Step 5 deferred)"]
        DB["ASP.NET Core\nappsettings.json\nNlpService:BaseUrl = http://nlp:8000\nDB = postgres:5432"]
        DC["Python NLP\nDockerfile\nuvicorn (no --reload)"]
        DD["PostgreSQL\nprojects_db container"]
    end
```

---

## 5. Full Request — CSV Upload (End to End)

The most complex flow in the system. Traces a user action from browser click
to database row.

```mermaid
sequenceDiagram
    participant User
    participant NG   as Angular\nUpload component
    participant API  as ASP.NET Core\nPOST /api/surveys
    participant CSV  as CsvParserService
    participant DB   as PostgreSQL
    participant SC   as SentimentClient
    participant NLP  as Python FastAPI\nPOST /analyze

    User->>NG: Select file, click Upload
    NG->>NG: new FormData()\nappend file + name
    NG->>API: POST /api/surveys\nmultipart/form-data

    Note over API: Validate file

    API->>CSV: ParseAsync(IFormFile)
    CSV-->>API: ParsedSurvey { columns[], rows[] }

    API->>DB: INSERT Survey (status=processing)
    DB-->>API: survey.Id

    API->>DB: INSERT SurveyColumn[] (one per CSV header)
    DB-->>API: column Ids

    loop Batches of 500 rows
        API->>DB: INSERT SurveyResponse + ResponseValue[]\n(EF Core nav properties resolve FKs)
    end

    API->>DB: SELECT ResponseValues\nWHERE ColumnId IN text-column Ids
    DB-->>API: textValues[]

    loop Per text cell (batches of 100)
        API->>SC: AnalyzeAsync(rawValue)
        SC->>NLP: POST /analyze { text }
        NLP-->>SC: { label, positive, neutral, negative }
        SC-->>API: SentimentResponse
        API->>DB: INSERT SentimentResult
    end

    loop Per text column
        API->>DB: SELECT SentimentResults WHERE ColumnId = x
        API->>DB: INSERT KpiAggregate (avg + counts)
    end

    API->>DB: UPDATE Survey (status=complete)
    API-->>NG: 201 { id, name, status, sentimentAnalyzed }
    NG->>NG: router.navigate(['/surveys', result.id])
    NG->>User: Dashboard page rendered
```

---

## 6. Full Request — View Survey Dashboard (End to End)

```mermaid
sequenceDiagram
    participant User
    participant NG   as Angular\nDashboard component
    participant API  as ASP.NET Core\nGET /api/surveys/{id}
    participant DB   as PostgreSQL

    User->>NG: navigate to /surveys/1
    NG->>NG: ngOnInit()\nid = route.snapshot.paramMap.get('id')
    NG->>API: GET /api/surveys/1
    API->>DB: SELECT Surveys WHERE Id=1\nINCLUDE SurveyColumns (eager load JOIN)
    DB-->>API: survey + columns[]
    API-->>NG: 200 { id, name, status, totalRows,\nprocessedRows, columns[] }
    NG->>NG: this.survey = response\nloading = false
    NG->>User: render header card + stats + columns table
```

**EF Core eager loading:** `db.Surveys.Include(s => s.Columns)` generates a
single SQL query with a JOIN to `SurveyColumns`. Without `.Include()`, the
`Columns` navigation property would be `null` (lazy loading is disabled by
default in EF Core).

---

## 7. Service Boundaries and Contracts

Each service has a well-defined HTTP contract. These are the exact shapes
crossing each boundary.

### Angular → ASP.NET Core

| Direction | Method + Path | Request shape | Response shape |
|---|---|---|---|
| List surveys | `GET /api/surveys` | — | `Survey[]` |
| Get survey detail | `GET /api/surveys/{id}` | — | `SurveyDetail` (with `columns[]`) |
| Upload CSV | `POST /api/surveys` | `multipart/form-data` file + optional name | `UploadResult` (201) |

### ASP.NET Core → Python NLP

| Direction | Method + Path | Request shape | Response shape |
|---|---|---|---|
| Analyze text | `POST /analyze` | `{ "text": "..." }` | `{ "label": "positive\|neutral\|negative", "positive": float, "neutral": float, "negative": float }` |
| Health check | `GET /health` | — | `{ "status": "healthy" }` |

### ASP.NET Core → PostgreSQL (EF Core)

Not HTTP — EF Core uses Npgsql to send SQL over TCP port 5432. The queries
are generated from LINQ expressions in `Program.cs`:

| Operation | EF Core call | Generated SQL |
|---|---|---|
| Save survey | `db.Surveys.Add(survey)` + `SaveChangesAsync()` | `INSERT INTO "Surveys" ...` |
| Load survey + columns | `db.Surveys.Include(s => s.Columns).FirstOrDefaultAsync(s => s.Id == id)` | `SELECT ... FROM "Surveys" JOIN "SurveyColumns" ...` |
| Load text values | `db.ResponseValues.Where(rv => ids.Contains(rv.ColumnId))` | `SELECT ... FROM "ResponseValues" WHERE "ColumnId" = ANY(...)` |
| Load sentiment by column | `db.SentimentResults.Where(sr => sr.ResponseValue.ColumnId == id)` | `SELECT ... FROM "SentimentResults" JOIN "ResponseValues" ...` |

---

## 8. Error Propagation Across Layers

What happens when something goes wrong at each boundary:

```mermaid
flowchart TD
    A["NLP service is down\nor returns non-200"] -->|SentimentClient returns null| B["Cell is skipped\nno SentimentResult row created\nupload continues"]
    C["CSV is malformed\nor empty"] -->|CsvParserService throws| D["POST /api/surveys\nreturns 400 Bad Request\n{ error: 'Failed to parse CSV' }"]
    D -->|Angular HttpClient error| E["Upload.onSubmit()\nerr.error?.error shown\nin template"]
    F["PostgreSQL connection fails"] -->|EF Core throws| G["500 Internal Server Error\nfrom ASP.NET Core"]
    G -->|Angular HttpClient error| H["Upload component\nshows generic error message"]
    I["Invalid file type\n(not .csv)"] -->|Program.cs validation| J["400 Bad Request\n{ error: 'Only .csv files accepted' }"]
    J --> E
```

---

## 9. Local Full-Stack Startup Order

Services must start in this order because of dependencies:

```mermaid
flowchart LR
    A["1. PostgreSQL\ndocker-compose up -d postgres\nMust be first — API migrates on startup"] -->
    B["2. ASP.NET Core API\ndotnet run\nApplies migrations on start\nConnects to postgres:5432"] -->
    C["3. Python NLP\nuv run uvicorn app.main:app --reload\nLoads RoBERTa model (~2-5s)"] -->
    D["4. Angular\nng serve\nProxy to localhost:5120 active"]
```

The API and NLP service are independent of each other at startup — the API
only calls NLP when a CSV is uploaded, not on startup. However, if NLP is not
running when a CSV is uploaded, all cells will be skipped silently (null returns
from `SentimentClient`). KpiAggregates will be empty for that upload.
