using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Infrastructure.Data;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

public class AppDbContextModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Model_BuildsWithoutErrors()
    {
        using var context = CreateContext();

        var act = () => context.Model.GetEntityTypes().ToList();

        act.Should().NotThrow();
    }

    [Fact]
    public void Database_CanBeCreated()
    {
        using var context = CreateContext();

        var created = context.Database.EnsureCreated();

        created.Should().BeTrue();
    }
}
