using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ECommerce.Identity.Application;

public sealed record RegisterCommand([EmailAddress] string Email, string Password, string DisplayName);

public sealed record LoginCommand([EmailAddress] string Email, string Password);

public sealed record RefreshCommand(string RefreshToken);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed record ProfileCommand(string DisplayName);

public sealed record RoleCommand([property: JsonRequired] bool IsAdmin);

public sealed record UserProfile(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsActive);

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserProfile User);

public interface IIdentityService
{
    Task<TokenResponse> RegisterAsync(RegisterCommand command, CancellationToken ct);

    Task<TokenResponse> LoginAsync(LoginCommand command, CancellationToken ct);

    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);

    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct);

    Task LogoutAllAsync(Guid userId, CancellationToken ct);

    Task<UserProfile> GetAsync(Guid userId, CancellationToken ct);

    Task<UserProfile> UpdateProfileAsync(Guid userId, ProfileCommand command, CancellationToken ct);

    Task ChangePasswordAsync(Guid userId, ChangePasswordCommand command, CancellationToken ct);

    Task<UserProfile> SetAdminAsync(Guid actorId, Guid userId, bool isAdmin, CancellationToken ct);

}
