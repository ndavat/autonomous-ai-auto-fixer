using System.Net;
using System.Text;
using System.Text.Json;

namespace AutoFixer.Tests;

/// <summary>
/// Shared test utilities for HTTP-based ingestor and VCS client tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Creates an <see cref="HttpResponseMessage"/> with JSON body serialized from <paramref name="payload"/>.
    /// </summary>
    public static HttpResponseMessage JsonResponse(HttpStatusCode status, object payload)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that captures every request and returns a
/// pre-configured response. Optionally buffers the request body so it remains
/// readable after the request completes.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    private readonly bool _bufferBody;
    public List<HttpRequestMessage> Requests { get; } = new();

    public CapturingHandler(
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null,
        bool bufferBody = false)
    {
        _responder = responder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK));
        _bufferBody = bufferBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_bufferBody && request.Content is not null)
        {
            var buffered = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            request.Content = new StringContent(buffered, Encoding.UTF8, "application/json");
        }
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
