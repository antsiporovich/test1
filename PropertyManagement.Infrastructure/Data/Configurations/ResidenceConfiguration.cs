using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Data.Configurations;

public class ResidenceConfiguration : IEntityTypeConfiguration<Residence>
{
    public void Configure(EntityTypeBuilder<Residence> builder)
    {
        builder.Property(r => r.AddressLine1).IsRequired().HasMaxLength(200);
        builder.Property(r => r.AddressLine2).HasMaxLength(200);
        builder.Property(r => r.City).IsRequired().HasMaxLength(100);
        builder.Property(r => r.State).IsRequired().HasMaxLength(50);
        builder.Property(r => r.ZipCode).IsRequired().HasMaxLength(20);
        builder.Property(r => r.LandlordName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.LandlordPhone).IsRequired().HasMaxLength(30);

        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.ApplicationId);

        builder.HasOne(r => r.Application)
            .WithMany(a => a.Residences)
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
