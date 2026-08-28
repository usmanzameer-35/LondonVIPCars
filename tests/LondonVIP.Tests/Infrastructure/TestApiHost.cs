using LondonVIP.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;

namespace LondonVIP.Tests.Infrastructure;

internal sealed class TestApiHost : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private TestApiHost(WebApplication app, SqliteConnection connection, HttpClient client)
    {
        App = app;
        this.connection = connection;
        Client = client;
    }

    public WebApplication App { get; }
    public HttpClient Client { get; }

    public static async Task<TestApiHost> StartAsync(Action<IServiceCollection>? configureServices = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var app = LondonVIP.Api.Program.CreateApp(
            ["--environment", "Development"],
            services =>
            {
                services.RemoveAll<LondonVIPDbContext>();
                services.RemoveAll<DbContextOptions<LondonVIPDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<LondonVIPDbContext>>();
                services.AddDbContext<LondonVIPDbContext>(options => options.UseSqlite(connection));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
                configureServices?.Invoke(services);
            });

        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };
            return new TestApiHost(app, connection, client);
        }
        catch
        {
            await app.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await connection.DisposeAsync();
    }
}
