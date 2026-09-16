using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Data.Configurations;

public class ApplicationApplicantConfiguration : IEntityTypeConfiguration<ApplicationApplicant>
{
    public void Configure(EntityTypeBuilder<ApplicationApplicant> builder)
    {
        builder.Property(a => a.UserId).IsRequired().HasMaxLength(450);

        builder.HasIndex(a => new { a.ApplicationId, a.UserId }).IsUnique();
        builder.HasIndex(a => a.UserId);

        builder.HasOne(a => a.Application)
            .WithMany(app => app.Applicants)
            .HasForeignKey(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to AspNetUsers (no nav property, to keep Domain free of Identity).
        // Restrict, not Cascade: this table already cascades from Application, and a
        // second cascade path from the user would be rejected by SQL Server.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
