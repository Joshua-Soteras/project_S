using ProjectS.Web.Services;

namespace ProjectS.Web.Workers;

// ============================================
// SurveyProcessingWorker — Background Queue Consumer
// ============================================
//
// A hosted background service that runs for the lifetime of the app.
// It reads survey IDs from SurveyQueue and delegates processing to
// SurveyProcessingService.
//
// LIFETIME: Singleton (BackgroundService is always singleton).
// Because SurveyProcessingService is Scoped, the worker creates a
// fresh IServiceScope per job — this gives the service its own
// DbContext and HttpClient instance, avoiding cross-request contamination.
//
// CONCURRENCY: Single-reader channel + sequential processing.
// One survey is processed at a time. This is intentional — the NLP
// service is CPU-bound on the Python side and doesn't benefit from
// concurrent requests in the current architecture.
//
// TODO (Step 8 — Azure):
// Replace SurveyQueue with Azure Service Bus. The worker reads
// ServiceBusReceivedMessage instead of int, completes or abandons
// the message based on success/failure (at-least-once delivery).
// ============================================
public sealed class SurveyProcessingWorker(
    SurveyQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SurveyProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SurveyProcessingWorker started");

        // ReadAllAsync() suspends here when the queue is empty —
        // no polling, no busy-wait. Resumes as soon as an ID is enqueued.
        await foreach (var surveyId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            logger.LogInformation("Dequeued survey {SurveyId} for processing", surveyId);

            // Create a new DI scope per job so SurveyProcessingService
            // gets its own DbContext (scoped) and SentimentClient (transient).
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<SurveyProcessingService>();

            try
            {
                await processor.ProcessAsync(surveyId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown in progress — stop the loop cleanly.
                break;
            }
            catch (Exception ex)
            {
                // SurveyProcessingService already sets survey.Status = "error"
                // and logs the exception. This outer catch is a safety net
                // in case the service itself throws unexpectedly.
                logger.LogError(ex,
                    "Unhandled error in worker for survey {SurveyId}", surveyId);
            }
        }

        logger.LogInformation("SurveyProcessingWorker stopped");
    }
}
