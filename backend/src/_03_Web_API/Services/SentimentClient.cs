// ============================================
// SentimentClient.cs
// ============================================
//
// RESPONSIBILITY:
// HTTP client wrapper that calls the Python NLP microservice.
// Sends one text string → gets back a sentiment score object.
//
// THIS CLASS DOES NOT TOUCH THE DATABASE.
// It only makes HTTP calls and returns typed results.
// The endpoint in Program.cs is responsible for saving to the DB.
//
// WHY A TYPED HTTPCLIENT?
// AddHttpClient<SentimentClient>() in Program.cs registers this class
// with a pre-configured HttpClient (base URL from config, connection pooling,
// retry policies can be added via Polly later). No manual HttpClient
// construction or disposal needed.
//
// WHERE THIS FITS:
//   POST /api/surveys endpoint
//       → SentimentClient.AnalyzeAsync(text)   ← THIS FILE
//           → POST http://nlp:8000/analyze
//               → { label, positive, neutral, negative }
//       → endpoint saves SentimentResult to DB
// ============================================

using System.Net.Http.Json;

namespace ProjectS.Web.Services;

// ============================================
// REQUEST / RESPONSE CONTRACTS
// ============================================
// These records mirror the JSON shapes the Python service expects/returns.
//
// PostAsJsonAsync uses JsonSerializerDefaults.Web (camelCase + case-insensitive),
// so "Text" serializes as "text" in the request body, and "label"/"positive"/
// "neutral"/"negative" from the response map to "Label"/"Positive"/etc.
// No [JsonPropertyName] attributes needed.
// ============================================

/// <summary>
/// JSON body sent to POST /analyze on the NLP service.
/// </summary>
public record SentimentRequest(string Text);

/// <summary>
/// JSON body returned by POST /analyze on the NLP service.
///
/// Label   — winning sentiment: "positive" | "neutral" | "negative"
/// Positive — probability score for positive sentiment (0.0–1.0)
/// Neutral  — probability score for neutral sentiment  (0.0–1.0)
/// Negative — probability score for negative sentiment (0.0–1.0)
///
/// The three scores always sum to ~1.0 (softmax output from the model).
/// </summary>
public record SentimentResponse(
    string Label,
    float Positive,
    float Neutral,
    float Negative
);

// ============================================
// CLIENT
// ============================================
public class SentimentClient(HttpClient http)
{
    // ----------------------------------------
    // AnalyzeAsync — Call POST /analyze
    // ----------------------------------------
    // Sends the text to the NLP service and returns the sentiment result.
    // Returns null if the NLP service returns a non-success status code
    // (e.g. empty text, service unavailable). The caller skips the row
    // rather than failing the entire upload.
    // ----------------------------------------
    public async Task<SentimentResponse?> AnalyzeAsync(string text)
    {
        // PostAsJsonAsync serializes the C# record to JSON and sets
        // Content-Type: application/json automatically.
        var response = await http.PostAsJsonAsync("/analyze", new SentimentRequest(text));

        if (!response.IsSuccessStatusCode)
            return null;

        // ReadFromJsonAsync deserializes the response body.
        // Uses JsonSerializerDefaults.Web so "positive" maps to Positive, etc.
        return await response.Content.ReadFromJsonAsync<SentimentResponse>();
    }
}
