# System Architecture — KPI Survey Dashboard

## Overview

Users upload CSV survey data through an Angular frontend. The ASP.NET Core API parses the data, farms out free-text responses to a Python sentiment analysis microservice (RoBERTa), and persists everything to Azure PostgreSQL. The frontend renders KPI dashboards and sentiment breakdowns from the stored results.

---

## 1. High-Level System Diagram

```mermaid
graph TB
    subgraph Client["Client"]
        A["Angular Frontend\n(localhost:4200 / Azure Static Web App)"]
    end

    subgraph Azure["Azure Cloud"]
        subgraph AppLayer["Application Layer"]
            B["ASP.NET Core Web API\n(ProjectS.Web — port 8080)"]
            C["Python Sentiment Service\n(FastAPI + RoBERTa — port 8000)"]
        end

        subgraph DataLayer["Data Layer"]
            D[("Azure Database\nfor PostgreSQL")]
            E["Azure Blob Storage\n(raw CSV files)"]
        end
    end

    A -- "POST /api/surveys (CSV upload)\nGET /api/kpis\nGET /api/surveys" --> B
    B -- "store raw file" --> E
    B -- "POST /analyze" --> C
    C -- "sentiment scores" --> B
    B -- "read / write" --> D
```

---

## 2. CSV Upload & Processing Flow

```mermaid
sequenceDiagram
    participant U  as User
    participant FE as Angular
    participant API as ASP.NET Core API
    participant Blob as Azure Blob Storage
    participant NLP as Python Sentiment Service
    participant DB  as PostgreSQL

    U->>FE: Select & upload CSV
    FE->>API: POST /api/surveys (multipart/form-data)
    API->>Blob: Upload raw CSV → returns BlobUrl
    API->>DB: INSERT Survey (name, blobUrl, status=Processing)
    API->>API: Parse CSV rows + detect columns

    loop For each row × text column
        API->>NLP: POST /analyze  { "text": "..." }
        NLP->>API: { label, positive, neutral, negative }
    end

    API->>DB: INSERT SurveyResponses, ResponseValues, SentimentResults
    API->>DB: UPDATE Survey status=Complete
    API->>FE: 200 { surveyId, rowCount, status }

    U->>FE: Open Dashboard
    FE->>API: GET /api/kpis?surveyId=123
    API->>DB: Aggregate query (avg sentiment, score distributions)
    API->>FE: KPI payload
    FE->>U: Render charts + sentiment breakdown
```

---

## 3. Service & Component Breakdown

```mermaid
graph LR
    subgraph Angular["Angular Frontend"]
        A1["CSV Upload Component"]
        A2["Dashboard Component\n(ng2-charts / Chart.js)"]
        A3["Survey List Component"]
        A4["Response Detail Component\n(per-row sentiment)"]
        A5["API Service (HttpClient)"]
    end

    subgraph ASPNET["ASP.NET Core — ProjectS.Web"]
        B1["Survey Endpoints\n/api/surveys"]
        B2["KPI Endpoints\n/api/kpis"]
        B3["CsvParserService"]
        B4["BlobStorageService"]
        B5["SentimentClient\n(HttpClient wrapper)"]
    end

    subgraph Python["Python Microservice"]
        C1["FastAPI App"]
        C2["RoBERTa Pipeline\ncardiffnlp/twitter-roberta-\nbase-sentiment-latest"]
    end

    subgraph DB["PostgreSQL (Azure)"]
        D1["surveys"]
        D2["survey_columns"]
        D3["survey_responses"]
        D4["response_values"]
        D5["sentiment_results"]
    end

    A5 --> B1
    A5 --> B2
    B1 --> B3
    B1 --> B4
    B3 --> B5
    B5 --> C1
    C1 --> C2
    B1 --> D1
    B1 --> D2
    B3 --> D3
    B3 --> D4
    B5 --> D5
    B2 --> D1
    B2 --> D5
```

---

## 4. Database Schema

```mermaid
erDiagram
    surveys {
        int     id          PK
        string  name
        string  blob_url
        string  status      "pending | processing | complete | error"
        int     total_rows
        timestamp uploaded_at
    }

    survey_columns {
        int     id              PK
        int     survey_id       FK
        string  column_name
        string  column_type     "text | numeric | date | boolean"
        bool    analyze_sentiment
        int     column_index
    }

    survey_responses {
        int     id          PK
        int     survey_id   FK
        int     row_index
        timestamp created_at
    }

    response_values {
        int     id              PK
        int     response_id     FK
        int     column_id       FK
        string  raw_value
    }

    sentiment_results {
        int     id                  PK
        int     response_value_id   FK
        string  label               "positive | neutral | negative"
        float   positive_score
        float   neutral_score
        float   negative_score
        timestamp processed_at
    }

    surveys          ||--o{ survey_columns   : "defines"
    surveys          ||--o{ survey_responses : "contains"
    survey_responses ||--o{ response_values  : "has"
    survey_columns   ||--o{ response_values  : "typed by"
    response_values  ||--o|  sentiment_results : "analyzed as"
```

---

## 5. Docker Compose (Target State)

```
┌─────────────────────────────────────────────────────────────┐
│                     docker-compose.yml                       │
│                                                             │
│  postgres      → port 5432  (Azure DB in prod)             │
│  backend       → port 5000→8080  (ASP.NET Core)            │
│  nlp           → port 8000→8000  (Python FastAPI)          │
│  frontend      → port 4200→80    (Angular, nginx)          │
└─────────────────────────────────────────────────────────────┘
```

Services communicate over an internal Docker network. In production, `postgres` is replaced by Azure Database for PostgreSQL (Flexible Server), with the connection string injected via Azure Key Vault / environment variables.

---

## 6. Azure Production Topology

```mermaid
graph TB
    subgraph Internet
        U["Users"]
    end

    subgraph Azure
        SWA["Azure Static Web App\n(Angular)"]
        ACA["Azure Container Apps"]

        subgraph ACA
            API["projects_backend\nASP.NET Core"]
            NLP["projects_nlp\nPython FastAPI"]
        end

        PG["Azure Database\nfor PostgreSQL\n(Flexible Server)"]
        BLOB["Azure Blob Storage\n(survey-csvs container)"]
        KV["Azure Key Vault\n(secrets)"]
    end

    U --> SWA
    SWA --> API
    API --> NLP
    API --> PG
    API --> BLOB
    API --> KV
    NLP --> KV
```

---

## 7. Build-Out Phases

### Phase 1 — Backend Foundation
- [ ] Replace Nuxt frontend folder with Angular project (`ng new frontend`)
- [ ] Add `surveys`, `survey_columns`, `survey_responses`, `response_values`, `sentiment_results` EF Core entities + migration
- [ ] `POST /api/surveys` — accept multipart CSV, upload to Blob, parse, persist rows (no sentiment yet)
- [ ] `GET /api/surveys` and `GET /api/surveys/{id}/responses`

### Phase 2 — Sentiment Microservice
- [ ] Create `nlp/` directory at project root
- [ ] FastAPI app with `POST /analyze` using `cardiffnlp/twitter-roberta-base-sentiment-latest`
- [ ] Add `nlp` service to `docker-compose.yml`
- [ ] Wire `SentimentClient` in ASP.NET Core → call NLP service during CSV processing

### Phase 3 — KPI Aggregation
- [ ] `GET /api/kpis?surveyId=` — returns sentiment distribution, score averages per column, total responses
- [ ] Angular: CSV upload flow → progress indicator → redirect to dashboard
- [ ] Angular: Dashboard with Chart.js (donut for sentiment split, bar/line for scores over time)

### Phase 4 — Azure Deployment
- [ ] Provision Azure PostgreSQL Flexible Server + Blob Storage + Key Vault
- [ ] Dockerfiles for `backend` and `nlp` services
- [ ] Azure Container Apps deployment (or App Service)
- [ ] Angular → Azure Static Web Apps
- [ ] GitHub Actions CI/CD pipeline

---

## Key Technical Decisions

| Decision | Choice | Reason |
|---|---|---|
| Sentiment model | `cardiffnlp/twitter-roberta-base-sentiment-latest` | Pretrained, open-source, good general sentiment |
| NLP service framework | FastAPI | Async, lightweight, auto OpenAPI docs |
| CSV processing | Synchronous in request (Phase 1), consider background job if files are large | Simple to start; can add Azure Service Bus queue later |
| Frontend | Angular (replacing Nuxt) | User requirement |
| File storage | Azure Blob Storage | Keep DB lean; CSVs can be large |
| Secrets | Azure Key Vault (prod) / `.env` (local) | Security |
