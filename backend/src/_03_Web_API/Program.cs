// ============================================
// Program.cs - ASP.NET Core Entry Point
// ============================================
//
// This file configures and runs the web application.
// It has two main phases:
// 1. BUILDER PHASE: Configure services (DI container)
// 2. APP PHASE: Configure middleware pipeline and endpoints
// ============================================

using Microsoft.EntityFrameworkCore;      // EF Core (UseNpgsql, Migrate, ToListAsync)
using ProjectS.Infrastructure.Data;       // ApplicationDbContext
using ProjectS.Core.Entities;             // User, TestItem entities
using ProjectS.Web.Services;              // [ADDED] CsvParserService, SurveyProcessingService
using ProjectS.Web.Workers;               // [ADDED] SurveyQueue, SurveyProcessingWorker

// ============================================
// BUILDER PHASE - Configure Services
// ============================================
// Create the WebApplication builder.
// 'args' = command line arguments (e.g., --urls, --environment)
var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------
// REGISTER DBCONTEXT WITH DEPENDENCY INJECTION
// ----------------------------------------
// AddDbContext<T>() does three things:
// 1. Registers ApplicationDbContext with the DI container
// 2. Configures it to use PostgreSQL (UseNpgsql)
// 3. Reads connection string from appsettings.json
//
// LIFETIME: "Scoped" = one instance per HTTP request
// This prevents connection leaks and ensures proper disposal
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ----------------------------------------
// REGISTER OPENAPI (SWAGGER)
// ----------------------------------------
// Provides automatic API documentation at /openapi/v1.json
builder.Services.AddOpenApi();

// ----------------------------------------
// [ADDED] REGISTER APPLICATION SERVICES
// ----------------------------------------
// AddScoped = one instance per HTTP request (same lifetime as DbContext).
// CsvParserService reads an uploaded file and returns structured data.
// It is injected into endpoints that need CSV parsing.
builder.Services.AddScoped<CsvParserService>();

// ----------------------------------------
// [ADDED] REGISTER SENTIMENT CLIENT (TYPED HTTPCLIENT)
// ----------------------------------------
// AddHttpClient<T>() registers SentimentClient as a typed HttpClient.
// It manages the underlying HttpClientHandler lifetime (connection pooling,
// DNS refresh) automatically — no manual HttpClient creation or disposal.
//
// BaseAddress is read from config:
//   Development : appsettings.Development.json → http://localhost:8000
//   Docker      : appsettings.json             → http://nlp:8000
//                 ("nlp" is the docker-compose service name)
builder.Services.AddHttpClient<SentimentClient>(client =>
{
    var url = builder.Configuration["NlpService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(url);
});

// ----------------------------------------
// [ADDED] REGISTER ASYNC QUEUE INFRASTRUCTURE (Step 6)
// ----------------------------------------
// SurveyQueue — singleton Channel<int> that holds survey IDs waiting
// for NLP processing. Lives for the full app lifetime.
builder.Services.AddSingleton<SurveyQueue>();

// SurveyProcessingService — scoped service with the NLP + KPI pipeline.
// Resolved inside a fresh DI scope per background job so it gets its
// own DbContext instance (avoids cross-job state contamination).
builder.Services.AddScoped<SurveyProcessingService>();

// SurveyProcessingWorker — hosted BackgroundService that reads from
// SurveyQueue and calls SurveyProcessingService for each survey ID.
// Starts automatically when the app starts; stops on shutdown.
builder.Services.AddHostedService<SurveyProcessingWorker>();

// ============================================
// BUILD THE APPLICATION
// ============================================
// Compiles all configuration into a runnable WebApplication.
// After this line, you cannot add more services.
var app = builder.Build();

// ============================================
// DATABASE MIGRATION (DEVELOPMENT ONLY)
// ============================================
// Automatically applies pending migrations when the app starts.
// This ensures your database schema matches your C# entities.
//
// WARNING: Only do this in development!
// In production, run migrations manually via CI/CD.
if (app.Environment.IsDevelopment())
{
    // CreateScope() = create a temporary DI scope
    // Needed because DbContext is "scoped" (one per request)
    using var scope = app.Services.CreateScope();

    // Get ApplicationDbContext from DI container
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Apply all pending migrations
    // Creates database and tables if they don't exist
    db.Database.Migrate();
}

// ============================================
// MIDDLEWARE PIPELINE
// ============================================
// Middleware processes requests in order.
// Request → [Middleware 1] → [Middleware 2] → [Endpoint] → Response

// Enable OpenAPI/Swagger in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redirect HTTP to HTTPS for security
app.UseHttpsRedirection();

// ============================================
// API ENDPOINTS (MINIMAL APIs)
// ============================================
// Minimal APIs = define endpoints without controllers.
// Simpler syntax, great for small APIs and learning.
//
// PARAMETER INJECTION:
// (ApplicationDbContext db) → DI automatically provides the DbContext

// ----------------------------------------
// HEALTH CHECK - Test Database Connection
// ----------------------------------------
// GET /api/health
// Returns: { "status": "healthy", "database": "connected" }
// Or error details if connection fails.
app.MapGet("/api/health", async (ApplicationDbContext db) =>
{
    try
    {
        // CanConnectAsync() attempts to open a database connection
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

// ----------------------------------------
// TEST ITEMS ENDPOINTS (CRUD)
// ----------------------------------------

// GET /api/test - Get all test items
app.MapGet("/api/test", async (ApplicationDbContext db) =>
{
    // ToListAsync() → SELECT * FROM "TestItems"
    return await db.TestItems.ToListAsync();
});

// GET /api/test/{id} - Get single test item by ID
app.MapGet("/api/test/{id}", async (int id, ApplicationDbContext db) =>
{
    // FindAsync() → SELECT * FROM "TestItems" WHERE "Id" = @id
    var item = await db.TestItems.FindAsync(id);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

// POST /api/test - Create new test item
// Request body: { "name": "My Item" }
app.MapPost("/api/test", async (TestItem item, ApplicationDbContext db) =>
{
    // Add() marks entity for insertion
    db.TestItems.Add(item);

    // SaveChangesAsync() → INSERT INTO "TestItems" ...
    await db.SaveChangesAsync();

    // Return 201 Created with location header
    return Results.Created($"/api/test/{item.Id}", item);
});

// PUT /api/test/{id} - Update existing test item
// Request body: { "name": "Updated Name" }
app.MapPut("/api/test/{id}", async (int id, TestItem updatedItem, ApplicationDbContext db) =>
{
    var item = await db.TestItems.FindAsync(id);
    if (item is null) return Results.NotFound();

    // EF Core tracks changes automatically
    item.Name = updatedItem.Name;

    // SaveChangesAsync() → UPDATE "TestItems" SET "Name" = @name WHERE "Id" = @id
    await db.SaveChangesAsync();
    return Results.Ok(item);
});

// DELETE /api/test/{id} - Delete test item
app.MapDelete("/api/test/{id}", async (int id, ApplicationDbContext db) =>
{
    var item = await db.TestItems.FindAsync(id);
    if (item is null) return Results.NotFound();

    // Remove() marks entity for deletion
    db.TestItems.Remove(item);

    // SaveChangesAsync() → DELETE FROM "TestItems" WHERE "Id" = @id
    await db.SaveChangesAsync();

    // Return 204 No Content
    return Results.NoContent();
});

// ----------------------------------------
// USERS ENDPOINTS
// ----------------------------------------

// GET /api/users - Get all users
app.MapGet("/api/users", async (ApplicationDbContext db) =>
{
    return await db.Users.ToListAsync();
});

// GET /api/users/{id} - Get single user by ID
app.MapGet("/api/users/{id}", async (int id, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

// POST /api/users - Create new user
// Request body: { "email": "user@example.com", "displayName": "John" }
app.MapPost("/api/users", async (User user, ApplicationDbContext db) =>
{
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", user);
});

// ============================================
// [ADDED] SURVEY ENDPOINTS
// ============================================
//
// These endpoints handle the core feature of the application:
// uploading a CSV of survey responses and processing it into
// the database so the dashboard can display KPI data.
//
// CURRENT STATE (Step 6 — Async Queue):
// Upload → parse CSV → save rows → enqueue survey ID → 202 Accepted.
// SurveyProcessingWorker picks the ID off the Channel<int> and runs
// NLP + KPI computation in the background.
// Angular polls GET /api/surveys/{id} until status = "complete".
// ============================================

// ----------------------------------------
// POST /api/surveys — Upload a CSV file (returns immediately)
// ----------------------------------------
// Accepts: multipart/form-data
//   - file  (required) : the CSV file
//   - name  (optional) : display name; defaults to filename
//
// Returns: 202 Accepted
//   { "id": 1, "name": "Q1 Feedback", "status": "queued", "totalRows": 500, "columnCount": 6 }
//
// The caller should poll GET /api/surveys/{id} until status = "complete" or "error".
//
// .DisableAntiforgery() is required for any endpoint that accepts
// IFormFile in a minimal API. Since this is a pure API (not a
// browser form), we don't need CSRF protection here.
// ----------------------------------------
app.MapPost("/api/surveys", async (
    IFormFile file,
    string? name,
    CsvParserService parser,
    SurveyQueue queue,
    ApplicationDbContext db) =>
{
    // --- VALIDATION ---
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file provided." });

    if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Only .csv files are accepted." });

    // --- PARSE THE CSV ---
    // CsvParserService reads the file, detects column types,
    // and returns structured data. If the file is malformed,
    // it returns a 400 with the parser error message.
    ParsedSurvey parsed;
    try
    {
        parsed = await parser.ParseAsync(file);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Failed to parse CSV: {ex.Message}" });
    }

    if (parsed.Rows.Count == 0)
        return Results.BadRequest(new { error = "The CSV file contains no data rows." });

    // --- SAVE SURVEY RECORD ---
    // Create the top-level Survey row first so we get its Id,
    // which all child records (columns, responses, values) need as a FK.
    var survey = new Survey
    {
        Name = name ?? Path.GetFileNameWithoutExtension(file.FileName),

        // TODO (Step 8 — Azure): replace with the Azure Blob Storage URL
        // after uploading the raw file to a "survey-csvs" container.
        BlobUrl = file.FileName,

        Status = "queued",
        TotalRows = parsed.Rows.Count,

        // TODO (Step 8 — Auth): replace with the authenticated user's
        // Entra ID principal name (e.g. "john.doe@company.com").
        UploadedBy = "anonymous",
    };
    db.Surveys.Add(survey);
    await db.SaveChangesAsync(); // commit now to get survey.Id

    // --- SAVE COLUMN DEFINITIONS ---
    // One SurveyColumn row per header in the CSV.
    // These describe the schema of this specific upload — because
    // every CSV can have different columns, we store them per survey.
    var columnEntities = parsed.Columns.Select(c => new SurveyColumn
    {
        SurveyId = survey.Id,
        ColumnName = c.Name,
        ColumnType = c.Type,
        ColumnIndex = c.Index,
        AnalyzeSentiment = c.AnalyzeSentiment,
    }).ToList();

    db.SurveyColumns.AddRange(columnEntities);
    await db.SaveChangesAsync(); // commit now to get column Ids

    // --- SAVE RESPONSES AND CELL VALUES IN BATCHES ---
    // Processing in batches of 500 rows at a time prevents a single
    // large SaveChangesAsync() call from holding a DB transaction open
    // for too long, which can cause lock contention under load.
    //
    // EF Core navigation property pattern:
    //   response.Values = [list of ResponseValue]
    // When we call SaveChangesAsync(), EF Core automatically sets
    // ResponseId on each ResponseValue to match response.Id.
    // We don't have to set it manually.
    const int batchSize = 500;

    for (var i = 0; i < parsed.Rows.Count; i += batchSize)
    {
        var batch = parsed.Rows.Skip(i).Take(batchSize);

        var responseEntities = batch.Select(row =>
        {
            var response = new SurveyResponse
            {
                SurveyId = survey.Id,
                RowIndex = row.RowIndex,
            };

            // Attach all cells for this row via the navigation property.
            // EF Core will resolve the FK (ResponseId) automatically on save.
            response.Values = row.Cells
                .Where(cell => cell.ColumnIndex < columnEntities.Count)
                .Select(cell => new ResponseValue
                {
                    // Link each cell to its column definition by Id
                    ColumnId = columnEntities[cell.ColumnIndex].Id,
                    RawValue = cell.Value,
                }).ToList();

            return response;
        }).ToList();

        db.SurveyResponses.AddRange(responseEntities);
        await db.SaveChangesAsync();
    }

    // --- ENQUEUE FOR BACKGROUND PROCESSING ---
    // All CSV rows are now persisted. Hand off the survey ID to the
    // background worker (SurveyProcessingWorker) via the in-memory
    // Channel<int>. The worker runs NLP + KPI computation and updates
    // survey.Status to "processing" → "complete" (or "error").
    queue.Enqueue(survey.Id);

    // Return 202 Accepted — the resource exists but processing is pending.
    // Angular polls GET /api/surveys/{id} until status is terminal.
    return Results.Accepted($"/api/surveys/{survey.Id}", new
    {
        survey.Id,
        survey.Name,
        survey.Status,
        survey.TotalRows,
        ColumnCount = columnEntities.Count,
    });
}).DisableAntiforgery();

// ----------------------------------------
// GET /api/surveys — List all surveys
// ----------------------------------------
// Returns a summary list — no columns or row data included.
// Ordered newest first.
// ----------------------------------------
app.MapGet("/api/surveys", async (ApplicationDbContext db) =>
{
    var surveys = await db.Surveys
        .OrderByDescending(s => s.UploadedAt)
        .Select(s => new
        {
            s.Id,
            s.Name,
            s.Status,
            s.TotalRows,
            s.ProcessedRows,
            s.UploadedBy,
            s.UploadedAt,
            s.CompletedAt,
        })
        .ToListAsync();

    return Results.Ok(surveys);
});

// ----------------------------------------
// GET /api/surveys/{id} — Get a single survey with its columns
// ----------------------------------------
// Returns the survey metadata and all detected column definitions.
// Use this to know what columns exist before querying responses.
// ----------------------------------------
app.MapGet("/api/surveys/{id}", async (int id, ApplicationDbContext db) =>
{
    // Include(s => s.Columns) = JOIN to SurveyColumns table
    // This is EF Core's way of eager-loading a related collection.
    var survey = await db.Surveys
        .Include(s => s.Columns)
        .FirstOrDefaultAsync(s => s.Id == id);

    if (survey is null) return Results.NotFound();

    return Results.Ok(new
    {
        survey.Id,
        survey.Name,
        survey.Status,
        survey.TotalRows,
        survey.ProcessedRows,
        survey.ErrorMessage,
        survey.UploadedBy,
        survey.UploadedAt,
        survey.CompletedAt,
        Columns = survey.Columns
            .OrderBy(c => c.ColumnIndex)
            .Select(c => new
            {
                c.Id,
                c.ColumnName,
                c.ColumnType,
                c.AnalyzeSentiment,
                c.ColumnIndex,
            }),
    });
});

// ============================================
// START THE APPLICATION
// ============================================
// Starts the web server and listens for HTTP requests.
// Blocks until the app is shut down (Ctrl+C).
app.Run();
