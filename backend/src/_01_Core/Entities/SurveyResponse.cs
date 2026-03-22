namespace ProjectS.Core.Entities;

public class SurveyResponse
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public int RowIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Survey Survey { get; set; } = null!;
    public List<ResponseValue> Values { get; set; } = new();
}
