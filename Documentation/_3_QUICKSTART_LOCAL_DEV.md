# Quickstart: Running the Project Locally

This guide gets you up and running with the ASP.NET Core backend using a containerized PostgreSQL database.

---

## Prerequisites

1. **.NET SDK 8.0+** - [Download here](https://dotnet.microsoft.com/download)
   ```bash
   dotnet --version  # Verify installation
   ```

2. **Docker Desktop** - [Download here](https://www.docker.com/products/docker-desktop)
   ```bash
   docker --version  # Verify installation
   ```

3. **EF Core CLI Tools**
   ```bash
   dotnet tool install --global dotnet-ef
   ```

---

## Step 1: Clone and Navigate

```bash
git clone <repository-url>
cd project_S
```

---

## Step 2: Create the `.env` File

In the **project root** (same folder as `docker-compose.yml`), create a file named `.env`:

```env
POSTGRES_USER=projects_admin
POSTGRES_PASSWORD=YourSecurePass123!
POSTGRES_DB=projects_db
```

> **Note:** This file is in `.gitignore` and won't be committed.

---

## Step 3: Start PostgreSQL Container

Make sure Docker Desktop is running, then:

```bash
docker-compose up -d postgres
```

Verify it's running:
```bash
docker ps
```

You should see `projects_db` on port `5432`.

---

## Step 4: Restore Dependencies

```bash
cd backend
dotnet restore
```

---

## Step 5: Apply Database Migrations

```bash
cd src/_03_Web_API
dotnet ef database update --project ../_02_Infrastructure
```

---

## Step 6: Run the API

```bash
dotnet run
```

Or with hot reload (auto-restarts on code changes):
```bash
dotnet watch run
```

---

## Step 7: Test the API

The API runs at **http://localhost:5120**

```bash
# Health check
curl http://localhost:5120/api/health

# Test items endpoint
curl http://localhost:5120/api/testitems
```

Or open http://localhost:5120/api/health in your browser.

---

## Shutting Down

```bash
# Stop the API
Ctrl+C

# Stop PostgreSQL (keeps your data)
docker-compose down

# Stop PostgreSQL (deletes all data)
docker-compose down -v
```

---

## Quick Reference

| Task | Command |
|------|---------|
| Start PostgreSQL | `docker-compose up -d postgres` |
| Stop PostgreSQL | `docker-compose down` |
| Run API | `dotnet run` (from `backend/src/_03_Web_API`) |
| Run API with hot reload | `dotnet watch run` |
| Apply migrations | `dotnet ef database update --project ../_02_Infrastructure` |
| Check running containers | `docker ps` |
| View PostgreSQL logs | `docker logs projects_db` |

---

## Troubleshooting

### "Cannot connect to the Docker daemon"
- Open Docker Desktop and wait for it to fully start

### "Connection refused" to database
- Check if container is running: `docker ps`
- Check logs: `docker logs projects_db`

### "dotnet ef" command not found
- Install tools: `dotnet tool install --global dotnet-ef`
- Restart your terminal

### Port 5432 already in use
- Check what's using it: `lsof -i :5432`
- Stop other PostgreSQL instances or change the port in `docker-compose.yml`
