using ECommerce.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Inventory.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> Stocks => Set<StockItem>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<StockAdjustment> Adjustments => Set<StockAdjustment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var stock = model.Entity<StockItem>();

        stock.ToTable(
            "stock_items",
            t => t.HasCheckConstraint("CK_stock_balance", "\"OnHand\" >= \"Reserved\" AND \"Reserved\" >= 0"));

        stock.HasKey(x => x.ProductId);

        stock.Property(x => x.ProductId).ValueGeneratedNever();

        stock.Ignore(x => x.Available);

        stock.Property(x => x.UpdatedAt).IsConcurrencyToken();

        var reservation = model.Entity<Reservation>();

        reservation.ToTable("reservations");

        reservation.HasKey(x => x.OrderId);

        reservation.Property(x => x.OrderId).ValueGeneratedNever();

        reservation.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        reservation.HasIndex(x => x.Status);

        reservation.OwnsMany(x => x.Items, line =>
        {
            line.ToTable("reservation_items", t => t.HasCheckConstraint("CK_reservation_quantity", "\"Quantity\" > 0"));

            line.WithOwner().HasForeignKey("OrderId");

            line.HasKey("OrderId", nameof(ReservationLine.ProductId));

            line.Property(x => x.ProductId).ValueGeneratedNever();

            line.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        });

        reservation.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);

        var adjustment = model.Entity<StockAdjustment>();

        adjustment.ToTable("stock_adjustments");

        adjustment.HasKey(x => x.RequestId);

        adjustment.Property(x => x.RequestId).ValueGeneratedNever();

        adjustment.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        adjustment.HasIndex(x => new { x.ProductId, x.CreatedAt });

        adjustment.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

    }

}
