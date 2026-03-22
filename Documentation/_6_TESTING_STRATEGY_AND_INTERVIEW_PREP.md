# Testing Strategy & Interview Prep — NVIDIA Compiler QA Role

This document maps every skill from the NVIDIA job description to something concrete
built in project_S. Use it to guide what to build, what to learn, and what to say.

---

## 1. Skills Map — Job Requirement → What We Build

| NVIDIA Requirement | What We Build in project_S | Interview Talking Point |
|---|---|---|
| Write test plans | `docs/TEST_PLAN.md` per service | "I authored formal test plans covering scope, risk, entry/exit criteria" |
| Implement test cases | xUnit (API), pytest (NLP), Playwright (Angular) | "I implemented unit, integration, and e2e tests across a multi-service stack" |
| Test automation | GitHub Actions runs all tests on every PR | "Tests run automatically — no manual trigger needed" |
| Author test reports | GitHub Actions uploads HTML coverage + results | "Reports are generated and archived as CI artifacts on every run" |
| Regression root cause | Baseline JSON + diff on every run | "I built a performance baseline system that flags regressions automatically" |
| Performance analysis | BenchmarkDotNet + pytest-benchmark | "I measured CSV processing throughput and NLP latency, tracked trends over time" |
| Identify outliers | Statistical thresholds on benchmark data | "I flagged results that deviated more than 2 standard deviations from baseline" |
| Maintain baselines | `benchmarks/baselines/` committed to repo | "Baselines live in source control — any regression is visible in the PR diff" |
| Improve test coverage | Coverage reports (Coverlet + pytest-cov) | "I tracked line and branch coverage and set a minimum gate in CI" |
| Collaborate cross-team | PR checks block merge on test failure | "Tests are the contract between me and the rest of the team" |

---

## 2. Testing Layers for project_S

```
┌────────────────────────────────────────────────────────────┐
│  Layer 4 — End-to-End (Playwright)                         │
│  Angular UI → API → DB → back to UI                        │
│  Slowest. Run on merge to main only.                        │
├────────────────────────────────────────────────────────────┤
│  Layer 3 — Integration Tests                               │
│  ASP.NET WebApplicationFactory (real DB, real HTTP)        │
│  Python: pytest + httpx against running FastAPI             │
│  Run on every PR.                                           │
├────────────────────────────────────────────────────────────┤
│  Layer 2 — Unit Tests                                      │
│  ASP.NET: xUnit — CsvParserService, SentimentClient        │
│  Python: pytest — analyze() function, model output shape   │
│  Angular: Jasmine/Karma — components, API service          │
│  Run on every push.                                         │
├────────────────────────────────────────────────────────────┤
│  Layer 1 — Performance / Benchmark Tests                   │
│  BenchmarkDotNet (C#) — CSV parsing, DB writes             │
│  pytest-benchmark (Python) — NLP inference latency         │
│  Run on schedule (nightly) and before releases.            │
└────────────────────────────────────────────────────────────┘
```

---

## 3. What to Build & In What Order

### Phase 1 — Unit Tests (Start Here)

**Backend (C# / xUnit)**

File: `backend/tests/ProjectS.Tests/`

```
ProjectS.Tests/
  Unit/
    CsvParserServiceTests.cs     ← parse valid CSV, empty file, missing columns
    SentimentClientTests.cs      ← mock HTTP, verify request shape + error handling
    SurveyValidatorTests.cs      ← file size limits, unsupported formats
  Integration/
    SurveyEndpointTests.cs       ← WebApplicationFactory, real DB (test container)
    HealthEndpointTests.cs       ← GET /api/health returns 200 + "connected"
```

Run: `dotnet test` from `backend/`

**Python NLP service (pytest)**

File: `nlp/tests/`

```
nlp/tests/
  test_analyze.py        ← known inputs → expected label (positive/negative/neutral)
  test_api.py            ← httpx TestClient, POST /analyze returns correct schema
  test_edge_cases.py     ← empty string, very long text, non-English input
```

Run: `pytest nlp/tests/ --cov=nlp --cov-report=html`

**Angular (Jasmine/Karma)**

```
frontend/src/
  app/
    upload/upload.component.spec.ts    ← file selection, validation feedback
    dashboard/dashboard.component.spec.ts
    services/api.service.spec.ts       ← HttpClientTestingModule
```

Run: `ng test --watch=false --code-coverage`

---

### Phase 2 — Integration Tests

**ASP.NET Core — WebApplicationFactory pattern**

```csharp
// SurveyEndpointTests.cs (example structure)
public class SurveyEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    // POST /api/surveys with a real CSV file
    // Assert: Survey row created in DB, responses saved, status = Complete
}
```

Uses **Testcontainers** (NuGet: `Testcontainers.PostgreSql`) to spin up a real
PostgreSQL instance per test run — no mocking, no shared state.

**Python — full request cycle**

```python
# test_api.py (example)
def test_analyze_positive():
    response = client.post("/analyze", json={"text": "I love this product"})
    assert response.status_code == 200
    assert response.json()["label"] == "positive"
    assert response.json()["positive_score"] > 0.7
```

---

### Phase 3 — Performance Benchmarks

**C# — BenchmarkDotNet**

File: `backend/benchmarks/ProjectS.Benchmarks/`

```csharp
[MemoryDiagnoser]
public class CsvProcessingBenchmarks
{
    [Params(100, 1000, 5000)]
    public int RowCount;

    [Benchmark]
    public void ParseCsvRows() { /* ... */ }

    [Benchmark]
    public async Task SaveToDatabase() { /* ... */ }
}
```

Produces JSON results → save to `benchmarks/baselines/dotnet_baseline.json`

**Python — pytest-benchmark**

```python
def test_inference_speed(benchmark):
    result = benchmark(analyze_sentiment, "This is a test sentence.")
    assert result["label"] in ["positive", "neutral", "negative"]
```

Produces JSON → save to `benchmarks/baselines/nlp_baseline.json`

**Regression detection script** (`benchmarks/compare_baselines.py`):

```
Load current results
Load baseline JSON
For each metric: compute % change
If any metric degrades > threshold (e.g. 10%): exit(1)  ← fails CI
Print table: metric | baseline | current | delta | status
```

This is the core skill NVIDIA is hiring for: automated regression detection.

---

### Phase 4 — End-to-End Tests (Playwright)

File: `e2e/tests/`

```
e2e/tests/
  upload-flow.spec.ts       ← upload CSV → wait for processing → assert dashboard loads
  dashboard-kpis.spec.ts    ← sentiment chart rendered, correct counts displayed
  error-states.spec.ts      ← invalid file type, oversized file, server error
```

Run: `npx playwright test`

---

## 4. GitHub Actions Pipeline

File: `.github/workflows/ci.yml`

```
Triggers:
  push → any branch
  pull_request → main

Jobs:
┌─────────────────────────────────────────────┐
│  job: test-backend                           │
│    - dotnet restore                          │
│    - dotnet test --collect:"XPlat Code Cov" │
│    - Upload coverage to Codecov / artifact   │
├─────────────────────────────────────────────┤
│  job: test-nlp                               │
│    - pip install -r requirements.txt         │
│    - pytest --cov --cov-report=xml           │
│    - Upload coverage artifact                │
├─────────────────────────────────────────────┤
│  job: test-frontend                          │
│    - npm ci                                  │
│    - ng test --watch=false --code-coverage   │
│    - Upload coverage artifact                │
├─────────────────────────────────────────────┤
│  job: benchmarks (nightly schedule only)     │
│    - Run BenchmarkDotNet + pytest-benchmark  │
│    - Run compare_baselines.py                │
│    - Upload benchmark results artifact       │
│    - Fail job if regression detected         │
├─────────────────────────────────────────────┤
│  job: e2e (main branch only)                 │
│    - docker-compose up --build -d            │
│    - npx playwright test                     │
│    - Upload Playwright HTML report artifact  │
└─────────────────────────────────────────────┘
```

**Why this matters for the interview:**
"Every PR is blocked from merging if unit or integration tests fail. Benchmarks run
nightly and automatically compare against a committed baseline — any regression over
10% fails the job and notifies the team."

---

## 5. Azure Integration

| Azure Service | Role in Testing |
|---|---|
| Azure Database for PostgreSQL | Integration tests hit a real cloud DB in CI (not a local mock) |
| Azure Blob Storage | Integration tests upload real CSV files and verify retrieval |
| Azure Container Registry | CI builds and pushes Docker images; test job pulls them |
| Azure Monitor / App Insights | Runtime performance data — latency, error rates, throughput |
| GitHub Actions → Azure | `azure/login` action + service principal for deployment gating |

**Key pattern:** The CI pipeline can deploy to a staging slot in Azure, run integration
tests against it, and only promote to production if all tests pass. This is called
**deployment gating** — a real production practice worth mentioning.

---

## 6. Formal Test Plan (What to Write)

For each service, maintain a `TEST_PLAN.md`. Structure:

```
1. Scope         — what is and isn't tested
2. Test Types    — unit / integration / e2e / performance
3. Entry Criteria — what must be true before testing starts
                   (e.g., Docker running, DB migrated)
4. Exit Criteria  — what must be true to consider testing done
                   (e.g., 80% coverage, all critical paths pass)
5. Risk Areas     — what could break (CSV parsing edge cases,
                    model timeout, DB connection pool exhaustion)
6. Test Cases     — table: ID | description | input | expected output | pass/fail
```

Writing this document is something most junior developers skip. Having one is a
strong differentiator.

---

## 7. Statistical Analysis — Outlier Detection

This directly maps to: *"Familiarity with statistical analysis tools for identifying
and isolating out-of-bound behavior."*

Apply it to sentiment score data in project_S:

```python
# benchmarks/outlier_analysis.py
import json, statistics

def find_outliers(scores: list[float], threshold_stdev: float = 2.0):
    mean = statistics.mean(scores)
    stdev = statistics.stdev(scores)
    return [s for s in scores if abs(s - mean) > threshold_stdev * stdev]
```

Use cases:
- Flag survey responses where sentiment model confidence is unusually low
- Flag benchmark runs where latency is an outlier (flaky test vs. real regression)
- Track sentiment score distributions across surveys over time (baseline drift)

In the interview: *"I used standard deviation thresholds to separate statistical noise
from real performance regressions — the same principle used in production monitoring."*

---

## 8. Toolchain Summary

| Tool | Purpose | Where Used |
|---|---|---|
| xUnit | .NET unit + integration tests | `backend/tests/` |
| Testcontainers | Spin up real PostgreSQL in tests | Backend integration tests |
| BenchmarkDotNet | .NET micro-benchmarks | `backend/benchmarks/` |
| Coverlet | .NET code coverage | Backend CI job |
| pytest | Python unit + integration tests | `nlp/tests/` |
| pytest-benchmark | Python performance benchmarks | `nlp/tests/` |
| pytest-cov | Python code coverage | NLP CI job |
| httpx | Async HTTP client for FastAPI tests | NLP integration tests |
| Jasmine/Karma | Angular unit tests | `frontend/` |
| Playwright | End-to-end browser tests | `e2e/` |
| GitHub Actions | CI/CD pipeline | `.github/workflows/` |
| Azure Container Apps | Staging + production hosting | Deployment |
| Azure Monitor | Runtime perf tracking | Production |

---

## 9. Interview Talking Points Cheat Sheet

**"Tell me about your testing experience."**
> "I built a multi-layer test suite for a fullstack project: unit tests with xUnit and
> pytest, integration tests using WebApplicationFactory against a real PostgreSQL
> database, and end-to-end tests with Playwright. Everything runs automatically in
> GitHub Actions on every pull request."

**"How do you handle performance regressions?"**
> "I set up nightly benchmark jobs using BenchmarkDotNet and pytest-benchmark. Results
> are saved as JSON and compared against a committed baseline. If any metric degrades
> beyond a threshold, the CI job fails and the team is notified before it reaches
> production."

**"How do you ensure test coverage doesn't slip?"**
> "Coverage is collected on every CI run using Coverlet and pytest-cov. I set a minimum
> threshold in the pipeline — if coverage drops below it, the PR is blocked. Reports are
> uploaded as artifacts so reviewers can see exactly which lines aren't covered."

**"What's your approach to identifying outliers in performance data?"**
> "I used standard deviation analysis — any result more than two standard deviations
> from the historical mean gets flagged. This separates genuine regressions from noise,
> which is especially important when test environments have variable load."

**"How do you collaborate with developers on quality?"**
> "Tests are part of the PR contract. I structured the pipeline so that a failing test
> blocks the merge — not as a gatekeeping measure, but so quality becomes a shared
> responsibility, not a phase at the end."
