using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Data;
using QTKD.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("Qtkd");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Qtkd' was not found. Configure it with .NET User Secrets for development.");
}

builder.Services.AddDbContext<QtkdDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<PricingService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
        // Malformed JSON / wrong value types use the same error shape as business errors.
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiErrorCodes.FromModelState(context.ModelState)));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Unexpected exceptions are logged by the middleware and returned without internal details.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ApiErrorResponse(
        ApiErrorCodes.InternalError,
        "An unexpected error occurred. Please try again or contact the administrator."));
}));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "QTKD API v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
