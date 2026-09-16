using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicantInfo> ApplicantInfos => Set<ApplicantInfo>();
    public DbSet<Residence> Residences => Set<Residence>();
    public DbSet<ApplicationApplicant> ApplicationApplicants => Set<ApplicationApplicant>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationNote> ApplicationNotes => Set<ApplicationNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQL Server's `rowversion` type auto-generates on insert/update; SQLite has no
            // equivalent, so IsRowVersion() columns would insert as NULL and violate NOT NULL.
            // Only used by SQLite-backed tests (efcore-code-first-sqlserver.md /
            // dotnet-unit-testing.md's transaction/concurrency-test guidance) — no effect on
            // the real SQL Server schema/migrations.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.FindProperty("RowVersion")?.SetDefaultValueSql("randomblob(8)");
            }
        }
    }
}
