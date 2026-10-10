using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InventoryTracking.Syncer.Sync;

/// <summary>Talks to the viewer's API: <c>POST /api/v1/sessions</c>, <c>GET /api/v1/session</c>, <c>POST /api/v1/session/files</c>, <c>POST /api/v1/session/files/delete</c>.</summary>
public sealed class SessionClient : ISessionClient
{
    public const string RequiredHeader = "X-InventoryTracking";
    public const string SessionHeader = "X-InventoryTracking-Session";
    private const int MaxFilesPerRequest = 20;

    private readonly HttpClient _http;
    private readonly Uri _root;

    /// <param name="server">The viewer's address with a trailing slash.</param>
    /// <param name="handler">Test hook; production uses the default handler without automatic redirects (the session header must not follow a redirect).</param>
    public SessionClient(string server, HttpMessageHandler? handler = null)
    {
        _root = new Uri(server);
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> CreateSessionAsync(CancellationToken cancellation)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/sessions", null, null, cancellation);
        var json = await ReadJsonAsync(response, cancellation);
        var id = json.TryGetProperty("id", out var value) ? value.GetString() : null;
        return SessionLink.IsSessionId(id) ? id! : throw new SessionException(SessionProblem.Server, "The server did not return a session id.");
    }

    public async Task CheckSessionAsync(string session, CancellationToken cancellation)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/session", session, null, cancellation);
        await ReadJsonAsync(response, cancellation);
    }

    public async Task<IReadOnlyList<UploadOutcome>> UploadAsync(string session, IReadOnlyList<LocalFile> files, CancellationToken cancellation)
    {
        var outcomes = new List<UploadOutcome>();
        foreach (var batch in files.Chunk(MaxFilesPerRequest))
        {
            using var form = new MultipartFormDataContent();
            foreach (var file in batch)
            {
                var part = new ByteArrayContent(file.Content);
                part.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                form.Add(part, "files", file.Name);
            }

            using var response = await SendAsync(HttpMethod.Post, "api/v1/session/files", session, form, cancellation);
            var json = await ReadJsonAsync(response, cancellation);
            if (!json.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                throw new SessionException(SessionProblem.Server, "The server answer had no upload results.");
            }

            foreach (var result in results.EnumerateArray())
            {
                outcomes.Add(new UploadOutcome(
                    result.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    result.TryGetProperty("accepted", out var accepted) && accepted.GetBoolean(),
                    result.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : null));
            }
        }

        return outcomes;
    }

    public async Task DeleteAsync(string session, IReadOnlyList<string> names, CancellationToken cancellation)
    {
        foreach (var batch in names.Chunk(MaxFilesPerRequest * 5))
        {
            var body = new StringContent(JsonSerializer.Serialize(new { names = batch }), Encoding.UTF8, "application/json");
            using var response = await SendAsync(HttpMethod.Post, "api/v1/session/files/delete", session, body, cancellation);
            await ReadJsonAsync(response, cancellation);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? session, HttpContent? content, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, new Uri(_root, path)) { Content = content };
        request.Headers.Add(RequiredHeader, "1");
        if (session is not null)
        {
            request.Headers.Add(SessionHeader, session);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            if (cancellation.IsCancellationRequested)
            {
                throw;
            }

            throw new SessionException(SessionProblem.Network, "The server cannot be reached.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var message = await ErrorMessageAsync(response, cancellation);
        var status = response.StatusCode;
        response.Dispose();
        throw status switch
        {
            HttpStatusCode.NotFound or HttpStatusCode.Unauthorized => new SessionException(SessionProblem.Unknown, message),
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => new SessionException(SessionProblem.Network, "The server redirected the request; check the server address."),
            _ => new SessionException(SessionProblem.Server, message),
        };
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            using var limited = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellation)) > 0)
            {
                limited.Write(buffer, 0, read);
                if (limited.Length > 1_000_000)
                {
                    throw new SessionException(SessionProblem.Server, "The server answer is too large.");
                }
            }

            limited.Position = 0;
            using var document = await JsonDocument.ParseAsync(limited, cancellationToken: cancellation);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new SessionException(SessionProblem.Server, "The server answer is not valid JSON; check the server address.", ex);
        }
    }

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellation);
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("error", out var error) && error.GetString() is { Length: > 0 } message)
            {
                return message;
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException)
        {
            // Not a JSON error body.
        }

        return $"The server answered {(int)response.StatusCode}.";
    }
}
