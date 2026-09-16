using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Data.Configurations;

public class ApplicationNoteConfiguration : IEntityTypeConfiguration<ApplicationNote>
{
    public void Configure(EntityTypeBuilder<ApplicationNote> builder)
    {
        builder.Property(n => n.AuthorUserId).IsRequired().HasMaxLength(450);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(4000);

        builder.HasIndex(n => n.ApplicationId);

        builder.HasOne(n => n.Application)
            .WithMany(a => a.Notes)
            .HasForeignKey(n => n.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit FK to the author in AspNetUsers; Restrict (Application already cascades here).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
