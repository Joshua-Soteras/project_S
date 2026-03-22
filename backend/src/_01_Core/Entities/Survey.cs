namespace ProjectS.Core.Entities;

public class Survey
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;

    /// <summary>queued | processing | complete | error</summary>
    public string Status { get; set; } = "queued";

    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public string? ErrorMessage { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public List<SurveyColumn> Columns { get; set; } = new();
    public List<SurveyResponse> Responses { get; set; } = new();
    public List<KpiAggregate> KpiAggregates { get; set; } = new();
}
