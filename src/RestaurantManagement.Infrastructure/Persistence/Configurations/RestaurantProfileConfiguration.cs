using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Infrastructure.Persistence.Configurations;

public class RestaurantProfileConfiguration : IEntityTypeConfiguration<RestaurantProfile>
{
    public void Configure(EntityTypeBuilder<RestaurantProfile> builder)
    {
        builder.ToTable("RestaurantProfiles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RestaurantName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Address)
            .HasMaxLength(500);

        builder.Property(r => r.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(r => r.CurrencyCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(r => r.GstOrTaxNumber)
            .HasMaxLength(50);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired(false);
    }
}
