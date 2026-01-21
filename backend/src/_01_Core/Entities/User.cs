// ============================================
// User Entity
// ============================================
//
// WHAT IS AN ENTITY?
// An entity is a C# class that maps directly to a database table.
// - Each PROPERTY becomes a COLUMN in the table
// - Each INSTANCE of the class becomes a ROW in the table
// - The CLASS NAME (pluralized) becomes the TABLE NAME
//
// Example mapping:
//   C# Class: User        →  Database Table: Users
//   Property: Id          →  Column: id (PRIMARY KEY)
//   Property: Email       →  Column: email (VARCHAR)
//   Property: CreatedAt   →  Column: created_at (TIMESTAMP)
//
// WHY IN THE CORE LAYER?
// Entities live in the Core layer because they represent your
// domain/business objects. They should be "pure" C# with no
// dependencies on frameworks like Entity Framework.
// ============================================

namespace ProjectS.Core.Entities;

/// <summary>
/// Represents a user in the system.
/// Maps to the "Users" table in PostgreSQL.
/// </summary>
public class User
{
    // ----------------------------------------
    // PRIMARY KEY
    // ----------------------------------------
    // EF Core Convention: A property named "Id" or "{ClassName}Id"
    // is automatically detected as the primary key.
    // It will be auto-incremented (SERIAL in PostgreSQL).
    // ----------------------------------------
    public int Id { get; set; }

    // ----------------------------------------
    // REQUIRED STRING PROPERTY
    // ----------------------------------------
    // Non-nullable string = NOT NULL column in database.
    // "= string.Empty" provides a default value to avoid
    // C# nullable warnings. This doesn't affect the database.
    // ----------------------------------------
    public string Email { get; set; } = string.Empty;

    // ----------------------------------------
    // NULLABLE STRING PROPERTY
    // ----------------------------------------
    // The "?" makes this nullable (string? = NULL allowed).
    // In the database, this column will allow NULL values.
    // Use nullable for optional fields.
    // ----------------------------------------
    public string? DisplayName { get; set; }

    // ----------------------------------------
    // DATETIME WITH DEFAULT VALUE
    // ----------------------------------------
    // "= DateTime.UtcNow" sets the default in C# code.
    // When you create a new User(), CreatedAt is automatically
    // set to the current UTC time.
    //
    // NOTE: This default is set by C#, not the database.
    // For database-level defaults, use Fluent API:
    //   entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
    // ----------------------------------------
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ----------------------------------------
    // NULLABLE DATETIME
    // ----------------------------------------
    // DateTime? = nullable timestamp.
    // This will be NULL until the record is updated.
    // Useful for tracking "last modified" timestamps.
    // ----------------------------------------
    public DateTime? UpdatedAt { get; set; }

    // ----------------------------------------
    // NAVIGATION PROPERTIES (for relationships)
    // ----------------------------------------
    // Uncomment when you add related entities like Post:
    //
    // ONE-TO-MANY: One User has many Posts
    // public List<Post> Posts { get; set; } = new();
    //
    // This creates a foreign key relationship where:
    // - User table has no extra columns
    // - Post table gets an "AuthorId" column (FK to User.Id)
    // ----------------------------------------
}
