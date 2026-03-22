namespace ProjectS.Core.Entities;

public class KpiAggregate
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public int ColumnId { get; set; }
    public int TotalResponses { get; set; }
    public float AvgPositive { get; set; }
    public float AvgNeutral { get; set; }
    public float AvgNegative { get; set; }
    public int CountPositive { get; set; }
    public int CountNeutral { get; set; }
    public int CountNegative { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Survey Survey { get; set; } = null!;
    public SurveyColumn Column { get; set; } = null!;
}
