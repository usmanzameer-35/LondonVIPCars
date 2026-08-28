using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Pricing;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class Program
{
    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }

    public static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddDbContext<LondonVIPDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.")));
        builder.Services.AddScoped<IPricingService, PricingService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.MapGet("/api/status", () => new
        {
            service = "London VIP Cars",
            status = "online"
        });

        app.MapPost("/api/quotes", async (
            QuoteRequest request,
            IPricingService pricingService,
            CancellationToken cancellationToken) =>
        {
            var quote = await pricingService.CalculateQuoteAsync(request, cancellationToken);
            return Results.Ok(quote);
        });

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", () =>
        {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast");

        return app;
    }
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
