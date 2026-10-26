using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api;
using PaymentGateway.Api.Data.Repositories;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.RateLimiting;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.Impl;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Logging.Configure(options => options.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
// The exception handler emits a sanitized event; suppress the framework's raw exception log.
builder.Logging.AddFilter(Constants.Telemetry.ExceptionMiddlewareLogCategory, LogLevel.None);
builder.Services.AddGatewayObservability(builder.Configuration);
builder.Services.AddTrustedProxyHeaders(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions[Constants.Telemetry.TraceIdExtensionKey] = ApiProblems.TraceId(context.HttpContext));

builder.Services.AddOptions<PaymentRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(Constants.RateLimiting.ConfigurationSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string, PaymentRateLimitPolicy>(Constants.RateLimiting.PolicyName);
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = ApiProblems.InvalidModelState);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile), includeControllerXmlComments: true);
});
builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IPaymentValidationService, PaymentValidationService>();
builder.Services.AddScoped<IPaymentsService, PaymentsService>();
builder.Services.AddHttpClient<IBankClient, BankClientService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration[Constants.Bank.BaseUrlConfigurationKey] ?? Constants.Bank.DefaultBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(Constants.Bank.TimeoutSeconds);
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    var traceId = ApiProblems.TraceId(context);
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[Constants.Telemetry.TraceIdHeaderName] = traceId;
        return Task.CompletedTask;
    });
    using var scope = app.Logger.BeginScope(new Dictionary<string, object> { [Constants.Telemetry.TraceIdLogProperty] = traceId });
    await next(context);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (builder.Configuration.GetValue(Constants.Hosting.UseHttpsRedirectionConfigurationKey, true))
    app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks(Constants.Hosting.LivenessPath).DisableRateLimiting();
app.MapHealthChecks(Constants.Hosting.ReadinessPath).DisableRateLimiting();
app.Run();

public partial class Program;