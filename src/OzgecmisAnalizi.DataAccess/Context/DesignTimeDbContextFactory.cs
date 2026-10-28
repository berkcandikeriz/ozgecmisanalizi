using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OzgecmisAnalizi.DataAccess.Context;

/// <summary>
/// "dotnet ef migrations add ..." komutları için. Bağlantı dizesi OZGECMIS_DB ortam değişkeninden
/// okunur; yoksa LocalDB kullanılır. Migration üretmek için veritabanının var olması gerekmez.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string DefaultConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=OzgecmisAnaliziDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OZGECMIS_DB") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }
}
