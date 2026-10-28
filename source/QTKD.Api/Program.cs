using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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
