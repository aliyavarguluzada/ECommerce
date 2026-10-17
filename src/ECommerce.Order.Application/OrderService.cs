using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Order.Domain;

namespace ECommerce.Order.Application;

public sealed class OrderService(
    IOrderStore store,
    ICatalogClient catalog,
    IInventoryClient inventory,
    IPaymentClient payments)
{
    public async Task<OrderResponse> CreateAsync(CreateOrderCommand command, CancellationToken ct)
    {
        if (command.RequestId == Guid.Empty || command.CustomerId == Guid.Empty || command.ShippingAddress is null || command.Items is null || command.Items.Count is < 1 or > 100 || command.Items.Any(
            x => x is null || x.ProductId == Guid.Empty || x.Quantity is < 1 or > 100000) || command.Items.Select(
            x => x.ProductId).Distinct().Count() != command.Items.Count)
        {
            throw new DomainException("Request ID, customer, valid address, and 1–100 distinct products with quantities 1–100000 are required.");

        }

        var address = command.ShippingAddress.ToDomain();

        var fingerprint = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new {
                            command.CustomerId,
                            Address = address,
                            Items = command.Items.OrderBy(x => x.ProductId).ToArray() }))));

        await using (var tx = await store.BeginAsync(command.RequestId, ct))
        {
            var existing = await store.FindAsync(command.RequestId, ct);

            if (existing is not null)
            {
                if (existing.RequestFingerprint != fingerprint)
                {
                    throw new ServiceException(409, "Request ID was already used for a different order.");

                }

                await tx.CommitAsync(ct);

            }

            else
            {
                var lines = new List<OrderLine>();

                string? currency = null;

                foreach (var item in command.Items.OrderBy(x => x.ProductId))
                {
                    var product = await catalog.GetAsync(item.ProductId, ct);

                    if (!product.IsActive)
                    {
                        throw new ServiceException(400, $"Product {item.ProductId} is discontinued.");

                    }

                    currency ??= product.Currency;

                    if (currency != product.Currency)
                    {
                        throw new DomainException("All order items must have the same currency.");

                    }

                    lines.Add(new OrderLine(item.ProductId, product.Name, item.Quantity, product.Price));

                }

                store.Add(
                    PurchaseOrder.Create(
                        command.RequestId,
                        command.CustomerId,
                        fingerprint,
                        currency!,
                        address,
                        lines));

                await store.SaveAsync(ct);

                await tx.CommitAsync(ct);

            }

        }

        try
        {
            return await RetryAsync(command.RequestId, ct);
        }

        catch (ServiceException error)when (error.StatusCode is 503 or 409)
        {
            return await GetAsync(command.RequestId, ct);
        }

    }

    public async Task<OrderResponse> GetAsync(
        Guid id,
        CancellationToken ct) => OrderResponse.From(
        await Required(id, ct));

    public Task<PagedResult<OrderResponse>> ListAsync(
        int page,
        int size,
        Guid? customerId,
        OrderStatus? status,
        CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue || customerId == Guid.Empty || (status.HasValue && !Enum.IsDefined(
            status.Value)))
        {
            throw new DomainException("Invalid pagination or order filters.");

        }

        return store.ListAsync(page, size, customerId, status, ct);
    }

    public async Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken ct)
    {
        await using (var tx = await store.BeginAsync(id, ct))
        {
            var order = await Required(id, ct);

            if (order.Status is OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered)
            {
                await tx.CommitAsync(ct);

                return OrderResponse.From(order);
            }

            if (order.Status != OrderStatus.Confirming)
            {
                order.BeginConfirmation();

                await store.SaveAsync(ct);

            }

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> CancelAsync(Guid id, CancelOrderCommand command, CancellationToken ct)
    {
        await using (var tx = await store.BeginAsync(id, ct))
        {
            var order = await Required(id, ct);

            if (order.Status == OrderStatus.Cancelled)
            {
                await tx.CommitAsync(ct);

                return OrderResponse.From(order);
            }

            if (order.Status != OrderStatus.Cancelling)
            {
                order.BeginCancellation(command.Reason);

                await store.SaveAsync(ct);

            }

            else if (order.CancellationReason != command.Reason?.Trim())
            {
                throw new ServiceException(409, "Cancellation is already in progress with a different reason.");

            }

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> RecordPaymentAsync(Guid id, RecordPaymentCommand command, CancellationToken ct)
    {
        await using (var tx = await store.BeginAsync(id, ct))
        {
            var order = await Required(id, ct);

            order.RecordPayment(command.PaymentId, command.Amount, command.Currency);

            if (order.Status == OrderStatus.Reserved)
            {
                order.BeginConfirmation();

            }

            else if (order.Status is not (OrderStatus.Confirming or OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered))
            {
                throw new ServiceException(409, "Order inventory is not ready for payment confirmation.");

            }

            await store.SaveAsync(ct);

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> RetryAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await store.BeginAsync(id, ct);

        var order = await Required(id, ct);

        switch (order.Status)
        {
            case OrderStatus.PendingInventory:

                var reserve = await inventory.ReserveAsync(
                    id,
                    order.Items.Select(x => new OrderItemRequest(x.ProductId, x.Quantity)),
                    ct);

                if (reserve.Status != "Reserved")
                {
                    throw new ServiceException(409, "Inventory reservation is not in the expected state.");

                }

                order.MarkReserved();

                break;

            case OrderStatus.Confirming:

                var commit = await inventory.CommitAsync(id, ct);

                if (commit.Status != "Committed")
                {
                    throw new ServiceException(409, "Inventory commit is not in the expected state.");

                }

                order.Confirm();

                break;

            case OrderStatus.Cancelling:

                var release = await inventory.ReleaseAsync(id, ct);

                if (release.Status != "Released")
                {
                    throw new ServiceException(409, "Inventory release is not in the expected state.");

                }

                if (order.PaymentId.HasValue && !order.PaymentRefunded)
                {
                    var refund = await payments.RefundAsync(id, ct);

                    if (refund.Status != "Refunded" || refund.PaymentId != order.PaymentId)
                    {
                        throw new ServiceException(503, "Payment refund remains pending.");

                    }

                    order.RecordRefund();

                }

                order.Cancel();

                break;

            default:

                await tx.CommitAsync(ct);

                return OrderResponse.From(order);
        }

        await store.SaveAsync(ct);

        await tx.CommitAsync(ct);

        return OrderResponse.From(order);
    }

    public async Task<OrderResponse> ShipAsync(Guid id, ShipOrderCommand command, CancellationToken ct)
    {
        await using var tx = await store.BeginAsync(id, ct);

        var order = await Required(id, ct);

        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            if (order.Carrier != command.Carrier?.Trim() || order.TrackingNumber != command.TrackingNumber?.Trim())
            {
                throw new ServiceException(409, "Order was already shipped with different tracking details.");

            }

        }

        else
        {
            order.Ship(command.Carrier, command.TrackingNumber);

            await store.SaveAsync(ct);

        }

        await tx.CommitAsync(ct);

        return OrderResponse.From(order);
    }

    public async Task<OrderResponse> RecoverAsync(Guid id, DateTime expiryCutoff, CancellationToken ct)
    {
        await using (var tx = await store.BeginAsync(id, ct))
        {
            var order = await Required(id, ct);

            if (order.CreatedAt <= expiryCutoff && order.Status is (OrderStatus.PendingInventory or OrderStatus.Reserved))
            {
                order.BeginCancellation("Unconfirmed order reservation expired.");

            }

            if (order.Status is OrderStatus.PendingInventory or OrderStatus.Confirming or OrderStatus.Cancelling)
            {
                order.RecordRecoveryAttempt();

                await store.SaveAsync(ct);

            }

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> DeliverAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await store.BeginAsync(id, ct);

        var order = await Required(id, ct);

        if (order.Status != OrderStatus.Delivered)
        {
            order.Deliver();

            await store.SaveAsync(ct);

        }

        await tx.CommitAsync(ct);

        return OrderResponse.From(order);
    }

    private async Task<PurchaseOrder> Required(
        Guid id,
        CancellationToken ct) => await store.FindAsync(
        id,
        ct) ?? throw new ServiceException(
        404,
        "Order not found.");

}
