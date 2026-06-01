using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ChatBot.Core;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Интеграционные тесты <c>POST /api/documents/upload</c>.
/// </summary>
public class DocumentsEndpointTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public DocumentsEndpointTests(TestWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var emptyFile = new ByteArrayContent(Array.Empty<byte>());
        emptyFile.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(emptyFile, "file", "empty.pdf");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsIngestionResult()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("fake-pdf-bytes");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "resume.pdf");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IngestionResult>();
        Assert.NotNull(result);
        Assert.Equal("resume.pdf", result!.DocumentName);
        Assert.Equal(7, result.ChunksIndexed);
    }
}
