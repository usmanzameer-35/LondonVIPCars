using LondonVIP.Web.Components;

namespace LondonVIP.Web;

public static class WebProgram
{
    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }

    public static WebApplication CreateApp(string[] args)
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
        builder.Services.AddHttpClient("LondonVIP.Api", client =>
            client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5058/"));

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();
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
