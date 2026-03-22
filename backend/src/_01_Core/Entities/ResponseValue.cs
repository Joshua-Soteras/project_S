namespace ProjectS.Core.Entities;

public class ResponseValue
{
    public int Id { get; set; }
    public int ResponseId { get; set; }
    public int ColumnId { get; set; }
    public string? RawValue { get; set; }

    // Navigation properties
    public SurveyResponse Response { get; set; } = null!;
    public SurveyColumn Column { get; set; } = null!;
    public SentimentResult? SentimentResult { get; set; }
}
