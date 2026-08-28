using LondonVIP.Web.Components;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using LondonVIP.Web.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace LondonVIP.Web;

public static class WebProgram
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
            ApplicationName = typeof(WebProgram).Assembly.GetName().Name,
            ContentRootPath = ResolveContentRoot("LondonVIP.Web"),
            EnvironmentName = ResolveEnvironment(args)
        });

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddDataProtection().SetApplicationName("LondonVIPCars")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "LondonVIPCars-DataProtectionKeys")));
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = ".LondonVIP.Erp.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = "/erp/login";
            options.AccessDeniedPath = "/erp/access-denied";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
        });
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CookieForwardingHandler>();
        builder.Services.AddHttpClient("LondonVIP.Api", client =>
            client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5058/"))
            .AddHttpMessageHandler<CookieForwardingHandler>();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
                return Task.CompletedTask;
            });
            await next();
        });

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapPost("/erp/session/login", async (HttpContext context, IHttpClientFactory clients, IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync();
            var response = await clients.CreateClient("LondonVIP.Api").PostAsJsonAsync("api/auth/login", new LoginRequest { Email = form["email"].ToString(), Password = form["password"].ToString(), RememberMe = form["rememberMe"] == "on" });
            if (!response.IsSuccessStatusCode) return Results.Redirect("/erp/login?failed=true");
            foreach (var cookie in response.Headers.GetValues("Set-Cookie")) context.Response.Headers.Append("Set-Cookie", cookie);
            return Results.Redirect("/erp");
        }).AllowAnonymous();
        app.MapPost("/erp/session/logout", async (HttpContext context, IHttpClientFactory clients, IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            using var response = await clients.CreateClient("LondonVIP.Api").PostAsync("api/auth/logout", null);
            context.Response.Cookies.Delete(".LondonVIP.Erp.Auth");
            return Results.Redirect("/erp/login");
        }).RequireAuthorization();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

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
