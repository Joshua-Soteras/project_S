# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Architecture

Fullstack monorepo with two separate services:

- **frontend/** - Nuxt 4 (Vue 3, Tailwind CSS), runs on port 3000
- **backend/** - ASP.NET Core (.NET 10) Minimal API, runs on port 5120 locally / 8080 in container
- **docker-compose.yml** - Orchestrates PostgreSQL (and eventually all services)

The backend uses a 3-layer architecture inside `backend/src/`:

| Project | Layer | Responsibility |
|---|---|---|
| `_01_Core` (`ProjectS.Core`) | Domain | Entities only (`User`, `TestItem`). No framework dependencies. |
| `_02_Infrastructure` (`ProjectS.Infrastructure`) | Persistence | `ApplicationDbContext`, EF Core migrations, Npgsql |
| `_03_Web_API` (`ProjectS.Web`) | Presentation | `Program.cs` with Minimal API endpoints, startup config |

Dependency direction: `Web API` → `Infrastructure` → `Core`

## Frontend

**Working directory:** `frontend/`

```bash
npm install        # Install dependencies
npm run dev        # Dev server at http://localhost:3000
npm run build      # Production build
npm run preview    # Preview production build
```

- Main entry: `app/app.vue`
- Tailwind via `@nuxtjs/tailwindcss` module

## Backend

**Working directory for running:** `backend/src/_03_Web_API/`

```bash
# From project root, start PostgreSQL first
docker-compose up -d postgres

# From backend/src/_03_Web_API/
dotnet run              # Run API (auto-applies migrations in Development)
dotnet watch run        # Run with hot reload

# Restore dependencies (from backend/)
dotnet restore
```

**EF Core migrations** (run from `backend/src/_03_Web_API/`):

```bash
# Create a new migration
dotnet ef migrations add <MigrationName> --project ../_02_Infrastructure --output-dir Data/Migrations

# Apply migrations manually
dotnet ef database update --project ../_02_Infrastructure
```

In Development mode, `Program.cs` calls `db.Database.Migrate()` on startup automatically.

## Database & Docker

PostgreSQL runs in Docker. Credentials come from a `.env` file in the project root (not committed):

```env
POSTGRES_USER=projects_admin
POSTGRES_PASSWORD=YourSecurePass123!
POSTGRES_DB=projects_db
```

Development connection string (in `appsettings.Development.json`):
`Host=localhost;Port=5432;Database=projects_db;Username=projects_admin;Password=YourSecurePass123!`

```bash
docker-compose up -d postgres       # Start DB only
docker-compose up --build           # Build and start all containers
docker-compose down                 # Stop (keep data)
docker-compose down -v              # Stop and delete all data
docker exec -it projects_db psql -U projects_admin -d projects_db  # psql shell
```

## API Endpoints

The API listens at `http://localhost:5120` locally.

| Method | Path | Description |
|---|---|---|
| GET | `/api/health` | DB connectivity check |
| GET/POST | `/api/test` | TestItems CRUD |
| GET/PUT/DELETE | `/api/test/{id}` | Single TestItem |
| GET/POST | `/api/users` | Users |
| GET | `/api/users/{id}` | Single User |

OpenAPI spec available at `/openapi/v1.json` in Development.

## Adding New Entities

1. Add entity class to `backend/src/_01_Core/Entities/`
2. Add `DbSet<T>` and Fluent API config to `ApplicationDbContext` in `_02_Infrastructure`
3. Create migration from `backend/src/_03_Web_API/`
4. Add Minimal API endpoints to `Program.cs`
