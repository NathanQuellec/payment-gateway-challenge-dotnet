using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.TestSupport;

internal sealed class BankStubServer(WebApplication app) : IAsyncDisposable
{
    public Uri BaseAddress { get; private set; } = null!;
    public ConcurrentQueue<JsonElement> Requests { get; } = new();

    public static async Task<BankStubServer> StartAsync(RequestDelegate respond)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        var bank = new BankStubServer(app);
        app.MapPost("/payments", async context =>
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            bank.Requests.Enqueue(body.RootElement.Clone());
            await respond(context);
        });

        try
        {
            await app.StartAsync();
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            bank.BaseAddress = new Uri(Assert.Single(addresses.Addresses));
            return bank;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}