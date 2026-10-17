using ECommerce.Payment.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Payment.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var payment = model.Entity<PaymentRecord>();

        payment.ToTable("payments");

        payment.HasKey(x => x.Id);

        payment.Property(x => x.Id).ValueGeneratedNever();

        payment.Property(x => x.Amount).HasPrecision(18, 2);

        payment.Property(x => x.Currency).HasMaxLength(3);

        payment.Property(x => x.MethodToken).HasMaxLength(100);

        payment.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        payment.Property(x => x.ProviderReference).HasMaxLength(200);

        payment.Property(x => x.FailureReason).HasMaxLength(500);

        payment.Property(x => x.UpdatedAt).IsConcurrencyToken();

        payment.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"Status\" <> 'Failed'");

        payment.HasIndex(x => new { x.CustomerId, x.CreatedAt });

    }

}
