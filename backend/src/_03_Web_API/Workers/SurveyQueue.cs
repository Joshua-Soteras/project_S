using System.Threading.Channels;

namespace ProjectS.Web.Workers;

// ============================================
// SurveyQueue — In-Memory Async Work Queue
// ============================================
//
// A singleton wrapper around System.Threading.Channels.Channel<int>.
// The channel holds survey IDs that are waiting for NLP processing.
//
// LIFETIME: Singleton — one instance for the entire app lifetime.
// The channel must outlive any single HTTP request.
//
// PRODUCER: POST /api/surveys calls Enqueue() after saving CSV rows.
// CONSUMER: SurveyProcessingWorker reads from Reader in a background loop.
//
// WHY Channel<int> instead of ConcurrentQueue<int>?
// Channel has a built-in async wait — ReadAllAsync() suspends the
// worker when the queue is empty without spinning or polling.
// ConcurrentQueue would require a manual sleep/poll loop.
//
// FUTURE (Step 8 — Azure):
// Swap this class out for an Azure Service Bus sender/receiver.
// The worker and processing service don't need to change — only
// this class and its registration in Program.cs change.
// ============================================
public sealed class SurveyQueue
{
    // UnboundedChannel = no upper limit on items.
    // SingleReader = only one consumer (the worker), enables internal optimisations.
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions { SingleReader = true });

    /// <summary>
    /// Enqueues a survey ID for background NLP processing.
    /// Called by POST /api/surveys after saving CSV rows.
    /// </summary>
    public void Enqueue(int surveyId) =>
        _channel.Writer.TryWrite(surveyId);

    /// <summary>
    /// Async-enumerable reader consumed by SurveyProcessingWorker.
    /// ReadAllAsync() suspends until an item is available.
    /// </summary>
    public ChannelReader<int> Reader => _channel.Reader;
}
