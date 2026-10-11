using ECommerce.Order.Domain;
using Microsoft.EntityFrameworkCore;
namespace ECommerce.Order.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseOrder> Orders => Set<PurchaseOrder>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var order = model.Entity<PurchaseOrder>();
        order.ToTable("orders"); order.HasKey(x => x.Id); order.Property(x => x.Id).ValueGeneratedNever();
        order.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        order.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        order.Property(x => x.Total).HasPrecision(18, 2);
        order.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        order.Property(x => x.UpdatedAt).IsConcurrencyToken();
        order.Property(x => x.CancellationReason).HasMaxLength(500);
        order.Property(x => x.Carrier).HasMaxLength(100);
        order.Property(x => x.TrackingNumber).HasMaxLength(150);
        order.HasIndex(x => new { x.CustomerId, x.CreatedAt }); order.HasIndex(x => x.Status);
        order.OwnsOne(x => x.ShippingAddress, address =>
        {
            address.Property(x => x.Recipient).HasMaxLength(150).IsRequired();
            address.Property(x => x.Line1).HasMaxLength(200).IsRequired();
            address.Property(x => x.Line2).HasMaxLength(200);
            address.Property(x => x.City).HasMaxLength(100).IsRequired();
            address.Property(x => x.Region).HasMaxLength(100);
            address.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
            address.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        });
        order.Navigation(x => x.ShippingAddress).IsRequired();
        order.OwnsMany(x => x.Items, line =>
        {
            line.ToTable("order_items"); line.WithOwner().HasForeignKey("OrderId");
            line.HasKey("OrderId", nameof(OrderLine.ProductId));
            line.Property(x => x.ProductId).ValueGeneratedNever();
            line.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            line.Property(x => x.UnitPrice).HasPrecision(18, 2); line.Ignore(x => x.LineTotal);
        });
        order.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
