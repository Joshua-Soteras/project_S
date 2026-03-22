using Microsoft.EntityFrameworkCore;
using ProjectS.Core.Entities;
using ProjectS.Infrastructure.Data;

namespace ProjectS.Web.Services;

// ============================================
// SurveyProcessingService — NLP + KPI Pipeline
// ============================================
//
// Contains the full NLP + KPI computation pipeline for a single survey.
// This logic was previously inline in POST /api/surveys (Step 4).
// Extracting it here lets the background worker call it with a fresh
// DI scope without duplicating code.
//
// LIFETIME: Scoped — resolved once per background job inside a
// manually created IServiceScope in SurveyProcessingWorker.
//
// FLOW:
//   1. Set survey status → "processing"
//   2. Find all text columns (AnalyzeSentiment = true)
//   3. Load all ResponseValues for those columns
//   4. Call Python NLP service in batches of 100, save SentimentResult rows
//   5. Compute KpiAggregate per text column (avg + label counts)
//   6. Set survey status → "complete"
//   On any exception → set status → "error" with message
// ============================================
public class SurveyProcessingService(
    ApplicationDbContext db,
    SentimentClient sentiment,
    ILogger<SurveyProcessingService> logger)
{
    private const int NlpBatchSize = 100;

    public async Task ProcessAsync(int surveyId, CancellationToken ct = default)
    {
        var survey = await db.Surveys.FindAsync([surveyId], ct);
        if (survey is null)
        {
            logger.LogWarning("ProcessAsync called for unknown survey {SurveyId}", surveyId);
            return;
        }

        try
        {
            // Mark as in-progress so the dashboard shows a spinner
            survey.Status = "processing";
            await db.SaveChangesAsync(ct);

            // --- IDENTIFY TEXT COLUMNS ---
            var textColumnIds = (await db.SurveyColumns
                .Where(c => c.SurveyId == surveyId && c.AnalyzeSentiment)
                .Select(c => c.Id)
                .ToListAsync(ct))
                .ToHashSet();

            var sentimentCount = 0;

            if (textColumnIds.Count > 0)
            {
                // --- LOAD TEXT RESPONSE VALUES ---
                // Query all non-null ResponseValues for text columns.
                var textValues = await db.ResponseValues
                    .Where(rv => textColumnIds.Contains(rv.ColumnId) && rv.RawValue != null)
                    .ToListAsync(ct);

                // --- CALL NLP SERVICE IN BATCHES ---
                for (var i = 0; i < textValues.Count; i += NlpBatchSize)
                {
                    ct.ThrowIfCancellationRequested();

                    var batch = textValues.Skip(i).Take(NlpBatchSize);
                    var sentimentEntities = new List<SentimentResult>();

                    foreach (var rv in batch)
                    {
                        if (string.IsNullOrWhiteSpace(rv.RawValue)) continue;

                        // Returns null if the NLP service is down — skip and continue.
                        var result = await sentiment.AnalyzeAsync(rv.RawValue);
                        if (result is null) continue;

                        sentimentEntities.Add(new SentimentResult
                        {
                            ResponseValueId = rv.Id,
                            Label           = result.Label,
                            PositiveScore   = result.Positive,
                            NeutralScore    = result.Neutral,
                            NegativeScore   = result.Negative,
                        });
                    }

                    db.SentimentResults.AddRange(sentimentEntities);
                    await db.SaveChangesAsync(ct);
                    sentimentCount += sentimentEntities.Count;

                    logger.LogInformation(
                        "Survey {SurveyId}: processed NLP batch {Batch}/{Total}",
                        surveyId, (i / NlpBatchSize) + 1,
                        (int)Math.Ceiling(textValues.Count / (double)NlpBatchSize));
                }

                // --- COMPUTE KPI AGGREGATES ---
                // One KpiAggregate row per text column — pre-computed summary
                // read by the dashboard instead of live AVG/COUNT queries.
                foreach (var columnId in textColumnIds)
                {
                    var columnResults = await db.SentimentResults
                        .Where(sr => sr.ResponseValue.ColumnId == columnId)
                        .ToListAsync(ct);

                    if (columnResults.Count == 0) continue;

                    db.KpiAggregates.Add(new KpiAggregate
                    {
                        SurveyId       = surveyId,
                        ColumnId       = columnId,
                        TotalResponses = columnResults.Count,
                        AvgPositive    = (float)columnResults.Average(r => r.PositiveScore),
                        AvgNeutral     = (float)columnResults.Average(r => r.NeutralScore),
                        AvgNegative    = (float)columnResults.Average(r => r.NegativeScore),
                        CountPositive  = columnResults.Count(r => r.Label == "positive"),
                        CountNeutral   = columnResults.Count(r => r.Label == "neutral"),
                        CountNegative  = columnResults.Count(r => r.Label == "negative"),
                    });
                }

                await db.SaveChangesAsync(ct);
            }

            // --- MARK COMPLETE ---
            survey.Status       = "complete";
            survey.ProcessedRows = await db.SurveyResponses.CountAsync(r => r.SurveyId == surveyId, ct);
            survey.CompletedAt  = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Survey {SurveyId} complete — {SentimentCount} sentiment results",
                surveyId, sentimentCount);
        }
        catch (OperationCanceledException)
        {
            // App is shutting down — leave status as "processing".
            // The worker will not re-enqueue, so the survey stays
            // in processing state until the next app start.
            // TODO (Step 8): durable queue (Service Bus) handles re-delivery.
            logger.LogWarning("Survey {SurveyId} processing cancelled (app shutdown)", surveyId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Survey {SurveyId} processing failed", surveyId);

            // Write the error to the survey row so the dashboard can show it.
            // Use CancellationToken.None — the original token may already be cancelled.
            survey.Status       = "error";
            survey.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
