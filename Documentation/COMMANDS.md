# Developer Command Reference

Quick reference for all commands needed to develop, run, and test this project.
Organized by workflow. Run commands from the project root unless otherwise noted.

---

## Prerequisites

These must be installed before anything else works.

```bash
# Verify .NET SDK (requires 10.0+)
dotnet --version

# Verify Docker Desktop is running
docker --version

# Verify Node.js (requires 18+)
node --version

# Verify Angular CLI
ng version

# Install EF Core CLI tools (one-time global install)
dotnet tool install --global dotnet-ef

# Verify EF Core CLI is installed
dotnet ef --version
```

---

## Environment Setup

One-time setup before running the project for the first time.

```bash
# Create the .env file at the project root (never committed — in .gitignore)
# Copy this block and save it as .env
POSTGRES_USER=projects_admin
POSTGRES_PASSWORD=YourSecurePass123!
POSTGRES_DB=projects_db
```

---

## Database (PostgreSQL via Docker)

**Working directory:** project root

```bash
# Start the PostgreSQL container in the background
docker-compose up -d postgres

# Stop the PostgreSQL container (data is preserved)
docker-compose down

# Stop the PostgreSQL container AND delete all data (full reset)
docker-compose down -v

# Check if the container is running
docker ps

# View PostgreSQL logs
docker logs projects_db

# Open a PostgreSQL shell inside the container
docker exec -it projects_db psql -U projects_admin -d projects_db
```

**Inside the PostgreSQL shell (`psql`):**

```sql
-- List all tables
\dt

-- Query surveys
SELECT id, name, status, total_rows FROM "Surveys";

-- Query columns for a specific survey
SELECT * FROM "SurveyColumns" WHERE "SurveyId" = 1;

-- Exit psql
\q
```

---

## Backend (ASP.NET Core)

**Working directory:** `backend/src/_03_Web_API/`

```bash
# Restore NuGet dependencies (run from backend/)
cd backend && dotnet restore

# Run the API (auto-applies migrations on startup in Development mode)
cd backend/src/_03_Web_API && dotnet run

# Run with hot reload (restarts automatically on code changes)
cd backend/src/_03_Web_API && dotnet watch run

# Build without running (verify no compile errors)
cd backend/src/_03_Web_API && dotnet build
```

**The API runs at:** `http://localhost:5120`

---

## Database Migrations (EF Core)

**Working directory:** `backend/src/_03_Web_API/`

```bash
# Create a new migration after changing entities
# Replace <MigrationName> with a descriptive name e.g. AddSurveyTable
dotnet ef migrations add <MigrationName> \
  --project ../_02_Infrastructure \
  --output-dir Data/Migrations

# Apply all pending migrations to the database manually
dotnet ef database update --project ../_02_Infrastructure

# List all migrations and their applied status
dotnet ef migrations list --project ../_02_Infrastructure

# Remove the last migration (only if NOT yet applied to the database)
dotnet ef migrations remove --project ../_02_Infrastructure
```

> **Note:** In Development mode, `dotnet run` automatically applies pending
> migrations on startup via `db.Database.Migrate()` in `Program.cs`.
> You only need `database update` if you want to apply without running the app.

**Migrations run so far:**

| Migration | What it created |
|---|---|
| `InitialCreate` | `Users`, `TestItems` tables |
| `AddSurveySchema` | `Surveys`, `SurveyColumns`, `SurveyResponses`, `ResponseValues`, `SentimentResults`, `KpiAggregates` tables |

---

## API — Manual Testing with curl

Make sure the API and database are both running before these.

```bash
# Health check — confirms API and DB are connected
curl http://localhost:5120/api/health

# Upload a CSV file (replace path with a real .csv file)
curl -X POST http://localhost:5120/api/surveys \
  -F "file=@/path/to/your/file.csv"

# Upload a CSV with a custom name
curl -X POST http://localhost:5120/api/surveys \
  -F "file=@/path/to/your/file.csv" \
  --data-urlencode "name=Q1 Customer Feedback"

# List all surveys
curl http://localhost:5120/api/surveys

# Get a specific survey with its columns (replace 1 with a real Id)
curl http://localhost:5120/api/surveys/1
```

---

## Frontend (Angular)

**Working directory:** `frontend/`

> **TODO:** Angular has not been scaffolded yet (Step 5 in the build order).
> Commands will be added here once `ng new frontend` is run.

```bash
# TODO: Scaffold Angular project (one-time)
# cd to project root, then:
ng new frontend --routing --style=scss

# TODO: Install dependencies
cd frontend && npm install

# TODO: Start dev server (http://localhost:4200)
ng serve

# TODO: Build for production
ng build --configuration production

# TODO: Run unit tests
ng test --watch=false --code-coverage

# TODO: Run end-to-end tests (Playwright)
npx playwright test
```

---

## Python NLP Service

**Working directory:** `nlp/`

This project uses [uv](https://docs.astral.sh/uv/) for Python dependency management.
Dependencies are declared in `pyproject.toml` and locked in `uv.lock`.

```bash
# Install uv (one-time global install — macOS/Linux)
brew install uv

# Create the virtual environment and install all dependencies from uv.lock
# Run this once after cloning, and again whenever uv.lock changes
cd nlp && uv sync

# Run the FastAPI service locally with hot reload (http://localhost:8000)
# --reload watches for file changes and restarts automatically
uv run uvicorn app.main:app --reload

# Run without hot reload (closer to production behaviour)
uv run uvicorn app.main:app --host 0.0.0.0 --port 8000
```

**The NLP service runs at:** `http://localhost:8000`

**OpenAPI docs available at:** `http://localhost:8000/docs` (Swagger UI, auto-generated by FastAPI)

```bash
# Add a new package dependency (updates pyproject.toml and uv.lock)
uv add <package>

# Remove a package dependency
uv remove <package>

# Upgrade all packages to their latest allowed versions
uv lock --upgrade

# Run a one-off Python command inside the venv (no manual activation needed)
uv run python -c "from app.model import analyze; print(analyze('test'))"

# Run Python tests (Step 7 — test files not yet created)
uv run pytest tests/ --cov=app --cov-report=html
```

**Manual curl tests:**

```bash
# Health check
curl http://localhost:8000/health

# Analyze a text string
curl -X POST http://localhost:8000/analyze \
  -H "Content-Type: application/json" \
  -d '{"text": "The support team was incredibly helpful!"}'

# Expected response
# {"label":"positive","positive":0.97,"neutral":0.02,"negative":0.01}
```

---

## Docker — Full Stack

**Working directory:** project root

```bash
# Start only the database
docker-compose up -d postgres

# Start the database and NLP service (current workflow — Steps 1–4)
docker-compose up -d postgres nlp

# Build the NLP image from scratch (required on first run or after Dockerfile changes)
docker-compose build nlp

# Build and start all services in the background
docker-compose up --build -d

# Build and start all services in the foreground (see all logs)
docker-compose up --build

# Rebuild a single service after code changes without restarting others
docker-compose up --build nlp

# View live logs for a specific service
docker-compose logs -f backend
docker-compose logs -f nlp
docker-compose logs -f postgres

# Stop all containers (data is preserved)
docker-compose down

# Stop all containers and delete all data (full reset)
docker-compose down -v

# Check which containers are running and their health status
docker ps
```

> **Note:** The NLP Docker image downloads and bakes in the RoBERTa model weights (~500MB)
> at build time. The first `docker-compose build nlp` will take several minutes.
> Subsequent builds use Docker layer cache and are fast unless `pyproject.toml` or
> `Dockerfile` changed.

---

## Testing (Backend)

> **TODO:** Test projects have not been created yet.
> See `Documentation/_6_TESTING_STRATEGY_AND_INTERVIEW_PREP.md` for the full plan.

```bash
# TODO: Run all backend tests
cd backend && dotnet test

# TODO: Run tests with code coverage
cd backend && dotnet test --collect:"XPlat Code Coverage"

# TODO: Run a single test class
cd backend && dotnet test --filter "ClassName=SurveyEndpointTests"

# TODO: Run benchmarks
cd backend/benchmarks && dotnet run -c Release
```

---

## Git Workflow

```bash
# Check current status
git status

# Create a new feature branch off main
git checkout main && git pull
git checkout -b your-branch-name

# Stage specific files (preferred over git add .)
git add path/to/file1 path/to/file2

# Commit with a message
git commit -m "feat: short description of what changed"

# Push branch and set upstream
git push -u origin your-branch-name
```

**Branch naming convention:**
- `feat/` — new feature
- `fix/` — bug fix
- `docs/` — documentation only
- `test/` — adding or updating tests
- `chore/` — maintenance (deps, config, migrations)

---

## Quick Start (Fresh Clone)

Run these in order to get the project running from a clean clone.

```bash
# 1. Clone and navigate
git clone <repository-url>
cd project_S

# 2. Create .env file (see Environment Setup section above)

# 3. Start the database
docker-compose up -d postgres

# 4. Install backend dependencies
cd backend && dotnet restore

# 5. Run the API (migrations apply automatically)
cd src/_03_Web_API && dotnet run

# 6. Verify everything is working
curl http://localhost:5120/api/health
```
