using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Data.Configurations;

public class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        // One lease per application (approval issues exactly one, never a second).
        builder.HasIndex(l => l.ApplicationId).IsUnique();

        // Shared availability predicate (Features/09, LEASE-2) filters on UnitId + date range.
        builder.HasIndex(l => new { l.UnitId, l.StartDate, l.EndDate });

        builder.HasOne(l => l.Unit)
            .WithMany(u => u.Leases)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Application)
            .WithOne(a => a.Lease)
            .HasForeignKey<Lease>(l => l.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
