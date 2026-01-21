# ASP.NET Core Local Development Setup

This guide walks you through setting up ASP.NET Core for local development on your machine.

---

## Prerequisites

### 1. Install .NET SDK

Download and install the .NET SDK (version 8.0 or later recommended):

**macOS (using Homebrew):**
```bash
brew install dotnet-sdk
```

**macOS/Windows/Linux (manual download):**
- Visit: https://dotnet.microsoft.com/download
- Download the SDK (not just the runtime)
- Run the installer

**Verify installation:**
```bash
dotnet --version
```

You should see something like `8.0.xxx`.

---

### 2. Install EF Core CLI Tools (Global)

Entity Framework Core command-line tools are required for migrations:

```bash
dotnet tool install --global dotnet-ef
```

**Verify installation:**
```bash
dotnet ef --version
```

**If already installed, update to latest:**
```bash
dotnet tool update --global dotnet-ef
```

---

### 3. Install Docker Desktop

Required for running PostgreSQL locally:

**macOS:**
```bash
brew install --cask docker
```

**Or download directly:**
- Visit: https://www.docker.com/products/docker-desktop
- Download and install for your OS
- **Important:** Launch Docker Desktop and wait for it to fully start (whale icon in menu bar should be steady, not animating)

**Verify installation:**
```bash
docker --version
docker-compose --version
```

---

## Project Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd project_S
```

### 2. Navigate to Backend Directory

```bash
cd backend
```

### 3. Restore NuGet Packages

```bash
dotnet restore
```

This downloads all dependencies defined in the `.csproj` files.

---

## Database Setup

### 1. Create the `.env` File

In the **project root** (same level as `docker-compose.yml`), create a `.env` file:

```bash
# From project root
touch .env
```

Add the following content:

```env
POSTGRES_USER=projects_admin
POSTGRES_PASSWORD=YourSecurePass123!
POSTGRES_DB=projects_db
```

**Note:** The `.env` file is in `.gitignore` and should never be committed.

### 2. Start PostgreSQL Container

```bash
# From project root (where docker-compose.yml is located)
docker-compose up -d postgres
```

**Verify it's running:**
```bash
docker ps
```

You should see `projects_db` container running on port `5432`.

### 3. Run Database Migrations

```bash
# From backend/src/_03_Web_API directory
cd backend/src/_03_Web_API

dotnet ef migrations add InitialCreate --project ../_02_Infrastructure --output-dir Data/Migrations
```

**Apply migrations to the database:**
```bash
dotnet ef database update --project ../_02_Infrastructure
```

---

## Running the Application

### 1. Start the API

```bash
# From backend/src/_03_Web_API directory
dotnet run
```

**Or with hot reload (watches for file changes):**
```bash
dotnet watch run
```

### 2. Default URLs

The API runs on:
- **HTTP:** http://localhost:5120
- **HTTPS:** https://localhost:7240

These are configured in `Properties/launchSettings.json`.

### 3. Test the API

Open a browser or use curl:

```bash
# Health check endpoint
curl http://localhost:5120/api/health

# Test items endpoint
curl http://localhost:5120/api/testitems
```

---

## Useful Commands

### Docker Commands

| Command | Description |
|---------|-------------|
| `docker-compose up -d postgres` | Start PostgreSQL container |
| `docker-compose down` | Stop containers (keep data) |
| `docker-compose down -v` | Stop containers and delete data |
| `docker ps` | List running containers |
| `docker logs projects_db` | View PostgreSQL logs |

### .NET Commands

| Command | Description |
|---------|-------------|
| `dotnet restore` | Restore NuGet packages |
| `dotnet build` | Build the solution |
| `dotnet run` | Run the application |
| `dotnet watch run` | Run with hot reload |
| `dotnet clean` | Clean build artifacts |

### EF Core Commands

| Command | Description |
|---------|-------------|
| `dotnet ef migrations add <Name>` | Create a new migration |
| `dotnet ef database update` | Apply pending migrations |
| `dotnet ef migrations list` | List all migrations |
| `dotnet ef database drop` | Drop the database |

**Note:** EF Core commands must include `--project ../_02_Infrastructure` when run from `_03_Web_API`.

---

## Configuration Files

### Connection String Location

The database connection string is in:
- **Development:** `appsettings.Development.json`
- **Production:** `appsettings.json` (or environment variables)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=projects_db;Username=projects_admin;Password=YourSecurePass123!"
  }
}
```

### Launch Profiles

Configured in `Properties/launchSettings.json`:
- `http` profile: Uses HTTP only (port 5120)
- `https` profile: Uses HTTPS (port 7240) + HTTP (port 5120)

To use a specific profile:
```bash
dotnet run --launch-profile http
```

---

## Troubleshooting

### "Cannot connect to the Docker daemon"
- Make sure Docker Desktop is running
- Wait for the whale icon to stop animating

### "Connection refused" to PostgreSQL
- Check if container is running: `docker ps`
- Verify port 5432 is not used by another process: `lsof -i :5432`
- Check container logs: `docker logs projects_db`

### EF Core command not found
- Install the tools: `dotnet tool install --global dotnet-ef`
- Restart your terminal after installation

### Port already in use
- Find what's using the port: `lsof -i :5120`
- Kill the process or change the port in `launchSettings.json`

---

## IDE Setup (Optional)

### VS Code Extensions

Recommended extensions for ASP.NET Core development:
- **C#** (by Microsoft) - IntelliSense and debugging
- **C# Dev Kit** (by Microsoft) - Enhanced C# experience
- **NuGet Package Manager** - Manage packages from VS Code

### Rider / Visual Studio

Both IDEs have built-in support for ASP.NET Core. Just open the `backend.sln` file.
