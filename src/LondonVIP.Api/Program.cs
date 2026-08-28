using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Pricing;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class Program
{
    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }

    public static WebApplication CreateApp(string[] args, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = ResolveContentRoot("LondonVIP.Api"),
            EnvironmentName = ResolveEnvironment(args)
        });

        builder.Services.AddOpenApi();
        builder.Services.AddDbContext<LondonVIPDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.")));
        builder.Services.AddScoped<IPricingService, PricingService>();
        builder.Services.AddSingleton<ICompanyContext, DefaultCompanyContext>();
        configureServices?.Invoke(builder.Services);

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

        app.MapCompanySetupEndpoints();
        app.MapBookingEndpoints();

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

    private static string ResolveContentRoot(string projectName)
    {
        foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath); directory is not null; directory = directory.Parent)
            {
                var workspaceProject = Path.Combine(directory.FullName, "src", projectName);
                if (File.Exists(Path.Combine(workspaceProject, $"{projectName}.csproj"))) return workspaceProject;
                if (File.Exists(Path.Combine(directory.FullName, $"{projectName}.csproj"))) return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? ResolveEnvironment(string[] args)
    {
        var index = Array.FindIndex(args, argument => argument is "--environment" or "-e");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
