using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Repositories;
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
            Status = 400,
            Title = "The payment request was rejected."
        };
        problem.Extensions["paymentStatus"] = "Rejected";
        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IPaymentValidationService, PaymentValidationService>();
builder.Services.AddScoped<IPaymentsService, PaymentsService>();
builder.Services.AddHttpClient<IBankClient, BankClientService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Bank:BaseUrl"] ?? "http://localhost:8080/");
    client.Timeout = TimeSpan.FromSeconds(10);
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

