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
        RegisterRequest command,
        CancellationToken ct) => Ok(
        await service.RegisterAsync(command, ct));

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("identity")]

    public async Task<ActionResult<TokenResponse>> Login(
        LoginRequest command,
        CancellationToken ct) => Ok(
        await service.LoginAsync(command, ct));

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("identity")]

    public async Task<ActionResult<TokenResponse>> Refresh(
        RefreshRequest command,
        CancellationToken ct) => Ok(
        await service.RefreshAsync(command.RefreshToken, ct));

    [HttpPost("logout"), Authorize(Policy = "Customer")]

    public async Task<IActionResult> Logout(RefreshRequest command, CancellationToken ct)
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
        ProfileRequest command,
        CancellationToken ct) => Ok(
        await service.UpdateProfileAsync(User.CustomerId(), command, ct));

    [HttpPost("change-password"), Authorize(Policy = "Customer")]

    public async Task<IActionResult> ChangePassword(ChangePasswordRequest command, CancellationToken ct)
    {
        await service.ChangePasswordAsync(User.CustomerId(), command, ct);

        return NoContent();
    }

    [HttpPut("users/{id:guid}/admin"), Authorize(Policy = "Admin")]

    public async Task<ActionResult<UserProfile>> Admin(
        Guid id,
        RoleRequest command,
        CancellationToken ct) => Ok(
        await service.SetAdminAsync(User.CustomerId(), id, command.IsAdmin, ct));

}
