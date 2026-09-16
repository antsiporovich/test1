using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Data.Configurations;

public class ApplicantInfoConfiguration : IEntityTypeConfiguration<ApplicantInfo>
{
    public void Configure(EntityTypeBuilder<ApplicantInfo> builder)
    {
        builder.HasKey(a => a.ApplicationId);

        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Phone).IsRequired().HasMaxLength(30);
        builder.Property(a => a.Email).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AddressLine1).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AddressLine2).HasMaxLength(200);
        builder.Property(a => a.City).IsRequired().HasMaxLength(100);
        builder.Property(a => a.State).IsRequired().HasMaxLength(50);
        builder.Property(a => a.ZipCode).IsRequired().HasMaxLength(20);

        builder.Property(a => a.RowVersion).IsRowVersion();

        // 1:1 with Application; PK doubles as FK so this row lives/dies with its parent.
        builder.HasOne(a => a.Application)
            .WithOne(app => app.ApplicantInfo)
            .HasForeignKey<ApplicantInfo>(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
