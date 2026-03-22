# How It All Connects — Python NLP Service Architecture

This document covers the Python FastAPI sentiment analysis service in `nlp/`.
Read it alongside the source files — each section points to the exact file and
what to look for.

---

## 1. Project Structure

```mermaid
graph TD
    subgraph Root["nlp/"]
        A["pyproject.toml\nDependencies + uv config\ntorch routed to CPU wheel index"]
        B["uv.lock\nExact locked versions\nfor all 47 packages"]
        C["requirements.txt\nPlaceholder — points to pyproject.toml"]
        D["Dockerfile\nuv-based build\nModel baked in at build time"]
    end

    subgraph App["nlp/app/"]
        E["__init__.py\n(empty — marks app/ as a Python package)"]
        F["main.py\nFastAPI app\nLifespan hook\nEndpoints: /health + /analyze"]
        G["model.py\nModel loader + inference\n_pipeline global variable\nload_model() + analyze()"]
    end

    F -->|imports| G
    D -->|copies| App
```

---

## 2. Application Lifecycle

The model is loaded **once at startup**, not per request. This is the most
important design decision in the service — the model is 700MB and takes 2–5
seconds to load.

```mermaid
sequenceDiagram
    participant Docker  as Docker / Process Start
    participant Uvicorn as uvicorn (ASGI server)
    participant Main    as app/main.py
    participant Model   as app/model.py
    participant HF      as Hugging Face\ntransformers cache

    Docker->>Uvicorn: CMD uvicorn app.main:app --host 0.0.0.0 --port 8000
    Uvicorn->>Main: import app module
    Main->>Main: @asynccontextmanager lifespan(app) registered
    Uvicorn->>Main: enter lifespan context (startup phase)
    Main->>Model: load_model()
    Model->>HF: pipeline("text-classification",\nmodel="cardiffnlp/twitter-roberta-base-sentiment-latest",\nreturn_all_scores=True)
    HF-->>Model: load weights from ~/.cache/huggingface/\n(pre-downloaded at Docker build time)
    Model->>Model: _pipeline = loaded pipeline object\n(module-level global variable)
    Model-->>Main: return (model ready)
    Main->>Main: yield — service is now live
    Uvicorn-->>Docker: listening on :8000
```

**Why a module-level global?** `_pipeline` is set at the module level in
`model.py`. Any code that calls `analyze()` — regardless of which request it
belongs to — uses the same pipeline object. Python's GIL means this is safe
for the single-threaded async FastAPI model without locks.

---

## 3. Module + Function Diagram

```mermaid
classDiagram
    class main_module ["app/main.py"] {
        +FastAPI app
        +asynccontextmanager lifespan(app)
        +GET /health() dict
        +POST /analyze(request: AnalyzeRequest) AnalyzeResponse
    }

    class model_module ["app/model.py"] {
        -Pipeline _pipeline
        -int TypeDetectionSampleSize
        +load_model() None
        +analyze(text: str) dict
    }

    class AnalyzeRequest {
        +str text
    }

    class AnalyzeResponse {
        +str label
        +float positive
        +float neutral
        +float negative
    }

    main_module --> model_module : imports load_model, analyze
    main_module --> AnalyzeRequest : receives
    main_module --> AnalyzeResponse : returns
```

---

## 4. Request Lifecycle — POST /analyze

```mermaid
sequenceDiagram
    participant ASPNET as ASP.NET Core\nSentimentClient
    participant FastAPI as app/main.py\nPOST /analyze
    participant Model   as app/model.py\nanalyze()
    participant Pipeline as transformers\nRoBERTa Pipeline

    ASPNET->>FastAPI: POST /analyze\n{ "text": "Great product!" }
    FastAPI->>FastAPI: Pydantic validates AnalyzeRequest\ntext field must be a string
    FastAPI->>FastAPI: request.text.strip() == "" ?\nif yes → 400 HTTPException
    FastAPI->>Model: run_sentiment(request.text)
    Model->>Model: check _pipeline is not None
    Model->>Pipeline: _pipeline("Great product!")
    Pipeline->>Pipeline: tokenize → RoBERTa forward pass\n→ softmax over 3 labels
    Pipeline-->>Model: [ {label: "negative", score: 0.01},\n  {label: "neutral",  score: 0.03},\n  {label: "positive", score: 0.96} ]
    Model->>Model: scores = { label.lower(): score }\n→ { "negative": 0.01, "neutral": 0.03, "positive": 0.96 }
    Model->>Model: label = max(scores, key=lambda k: scores[k])\n→ "positive"
    Model-->>FastAPI: { label: "positive", positive: 0.96,\n  neutral: 0.03, negative: 0.01 }
    FastAPI->>FastAPI: Pydantic serializes to AnalyzeResponse
    FastAPI-->>ASPNET: 200 { "label": "positive",\n"positive": 0.96, "neutral": 0.03, "negative": 0.01 }
```

---

## 5. Model Output — Why `return_all_scores=True`

The `transformers` pipeline has two modes:

| Mode | What it returns | Used for |
|---|---|---|
| Default (`return_all_scores=False`) | `[{ label: "positive", score: 0.96 }]` | Only the winning label + its score |
| `return_all_scores=True` | `[{ label: "negative", score: 0.01 }, { label: "neutral", score: 0.03 }, { label: "positive", score: 0.96 }]` | All three labels + all three scores |

This project uses `return_all_scores=True` because the database stores all
three scores (`PositiveScore`, `NeutralScore`, `NegativeScore`) in `SentimentResult`.
Storing all three lets the dashboard show full sentiment distributions, not
just the winning label.

---

## 6. Label Mapping — cardiffnlp Model

The model `cardiffnlp/twitter-roberta-base-sentiment-latest` returns these labels:

| Model label | Normalized (`.lower()`) | Stored as |
|---|---|---|
| `negative` | `negative` | `SentimentResult.Label = "negative"` |
| `neutral` | `neutral` | `SentimentResult.Label = "neutral"` |
| `positive` | `positive` | `SentimentResult.Label = "positive"` |

The `.lower()` call in `model.py` is defensive — if the model ever returns
uppercase labels (`NEGATIVE`, `POSITIVE`) from a different checkpoint, the
normalization prevents a mismatch with the values the C# code expects.

---

## 7. Pydantic Request/Response Validation

FastAPI uses **Pydantic** models to automatically validate incoming JSON bodies
and serialize outgoing responses.

```mermaid
flowchart LR
    A["Incoming JSON\n{ text: 123 }"] -->|Pydantic AnalyzeRequest| B{text is str?}
    B -->|no| C["422 Unprocessable Entity\nautomatic — no code needed"]
    B -->|yes| D["text.strip() empty?"]
    D -->|yes| E["400 HTTPException\n'text must not be empty'"]
    D -->|no| F["analyze(text)"]
    F --> G["Return dict\n{ label, positive, neutral, negative }"]
    G -->|Pydantic AnalyzeResponse| H["Serialized JSON\n{ label: str, positive: float ... }"]
```

If the ASP.NET Core API ever sends malformed JSON (e.g. a missing `text` field),
Pydantic returns a `422` response automatically. The C# `SentimentClient` treats
any non-success status as `null` and skips that cell — the upload continues.

---

## 8. Docker Build — Model Baking

```mermaid
flowchart TD
    A["FROM python:3.11-slim"] --> B["COPY --from ghcr.io/astral-sh/uv\n/uv /uvx → /bin/"]
    B --> C["apt-get install curl\n(needed for healthcheck)"]
    C --> D["COPY pyproject.toml uv.lock"]
    D --> E["uv sync --frozen --no-install-project\nInstalls all Python packages\nincluding CPU-only torch ~700MB"]
    E --> F["COPY app/ ./app/"]
    F --> G["RUN uv run python -c\n'pipeline(...model=cardiffnlp/...)'\nDownloads model weights ~500MB\ninto /root/.cache/huggingface/"]
    G --> H["useradd appuser\nchown -R appuser /app\nchown -R appuser /root/.cache"]
    H --> I["USER appuser"]
    I --> J["CMD uvicorn app.main:app\n--host 0.0.0.0 --port 8000"]
```

**Why bake the model at build time?**

| Approach | Build time | Startup time | Network dependency at runtime |
|---|---|---|---|
| Download at container start | Fast | 30–60s per cold start | Yes — fails if no internet |
| Bake into image (used here) | Slow (one time) | ~2s | No |

The `chown -R appuser /root/.cache` step is critical — the model weights are
cached in root's home directory during build. Transferring ownership ensures
the non-root `appuser` can read them at runtime.

---

## 9. uv Dependency Management

```mermaid
flowchart LR
    A["pyproject.toml\n[project] dependencies\nfastapi, uvicorn, transformers\ntorch, scipy, numpy"] --> B["[tool.uv.sources]\ntorch = { index: pytorch-cpu }"]
    B --> C["[[tool.uv.index]]\nname: pytorch-cpu\nurl: download.pytorch.org/whl/cpu"]
    C --> D["uv sync\nResolves all 47 packages\nRoutes torch to CPU wheel\nWrites uv.lock"]
    D --> E["uv.lock\nExact pinned versions\nReproducible across\nall environments"]
```

The `[tool.uv.sources]` override is what makes `torch` come from the CPU-only
wheel index. Without it, `uv` would pull the default PyPI `torch` package which
includes CUDA support — a 3.5GB download that we don't need since the model runs
fast enough on CPU for batch survey processing.

---

## 10. Health Check

```mermaid
sequenceDiagram
    participant Docker
    participant Health as GET /health

    loop every 30s
        Docker->>Health: curl -f http://localhost:8000/health
        Health-->>Docker: 200 { "status": "healthy" }
    end

    Note over Docker: If 3 consecutive checks fail\n(timeout 10s each)\nDocker marks container unhealthy
```

The `GET /health` endpoint in `main.py` returns immediately without calling
the model or touching any state. It exists purely to confirm the web server
is up and the port is reachable. The ASP.NET Core `SentimentClient` does not
currently call `/health` before sending requests — this is a future improvement
for the Step 6 async worker.
