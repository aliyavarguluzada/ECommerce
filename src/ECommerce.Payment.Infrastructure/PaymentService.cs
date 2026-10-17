using ECommerce.Payment.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ECommerce.Payment.Domain;

namespace ECommerce.Payment.Infrastructure;

public class PaymentService(PaymentDbContext db, IOrderClient orders, IPaymentProvider provider) : IPaymentService
{
    public async Task<PaymentResponse> PayAsync(Guid customer, PayRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.OrderId == Guid.Empty)
        {
            throw new ServiceException(400, "Request ID and order ID are required.");

        }

        var previous = await FindAsync(request.RequestId, ct);

        if (previous is not null)
        {
            if (previous.CustomerId != customer || previous.OrderId != request.OrderId || previous.MethodToken != request.MethodToken)
            {
                throw new ServiceException(409, "Payment request ID was used for different content.");

            }

            return await ProcessAsync(previous.Id, ct);
        }

        var order = await orders.GetAsync(request.OrderId, ct);

        if (order.CustomerId != customer)
        {
            throw new ServiceException(404, "Order not found.");

        }

        if (order.Status != "Reserved")
        {
            throw new ServiceException(409, "Only an order with reserved inventory can be paid.");

        }

        var payment = PaymentRecord.Create(
            request.RequestId,
            order.Id,
            customer,
            order.Total,
            order.Currency,
            request.MethodToken);

        await using (var tx = await BeginTransactionAsync(order.Id, ct))
        {
            previous = await FindAsync(request.RequestId, ct);

            if (previous is not null)
            {
                if (previous.CustomerId != customer || previous.OrderId != request.OrderId || previous.MethodToken != request.MethodToken)
                {
                    throw new ServiceException(409, "Payment request ID was used for different content.");

                }

            }

            else
            {
                if (await FindActiveByOrderAsync(order.Id, ct)is not null)
                {
                    throw new ServiceException(
                        409,
                        "This order already has an active payment. Retry its original request.");

                }

                Add(payment);

                await SaveChangesAsync(ct);

            }

            await tx.CommitAsync(ct);

        }

        return await ProcessAsync(request.RequestId, ct);
    }

    public async Task<PaymentResponse> GetAsync(Guid id, Guid customer, bool admin, CancellationToken ct)
    {
        var payment = await Required(id, ct);

        if (!admin && payment.CustomerId != customer)
        {
            throw new ServiceException(404, "Payment not found.");

        }

        return PaymentResponse.From(payment);
    }

    public Task<PagedResult<PaymentResponse>> ListAsync(Guid? customer, int page, int size, CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
        {
            throw new ServiceException(400, "Invalid pagination.");

        }

        return ReadListAsync(customer, page, size, ct);
    }

    public async Task<PaymentResponse> ProcessAsync(Guid id, CancellationToken ct)
    {
        var known = await Required(id, ct);

        await using (var tx = await BeginTransactionAsync(known.OrderId, ct))
        {
            var payment = await Required(id, ct);

            if (payment.Status == PaymentStatus.Pending)
            {
                var order = await orders.GetAsync(payment.OrderId, ct);

                if (order.Status is "Cancelling" or "Cancelled")
                {
                    payment.Fail("Order was cancelled before payment.");

                    await SaveChangesAsync(ct);

                }

                else
                {
                    if (order.Status != "Reserved")
                    {
                        throw new ServiceException(409, "Order is not ready for payment.");

                    }

                    var result = await provider.ChargeAsync(
                        id,
                        payment.Amount,
                        payment.Currency,
                        payment.MethodToken,
                        ct);

                    if (result.Succeeded)
                    {
                        payment.Succeed(result.Reference!);

                    }

                    else
                    {
                        payment.Fail(result.Failure ?? "Payment was declined.");

                    }

                    await SaveChangesAsync(ct);

                }

            }

            await tx.CommitAsync(ct);

        }

        known = await Required(id, ct);

        if (known.Status == PaymentStatus.Refunding)
        {
            await RefundOrderAsync(known.OrderId, ct);

        }

        else if (known.Status == PaymentStatus.Succeeded && !known.OrderNotified)
        {
            try
            {
                await NotifyAsync(id, ct);

            }

            catch (ServiceException error)when (error.StatusCode == 503)

            { /* receipt remains durable; background worker retries */

            }

        }

        return PaymentResponse.From(await Required(id, ct));
    }

    public async Task<RefundSnapshot> RefundOrderAsync(Guid orderId, CancellationToken ct)
    {
        await using (var tx = await BeginTransactionAsync(orderId, ct))
        {
            var payment = await FindActiveByOrderAsync(
                orderId,
                ct) ?? throw new ServiceException(
                404,
                "Successful payment not found.");

            if (payment.Status == PaymentStatus.Pending)
            {
                throw new ServiceException(409, "Payment outcome is not known yet.");

            }

            if (payment.Status == PaymentStatus.Succeeded)
            {
                payment.BeginRefund();

                await SaveChangesAsync(ct);

            }

            await tx.CommitAsync(ct);

        }

        await using (var tx = await BeginTransactionAsync(orderId, ct))
        {
            var payment = await FindActiveByOrderAsync(
                orderId,
                ct) ?? throw new ServiceException(
                404,
                "Payment not found.");

            if (payment.Status == PaymentStatus.Refunding)
            {
                await provider.RefundAsync(
                    payment.Id,
                    payment.ProviderReference!,
                    payment.Amount,
                    payment.Currency,
                    ct);

                payment.Refund();

                await SaveChangesAsync(ct);

            }

            await tx.CommitAsync(ct);

            return new(payment.Id, payment.Status.ToString());
        }

    }

    public async Task RecoverAsync(Guid id, CancellationToken ct)
    {
        var known = await Required(id, ct);

        await using (var tx = await BeginTransactionAsync(known.OrderId, ct))
        {
            var current = await Required(id, ct);

            current.RecordRecoveryAttempt();

            await SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

        }

        await ProcessAsync(id, ct);

    }

    private async Task NotifyAsync(Guid id, CancellationToken ct)
    {
        // No payment transaction is held while calling Order: cancellation may call us back for a refund.

        var payment = await Required(id, ct);

        var order = await orders.GetAsync(payment.OrderId, ct);

        if (order.Status is "Cancelled" or "Cancelling")
        {
            await RefundOrderAsync(payment.OrderId, ct);

            return;
        }

        try
        {
            await orders.RecordPaidAsync(payment.OrderId, payment.Id, payment.Amount, payment.Currency, ct);

        }

        catch (ServiceException error)when (error.StatusCode == 409)
        {
            order = await orders.GetAsync(payment.OrderId, ct);

            if (order.Status is "Cancelled" or "Cancelling")
            {
                await RefundOrderAsync(payment.OrderId, ct);

                return;
            }

            throw new ServiceException(503, "Order confirmation is pending recovery.");

        }

        await using var tx = await BeginTransactionAsync(payment.OrderId, ct);

        var current = await Required(id, ct);

        if (current.Status == PaymentStatus.Succeeded)
        {
            current.MarkNotified();

            await SaveChangesAsync(ct);

        }

        await tx.CommitAsync(ct);

    }

    private async Task<PaymentRecord> Required(
        Guid id,
        CancellationToken ct) => await FindAsync(
        id,
        ct) ?? throw new ServiceException(
        404,
        "Payment not found.");

    protected virtual async Task<IPaymentTransaction> BeginTransactionAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var key = "payment:" + id;

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

    protected virtual Task<PaymentRecord?> FindAsync(
        Guid id,
        CancellationToken ct) => db.Payments.SingleOrDefaultAsync(
        x => x.Id == id,
        ct);

    protected virtual Task<PaymentRecord?> FindActiveByOrderAsync(
        Guid id,
        CancellationToken ct) => db.Payments.SingleOrDefaultAsync(
        x => x.OrderId == id && x.Status != PaymentStatus.Failed,
        ct);

    protected virtual async Task<IReadOnlyList<Guid>> ReadRecoveryIdsAsync(
        int limit,
        CancellationToken ct) => await db.Payments.AsNoTracking().Where(
        x => x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Refunding || (x.Status == PaymentStatus.Succeeded && !x.OrderNotified)).OrderBy(
        x => x.LastRecoveryAttemptAt ?? x.CreatedAt).Take(limit).Select(
        x => x.Id).ToListAsync(ct);

    protected virtual async Task<PagedResult<PaymentResponse>> ReadListAsync(Guid? customer, int page, int size, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking();

        if (customer.HasValue)
        {
            query = query.Where(x => x.CustomerId == customer.Value);

        }

        var count = await query.CountAsync(ct);

        var items = await query.OrderByDescending(
            x => x.CreatedAt).ThenBy(
            x => x.Id).Skip(
            (page - 1) * size).Take(size).ToListAsync(ct);

        return new(items.Select(PaymentResponse.From).ToArray(), count, page, size);
    }

    protected virtual void Add(PaymentRecord payment) => db.Payments.Add(payment);

    protected virtual async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction tx) : IPaymentTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();

    }

    public Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(int limit, CancellationToken ct) => ReadRecoveryIdsAsync(limit, ct);
}
