using ECommerce.Cart.Domain;
using Microsoft.EntityFrameworkCore;
namespace ECommerce.Cart.Infrastructure;
public sealed class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options)
{
    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();
    public DbSet<CheckoutAttempt> Checkouts => Set<CheckoutAttempt>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var cart = model.Entity<ShoppingCart>(); cart.ToTable("carts"); cart.HasKey(x => x.CustomerId);
        cart.Property(x => x.CustomerId).ValueGeneratedNever(); cart.Property(x => x.UpdatedAt).IsConcurrencyToken();
        cart.OwnsMany(x => x.Items, line => { line.ToTable("cart_items"); line.WithOwner().HasForeignKey("CustomerId");
            line.HasKey("CustomerId", nameof(CartLine.ProductId)); line.Property(x => x.ProductId).ValueGeneratedNever(); });
        cart.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        var attempt = model.Entity<CheckoutAttempt>(); attempt.ToTable("checkout_attempts"); attempt.HasKey(x => x.RequestId);
        attempt.Property(x => x.RequestId).ValueGeneratedNever(); attempt.Property(x => x.AddressHash).HasMaxLength(64);
        attempt.Property(x => x.AddressJson).HasColumnType("text"); attempt.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        attempt.HasIndex(x => x.CustomerId);
        attempt.OwnsMany(x => x.Items, line => { line.ToTable("checkout_items"); line.WithOwner().HasForeignKey("RequestId");
            line.HasKey("RequestId", nameof(CartLine.ProductId)); line.Property(x => x.ProductId).ValueGeneratedNever(); });
        attempt.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
