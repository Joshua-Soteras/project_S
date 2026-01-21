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
// START THE APPLICATION
// ============================================
// Starts the web server and listens for HTTP requests.
// Blocks until the app is shut down (Ctrl+C).
app.Run();
