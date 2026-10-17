using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ECommerce.Identity.Application;

public sealed record RegisterRequest([EmailAddress] string Email, string Password, string DisplayName);

public sealed record LoginRequest([EmailAddress] string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ProfileRequest(string DisplayName);

public sealed record RoleRequest([property: JsonRequired] bool IsAdmin);

public sealed record UserProfile(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsActive);

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserProfile User);

public interface IIdentityService
{
    Task<TokenResponse> RegisterAsync(RegisterRequest command, CancellationToken ct);

    Task<TokenResponse> LoginAsync(LoginRequest command, CancellationToken ct);

    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);

    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct);

    Task LogoutAllAsync(Guid userId, CancellationToken ct);

    Task<UserProfile> GetAsync(Guid userId, CancellationToken ct);

    Task<UserProfile> UpdateProfileAsync(Guid userId, ProfileRequest command, CancellationToken ct);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest command, CancellationToken ct);

    Task<UserProfile> SetAdminAsync(Guid actorId, Guid userId, bool isAdmin, CancellationToken ct);

}
