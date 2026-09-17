using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Tests.Web;

/// <summary>
/// Boots the real MVC app against an isolated InMemory database (no SQL Server /
/// MigrateAsync). Uses <see cref="TestAuthHandler"/> so tests authenticate via headers.
/// InMemory lives only on this test project — ConfigureTestServices swaps the provider.
/// </summary>
public sealed class PropertyManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"pm-web-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContextRegistrations(services);
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.PostConfigure<MvcOptions>(options =>
                options.Filters.Add(new IgnoreAntiforgeryTokenAttribute()));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultForbidScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, _ => { });

            services.AddSingleton<Microsoft.AspNetCore.Antiforgery.IAntiforgery, TestAntiforgery>();
        });
    }

    /// <summary>
    /// Strip Program's SqlServer DbContextOptions so UseInMemoryDatabase is the only provider.
    /// </summary>
    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(AppDbContext)
                || d.ServiceType == typeof(DbContextOptions)
                || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)
                    && d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext))
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                    && d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)))
            .ToList();

        foreach (var descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }

    public async Task EnsureCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task<(ApplicationUser User, string Password)> SeedApplicantAsync(
        string email = "applicant@test.local",
        string password = "Demo#12345")
    {
        using var scope = Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync("Applicant"))
        {
            await roleManager.CreateAsync(new IdentityRole("Applicant"));
        }

        if (!await roleManager.RoleExistsAsync("PropertyManager"))
        {
            await roleManager.CreateAsync(new IdentityRole("PropertyManager"));
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return (existing, password);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test Applicant",
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, "Applicant");
        return (user, password);
    }

    public async Task<int> SeedApplicationAsync(string applicantUserId, ApplicationStatus status)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = new Property
        {
            Name = "Test Property",
            AddressLine1 = "1 Test St",
            City = "Town",
            State = "TS",
            ZipCode = "00000",
            IsActive = true,
        };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        var unit = new Unit
        {
            Property = property,
            UnitType = unitType,
            UnitNumber = "101",
            Bedrooms = 0,
            Bathrooms = 1,
            MonthlyRent = 1000m,
            IsActive = true,
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var app = new Application
        {
            UnitId = unit.Id,
            Status = status,
            CreatedAtUtc = now,
            ApplicantInfo = new ApplicantInfo
            {
                FullName = "Test Applicant",
                Phone = "555-0100",
                Email = "applicant@test.local",
                AddressLine1 = "1 Test St",
                City = "Town",
                State = "TS",
                ZipCode = "00000",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Employment = "Engineer",
                AnnualIncome = 60000m,
                DesiredMoveInDate = DateOnly.FromDateTime(now.UtcDateTime.AddMonths(1)),
                UpdatedAtUtc = now,
            },
        };
        app.Applicants.Add(new ApplicationApplicant { UserId = applicantUserId, AddedAtUtc = now });
        if (status is not ApplicationStatus.Draft)
        {
            app.ResidenceHistoryConfirmedAtUtc = now;
            app.SubmittedAtUtc = now;
        }

        db.Applications.Add(app);
        await db.SaveChangesAsync();
        return app.Id;
    }

    public HttpClient CreateAuthenticatedClient(ApplicationUser user, string role = "Applicant")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id);
        client.DefaultRequestHeaders.Add("X-Test-Email", user.Email ?? user.UserName ?? user.Id);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }
}
