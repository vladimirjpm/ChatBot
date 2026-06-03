using System.Net;
using System.Net.Http.Json;
using System.Text;
using ChatBot.Core;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Integration tests for the <c>POST /api/chat/stream</c> endpoint.
/// .NET: WebApplicationFactory spins up the real pipeline (middleware, routing, DI);
/// HTTP calls go through TestServer without an actual socket.
/// </summary>
public class ChatEndpointTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    // .NET: constructor injection of the fixture — IClassFixture<T> reuses the factory across tests in the class
    public ChatEndpointTests(TestWebFactory factory) => _factory = factory;

    [Fact]
    public async Task ChatStream_ReturnsSseTokensAndDone()
    {
        var client = _factory.CreateClient();
        var request = new ChatRequest("Привет");

        // ResponseHeadersRead — do not buffer the body, so streaming is observable
        // .NET: equivalent of HttpCompletionOption on HttpClient
        var response = await client.PostAsJsonAsync("/api/chat/stream", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        // FakeChatService yields three tokens + [DONE]
        Assert.Contains("data: \"Hello\"", body);
        Assert.Contains("data: \", \"", body);
        Assert.Contains("data: \"world!\"", body);
        Assert.Contains("data: [DONE]", body);

        // And the request reached the service
        Assert.Single(_factory.ChatService.ReceivedRequests);
        Assert.Equal("Привет", _factory.ChatService.ReceivedRequests[0].Message);
    }
}
