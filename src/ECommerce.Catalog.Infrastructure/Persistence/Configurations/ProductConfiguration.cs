using ECommerce.Catalog.Domain.Aggregates.Categories;
using ECommerce.Catalog.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(x => x.Id);

        builder.Ignore(x => x.DomainEvents);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

        builder.Property(x => x.Description).HasMaxLength(2000);

        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone").IsConcurrencyToken();

        builder.OwnsOne(x => x.Price, money =>
        {
            money.Property(x => x.Amount).HasColumnName("price").HasPrecision(18, 2).IsRequired();

            money.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();

        });

        builder.Navigation(x => x.Price).IsRequired();

        builder.HasIndex(x => x.CategoryId);

        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

    }

}
