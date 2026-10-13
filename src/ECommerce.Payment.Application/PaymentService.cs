using ECommerce.Payment.Domain;
namespace ECommerce.Payment.Application;
public sealed class PaymentService(IPaymentStore store, IOrderClient orders, IPaymentProvider provider)
{
    public async Task<PaymentResponse> PayAsync(Guid customer, PayCommand command, CancellationToken ct)
    {
        if (command.RequestId == Guid.Empty || command.OrderId == Guid.Empty) throw new ServiceException(400, "Request ID and order ID are required.");
        var previous = await store.FindAsync(command.RequestId, ct);
        if (previous is not null)
        {
            if (previous.CustomerId != customer || previous.OrderId != command.OrderId || previous.MethodToken != command.MethodToken)
                throw new ServiceException(409, "Payment request ID was used for different content.");
            return await ProcessAsync(previous.Id, ct);
        }
        var order = await orders.GetAsync(command.OrderId, ct);
        if (order.CustomerId != customer) throw new ServiceException(404, "Order not found.");
        if (order.Status != "Reserved") throw new ServiceException(409, "Only an order with reserved inventory can be paid.");
        var payment = PaymentRecord.Create(command.RequestId, order.Id, customer, order.Total, order.Currency, command.MethodToken);
        await using (var tx = await store.BeginAsync(order.Id, ct))
        {
            previous = await store.FindAsync(command.RequestId, ct);
            if (previous is not null)
            {
                if (previous.CustomerId != customer || previous.OrderId != command.OrderId || previous.MethodToken != command.MethodToken)
                    throw new ServiceException(409, "Payment request ID was used for different content.");
            }
            else
            {
                if (await store.FindActiveByOrderAsync(order.Id, ct) is not null) throw new ServiceException(409, "This order already has an active payment. Retry its original request.");
                store.Add(payment); await store.SaveAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        return await ProcessAsync(command.RequestId, ct);
    }
    public async Task<PaymentResponse> GetAsync(Guid id, Guid customer, bool admin, CancellationToken ct)
    {
        var payment = await Required(id, ct);
        if (!admin && payment.CustomerId != customer) throw new ServiceException(404, "Payment not found.");
        return PaymentResponse.From(payment);
    }
    public Task<PagedResult<PaymentResponse>> ListAsync(Guid? customer, int page, int size, CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue) throw new ServiceException(400, "Invalid pagination.");
        return store.ListAsync(customer, page, size, ct);
    }
    public async Task<PaymentResponse> ProcessAsync(Guid id, CancellationToken ct)
    {
        var known = await Required(id, ct);
        await using (var tx = await store.BeginAsync(known.OrderId, ct))
        {
            var payment = await Required(id, ct);
            if (payment.Status == PaymentStatus.Pending)
            {
                var order = await orders.GetAsync(payment.OrderId, ct);
                if (order.Status is "Cancelling" or "Cancelled")
                { payment.Fail("Order was cancelled before payment."); await store.SaveAsync(ct); }
                else
                {
                    if (order.Status != "Reserved") throw new ServiceException(409, "Order is not ready for payment.");
                    var result = await provider.ChargeAsync(id, payment.Amount, payment.Currency, payment.MethodToken, ct);
                    if (result.Succeeded) payment.Succeed(result.Reference!);
                    else payment.Fail(result.Failure ?? "Payment was declined.");
                    await store.SaveAsync(ct);
                }
            }
            await tx.CommitAsync(ct);
        }
        known = await Required(id, ct);
        if (known.Status == PaymentStatus.Refunding) await RefundOrderAsync(known.OrderId, ct);
        else if (known.Status == PaymentStatus.Succeeded && !known.OrderNotified)
        {
            try { await NotifyAsync(id, ct); }
            catch (ServiceException error) when (error.StatusCode == 503) { /* receipt remains durable; background worker retries */ }
        }
        return PaymentResponse.From(await Required(id, ct));
    }
    public async Task<RefundSnapshot> RefundOrderAsync(Guid orderId, CancellationToken ct)
    {
        await using (var tx = await store.BeginAsync(orderId, ct))
        {
            var payment = await store.FindActiveByOrderAsync(orderId, ct) ?? throw new ServiceException(404, "Successful payment not found.");
            if (payment.Status == PaymentStatus.Pending) throw new ServiceException(409, "Payment outcome is not known yet.");
            if (payment.Status == PaymentStatus.Succeeded) { payment.BeginRefund(); await store.SaveAsync(ct); }
            await tx.CommitAsync(ct);
        }
        await using (var tx = await store.BeginAsync(orderId, ct))
        {
            var payment = await store.FindActiveByOrderAsync(orderId, ct) ?? throw new ServiceException(404, "Payment not found.");
            if (payment.Status == PaymentStatus.Refunding)
            {
                await provider.RefundAsync(payment.Id, payment.ProviderReference!, payment.Amount, payment.Currency, ct);
                payment.Refund(); await store.SaveAsync(ct);
            }
            await tx.CommitAsync(ct); return new(payment.Id, payment.Status.ToString());
        }
    }
    public async Task RecoverAsync(Guid id, CancellationToken ct)
    {
        var known = await Required(id, ct);
        await using (var tx = await store.BeginAsync(known.OrderId, ct))
        { var current = await Required(id, ct); current.RecordRecoveryAttempt(); await store.SaveAsync(ct); await tx.CommitAsync(ct); }
        await ProcessAsync(id, ct);
    }
    private async Task NotifyAsync(Guid id, CancellationToken ct)
    {
        // No payment transaction is held while calling Order: cancellation may call us back for a refund.
        var payment = await Required(id, ct);
        var order = await orders.GetAsync(payment.OrderId, ct);
        if (order.Status is "Cancelled" or "Cancelling") { await RefundOrderAsync(payment.OrderId, ct); return; }
        try { await orders.RecordPaidAsync(payment.OrderId, payment.Id, payment.Amount, payment.Currency, ct); }
        catch (ServiceException error) when (error.StatusCode == 409)
        {
            order = await orders.GetAsync(payment.OrderId, ct);
            if (order.Status is "Cancelled" or "Cancelling") { await RefundOrderAsync(payment.OrderId, ct); return; }
            throw new ServiceException(503, "Order confirmation is pending recovery.");
        }
        await using var tx = await store.BeginAsync(payment.OrderId, ct);
        var current = await Required(id, ct);
        if (current.Status == PaymentStatus.Succeeded) { current.MarkNotified(); await store.SaveAsync(ct); }
        await tx.CommitAsync(ct);
    }
    private async Task<PaymentRecord> Required(Guid id, CancellationToken ct) =>
        await store.FindAsync(id, ct) ?? throw new ServiceException(404, "Payment not found.");
}
