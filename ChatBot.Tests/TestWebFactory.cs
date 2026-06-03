using ChatBot.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChatBot.Tests;

/// <summary>
/// Test application factory: starts <c>Program</c> in-memory and replaces
/// external dependencies (LLM, ingestion) with fakes so tests are deterministic
/// and require no network access or OpenAI key.
///
/// .NET: equivalent of <c>TestServer</c> + <c>IConfigureWebHostBuilder</c> from classic ASP.NET Core.
/// </summary>
public sealed class TestWebFactory : WebApplicationFactory<Program>
{
    public FakeChatService ChatService { get; } = new();
    public FakeIngestionService IngestionService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Llm:ApiKey is validated at Program startup — provide a formally valid string;
        // the real IChatCompletionService is never called because IChatService is replaced below.
        builder.UseSetting("Llm:ApiKey", "test-key-not-used");
        builder.UseSetting("Llm:Provider", "OpenAI");
        builder.UseSetting("Llm:ModelId", "gpt-4o-mini");

        builder.ConfigureServices(services =>
        {
            // .NET: services.Replace(ServiceDescriptor.Scoped<...>) — but easier to remove and re-add
            services.RemoveAll<IChatService>();
            services.RemoveAll<IIngestionService>();
            services.AddSingleton<IChatService>(ChatService);
            services.AddSingleton<IIngestionService>(IngestionService);
        });

        builder.UseEnvironment("Development");
    }
}
