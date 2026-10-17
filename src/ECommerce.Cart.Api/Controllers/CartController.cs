using ECommerce.Cart.Application;
using ECommerce.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Cart.Api.Controllers;

[ApiController, Authorize(Policy = "Customer"), Route("api/cart")]

public sealed class CartController(ICartService service) : ControllerBase
{
    [HttpGet]

    public async Task<ActionResult<CartResponse>> Get(
        CancellationToken ct) => Ok(
        await service.GetAsync(User.CustomerId(), ct));

    [HttpPut("items/{productId:guid}")]

    public async Task<IActionResult> Set(Guid productId, SetQuantityRequest command, CancellationToken ct)
    {
        await service.SetAsync(User.CustomerId(), productId, command.Quantity, ct);

        return NoContent();
    }

    [HttpDelete("items/{productId:guid}")]

    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct)
    {
        await service.RemoveAsync(User.CustomerId(), productId, ct);

        return NoContent();
    }

    [HttpDelete]

    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await service.ClearAsync(User.CustomerId(), ct);

        return NoContent();
    }

    [HttpPost("checkout")]

    public async Task<ActionResult<OrderSnapshot>> Checkout(CheckoutRequest command, CancellationToken ct)
    {
        var order = await service.CheckoutAsync(User.CustomerId(), command, ct);

        return order.Status == "PendingInventory" ? Accepted(order) : Ok(order);
    }

    [HttpPost("checkout/retry")]

    public async Task<ActionResult<OrderSnapshot>> RetryCheckout(CancellationToken ct)
    {
        var order = await service.RetryCheckoutAsync(User.CustomerId(), ct);

        return order.Status == "PendingInventory" ? Accepted(order) : Ok(order);
    }

}
