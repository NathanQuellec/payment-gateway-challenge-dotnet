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
                        !context.Request.Path.StartsWithSegments(Constants.Hosting.HealthPath))
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

}
