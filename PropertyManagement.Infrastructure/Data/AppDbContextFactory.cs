using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PropertyManagement.Infrastructure.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the context (and generate/apply
/// migrations) without booting the Web host. The connection string comes from the
/// <c>ConnectionStrings__DefaultConnection</c> environment variable when present; the
/// fallback is a local trusted-connection default for developer convenience. No secret
/// is embedded — production/remote credentials are supplied via env or user-secrets.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=PropertyManagement;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
