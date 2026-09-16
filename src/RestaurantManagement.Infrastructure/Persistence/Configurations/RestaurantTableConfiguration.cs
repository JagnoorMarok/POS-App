using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Infrastructure.Persistence.Configurations;

public class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
{
    public void Configure(EntityTypeBuilder<RestaurantTable> builder)
    {
        builder.ToTable("RestaurantTables");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TableNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => t.TableNumber)
            .IsUnique();

        builder.Property(t => t.Capacity)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.DisplayOrder)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(t => t.IsActive);
        builder.HasIndex(t => t.DisplayOrder);
    }
}
