using System.Text.Json;
using Microsoft.AspNetCore.Http;
using JournalRecall.AI.Core;

namespace JournalRecall.AI.Transport;

/// <summary>
/// <see cref="IResult"/> projections of an agent event stream onto the wire (ADR-0005): SSE (default),
/// NDJSON, or the drained ad-hoc terminal response. Each streamed event becomes a versioned
/// <see cref="WireEnvelope"/>.
/// </summary>
public static class AgentResults
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Streams events using the given transport (SSE, NDJSON, or ad-hoc terminal when None).</summary>
    public static IResult Stream(IAsyncEnumerable<AgentEvent> events, StreamTransport transport) => transport switch
    {
        StreamTransport.Sse => Sse(events),
        StreamTransport.Ndjson => Ndjson(events),
        StreamTransport.None => AdHoc(events),
        _ => Sse(events),
    };

    /// <summary>Server-Sent Events: <c>data: {envelope}\n\n</c> per event.</summary>
    public static IResult Sse(IAsyncEnumerable<AgentEvent> events) =>
        new StreamingResult(events, "text/event-stream", (env, json) => $"data: {json}\n\n");

    /// <summary>Newline-delimited JSON: one envelope per line.</summary>
    public static IResult Ndjson(IAsyncEnumerable<AgentEvent> events) =>
        new StreamingResult(events, "application/x-ndjson", (env, json) => $"{json}\n");

    /// <summary>Drains the stream and returns the terminal <see cref="AdHocResponse"/> as JSON.</summary>
    public static IResult AdHoc(IAsyncEnumerable<AgentEvent> events) => new AdHocResult(events);

    private sealed class StreamingResult(
        IAsyncEnumerable<AgentEvent> events, string contentType, Func<WireEnvelope, string, string> frame) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.ContentType = contentType;
            response.Headers.CacheControl = "no-cache";

            long seq = 0;
            await foreach (var @event in events.WithCancellation(httpContext.RequestAborted))
            {
                var envelope = WireProjection.ToEnvelope(@event, seq++, DateTimeOffset.UtcNow);
                await response.WriteAsync(frame(envelope, JsonSerializer.Serialize(envelope, Json)), httpContext.RequestAborted);
                await response.Body.FlushAsync(httpContext.RequestAborted);
            }
        }
    }

    private sealed class AdHocResult(IAsyncEnumerable<AgentEvent> events) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            AdHocResponse? terminal = null;
            await foreach (var @event in events.WithCancellation(httpContext.RequestAborted))
                terminal = @event switch
                {
                    AgentEvent.Completed e => AdHocResponse.From(e.Outcome),
                    AgentEvent.Stopped e => AdHocResponse.From(e.Outcome),
                    AgentEvent.Failed e => AdHocResponse.From(e.Outcome),
                    _ => terminal,
                };

            await Results.Json(terminal, Json).ExecuteAsync(httpContext);
        }
    }
}
