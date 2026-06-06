using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.FunctionalTests.TestUtilities;

/// <summary>
/// Terse JSON + fake-auth + SSE helpers for functional tests (PRD-0003). The fake-auth helpers only
/// attach the headers the test-only auth scheme reads (see <see cref="FakeAuthWebApplicationFactory"/>);
/// the request still flows through CSRF and the access gate.
/// </summary>
public static class HttpClientExtensions
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object value) =>
        client.PostAsJsonAsync(url, value, Web);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object value) =>
        client.PutAsJsonAsync(url, value, Web);

    public static async Task<T?> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Web);

    /// <summary>Impersonate a User via the test-only fake auth scheme (no token issuance).</summary>
    public static HttpClient AsUser(this HttpClient client, Guid userId, params string[] roles)
    {
        client.DefaultRequestHeaders.Remove(FakeAuthWebApplicationFactory.UserIdHeader);
        client.DefaultRequestHeaders.Add(FakeAuthWebApplicationFactory.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Remove(FakeAuthWebApplicationFactory.RolesHeader);
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(FakeAuthWebApplicationFactory.RolesHeader, string.Join(',', roles));
        return client;
    }

    /// <summary>Impersonate an Admin via the fake auth scheme.</summary>
    public static HttpClient AsAdmin(this HttpClient client, Guid? userId = null) =>
        client.AsUser(userId ?? Guid.CreateVersion7(), Roles.Admin);

    /// <summary>
    /// Reads a <c>text/event-stream</c> response into its <c>data:</c> payloads, hiding the SSE framing
    /// (the agent transport writes <c>data: {json}\n\n</c> per event).
    /// </summary>
    public static async Task<IReadOnlyList<string>> ReadServerSentEventsAsync(
        this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var events = new List<string>();
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    events.Add(data.ToString());
                    data.Clear();
                }
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line["data:".Length..].TrimStart());
            }
        }

        if (data.Length > 0)
            events.Add(data.ToString());

        return events;
    }
}
