namespace Poc.Exchange.Callback.Configuration;

public static class ConfigurationSetup
{
    /// <summary>
    /// Local: load secrets.json when present (project folder or repo root).
    /// Azure / CI: no file, so Graph__* environment variables / App Settings are used.
    /// Environment variables are always applied last so hosted settings win.
    /// </summary>
    public static void AddPocConfiguration(this WebApplicationBuilder builder)
    {
        foreach (var path in EnumerateSecretsPaths(builder.Environment.ContentRootPath))
        {
            if (File.Exists(path))
            {
                builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: true);
                break;
            }
        }

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<Program>(optional: true);
        }

        builder.Configuration.AddEnvironmentVariables();
    }

    private static IEnumerable<string> EnumerateSecretsPaths(string contentRoot)
    {
        yield return Path.Combine(contentRoot, "secrets.json");

        var parent = Directory.GetParent(contentRoot)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            yield return Path.Combine(parent, "secrets.json");
        }

        yield return Path.Combine(Directory.GetCurrentDirectory(), "secrets.json");
    }
}
