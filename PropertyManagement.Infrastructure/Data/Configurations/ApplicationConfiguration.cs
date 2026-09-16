using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ClaimedByUserId).HasMaxLength(450); // matches Identity's Id column length

        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.UnitId);

        builder.HasOne(a => a.Unit)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional FK to the claiming PM in AspNetUsers (nullable column ⇒ optional
        // relationship); Restrict so a claimed application never cascade-deletes a user.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.ClaimedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
