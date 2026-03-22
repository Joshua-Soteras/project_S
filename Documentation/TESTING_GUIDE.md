# Testing Guide — Project S

This document is the complete testing reference for Project S. It covers the
philosophy, methodology, tooling, and step-by-step implementation plan for
every layer of the stack. Read this before writing a single test.

---

## Table of Contents

1. [Testing Methodology — TDD](#1-testing-methodology--tdd)
2. [The Testing Pyramid](#2-the-testing-pyramid)
3. [Code Coverage — What It Is and Why It Matters](#3-code-coverage--what-it-is-and-why-it-matters)
4. [Python — pytest (Deep Dive)](#4-python--pytest-deep-dive)
5. [C# — xUnit (Unit + Integration)](#5-c--xunit-unit--integration)
6. [Angular — Jest (Recommended over Karma)](#6-angular--jest-recommended-over-karma)
7. [GitHub Actions CI Pipeline](#7-github-actions-ci-pipeline)
8. [TDD Workflow — Step by Step With Real Examples](#8-tdd-workflow--step-by-step-with-real-examples)

---

## 1. Testing Methodology — TDD

### What is TDD?

**Test-Driven Development (TDD)** is a development practice where you write the
test *before* you write the code it tests. This feels backwards at first. It is
not. It is one of the most important professional habits in software engineering.

### The Red-Green-Refactor Cycle

Every feature or bug fix in TDD follows exactly three steps. Always in this order.

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│   RED  →  GREEN  →  REFACTOR  →  RED  →  GREEN  →ループ │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**RED — Write a failing test first**

Before you write any production code, write a test that describes the behavior
you want. Run it. It must fail (it's red). If it passes without you writing
code, your test is wrong — it's testing nothing.

Example: You want `analyze("Great product!")` to return `label = "positive"`.
Write the test that asserts this. Run it. It fails because `analyze` doesn't
exist yet. That red failure is what you want.

**GREEN — Write the minimum code to make the test pass**

Now write the simplest, ugliest code that makes the test pass. You are not
allowed to write more code than the test requires. No "just in case" logic.
No extra features. The only goal is green.

**REFACTOR — Clean up without breaking the test**

Now that the test is green and the behavior is locked in, clean up the code.
Rename things, remove duplication, improve readability. Run the test after
every change. If it goes red, undo the last change. The test is your safety net.

### Why TDD is the Professional Standard

| Without TDD | With TDD |
|---|---|
| Tests are written as an afterthought, if at all | Behavior is specified before implementation |
| Tests often test the wrong thing (they pass by accident) | Tests fail first, proving they actually catch bugs |
| Refactoring is scary — you might break something silently | Refactoring is safe — tests catch regressions immediately |
| Design suffers — code is hard to test because it was never designed to be testable | Design improves — testable code is always better structured code |
| Bugs are found in production | Bugs are found in seconds, locally |

### What TDD is NOT

- It is not about having 100% code coverage
- It is not about testing obvious things (`assert 1 + 1 == 2`)
- It is not slower — it feels slower at first, then dramatically speeds you up
  because you debug less

---

## 2. The Testing Pyramid

The testing pyramid describes the mix of test types a professional codebase
should have. The shape matters: many small fast tests at the bottom, few slow
expensive tests at the top.

```
                    ┌───────────────┐
                    │               │
                    │   E2E Tests   │  ← Few. Slow. Test full user flows.
                    │               │     (Playwright, Cypress)
                   /│               │\
                  / └───────────────┘ \
                 /                     \
                ┌─────────────────────────┐
                │                         │
                │   Integration Tests     │  ← Some. Medium speed.
                │                         │     Test components working together.
               /│   (real DB, real HTTP)  │\    (EF Core + real PostgreSQL,
              / └─────────────────────────┘ \    FastAPI TestClient)
             /                               \
            ┌─────────────────────────────────┐
            │                                 │
            │         Unit Tests              │  ← Many. Fast. Test one thing.
            │                                 │     (xUnit, pytest, Jest)
            │  (mocked dependencies, isolated)│
            └─────────────────────────────────┘
```

### In This Project

| Type | Tools | Speed | Count target | What they test |
|---|---|---|---|---|
| Unit | pytest, xUnit, Jest | < 1s each | ~60–80% of all tests | Single function/class in isolation |
| Integration | pytest (TestClient), xUnit + Testcontainers | 2–10s | ~20–35% | Service talking to real DB or HTTP |
| E2E | Deferred to Step 9 | 10–60s | ~5% | Full browser → API → DB flows |

**Rule:** If a test needs a database, a network, or a running service, it is an
integration test. If it tests only one function with all dependencies replaced
by fakes, it is a unit test.

---

## 3. Code Coverage — What It Is and Why It Matters

### What is Code Coverage?

Coverage is a measurement of how much of your source code is actually executed
when your tests run. If you have 100 lines of code and your tests execute 80 of
them, you have 80% line coverage.

### Types of Coverage (from simple to thorough)

**Line coverage** — was this line executed at least once?
```python
def divide(a, b):
    if b == 0:          # ← was this line run?
        return None     # ← was this line run?
    return a / b        # ← was this line run?
```

**Branch coverage** — for every `if`, did tests hit both the `true` AND `false` path?
```python
if b == 0:    # branch 1: b IS 0  (return None)
              # branch 2: b is NOT 0 (return a/b)
```
A test calling `divide(10, 2)` gives 100% line coverage on the last line but
misses the `b == 0` branch entirely. Branch coverage catches this gap.

**Statement coverage** — similar to line coverage but counts individual statements
on the same line.

**In this project we track:** line coverage + branch coverage together (industry
standard combination).

### Coverage Tools by Layer

| Layer | Tool | Report format |
|---|---|---|
| Python | `pytest-cov` (wraps `coverage.py`) | Terminal summary + HTML + XML |
| C# | `coverlet` + `ReportGenerator` | XML (consumed by CI) + HTML |
| Angular | `@jest/coverage` (built-in, wraps Istanbul) | Terminal summary + LCOV |

### What Threshold Should We Use?

**80% line coverage is the widely accepted industry floor.** This is the number
most engineering teams and CI pipelines enforce.

Important nuances:
- 80% on the **meaningful** paths matters more than 100% everywhere.
- Some code is not worth testing (auto-generated migrations, boilerplate DI
  registration). These are excluded from coverage.
- Chasing 100% often leads to writing tests that pass trivially and test nothing
  meaningful. The goal is confidence, not a number.

**What 80% means in practice:** if you have a `CsvParserService` with 10 logical
paths (happy path, empty file, no headers, malformed rows, all-text columns,
no-text columns…), your tests must cover at least 8 of them.

### What Coverage Does NOT Tell You

Coverage tells you which lines ran. It does NOT tell you whether the assertions
are correct. You can have 100% coverage with wrong assertions.

```python
def add(a, b):
    return a - b   # ← bug: should be +

def test_add():
    add(1, 2)      # ← 100% coverage, but no assert → worthless
```

Coverage + meaningful assertions = confidence.

---

## 4. Python — pytest (Deep Dive)

### Why pytest over unittest

Python's built-in `unittest` is fine. `pytest` is strictly better:
- No class boilerplate required — functions are tests
- `assert` works naturally — no `assertEqual`, `assertTrue`, etc.
- Fixtures are more powerful than `setUp`/`tearDown`
- Rich plugin ecosystem (`pytest-cov`, `pytest-asyncio`, `httpx`)
- Better error output on failure

### Project Test Structure

```
nlp/
├── app/
│   ├── main.py
│   └── model.py
├── tests/
│   ├── __init__.py          ← makes tests/ a package
│   ├── conftest.py          ← shared fixtures (explained below)
│   ├── unit/
│   │   ├── __init__.py
│   │   ├── test_model.py    ← tests for analyze(), load_model()
│   │   └── test_main.py     ← tests for /health, /analyze endpoints
│   └── integration/
│       ├── __init__.py
│       └── test_api.py      ← TestClient hitting real FastAPI app
├── pyproject.toml
└── uv.lock
```

### Key pytest Concepts

#### Fixtures

A **fixture** is a function that provides a piece of setup to a test. Instead
of copy-pasting setup code into every test, you define it once and inject it.

```python
# conftest.py
import pytest
from fastapi.testclient import TestClient
from app.main import app

@pytest.fixture
def client():
    """Provides a TestClient connected to the FastAPI app."""
    return TestClient(app)
```

Now any test in the project can receive `client` as a parameter:

```python
def test_health(client):             # ← pytest injects client automatically
    response = client.get("/health")
    assert response.status_code == 200
```

Fixtures can have **scope**: `function` (default — recreated per test),
`module` (once per file), `session` (once per entire test run). Use `session`
scope for expensive setup like loading the ML model.

#### Monkeypatching (Mocking in pytest)

**Mocking** replaces a real dependency with a fake one you control. This is
the key to unit testing — you isolate the code under test from everything else.

`pytest`'s built-in `monkeypatch` fixture lets you swap out any object:

```python
def test_analyze_returns_positive(monkeypatch):
    # Replace the real _pipeline with a fake that returns known scores
    def fake_pipeline(text):
        return [[
            {"label": "negative", "score": 0.01},
            {"label": "neutral",  "score": 0.03},
            {"label": "positive", "score": 0.96},
        ]]

    import app.model as model
    monkeypatch.setattr(model, "_pipeline", fake_pipeline)

    result = model.analyze("Great product!")

    assert result["label"] == "positive"
    assert result["positive"] == pytest.approx(0.96)
    assert result["negative"] == pytest.approx(0.01)
```

Why mock the pipeline here? Because:
1. Loading the real RoBERTa model takes 2–5 seconds and 700MB RAM.
2. Unit tests should be fast (< 1s).
3. We are testing the **logic** of `analyze()` — the label-selection and
   score-extraction logic — not the ML model itself. The model is correct
   by definition (it's a library). Our code consuming its output is what
   could have bugs.

#### parametrize — Testing Multiple Inputs with One Test

Instead of writing 10 nearly-identical tests, `@pytest.mark.parametrize`
runs one test function with multiple input/output pairs:

```python
@pytest.mark.parametrize("text,expected_label", [
    ("This is absolutely amazing!", "positive"),
    ("I hate this product.",        "negative"),
    ("It is okay I guess.",         "neutral"),
])
def test_analyze_label(monkeypatch, text, expected_label):
    # mock pipeline to return dominant score matching expected_label
    ...
    result = model.analyze(text)
    assert result["label"] == expected_label
```

This generates three separate test cases in one block. If one fails, pytest
shows exactly which input caused the failure.

#### pytest.approx — Floating Point Comparison

Never use `==` with floats. `0.96 == 0.9600000001` is `False`.

```python
# ❌ Flaky
assert result["positive"] == 0.96

# ✅ Correct
assert result["positive"] == pytest.approx(0.96, abs=1e-4)
```

`pytest.approx` checks that the values are within a tolerance (default 1e-6).

#### Async Tests

FastAPI is async. Some functions use `async def`. Use `pytest-asyncio`:

```python
import pytest

@pytest.mark.asyncio
async def test_lifespan_loads_model():
    # Test that model is loaded after app startup
    ...
```

### What to Test in This Project (Python)

#### Unit Tests — `tests/unit/test_model.py`

| Test | What it verifies |
|---|---|
| `test_analyze_returns_positive_label` | When pipeline scores positive highest, `label` = "positive" |
| `test_analyze_returns_negative_label` | When pipeline scores negative highest, `label` = "negative" |
| `test_analyze_returns_neutral_label` | When pipeline scores neutral highest, `label` = "neutral" |
| `test_analyze_score_extraction` | All three float scores are extracted correctly from pipeline output |
| `test_analyze_label_is_lowercased` | Pipeline returns "POSITIVE" → `label` stored as "positive" |
| `test_analyze_scores_sum_to_approx_one` | positive + neutral + negative ≈ 1.0 |
| `test_load_model_sets_pipeline` | After `load_model()`, `_pipeline` is not None |
| `test_analyze_raises_if_pipeline_not_loaded` | If `_pipeline` is None, `analyze()` raises an informative error |

#### Unit Tests — `tests/unit/test_main.py`

| Test | What it verifies |
|---|---|
| `test_health_returns_200` | GET /health → 200 `{"status": "healthy"}` |
| `test_analyze_valid_text` | POST /analyze `{"text": "Great!"}` → 200 with label + scores |
| `test_analyze_empty_string_returns_400` | POST /analyze `{"text": ""}` → 400 |
| `test_analyze_whitespace_only_returns_400` | POST /analyze `{"text": "   "}` → 400 |
| `test_analyze_missing_text_field_returns_422` | POST /analyze `{}` → 422 (Pydantic validation) |
| `test_analyze_non_string_text_returns_422` | POST /analyze `{"text": 123}` → 422 |
| `test_analyze_response_shape` | Response has exactly: label, positive, neutral, negative |
| `test_analyze_label_is_string` | `label` field type is str |
| `test_analyze_scores_are_floats` | positive/neutral/negative are floats in [0, 1] |

#### Integration Tests — `tests/integration/test_api.py`

These tests use the **real loaded model** (no mocks). They run in CI but are
marked `@pytest.mark.integration` so you can skip them locally:

```bash
pytest -m "not integration"   # fast — unit tests only
pytest -m integration         # slow — loads real model
```

| Test | What it verifies |
|---|---|
| `test_real_positive_text` | "I love this product!" → label="positive" with score > 0.7 |
| `test_real_negative_text` | "Worst experience ever." → label="negative" |
| `test_real_neutral_text` | "The item arrived." → label="neutral" |
| `test_analyze_very_long_text` | 512+ token text does not crash (RoBERTa truncates at 512) |
| `test_analyze_special_characters` | Text with emoji, punctuation, unicode works without error |
| `test_health_check_with_loaded_model` | /health returns 200 after model is loaded |

### Configuration in `pyproject.toml`

```toml
[tool.pytest.ini_options]
testpaths = ["tests"]
asyncio_mode = "auto"
markers = [
    "integration: marks tests that require the real ML model (deselect with -m 'not integration')",
]

[tool.coverage.run]
source = ["app"]
omit = ["tests/*"]

[tool.coverage.report]
fail_under = 80
show_missing = true
```

`fail_under = 80` makes `pytest --cov` exit with a non-zero code if coverage
drops below 80%. CI treats non-zero exit as a failed build.

### Running Tests

```bash
# From nlp/
uv run pytest                          # all tests
uv run pytest -m "not integration"     # unit tests only (fast)
uv run pytest -m integration           # integration tests only
uv run pytest --cov --cov-report=html  # with coverage HTML report
uv run pytest -v                       # verbose (show each test name)
uv run pytest tests/unit/test_model.py # single file
uv run pytest -k "test_analyze"        # tests matching a name pattern
```

---

## 5. C# — xUnit (Unit + Integration)

### Project Structure

```
backend/
├── src/
│   ├── _01_Core/
│   ├── _02_Infrastructure/
│   └── _03_Web_API/
└── tests/
    ├── _04_Unit_Tests/              ← new project: ProjectS.UnitTests
    │   ├── _04_Unit_Tests.csproj
    │   ├── Services/
    │   │   ├── CsvParserServiceTests.cs
    │   │   ├── SentimentClientTests.cs
    │   │   └── SurveyProcessingServiceTests.cs
    │   └── GlobalUsings.cs
    └── _05_Integration_Tests/       ← new project: ProjectS.IntegrationTests
        ├── _05_Integration_Tests.csproj
        ├── SurveyEndpointTests.cs
        ├── Helpers/
        │   └── TestWebAppFactory.cs
        └── GlobalUsings.cs
```

### Why Two Separate Test Projects?

- **Unit tests** have zero external dependencies — they run in milliseconds
  and can run on any machine with just `dotnet test`.
- **Integration tests** need Docker (for a real PostgreSQL container via
  Testcontainers). They are slower and require more setup. Separating them
  lets CI run unit tests on every push and integration tests only on PRs.

### Unit Tests — Key Concepts

#### Mocking with `NSubstitute`

The most popular mocking library for .NET (alternative: `Moq`). Lets you
replace any interface with a fake:

```csharp
// Arrange
var mockSentiment = Substitute.For<SentimentClient>(...);
mockSentiment.AnalyzeAsync("Great!").Returns(
    new SentimentResponse("positive", 0.96f, 0.02f, 0.02f));

// Act
var result = await mockSentiment.AnalyzeAsync("Great!");

// Assert
Assert.Equal("positive", result!.Label);
```

#### The Arrange-Act-Assert Pattern (AAA)

Every xUnit test follows three phases, separated by blank lines:

```csharp
[Fact]
public async Task ParseAsync_WithValidCsv_DetectsTextColumns()
{
    // Arrange — set up the inputs and dependencies
    var service = new CsvParserService();
    var csv = "Name,Score,Feedback\nAlice,5,Great product!\nBob,3,It was okay.";
    var file = CreateFormFile(csv, "survey.csv");

    // Act — call the code under test
    var result = await service.ParseAsync(file);

    // Assert — verify the outcome
    Assert.Equal(3, result.Columns.Count);
    Assert.True(result.Columns.Single(c => c.Name == "Feedback").AnalyzeSentiment);
    Assert.False(result.Columns.Single(c => c.Name == "Score").AnalyzeSentiment);
}
```

`[Fact]` = a single test case.
`[Theory]` + `[InlineData(...)]` = parametrized test (like `pytest.mark.parametrize`).

### What to Test — Unit (`_04_Unit_Tests`)

#### `CsvParserServiceTests.cs`

| Test | What it verifies |
|---|---|
| `ParseAsync_DetectsNumericColumn` | Column of numbers → type="numeric", analyzeSentiment=false |
| `ParseAsync_DetectsTextColumn` | Column of words → type="text", analyzeSentiment=true |
| `ParseAsync_DetectsDateColumn` | Column of dates → type="date" |
| `ParseAsync_DetectsBooleanColumn` | Column of true/false → type="boolean" |
| `ParseAsync_PreservesRowOrder` | RowIndex matches original CSV order |
| `ParseAsync_HandlesEmptyValues` | Empty cells stored as null, not empty string |
| `ParseAsync_TrimsWhitespace` | " hello " stored as "hello" |
| `ParseAsync_ThrowsOnEmptyFile` | Zero rows → throws exception (not silently succeeds) |
| `ParseAsync_ReturnsCorrectColumnCount` | 5-column CSV → 5 SurveyColumn results |
| `ParseAsync_AmbiguousColumn_PrefersNumeric` | Column of "1","0","1" → numeric, not boolean |

#### `SentimentClientTests.cs`

| Test | What it verifies |
|---|---|
| `AnalyzeAsync_Returns_ParsedResponse` | 200 from NLP → returns SentimentResponse with correct fields |
| `AnalyzeAsync_Returns_Null_On_404` | 404 from NLP → returns null (does not throw) |
| `AnalyzeAsync_Returns_Null_On_500` | 500 from NLP → returns null |
| `AnalyzeAsync_Returns_Null_On_Timeout` | TaskCanceledException → returns null |
| `AnalyzeAsync_Deserializes_CamelCase` | `{"label":"positive"}` → `Label == "positive"` |

#### `SurveyProcessingServiceTests.cs`

| Test | What it verifies |
|---|---|
| `ProcessAsync_SetsStatusProcessing_BeforeNlp` | Survey.Status = "processing" before calling NLP |
| `ProcessAsync_SetsStatusComplete_OnSuccess` | Survey.Status = "complete" after all NLP succeeds |
| `ProcessAsync_SetsStatusError_OnException` | NLP throws → Survey.Status = "error" with message |
| `ProcessAsync_SkipsBlankCells` | Blank ResponseValues → AnalyzeAsync never called for them |
| `ProcessAsync_SkipsNullNlpResult` | AnalyzeAsync returns null → no SentimentResult created |
| `ProcessAsync_ComputesKpiAggregate` | 3 positive results → AvgPositive > 0, CountPositive = 3 |
| `ProcessAsync_OnlyProcessesTextColumns` | Numeric columns → AnalyzeAsync never called |

### Integration Tests — `_05_Integration_Tests`

#### Testcontainers

**Testcontainers** is a library that spins up real Docker containers during
tests and destroys them afterward. For database integration tests this is
the gold standard — you get a real PostgreSQL instance, not a fake in-memory
one. EF Core's `UseInMemoryDatabase()` is a lie: it doesn't enforce
constraints, doesn't run real SQL, and masks bugs that only appear with a
real database.

```csharp
// TestWebAppFactory.cs
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    // Testcontainers spins up a real PostgreSQL container
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("test_db")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace production DB connection with test container connection
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }
}
```

#### What to Test — Integration (`_05_Integration_Tests`)

| Test | What it verifies |
|---|---|
| `PostSurveys_ValidCsv_Returns202` | Real upload → 202 Accepted with id, status="queued" |
| `PostSurveys_InvalidExtension_Returns400` | `.txt` file → 400 with error message |
| `PostSurveys_EmptyFile_Returns400` | Empty CSV → 400 |
| `PostSurveys_SavesRowsToDatabase` | After upload, ResponseValues exist in real DB |
| `GetSurveys_Returns_AllSurveys` | Insert 2 surveys → GET /api/surveys returns both |
| `GetSurvey_ReturnsColumns_OrderedByIndex` | Columns ordered by ColumnIndex |
| `GetSurvey_Returns404_ForUnknownId` | GET /api/surveys/9999 → 404 |
| `ProcessingService_FullPipeline` | SurveyProcessingService + real DB → KpiAggregates created |

### Running C# Tests

```bash
# From backend/
dotnet test                                          # all tests
dotnet test tests/_04_Unit_Tests/                    # unit only
dotnet test tests/_05_Integration_Tests/             # integration only
dotnet test --collect:"XPlat Code Coverage"          # with coverage
dotnet test --filter "FullyQualifiedName~CsvParser"  # filter by name
```

---

## 6. Angular — Jest (Recommended over Karma)

### Why Jest over Karma/Jasmine

The default Angular setup ships with Karma (test runner) + Jasmine (assertion
library). Most professional Angular teams have migrated away from Karma to Jest.
Here is why:

| | Karma + Jasmine | Jest |
|---|---|---|
| **Runs in** | Real browser (or headless Chrome via ChromeDriver) | Node.js with jsdom (simulated DOM) |
| **Speed** | Slow — browser launch + reload per run | Fast — no browser overhead |
| **CI setup** | Requires installing Chrome in CI container | Zero extra dependencies |
| **Mocking** | Manual setup with `jasmine.createSpy()` | Built-in `jest.fn()`, `jest.spyOn()` |
| **Snapshot testing** | Not available | Built-in — great for component output |
| **Parallel execution** | Limited | Native — runs test files in parallel workers |
| **Industry trend** | Legacy, used in older projects | Industry standard for new projects |
| **Watch mode** | Slow | Instant — only reruns changed files |

**The tradeoff:** jsdom is not a real browser. Tests that depend on browser-only
APIs (canvas, WebGL, some CSS) may need a real browser. For this project's
components (forms, HTTP calls, routing), jsdom is fully sufficient.

### Project Test Structure

```
frontend-angular/
├── src/
│   └── app/
│       ├── services/
│       │   ├── api.service.ts
│       │   └── api.service.spec.ts      ← unit test (same folder, .spec.ts)
│       ├── components/
│       │   ├── dashboard/
│       │   │   ├── dashboard.ts
│       │   │   └── dashboard.spec.ts
│       │   ├── upload/
│       │   │   ├── upload.ts
│       │   │   └── upload.spec.ts
│       │   └── survey-list/
│       │       ├── survey-list.ts
│       │       └── survey-list.spec.ts
│       └── models/
│           └── survey.model.ts          ← no test needed (interface only)
├── jest.config.ts
└── package.json
```

### Key Angular/Jest Concepts

#### TestBed

`TestBed` is Angular's testing module. It creates a mini Angular app just for
your test, letting you test a component the same way Angular renders it at
runtime — with change detection, dependency injection, and template binding.

```typescript
beforeEach(async () => {
    await TestBed.configureTestingModule({
        imports: [Dashboard],                     // standalone component
        providers: [
            { provide: ApiService, useValue: mockApiService },  // inject mock
        ],
    }).compileComponents();

    fixture = TestBed.createComponent(Dashboard);
    component = fixture.componentInstance;
});
```

#### Mocking Services

In unit tests, you never use the real `ApiService` (that would make real HTTP
calls). Instead you provide a mock:

```typescript
const mockApiService = {
    getSurvey: jest.fn(),
    getSurveys: jest.fn(),
    uploadSurvey: jest.fn(),
};
```

`jest.fn()` creates a spy function you can control:

```typescript
mockApiService.getSurvey.mockReturnValue(of(fakeSurvey)); // returns Observable
```

`of(fakeSurvey)` from RxJS creates an Observable that immediately emits
`fakeSurvey` — simulating a real HTTP response without any network activity.

#### Testing Observables and Async

Angular uses RxJS Observables for HTTP calls. Tests must handle async behavior:

```typescript
it('should set survey after load', fakeAsync(() => {
    mockApiService.getSurvey.mockReturnValue(of(fakeSurvey));

    fixture.detectChanges();   // triggers ngOnInit
    tick();                    // flushes async queue
    fixture.detectChanges();   // update template bindings

    expect(component.survey).toEqual(fakeSurvey);
    expect(component.loading).toBe(false);
}));
```

`fakeAsync` + `tick()` lets you control the clock in tests, making async
code synchronous from the test's perspective.

#### Testing the Polling Behavior (Dashboard)

```typescript
it('should poll while status is queued and stop when complete', fakeAsync(() => {
    const queued  = { ...fakeSurvey, status: 'queued' };
    const complete = { ...fakeSurvey, status: 'complete' };

    // First call returns queued, second returns complete
    mockApiService.getSurvey
        .mockReturnValueOnce(of(queued))
        .mockReturnValueOnce(of(complete));

    fixture.detectChanges(); // ngOnInit → first call → queued
    tick();
    fixture.detectChanges();

    expect(component.survey!.status).toBe('queued');
    expect(mockApiService.getSurvey).toHaveBeenCalledTimes(1);

    tick(2000); // advance clock 2s → poll fires
    fixture.detectChanges();

    expect(component.survey!.status).toBe('complete');
    expect(mockApiService.getSurvey).toHaveBeenCalledTimes(2);

    // Cleanup
    fixture.destroy(); // triggers ngOnDestroy → clears interval
    discardPeriodicTasks();
}));
```

### What to Test — Angular

#### `api.service.spec.ts`

| Test | What it verifies |
|---|---|
| `getSurveys_calls_correct_endpoint` | GET request sent to `/api/surveys` |
| `getSurvey_includes_id_in_url` | GET request sent to `/api/surveys/5` for id=5 |
| `uploadSurvey_sends_form_data` | POST request body is FormData |
| `uploadSurvey_appends_file` | FormData contains `file` key |
| `uploadSurvey_appends_name_when_provided` | FormData contains `name` key |

Uses `HttpClientTestingModule` + `HttpTestingController` to intercept and
assert HTTP requests without a real server.

#### `dashboard.spec.ts`

| Test | What it verifies |
|---|---|
| `shows_loading_state_initially` | `loading = true` on init |
| `sets_survey_on_success` | After getSurvey resolves, `survey` is set |
| `sets_error_on_failure` | After getSurvey errors, `error` message is set |
| `starts_polling_when_status_queued` | setInterval called when status="queued" |
| `starts_polling_when_status_processing` | setInterval called when status="processing" |
| `stops_polling_when_status_complete` | clearInterval called when status="complete" |
| `stops_polling_when_status_error` | clearInterval called when status="error" |
| `clears_interval_on_destroy` | ngOnDestroy calls stopPolling |
| `renders_spinner_when_queued` | DOM contains spinner element when status="queued" |
| `renders_survey_name_in_template` | survey.name appears in rendered HTML |

#### `upload.spec.ts`

| Test | What it verifies |
|---|---|
| `submit_disabled_when_no_file_selected` | `selectedFile = null` → button disabled |
| `onFileSelected_stores_file` | File input change → `selectedFile` set |
| `onSubmit_calls_uploadSurvey` | Submit → `api.uploadSurvey` called with file |
| `onSubmit_navigates_to_dashboard_on_success` | 202 response → router.navigate to `/surveys/1` |
| `onSubmit_shows_error_on_failure` | 400 response → `error` set with message |
| `uploading_flag_true_during_request` | `uploading = true` while request in flight |
| `uploading_flag_false_after_response` | `uploading = false` after success or error |

### Running Angular Tests

```bash
# From frontend-angular/
npx jest                          # all tests
npx jest --watch                  # watch mode (re-runs changed files)
npx jest --coverage               # with coverage report
npx jest dashboard                # tests matching "dashboard"
npx jest --verbose                # show each test name
```

---

## 7. GitHub Actions CI Pipeline

### Industry Standard: When Pipelines Run

```
Every push to any branch  →  Run unit tests (fast gate, < 2 min)
Every PR targeting main   →  Run ALL tests (unit + integration + coverage)
Merge to main             →  Run ALL tests + publish coverage report
```

The logic: unit tests run constantly for fast feedback. Integration tests (which
need Docker and Testcontainers) are slower, so they gate the PR merge — not every
push.

### Pipeline Structure

```
.github/
└── workflows/
    ├── ci.yml              ← main pipeline: runs on push + PR
    └── (future) cd.yml     ← deploy pipeline: runs on merge to main
```

### Jobs in `ci.yml`

```
┌─────────────────────────────────────────────────────────┐
│  Trigger: push to any branch OR pull_request to main    │
├────────────────┬──────────────┬───────────────────────┤
│  test-python   │  test-dotnet │  test-angular         │
│                │              │                        │
│  uv sync       │  dotnet test │  npm ci               │
│  pytest unit   │  (unit only  │  jest --coverage      │
│  + coverage    │  on push)    │                        │
│                │              │                        │
│  On PR only:   │  On PR only: │                        │
│  pytest integ  │  dotnet test │                        │
│                │  (integ too) │                        │
└────────────────┴──────────────┴───────────────────────┘
```

All three jobs run **in parallel** — the total CI time is the slowest single
job, not the sum of all three.

### Branch Protection Rules (GitHub Settings)

Set these on the `main` branch in GitHub → Settings → Branches:

- ✅ Require status checks to pass before merging
  - `test-python`
  - `test-dotnet`
  - `test-angular`
- ✅ Require branches to be up to date before merging
- ✅ Require pull request reviews before merging (1 approval)
- ✅ Do not allow bypassing the above settings

These rules mean: **nothing merges to main without all three test jobs passing.**

### Coverage Reporting in CI

Coverage reports are generated as artifacts uploaded to GitHub Actions. On PRs,
a comment is posted showing the diff (did coverage go up or down?).

Tools:
- Python: `pytest --cov --cov-report=xml` → upload `coverage.xml`
- C#: `dotnet test --collect:"XPlat Code Coverage"` → upload `.cobertura.xml`
- Angular: `jest --coverage` → upload `lcov.info`

The CI fails if any layer drops below 80% (enforced by the tools themselves,
not the workflow YAML).

---

## 8. TDD Workflow — Step by Step With Real Examples

This is how you will write every new test in this project. We walk through
two real examples from Project S.

### Example 1 — Python: `test_analyze_label_is_lowercased`

**The behavior we want:** `analyze()` should normalize the model's output label
to lowercase so `"POSITIVE"` becomes `"positive"` before it reaches the database.

#### Step 1 — RED: Write the failing test

Open `nlp/tests/unit/test_model.py`. Add:

```python
def test_analyze_label_is_lowercased(monkeypatch):
    """Model may return uppercase labels. analyze() must normalize to lowercase."""

    def fake_pipeline(text):
        return [[
            {"label": "NEGATIVE", "score": 0.01},
            {"label": "NEUTRAL",  "score": 0.03},
            {"label": "POSITIVE", "score": 0.96},  # uppercase
        ]]

    import app.model as model
    monkeypatch.setattr(model, "_pipeline", fake_pipeline)

    result = model.analyze("Great!")

    assert result["label"] == "positive"   # must be lowercase
```

Run it: `uv run pytest tests/unit/test_model.py::test_analyze_label_is_lowercased`

It **fails** if `analyze()` doesn't `.lower()` the label. **That's the red state.**
Good. You've proven the test is actually checking something.

#### Step 2 — GREEN: Write the minimum fix

In `nlp/app/model.py`, ensure the `.lower()` call is on the label:

```python
label = max(scores, key=lambda k: scores[k])
return {"label": label, ...}   # label is already the lowercased key from the dict
```

Because we built the `scores` dict with `.lower()` on the keys:
```python
scores = {r["label"].lower(): r["score"] for r in results}
```

The label returned by `max()` is already lowercase. Test passes. **Green.**

#### Step 3 — REFACTOR

The `.lower()` is already there — no refactor needed. The test documented the
intention. If someone removes `.lower()` in the future, this test will catch it.

---

### Example 2 — C#: `ParseAsync_ThrowsOnEmptyFile`

**The behavior we want:** `CsvParserService.ParseAsync()` should throw a
meaningful exception when given a CSV with no data rows (only a header).

#### Step 1 — RED: Write the failing test

```csharp
[Fact]
public async Task ParseAsync_ThrowsOnEmptyFile_WhenOnlyHeaderPresent()
{
    // Arrange
    var service = new CsvParserService();
    var csvContent = "Name,Score,Feedback\n";  // header only, no rows
    var file = CreateFormFile(csvContent, "survey.csv");

    // Act + Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.ParseAsync(file));
}
```

Run it: `dotnet test --filter "ParseAsync_ThrowsOnEmptyFile"`. It **fails**
because the service currently returns an empty result instead of throwing.
**Red state confirmed.**

#### Step 2 — GREEN: Add the throw

In `CsvParserService.ParseAsync()`, after reading rows:

```csharp
if (rawRows.Count == 0)
    throw new InvalidOperationException("CSV file contains no data rows.");
```

Run the test again. **Green.**

#### Step 3 — REFACTOR

The message string could be a constant. But that's a minor cleanup — the
test still passes. Move on.

---

### The TDD Mindset — Final Notes

**Tests are first-class code.** Bad test code causes the same problems as
bad production code — it becomes hard to maintain and gives you false
confidence. Name your tests like sentences: `WhenInputIsEmpty_Returns400`.

**One assert per test (ideally).** A test with 10 assertions fails at the
first failure, hiding the rest. Split complex scenarios into focused tests.

**Tests are documentation.** A new developer reading `ParseAsync_DetectsTextColumn`
learns exactly what the service is supposed to do — better than any comment.

**Fast tests get run. Slow tests get skipped.** Keep unit tests under 100ms.
If a test takes more than a second, it has an undeclared dependency
(database, network, file system) — find it and mock it.

---

## Implementation Order for Step 7

1. **Configure pytest** — add `pytest-cov`, `pytest-asyncio`, `httpx` to `nlp/pyproject.toml`
2. **Write Python unit tests** — `tests/unit/test_model.py` + `test_main.py`
3. **Write Python integration tests** — `tests/integration/test_api.py`
4. **Scaffold C# test projects** — `_04_Unit_Tests` + `_05_Integration_Tests`
5. **Write C# unit tests** — CsvParser, SentimentClient, SurveyProcessingService
6. **Write C# integration tests** — Testcontainers + endpoint tests
7. **Migrate Angular from Karma to Jest** — install + configure
8. **Write Angular unit tests** — ApiService, Dashboard, Upload, SurveyList
9. **Write GitHub Actions `ci.yml`** — three parallel jobs, coverage gates
10. **Set branch protection rules** on GitHub
