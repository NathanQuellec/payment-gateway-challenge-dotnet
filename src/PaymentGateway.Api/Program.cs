using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Data.Repositories;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.impl;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // Do not echo model-binding errors that can contain submitted card data.
        var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(entry => entry.Key, _ => new[] { "A required value is missing or invalid." });
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = Constants.Payment.RejectedProblemTitle
        };
        problem.Extensions[Constants.Payment.StatusExtensionKey] = nameof(PaymentStatus.Rejected);
        return new BadRequestObjectResult(problem);
    };
});
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

public partial class Program;

