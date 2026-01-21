# Testing PostgreSQL with ASP.NET Core & Backend Containerization Guide

This guide covers:
1. Testing PostgreSQL connectivity with your ASP.NET Core backend
2. Containerizing the backend and running both containers together

---

# Part 1: Testing PostgreSQL with ASP.NET Core

## Step 1: Start PostgreSQL Container

```bash
# From project root
cd /Users/joshsoteras/Documents/1_Projects/project_S

# Start the PostgreSQL container in detached mode (-d = background)
docker-compose up -d postgres
```

### What This Does:
- Docker reads `docker-compose.yml`
- Downloads `postgres:16` image (if not cached)
- Creates a container named `projects_db`
- Creates a volume `postgres_data` for persistent storage
- Starts PostgreSQL listening on port 5432

### Verify It's Running:
```bash
docker ps
```
Expected output:
```
CONTAINER ID   IMAGE         STATUS          PORTS                    NAMES
abc123...      postgres:16   Up 10 seconds   0.0.0.0:5432->5432/tcp   projects_db
```

---

## Step 2: Create Initial Migration

```bash
# Navigate to the Web API project
cd backend/src/_03_Web_API

# Create the migration
dotnet ef migrations add InitialCreate --project ../_02_Infrastructure --output-dir Data/Migrations
```

### What Each Part Means:

| Part | Explanation |
|------|-------------|
| `dotnet ef` | Entity Framework Core CLI tool |
| `migrations add` | Create a new migration |
| `InitialCreate` | Name of the migration (descriptive name) |
| `--project ../_02_Infrastructure` | Where to put migration files (Infrastructure layer) |
| `--output-dir Data/Migrations` | Subfolder within that project |

### What This Does:
1. EF Core compares your `ApplicationDbContext` to the database (which is empty)
2. Generates C# code to create tables for `Users` and `TestItems`
3. Creates files in `_02_Infrastructure/Data/Migrations/`:
   - `YYYYMMDDHHMMSS_InitialCreate.cs` - The migration code
   - `YYYYMMDDHHMMSS_InitialCreate.Designer.cs` - Metadata
   - `ApplicationDbContextModelSnapshot.cs` - Current schema snapshot

### The Generated Migration Looks Like:
```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Creates the Users table
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Email = table.Column<string>(maxLength: 255, nullable: false),
                DisplayName = table.Column<string>(maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                UpdatedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

        // Creates the TestItems table
        migrationBuilder.CreateTable(
            name: "TestItems",
            columns: table => new { ... });

        // Creates unique index on Email
        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverses the migration (drops tables)
        migrationBuilder.DropTable(name: "TestItems");
        migrationBuilder.DropTable(name: "Users");
    }
}
```

---

## Step 3: Run the Backend

```bash
# From the Web API project directory
cd backend/src/_03_Web_API

# Run the application
dotnet run
```

### What This Does:
1. Compiles and starts your ASP.NET Core app
2. On startup, `db.Database.Migrate()` in Program.cs runs
3. This applies the `InitialCreate` migration to PostgreSQL
4. Creates the `Users` and `TestItems` tables
5. Starts listening on `http://localhost:5000` (or similar)

### Expected Output:
```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (15ms) [Parameters=[], CommandType='Text']
      CREATE TABLE "Users" (...)
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (5ms) [Parameters=[], CommandType='Text']
      CREATE TABLE "TestItems" (...)
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

## Step 4: Test the Endpoints

Open a **new terminal** (keep the app running in the first one).

### Test Health Check:
```bash
curl http://localhost:5000/api/health
```

**Expected Response:**
```json
{"status":"healthy","database":"connected"}
```

### Create a Test Item:
```bash
curl -X POST http://localhost:5000/api/test \
  -H "Content-Type: application/json" \
  -d '{"name": "My First Item"}'
```

**Expected Response:**
```json
{"id":1,"name":"My First Item","createdAt":"2026-01-19T12:00:00Z"}
```

### Get All Test Items:
```bash
curl http://localhost:5000/api/test
```

**Expected Response:**
```json
[{"id":1,"name":"My First Item","createdAt":"2026-01-19T12:00:00Z"}]
```

### Create a User:
```bash
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"email": "test@example.com", "displayName": "Test User"}'
```

**Expected Response:**
```json
{"id":1,"email":"test@example.com","displayName":"Test User","createdAt":"2026-01-19T12:00:00Z","updatedAt":null}
```

### Verify Data Persists in PostgreSQL:
```bash
# Connect to PostgreSQL inside the container
docker exec -it projects_db psql -U projects_admin -d projects_db

# Inside psql, run:
SELECT * FROM "Users";
SELECT * FROM "TestItems";

# Exit psql
\q
```

---

# Part 2: Containerize the Backend

## Step 1: Create Dockerfile for Backend

**File:** `backend/Dockerfile`

```dockerfile
# ============================================
# STAGE 1: BUILD
# ============================================
# Use the .NET SDK image to build the application.
# SDK includes compilers and build tools.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Set the working directory inside the container
WORKDIR /src

# Copy project files first (for Docker layer caching)
# If these files don't change, Docker reuses the cached layer
COPY src/_01_Core/ProjectS.Core.csproj src/_01_Core/
COPY src/_02_Infrastructure/ProjectS.Infrastructure.csproj src/_02_Infrastructure/
COPY src/_03_Web_API/ProjectS.Web.csproj src/_03_Web_API/

# Restore NuGet packages (dependencies)
# This is separate so it's cached unless .csproj files change
RUN dotnet restore src/_03_Web_API/ProjectS.Web.csproj

# Copy all source code
COPY src/ src/

# Build the application in Release mode
# Output goes to /app/publish
RUN dotnet publish src/_03_Web_API/ProjectS.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================
# STAGE 2: RUNTIME
# ============================================
# Use the smaller ASP.NET runtime image (no SDK, just runtime)
# This makes the final image much smaller (~200MB vs ~700MB)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Set the working directory
WORKDIR /app

# Copy the published app from the build stage
COPY --from=build /app/publish .

# Expose port 8080 (ASP.NET Core default in containers)
EXPOSE 8080

# Set environment variables
# ASPNETCORE_URLS tells the app which port to listen on
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
# This runs when the container starts
ENTRYPOINT ["dotnet", "ProjectS.Web.dll"]
```

### What Each Part Does:

| Section | Purpose |
|---------|---------|
| `FROM ... AS build` | Multi-stage build. First stage uses SDK to compile |
| `WORKDIR /src` | Sets current directory inside container |
| `COPY *.csproj` | Copy project files first for caching |
| `dotnet restore` | Download NuGet packages |
| `COPY src/ src/` | Copy all source code |
| `dotnet publish` | Compile in Release mode, output to /app/publish |
| `FROM ... AS runtime` | Second stage, smaller image with just runtime |
| `COPY --from=build` | Copy compiled app from first stage |
| `EXPOSE 8080` | Document which port the app uses |
| `ENTRYPOINT` | Command to run when container starts |

---

## Step 2: Update docker-compose.yml

**File:** `docker-compose.yml` (at project root)

```yaml
# ============================================
# Docker Compose - Multi-Container Setup
# ============================================
# Defines how to run PostgreSQL and Backend together.
# They communicate over a Docker network.

services:
  # ----------------------------------------
  # POSTGRESQL DATABASE
  # ----------------------------------------
  postgres:
    image: postgres:16                    # Official PostgreSQL image
    container_name: projects_db           # Friendly name for the container
    restart: unless-stopped               # Auto-restart on crash
    environment:
      POSTGRES_USER: ${POSTGRES_USER}           # From .env file
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}   # From .env file
      POSTGRES_DB: ${POSTGRES_DB}               # From .env file
    ports:
      - "5432:5432"                        # Expose to host (for local dev tools)
    volumes:
      - postgres_data:/var/lib/postgresql/data  # Persist data
    networks:
      - app-network                        # Connect to shared network
    healthcheck:
      # Check if PostgreSQL is ready to accept connections
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s                         # Check every 5 seconds
      timeout: 5s                          # Timeout after 5 seconds
      retries: 5                           # Retry 5 times before unhealthy

  # ----------------------------------------
  # ASP.NET CORE BACKEND
  # ----------------------------------------
  backend:
    build:
      context: ./backend                   # Build from backend/ directory
      dockerfile: Dockerfile               # Use backend/Dockerfile
    container_name: projects_backend       # Friendly name
    restart: unless-stopped
    ports:
      - "5000:8080"                         # Host:Container - access on localhost:5000
    environment:
      # Connection string for PostgreSQL
      # "postgres" is the service name (Docker DNS resolves it)
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      ASPNETCORE_ENVIRONMENT: Development   # Enable development features
    depends_on:
      postgres:
        condition: service_healthy          # Wait for PostgreSQL to be ready
    networks:
      - app-network                         # Same network as PostgreSQL

# ----------------------------------------
# NAMED VOLUMES
# ----------------------------------------
# Data persists even if containers are deleted
volumes:
  postgres_data:

# ----------------------------------------
# NETWORKS
# ----------------------------------------
# Containers on the same network can communicate by service name
networks:
  app-network:
    driver: bridge
```

### Key Concepts:

| Concept | Explanation |
|---------|-------------|
| `depends_on` | Backend waits for PostgreSQL to start |
| `condition: service_healthy` | Actually waits for PostgreSQL to be *ready*, not just started |
| `networks: app-network` | Both containers share a network |
| `Host=postgres` | Inside Docker, use service name as hostname (not localhost) |
| `ConnectionStrings__DefaultConnection` | Overrides appsettings via environment variable |
| `ports: "5000:8080"` | Map host port 5000 to container port 8080 |

---

## Step 3: Create appsettings.Production.json

**File:** `backend/src/_03_Web_API/appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

### Why Empty?
The connection string is provided via environment variable in `docker-compose.yml`. This is more secure than hardcoding credentials.

---

## Step 4: Run Both Containers

```bash
# From project root
cd /Users/joshsoteras/Documents/1_Projects/project_S

# Build and start all services
docker-compose up --build
```

### What Each Flag Does:

| Flag | Purpose |
|------|---------|
| `up` | Create and start containers |
| `--build` | Rebuild images (needed after code changes) |
| `-d` | (optional) Run in background (detached mode) |

### What Happens:
1. Docker builds the backend image using `backend/Dockerfile`
2. Starts PostgreSQL container
3. Waits for PostgreSQL healthcheck to pass
4. Starts backend container
5. Backend connects to `postgres:5432` (Docker DNS)
6. Runs migrations automatically
7. Both services are now running

### Expected Output:
```
[+] Building 45.2s
 => [backend build 1/7] FROM mcr.microsoft.com/dotnet/sdk:10.0
 => ...
[+] Running 3/3
 ✔ Network project_s_app-network  Created
 ✔ Container projects_db          Healthy
 ✔ Container projects_backend     Started
projects_backend  | info: Microsoft.EntityFrameworkCore...
projects_backend  | Now listening on: http://[::]:8080
```

---

## Step 5: Test the Containerized Setup

```bash
# Test health endpoint
curl http://localhost:5000/api/health

# Create a test item
curl -X POST http://localhost:5000/api/test \
  -H "Content-Type: application/json" \
  -d '{"name": "Containerized Item"}'

# Get all items
curl http://localhost:5000/api/test
```

---

## Useful Docker Commands

```bash
# Start containers (background)
docker-compose up -d

# Stop containers (keep data)
docker-compose down

# Stop and DELETE all data
docker-compose down -v

# View logs
docker-compose logs -f backend    # Follow backend logs
docker-compose logs -f postgres   # Follow postgres logs

# Rebuild after code changes
docker-compose up --build

# Check running containers
docker ps

# Execute command in container
docker exec -it projects_backend /bin/bash    # Shell into backend
docker exec -it projects_db psql -U projects_admin -d projects_db  # PostgreSQL CLI
```

---

## Summary: Two-Container Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Docker Network                          │
│                    (app-network)                            │
│                                                             │
│  ┌─────────────────┐         ┌─────────────────┐           │
│  │  projects_db    │         │ projects_backend │           │
│  │  (PostgreSQL)   │◄───────►│  (ASP.NET Core)  │           │
│  │                 │         │                  │           │
│  │  Port: 5432     │         │  Port: 8080      │           │
│  └────────┬────────┘         └────────┬─────────┘           │
│           │                           │                     │
└───────────┼───────────────────────────┼─────────────────────┘
            │                           │
            ▼                           ▼
      localhost:5432              localhost:5000
      (for DB tools)              (for API access)
```
