using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PaymentGateway.Api.Infrastructure;

internal static class HostingExtensions
{
    public static IServiceCollection AddGatewayObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(Constants.Telemetry.ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource(Constants.Telemetry.ServiceName)
                    .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(Constants.Hosting.HealthPathPrefix))
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(configuration[Constants.Telemetry.OtlpEndpointConfigurationKey]))
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(Constants.Telemetry.ServiceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(configuration[Constants.Telemetry.OtlpEndpointConfigurationKey]))
                    metrics.AddOtlpExporter();
            });
        return services;
    }

    public static IServiceCollection AddTrustedProxyHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Keep the framework's loopback defaults. Add only explicitly configured proxy networks.
            foreach (var network in configuration.GetSection(Constants.Hosting.KnownProxyNetworksConfigurationKey).Get<string[]>() ?? [])
            {
                var parts = network.Split('/');
                if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) ||
                    !int.TryParse(parts[1], out var prefix) || prefix < 0 ||
                    prefix > (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
                    throw new InvalidOperationException($"{Constants.Hosting.KnownProxyNetworksConfigurationKey} must contain valid CIDR networks.");
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefix));
            }
        });
        return services;
    }
}