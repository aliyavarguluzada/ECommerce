using ECommerce.Identity.Application;
using ECommerce.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Identity.Api.Controllers;

[ApiController]

[Route("api/identity")]

public sealed class IdentityController(IIdentityService service) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("identity")]

    public async Task<ActionResult<TokenResponse>> Register(
        RegisterCommand command,
        CancellationToken ct) => Ok(
        await service.RegisterAsync(command, ct));

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("identity")]

    public async Task<ActionResult<TokenResponse>> Login(
        LoginCommand command,
        CancellationToken ct) => Ok(
        await service.LoginAsync(command, ct));

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("identity")]

    public async Task<ActionResult<TokenResponse>> Refresh(
        RefreshCommand command,
        CancellationToken ct) => Ok(
        await service.RefreshAsync(command.RefreshToken, ct));

    [HttpPost("logout"), Authorize(Policy = "Customer")]

    public async Task<IActionResult> Logout(RefreshCommand command, CancellationToken ct)
    {
        await service.LogoutAsync(User.CustomerId(), command.RefreshToken, ct);

        return NoContent();
    }

    [HttpPost("logout-all"), Authorize(Policy = "Customer")]

    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await service.LogoutAllAsync(User.CustomerId(), ct);

        return NoContent();
    }

    [HttpGet("me"), Authorize(Policy = "Customer")]

    public async Task<ActionResult<UserProfile>> Me(
        CancellationToken ct) => Ok(
        await service.GetAsync(User.CustomerId(), ct));

    [HttpPut("me"), Authorize(Policy = "Customer")]

    public async Task<ActionResult<UserProfile>> Update(
        ProfileCommand command,
        CancellationToken ct) => Ok(
        await service.UpdateProfileAsync(User.CustomerId(), command, ct));

    [HttpPost("change-password"), Authorize(Policy = "Customer")]

    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command, CancellationToken ct)
    {
        await service.ChangePasswordAsync(User.CustomerId(), command, ct);

        return NoContent();
    }

    [HttpPut("users/{id:guid}/admin"), Authorize(Policy = "Admin")]

    public async Task<ActionResult<UserProfile>> Admin(
        Guid id,
        RoleCommand command,
        CancellationToken ct) => Ok(
        await service.SetAdminAsync(User.CustomerId(), id, command.IsAdmin, ct));

}
