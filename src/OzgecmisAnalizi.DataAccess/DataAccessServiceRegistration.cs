using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OzgecmisAnalizi.DataAccess.Context;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.DataAccess.Seed;

namespace OzgecmisAnalizi.DataAccess;

public static class DataAccessServiceRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Default") ?? DesignTimeDbContextFactory.DefaultConnection;

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connection, sql =>
            {
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
            }));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DbSeeder>();
        return services;
    }

    /// <summary>Bekleyen migration'ları uygular ve seed verisini yükler.</summary>
    public static async Task MigrateAndSeedAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Uygulanacak migration'lar: {Migrations}", string.Join(", ", pending));
        }

        await db.Database.MigrateAsync(ct);
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(ct);
    }
}
