namespace ProjectS.Core.Entities;

public class SurveyColumn
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>text | numeric | date | boolean</summary>
    public string ColumnType { get; set; } = "text";

    public bool AnalyzeSentiment { get; set; }
    public int ColumnIndex { get; set; }

    // Navigation properties
    public Survey Survey { get; set; } = null!;
    public List<ResponseValue> ResponseValues { get; set; } = new();
    public List<KpiAggregate> KpiAggregates { get; set; } = new();
}
