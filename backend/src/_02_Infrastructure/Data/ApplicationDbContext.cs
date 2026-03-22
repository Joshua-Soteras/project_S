// ============================================
// Application Database Context
// ============================================
//
// WHAT IS A DBCONTEXT?
// DbContext is the main class that coordinates Entity Framework
// functionality for your data model. Think of it as:
// - A session with the database
// - A unit of work that tracks changes
// - A factory for querying entities
//
// RESPONSIBILITIES:
// 1. Define which entities (tables) exist via DbSet<T> properties
// 2. Configure entity relationships and constraints in OnModelCreating
// 3. Provide methods to query and save data
//
// WHERE DOES THIS CLASS GO?
// In the Infrastructure layer because:
// - It depends on EF Core (a framework/infrastructure concern)
// - It handles data persistence (infrastructure)
// - The Core layer should not know about databases
// ============================================

// ============================================
// [ADDED] WHERE THIS FITS IN THE FULL ARCHITECTURE
// ============================================
//
//   _01_Core           →  defines entities (pure C# classes, no EF dependency)
//   _02_Infrastructure →  THIS FILE lives here. It knows about EF Core
//                         and PostgreSQL. Core does not.
//   _03_Web_API        →  registers this class with DI in Program.cs so any
//                         endpoint can receive it as a parameter
//
// [ADDED] FLOW OF A REQUEST:
//   HTTP request arrives
//     → ASP.NET DI injects ApplicationDbContext into the endpoint
//     → Endpoint calls db.Surveys.ToListAsync()
//     → EF Core translates that to SQL: SELECT * FROM "Surveys"
//     → Npgsql sends the query to PostgreSQL over TCP
//     → Results are mapped back to C# objects and returned
// ============================================

// NOTE: The IDE may flag this as redundant, but it is required.
// Microsoft.EntityFrameworkCore is NOT included as a global using in this project —
// DbContext, DbContextOptions, DbSet, and ModelBuilder all live in this namespace.
using Microsoft.EntityFrameworkCore;
using ProjectS.Core.Entities;

namespace ProjectS.Infrastructure.Data;

// ----------------------------------------
// [ADDED] PRIMARY CONSTRUCTOR (C# 12)
// ----------------------------------------
// Rather than a separate constructor body, the parameter is declared
// directly on the class. ': DbContext(options)' passes it straight to
// the EF Core base class, which sets up the internal connection machinery.
//
// This is equivalent to the older form:
//   public ApplicationDbContext(DbContextOptions<...> options) : base(options) { }
//
// DbContextOptions carries:
// - The connection string (how to reach PostgreSQL)
// - The database provider (Npgsql)
// - Any other EF Core settings configured in Program.cs via:
//     builder.Services.AddDbContext<ApplicationDbContext>(options =>
//         options.UseNpgsql(connectionString));
// ----------------------------------------

/// <summary>
/// The main database context for the application.
/// Inherits from EF Core's DbContext to gain database functionality.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    // ============================================
    // DBSETS (TABLE DEFINITIONS)
    // ============================================
    //
    // Each DbSet<T> represents a table in the database.
    // - DbSet<User> Users → "Users" table
    // - The generic type <User> tells EF Core the entity type
    // - The property name "Users" is what you use in queries
    //
    // USAGE EXAMPLES:
    //   // Query all users
    //   var users = await db.Users.ToListAsync();
    //
    //   // Find by ID
    //   var user = await db.Users.FindAsync(1);
    //
    //   // Query with filter
    //   var admins = await db.Users
    //       .Where(u => u.Email.Contains("@admin"))
    //       .ToListAsync();
    //
    //   // Insert
    //   db.Users.Add(new User { Email = "test@example.com" });
    //   await db.SaveChangesAsync();
    //
    //   // Update (EF Core tracks changes automatically)
    //   var user = await db.Users.FindAsync(1);
    //   user.DisplayName = "New Name";
    //   await db.SaveChangesAsync();
    //
    //   // Delete
    //   db.Users.Remove(user);
    //   await db.SaveChangesAsync();
    //
    // [ADDED] WHY { get; set; } = null! INSTEAD OF => Set<T>() ?
    // The { get; set; } pattern is the standard EF Core convention.
    // EF Core's own tooling and scaffolding generates this form.
    // The = null! tells the compiler "EF Core guarantees this is
    // initialized before use — trust me, it won't be null."
    // ============================================

    /// <summary>
    /// Represents the Users table in the database.
    /// Use this property to query, insert, update, or delete users.
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Represents the TestItems table in the database.
    /// Use this for testing database connectivity and CRUD operations.
    /// </summary>
    public DbSet<TestItem> TestItems { get; set; } = null!;

    // ============================================
    // [ADDED] SURVEY PIPELINE DBSETS
    // ============================================
    //
    // These six tables represent the full lifecycle of a survey
    // CSV file — from upload through sentiment analysis to KPI output.
    //
    // Relationship chain:
    //
    //   Survey
    //     ├── SurveyColumn[]      (what columns exist in this CSV)
    //     ├── SurveyResponse[]    (one per row in the CSV)
    //     │     └── ResponseValue[]   (one per cell: row × column)
    //     │           └── SentimentResult  (one per text cell, after NLP)
    //     └── KpiAggregate[]     (pre-computed averages, written when
    //                             processing completes — dashboard reads this)
    //
    // Why pre-compute KpiAggregates?
    // Without them, every dashboard load would run AVG() and COUNT()
    // across potentially millions of ResponseValue rows in real time.
    // With thousands of users hitting dashboards simultaneously, that
    // would kill the database. The NLP worker computes this once when
    // a survey finishes — dashboard reads a single flat row instead.
    // ============================================

    /// <summary>
    /// [ADDED] Top-level record for an uploaded CSV file.
    /// Tracks upload metadata, processing status, and who submitted it.
    /// Status values: "queued" | "processing" | "complete" | "error"
    /// </summary>
    public DbSet<Survey> Surveys { get; set; } = null!;

    /// <summary>
    /// [ADDED] Describes the columns detected in a specific survey's CSV.
    /// Each survey can have a completely different set of columns —
    /// this is how we handle variable CSV formats across different events.
    /// The AnalyzeSentiment flag marks which text columns get sent to the NLP service.
    /// </summary>
    public DbSet<SurveyColumn> SurveyColumns { get; set; } = null!;

    /// <summary>
    /// [ADDED] One record per row in the uploaded CSV.
    /// Acts as the parent grouping for all cell values in that row.
    /// RowIndex preserves the original order from the CSV file.
    /// </summary>
    public DbSet<SurveyResponse> SurveyResponses { get; set; } = null!;

    /// <summary>
    /// [ADDED] One record per cell (row × column intersection).
    /// Stores the raw string value from the CSV.
    /// For text columns marked AnalyzeSentiment=true, a SentimentResult
    /// will be created and linked to this record after NLP processing.
    /// </summary>
    public DbSet<ResponseValue> ResponseValues { get; set; } = null!;

    /// <summary>
    /// [ADDED] Stores the output of the RoBERTa sentiment model for a single text cell.
    /// One-to-one with ResponseValue. Only created for cells whose column
    /// has AnalyzeSentiment=true.
    /// Label is one of: "positive", "neutral", "negative".
    /// Scores are probabilities (0.0–1.0) that sum to ~1.0.
    /// </summary>
    public DbSet<SentimentResult> SentimentResults { get; set; } = null!;

    /// <summary>
    /// [ADDED] Pre-computed KPI aggregates per survey per column.
    /// Written by the NLP worker when a survey finishes processing.
    /// Dashboard endpoints read from this table instead of running
    /// expensive AVG/COUNT queries over millions of ResponseValues at runtime.
    /// </summary>
    public DbSet<KpiAggregate> KpiAggregates { get; set; } = null!;

    // ============================================
    // ONMODELCREATING (FLUENT API CONFIGURATION)
    // ============================================
    //
    // This method is called when EF Core builds the model (schema).
    // Use it to configure:
    // - Table names, column names, data types
    // - Primary keys, foreign keys, indexes
    // - Relationships between entities
    // - Seed data (initial data inserted on migration)
    //
    // WHY USE FLUENT API INSTEAD OF DATA ANNOTATIONS?
    // - Keeps entities "pure" (no EF Core attributes in Core layer)
    // - More powerful and flexible than annotations
    // - All configuration in one place
    //
    // [ADDED] WHY NOT PUT [Required] / [MaxLength] ON THE ENTITY CLASSES?
    // Entities live in _01_Core, which has zero EF dependencies.
    // Putting EF attributes on them would break that separation.
    // Fluent API keeps all database configuration here in Infrastructure,
    // which is the only layer that is supposed to know about the database.
    // ============================================
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Always call base first to apply any default configurations
        base.OnModelCreating(modelBuilder);

        // ----------------------------------------
        // USER ENTITY CONFIGURATION
        // ----------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            // INDEX: Create unique index on Email column
            // This ensures no two users can have the same email
            // Also improves query performance when searching by email
            entity.HasIndex(e => e.Email)
                  .IsUnique();

            // PROPERTY: Configure Email column
            // - IsRequired() = NOT NULL constraint
            // - HasMaxLength(255) = VARCHAR(255) instead of unlimited TEXT
            entity.Property(e => e.Email)
                  .IsRequired()
                  .HasMaxLength(255);

            // PROPERTY: Configure DisplayName column
            // - HasMaxLength(100) = limit to 100 characters
            entity.Property(e => e.DisplayName)
                  .HasMaxLength(100);

            // PROPERTY: Configure CreatedAt with database default
            // - HasDefaultValueSql("NOW()") = PostgreSQL sets the value
            //   if not provided, instead of relying on C# default
            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("NOW()");
        });

        // ----------------------------------------
        // TESTITEM ENTITY CONFIGURATION
        // ----------------------------------------
        modelBuilder.Entity<TestItem>(entity =>
        {
            // PROPERTY: Configure Name column
            // - IsRequired() = NOT NULL constraint
            // - HasMaxLength(200) = VARCHAR(200)
            entity.Property(e => e.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            // PROPERTY: Configure CreatedAt with database default
            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("NOW()");
        });

        // ----------------------------------------
        // [ADDED] SURVEY ENTITY CONFIGURATION
        //
        // Central table — everything else in the pipeline has a
        // foreign key back to this record.
        //
        // Status is stored as a string rather than a DB enum so that
        // adding new status values never requires a migration.
        // The application layer is responsible for validating the value.
        //
        // Two indexes are added beyond the primary key:
        //   - Status     → the NLP worker queries for surveys in "queued"
        //                  state; without an index this scans the full table
        //   - UploadedBy → dashboard filter "show me my uploads" needs this
        // ----------------------------------------
        modelBuilder.Entity<Survey>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);

            // BlobUrl stores the Azure Blob Storage path to the raw CSV.
            // 1024 chars covers long container + path combinations.
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(1024);

            entity.Property(e => e.Status).IsRequired().HasMaxLength(20)
                  .HasDefaultValue("queued");

            // UploadedBy will store the Azure Entra ID user principal name
            // (e.g. "john.doe@company.com") once auth is wired up.
            entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(255);

            entity.Property(e => e.UploadedAt).HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UploadedBy);
        });

        // ----------------------------------------
        // [ADDED] SURVEY COLUMN ENTITY CONFIGURATION
        //
        // Stores the detected column layout for a specific CSV upload.
        // Because every event's CSV can have different columns, we store
        // the schema per survey rather than assuming a fixed structure.
        //
        // OnDelete(Cascade): if the parent Survey is deleted, all of its
        // column definitions are deleted too — no orphaned records.
        // ----------------------------------------
        modelBuilder.Entity<SurveyColumn>(entity =>
        {
            entity.Property(e => e.ColumnName).IsRequired().HasMaxLength(255);

            // ColumnType tells the parser and NLP worker how to treat each column.
            // Defaults to "text" since most survey columns are free-text.
            entity.Property(e => e.ColumnType).IsRequired().HasMaxLength(20)
                  .HasDefaultValue("text");

            // Index on SurveyId: loading all columns for a given survey is
            // a very frequent operation — this makes it fast.
            entity.HasIndex(e => e.SurveyId);

            entity.HasOne(e => e.Survey)
                  .WithMany(s => s.Columns)
                  .HasForeignKey(e => e.SurveyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ----------------------------------------
        // [ADDED] SURVEY RESPONSE ENTITY CONFIGURATION
        //
        // One record per CSV row. The RowIndex preserves original CSV order
        // so the dashboard can paginate rows exactly as they appeared.
        //
        // Composite index (SurveyId, RowIndex): supports paginated queries
        // like "give me rows 500–600 of survey #42 in order" without
        // scanning the entire SurveyResponses table.
        // ----------------------------------------
        modelBuilder.Entity<SurveyResponse>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.SurveyId);

            // Composite index for ordered pagination within a survey
            entity.HasIndex(e => new { e.SurveyId, e.RowIndex });

            entity.HasOne(e => e.Survey)
                  .WithMany(s => s.Responses)
                  .HasForeignKey(e => e.SurveyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ----------------------------------------
        // [ADDED] RESPONSE VALUE ENTITY CONFIGURATION
        //
        // One record per cell — the intersection of a row and a column.
        // RawValue is nullable because not every CSV cell has a value.
        //
        // Two foreign keys:
        //   ResponseId → which row this cell belongs to
        //   ColumnId   → which column definition describes this cell
        //
        // OnDelete behavior differs between the two FKs intentionally:
        //   ResponseId → Cascade: if the row is deleted, delete its cells
        //   ColumnId   → Restrict: do NOT automatically delete cells if a
        //                column definition is removed — force an explicit
        //                operation to avoid accidental data loss
        // ----------------------------------------
        modelBuilder.Entity<ResponseValue>(entity =>
        {
            entity.HasIndex(e => e.ResponseId);
            entity.HasIndex(e => e.ColumnId);

            // Composite index: the most common query is
            // "give me all cells for this response, keyed by column"
            entity.HasIndex(e => new { e.ResponseId, e.ColumnId });

            entity.HasOne(e => e.Response)
                  .WithMany(r => r.Values)
                  .HasForeignKey(e => e.ResponseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Column)
                  .WithMany(c => c.ResponseValues)
                  .HasForeignKey(e => e.ColumnId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------
        // [ADDED] SENTIMENT RESULT ENTITY CONFIGURATION
        //
        // One-to-one with ResponseValue. Only exists for cells whose
        // parent column has AnalyzeSentiment=true.
        //
        // HasIndex(...).IsUnique() enforces the one-to-one relationship
        // at the database level — PostgreSQL will reject a second sentiment
        // result for the same cell, not just the application layer.
        //
        // Scores are stored as float (4-byte real in PostgreSQL).
        // Model confidence values between 0.0 and 1.0 do not need
        // double precision — float is sufficient and uses less storage.
        // ----------------------------------------
        modelBuilder.Entity<SentimentResult>(entity =>
        {
            entity.Property(e => e.Label).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ProcessedAt).HasDefaultValueSql("NOW()");

            // Unique index — enforces one sentiment result per cell
            entity.HasIndex(e => e.ResponseValueId).IsUnique();

            // HasForeignKey<SentimentResult> is required here because this
            // is a one-to-one relationship. EF needs to know which side
            // owns the foreign key column.
            entity.HasOne(e => e.ResponseValue)
                  .WithOne(rv => rv.SentimentResult)
                  .HasForeignKey<SentimentResult>(e => e.ResponseValueId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ----------------------------------------
        // [ADDED] KPI AGGREGATE ENTITY CONFIGURATION
        //
        // Pre-computed summary per (Survey × Column) pair.
        // Written once by the NLP worker when all rows finish processing.
        // Dashboard reads from this table — never runs live aggregation.
        //
        // Composite unique index (SurveyId, ColumnId): guarantees exactly
        // one aggregate row per column per survey. If a survey is reprocessed,
        // the worker issues an UPDATE on this row instead of inserting a duplicate.
        //
        // OnDelete for ColumnId is Restrict (not Cascade) for the same reason
        // as ResponseValue — column definitions should not silently delete
        // aggregated KPI data.
        // ----------------------------------------
        modelBuilder.Entity<KpiAggregate>(entity =>
        {
            entity.Property(e => e.ComputedAt).HasDefaultValueSql("NOW()");

            entity.HasIndex(e => new { e.SurveyId, e.ColumnId }).IsUnique();

            entity.HasOne(e => e.Survey)
                  .WithMany(s => s.KpiAggregates)
                  .HasForeignKey(e => e.SurveyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Column)
                  .WithMany(c => c.KpiAggregates)
                  .HasForeignKey(e => e.ColumnId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------
        // ADD MORE ENTITY CONFIGURATIONS HERE
        // ----------------------------------------
        // modelBuilder.Entity<Post>(entity => { ... });
        // modelBuilder.Entity<Comment>(entity => { ... });
    }
}
