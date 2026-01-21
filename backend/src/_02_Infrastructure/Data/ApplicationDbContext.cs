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

using Microsoft.EntityFrameworkCore;
using ProjectS.Core.Entities;  // Import entities from Core layer

namespace ProjectS.Infrastructure.Data;

/// <summary>
/// The main database context for the application.
/// Inherits from EF Core's DbContext to gain database functionality.
/// </summary>
public class ApplicationDbContext : DbContext
{
    // ----------------------------------------
    // CONSTRUCTOR
    // ----------------------------------------
    // Receives DbContextOptions from Dependency Injection.
    // These options contain:
    // - Connection string (how to connect to the database)
    // - Database provider (PostgreSQL via Npgsql)
    // - Other EF Core configurations
    //
    // The ": base(options)" passes the options to the parent
    // DbContext class, which uses them to configure the connection.
    // ----------------------------------------
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

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
    // ============================================

    /// <summary>
    /// Represents the Users table in the database.
    /// Use this property to query, insert, update, or delete users.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Represents the TestItems table in the database.
    /// Use this for testing database connectivity and CRUD operations.
    /// </summary>
    public DbSet<TestItem> TestItems => Set<TestItem>();

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
        // ADD MORE ENTITY CONFIGURATIONS HERE
        // ----------------------------------------
        // modelBuilder.Entity<Post>(entity => { ... });
        // modelBuilder.Entity<Comment>(entity => { ... });
    }
}