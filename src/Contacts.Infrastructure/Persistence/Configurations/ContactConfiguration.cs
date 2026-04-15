using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Contacts.Domain.Common;
using Contacts.Domain.Entities;
using Contacts.Domain.ValueObjects;

namespace Contacts.Infrastructure.Persistence.Configurations;

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        builder.HasKey(c => c.Id);

        // Map strongly-typed ContactId to a Guid column
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => ContactId.From(value));

        builder.Property(c => c.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        // Map Email value object; store only the string value
        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired()
            .HasConversion(
                email => email.Value,
                value => Email.From(value));

        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Map PhoneNumber value object (nullable)
        builder.Property(c => c.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20)
            .HasConversion(
                phone => phone != null ? phone.Value : null,
                value => value != null ? PhoneNumber.From(value) : null);

        builder.Property(c => c.Organisation)
            .HasColumnName("organisation")
            .HasMaxLength(200);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // Global query filter: soft-deleted contacts are invisible to the repository
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
