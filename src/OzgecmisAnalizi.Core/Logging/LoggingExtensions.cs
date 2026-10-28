using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace OzgecmisAnalizi.Core.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Serilog'u appsettings "Serilog" bölümünden okur; bölüm yoksa konsol + günlük dosya varsayılanlarını kullanır.
    /// </summary>
    public static WebApplicationBuilder AddCoreLogging(this WebApplicationBuilder builder, string applicationName)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", applicationName);

            if (!context.Configuration.GetSection("Serilog").Exists())
            {
                configuration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine(AppContext.BaseDirectory, "logs", $"{applicationName}-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14);
            }
        });

        return builder;
    }

    /// <summary>HTTP istek loglaması (yol, durum kodu, süre).</summary>
    public static WebApplication UseCoreRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0} ms)";
        });
        return app;
    }
}
