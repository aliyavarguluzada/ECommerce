using ECommerce.Cart.Application;
using ECommerce.Cart.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Cart.Infrastructure;

public sealed class CartStore(CartDbContext db) : ICartStore
{
    public async Task<ICartTransaction> BeginAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var key = "cart:" + id;

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                ct);

            return new Transaction(tx);
        }

        catch
        {
            await tx.DisposeAsync();

            throw;

        }

    }

    public Task<ShoppingCart?> FindAsync(
        Guid id,
        CancellationToken ct) => db.Carts.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.CustomerId == id,
        ct);

    public Task<CheckoutAttempt?> FindCheckoutAsync(
        Guid id,
        CancellationToken ct) => db.Checkouts.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.RequestId == id,
        ct);

    public void Add(ShoppingCart cart) => db.Carts.Add(cart);

    public void Add(CheckoutAttempt attempt) => db.Checkouts.Add(attempt);

    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction tx) : ICartTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();

    }

}
