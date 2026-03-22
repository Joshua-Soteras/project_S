# Entities, DbContext, and Migrations — Explained

---

## The Core Idea in One Sentence

You write C# classes. Entity Framework Core (EF Core) reads those classes and
translates them into real database tables — and tracks every change you make to
those classes over time so the database always stays in sync.

---

## Part 1 — Entities

An **entity** is just a plain C# class that EF Core treats as a database table.
Every property becomes a column. Every instance of the class becomes a row.

**From this project — `User.cs` in `_01_Core/Entities/`:**

```csharp
public class User
{
    public int Id { get; set; }           // column: Id (PRIMARY KEY, auto-increment)
    public string Email { get; set; }     // column: Email (VARCHAR 255, NOT NULL)
    public string? DisplayName { get; set; } // column: DisplayName (nullable)
    public DateTime CreatedAt { get; set; }  // column: CreatedAt
    public DateTime? UpdatedAt { get; set; } // column: UpdatedAt (nullable)
}
```

That class, when EF Core processes it, produces this in PostgreSQL:

```sql
CREATE TABLE "Users" (
    "Id"          SERIAL          PRIMARY KEY,
    "Email"       VARCHAR(255)    NOT NULL,
    "DisplayName" VARCHAR(100),
    "CreatedAt"   TIMESTAMP       NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMP
);
```

You never write that SQL. EF Core generates it. Your source of truth is the C# class.

**Why entities live in `_01_Core` and not `_02_Infrastructure`:**
Entities are pure business objects — they represent what your application *is*
(a User, a Survey, a SentimentResult). They have zero dependency on databases or
frameworks. The `_01_Core` project has no NuGet packages at all. This keeps your
domain clean and portable.

---

## Part 2 — DbContext

If entities are the *what* (your tables), `DbContext` is the *how* (your connection
to the database and the thing you actually query through).

Think of `DbContext` as a session with the database. It does three things:
1. Holds a connection to PostgreSQL
2. Exposes your entities as queryable collections (`DbSet<T>`)
3. Tracks changes to objects in memory and writes them to the DB on `SaveChangesAsync()`

**From this project — `ApplicationDbContext.cs` in `_02_Infrastructure/Data/`:**

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // These two lines are your "handles" to the database tables
    public DbSet<User> Users => Set<User>();
    public DbSet<TestItem> TestItems => Set<TestItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fine-tune column constraints beyond what EF infers from the class
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique(); // unique constraint on Email
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
        });
    }
}
```

**How you use it in `Program.cs`:**

```csharp
// Register DbContext with the DI container, pointed at PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

After this, any endpoint or service can receive `ApplicationDbContext db` as a
parameter and EF Core injects it automatically.

**A real query from `Program.cs`:**

```csharp
app.MapGet("/api/users", async (ApplicationDbContext db) =>
{
    return await db.Users.ToListAsync(); // SELECT * FROM "Users"
});

app.MapPost("/api/users", async (User user, ApplicationDbContext db) =>
{
    db.Users.Add(user);           // tell EF Core: track this object
    await db.SaveChangesAsync();  // INSERT INTO "Users" (...)
    return Results.Created(...);
});
```

`db.Users` is a `DbSet<User>` — you can think of it as a LINQ-queryable wrapper
around the `Users` table.

---

## Part 3 — Migrations

This is the part most developers find confusing at first.

### The Problem Migrations Solve

Your C# entity is the source of truth for your schema. But the database doesn't
automatically know when you change a class. If you add a property to `User`, the
`Users` table in PostgreSQL doesn't get a new column automatically — you'd have to
write an `ALTER TABLE` statement manually.

**Migrations are EF Core's solution to this.** Every time your entities change, you
generate a migration — a C# file that describes *exactly what SQL to run* to bring
the database in sync with your current entity state.

### What a Migration Actually Is

A migration is a C# class with two methods:

- `Up()` — what to do to upgrade the database (add table, add column, add index)
- `Down()` — how to undo it (drop table, drop column)

**From this project — `20260120203239_InitialCreate.cs`:**

```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Creates the TestItems table
        migrationBuilder.CreateTable(
            name: "TestItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table => table.PrimaryKey("PK_TestItems", x => x.Id)
        );

        // Creates the Users table + unique index on Email
        migrationBuilder.CreateTable(name: "Users", ...);
        migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", column: "Email", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverses everything — drops both tables
        migrationBuilder.DropTable(name: "TestItems");
        migrationBuilder.DropTable(name: "Users");
    }
}
```

You do not write this file. EF Core generates it by comparing your current entities
against the last known database state.

### The Snapshot File

There is a third file in the Migrations folder: `ApplicationDbContextModelSnapshot.cs`.

This is EF Core's "memory" of what the database looks like right now based on all
applied migrations. When you run `migrations add`, EF Core diffs your current entities
against this snapshot to figure out what changed. You never touch this file — EF
updates it automatically every time you add a migration.

```
Your entities (C#)
        │
        │  EF Core diffs these
        ▼
ApplicationDbContextModelSnapshot  ←── "what the DB looks like now"
        │
        │  difference becomes
        ▼
New migration file  ←── "here is what SQL to run next"
```

---

## Part 4 — The Commands

All migration commands run from `backend/src/_03_Web_API/` (the startup project).

### Create a migration

Run this after you add or change an entity:

```bash
dotnet ef migrations add <DescriptiveName> \
  --project ../_02_Infrastructure \
  --output-dir Data/Migrations
```

- `<DescriptiveName>` — name it after what changed: `AddSurveyTable`, `AddEmailIndex`
- `--project` — tells EF where to put the migration files (Infrastructure layer)
- `--output-dir` — subfolder within that project

This **only generates the C# migration file**. It does not touch the database yet.

### Apply migrations to the database

```bash
dotnet ef database update --project ../_02_Infrastructure
```

This runs the `Up()` method of every migration that hasn't been applied yet.
EF Core tracks which migrations have run in a special table it creates in your
database called `__EFMigrationsHistory`.

### Remove the last migration (if you haven't applied it yet)

```bash
dotnet ef migrations remove --project ../_02_Infrastructure
```

Use this if you made a mistake in the migration before running `database update`.
Once a migration is applied to the database, you should not remove it — write a
new migration to undo the change instead.

### List all migrations and their status

```bash
dotnet ef migrations list --project ../_02_Infrastructure
```

---

## Part 5 — How It All Connects

```
_01_Core/Entities/User.cs          ← you define this
         │
         │  ApplicationDbContext reads it
         ▼
_02_Infrastructure/Data/
  ApplicationDbContext.cs          ← registers User as a DbSet
  Migrations/
    20260120203239_InitialCreate.cs ← generated by EF, describes the SQL
    ApplicationDbContextModelSnapshot.cs ← EF's memory of current schema
         │
         │  dotnet ef database update runs the migration
         ▼
PostgreSQL (running in Docker)
  Table: "Users"                   ← the actual table now exists
         │
         │  Program.cs queries through DbContext
         ▼
GET /api/users → db.Users.ToListAsync() → SELECT * FROM "Users"
```

---

## Quick Reference

| Command | What it does |
|---|---|
| `migrations add <Name>` | Generates a new migration file from entity changes |
| `database update` | Applies pending migrations to the actual database |
| `migrations remove` | Deletes the last migration (only if not yet applied) |
| `migrations list` | Shows all migrations and which are applied |

| File | What it is |
|---|---|
| `User.cs`, `TestItem.cs` | Entities — your C# source of truth for the schema |
| `ApplicationDbContext.cs` | The session/connection — exposes DbSets, configures constraints |
| `YYYYMMDD_Name.cs` | Migration — generated SQL instructions for one set of changes |
| `ApplicationDbContextModelSnapshot.cs` | EF's memory — never edit manually |
