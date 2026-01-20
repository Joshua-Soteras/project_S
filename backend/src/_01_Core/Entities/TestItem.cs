// ============================================
// TestItem Entity
// ============================================
// A simple entity for testing database connectivity.
// Use this to verify EF Core and PostgreSQL are working.
// ============================================

namespace ProjectS.Core.Entities;

/// <summary>
/// A simple test entity for verifying database operations.
/// Maps to the "TestItems" table in PostgreSQL.
/// </summary>
public class TestItem
{
    /// <summary>
    /// Primary key - auto-incremented by the database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the test item. Required field.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the item was created. Defaults to current UTC time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
