using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        builder.Property(o => o.RestaurantTableId)
            .IsRequired(false);

        builder.Property(o => o.Status)
            .IsRequired();

        builder.Property(o => o.OrderType)
            .IsRequired();

        builder.Property(o => o.Subtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.DiscountAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.TaxAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasMaxLength(1000);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .IsRequired(false);

        builder.Property(o => o.CompletedAt)
            .IsRequired(false);

        // Indexes for querying
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.OrderType);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.RestaurantTableId);

        // Table relationship
        builder.HasOne(o => o.RestaurantTable)
            .WithMany()
            .HasForeignKey(o => o.RestaurantTableId)
            .OnDelete(DeleteBehavior.Restrict);

        // Items relationship (Cascade delete items when order is deleted)
        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Payments relationship (Restrict delete if payments exist)
        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
