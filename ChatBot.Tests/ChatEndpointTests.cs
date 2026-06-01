using System.Net;
using System.Net.Http.Json;
using System.Text;
using ChatBot.Core;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Интеграционные тесты эндпоинта <c>POST /api/chat/stream</c>.
/// .NET: WebApplicationFactory поднимает реальный pipeline (middleware, routing, DI),
/// HTTP-вызовы идут через TestServer без реального сокета.
/// </summary>
public class ChatEndpointTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    // .NET: конструктор-инжекция фикстуры — IClassFixture<T> переиспользует фабрику между тестами класса
    public ChatEndpointTests(TestWebFactory factory) => _factory = factory;

    [Fact]
    public async Task ChatStream_ReturnsSseTokensAndDone()
    {
        var client = _factory.CreateClient();
        var request = new ChatRequest("Привет");

        // ResponseHeadersRead — не буферизовать тело, чтобы видеть стриминг
        // .NET: эквивалент HttpCompletionOption у HttpClient
        var response = await client.PostAsJsonAsync("/api/chat/stream", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        // FakeChatService отдаёт три токена + [DONE]
        Assert.Contains("data: \"Hello\"", body);
        Assert.Contains("data: \", \"", body);
        Assert.Contains("data: \"world!\"", body);
        Assert.Contains("data: [DONE]", body);

        // И сам запрос долетел до сервиса
        Assert.Single(_factory.ChatService.ReceivedRequests);
        Assert.Equal("Привет", _factory.ChatService.ReceivedRequests[0].Message);
    }
}
