namespace ProjectS.Core.Entities;

public class SentimentResult
{
    public int Id { get; set; }
    public int ResponseValueId { get; set; }

    /// <summary>positive | neutral | negative</summary>
    public string Label { get; set; } = string.Empty;

    public float PositiveScore { get; set; }
    public float NeutralScore { get; set; }
    public float NegativeScore { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ResponseValue ResponseValue { get; set; } = null!;
}
