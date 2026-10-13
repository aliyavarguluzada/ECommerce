using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECommerce.Identity.Application;
using ECommerce.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore.Storage;
namespace ECommerce.Identity.Infrastructure;

public sealed class IdentityService(IdentityDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IConfiguration config) : IIdentityService
{
    public async Task<TokenResponse> RegisterAsync(RegisterCommand command, CancellationToken ct)
    {
        if (command.Email.Trim().Length > 256 || command.Password.Length > 1024)
            throw new ServiceException(400, "Email or password exceeds the supported length.");
        var name = Name(command.DisplayName);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = command.Email.Trim(), UserName = command.Email.Trim(), DisplayName = name };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Check(await users.CreateAsync(user, command.Password));
        Check(await users.AddToRoleAsync(user, "Customer"));
        var token = await IssueAsync(user, Guid.NewGuid(), ct);
        await tx.CommitAsync(ct); return token;
    }
    public async Task<TokenResponse> LoginAsync(LoginCommand command, CancellationToken ct)
    {
        if (command.Email.Trim().Length > 256 || command.Password.Length > 1024) throw new ServiceException(401, "Invalid email or password.");
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is null)
        {
            // Run password hashing work even when the account is absent.
            _ = new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), command.Password);
            throw new ServiceException(401, "Invalid email or password.");
        }
        await using var tx = await BeginUserAsync(user.Id, ct);
        user = await User(user.Id);
        if (!user.IsActive || !(await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true)).Succeeded)
        {
            await tx.CommitAsync(ct); // Preserve failed-attempt and lockout updates.
            throw new ServiceException(401, "Invalid email or password.");
        }
        var result = await IssueAsync(user, Guid.NewGuid(), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<TokenResponse> RefreshAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var candidate = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new ServiceException(401, "Invalid refresh token.");
        await using var tx = await BeginUserAsync(candidate.UserId, ct);
        var session = await db.RefreshSessions.FromSqlInterpolated($"SELECT * FROM refresh_sessions WHERE \"TokenHash\" = {hash} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (session is null) throw new ServiceException(401, "Invalid refresh token.");
        if (session.RevokedAt.HasValue)
        {
            await db.RefreshSessions.Where(x => x.FamilyId == session.FamilyId && x.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            await tx.CommitAsync(ct);
            throw new ServiceException(401, "Refresh token reuse detected. Sign in again.");
        }
        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (session.ExpiresAt <= DateTime.UtcNow || user is null || !user.IsActive || await users.IsLockedOutAsync(user))
            throw new ServiceException(401, "Refresh token expired or account unavailable.");
        var result = await IssueAsync(user, session.FamilyId, ct);
        session.Revoke(Hash(result.RefreshToken)); await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task LogoutAsync(Guid id, string token, CancellationToken ct)
    {
        var hash = Hash(token);
        await using var tx = await BeginUserAsync(id, ct);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash && x.UserId == id, ct);
        if (session is not null)
            await db.RefreshSessions.Where(x => x.FamilyId == session.FamilyId && x.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);
    }
    public async Task LogoutAllAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginUserAsync(id, ct);
        await RevokeAllAsync(id, ct); await tx.CommitAsync(ct);
    }
    public async Task<UserProfile> GetAsync(Guid id, CancellationToken ct) => await Profile(await User(id));
    public async Task<UserProfile> UpdateProfileAsync(Guid id, ProfileCommand command, CancellationToken ct)
    {
        var user = await User(id); user.DisplayName = Name(command.DisplayName);
        Check(await users.UpdateAsync(user)); return await Profile(user);
    }
    public async Task ChangePasswordAsync(Guid id, ChangePasswordCommand command, CancellationToken ct)
    {
        if (command.CurrentPassword.Length > 1024 || command.NewPassword.Length > 1024)
            throw new ServiceException(400, "Password exceeds the supported length.");
        await using var tx = await BeginUserAsync(id, ct);
        var user = await User(id); Check(await users.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword));
        await RevokeAllAsync(id, ct); await tx.CommitAsync(ct);
    }
    public async Task<UserProfile> SetAdminAsync(Guid actor, Guid id, bool admin, CancellationToken ct)
    {
        if (actor == id && !admin) throw new ServiceException(409, "Administrators cannot remove their own administrator role.");
        await using var tx = await BeginUserAsync(id, ct);
        var user = await User(id);
        if (admin && !await users.IsInRoleAsync(user, "Admin")) Check(await users.AddToRoleAsync(user, "Admin"));
        if (!admin && await users.IsInRoleAsync(user, "Admin")) Check(await users.RemoveFromRoleAsync(user, "Admin"));
        await RevokeAllAsync(id, ct); await tx.CommitAsync(ct); return await Profile(user);
    }
    private async Task<TokenResponse> IssueAsync(ApplicationUser user, Guid family, CancellationToken ct)
    {
        var privateKey = config["Auth:PrivateKey"] ?? throw new InvalidOperationException("Identity RSA private key is required.");
        using var rsa = RSA.Create(); rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        if (Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()) != config["Auth:PublicKey"])
            throw new InvalidOperationException("Identity private key does not match the configured public key.");
        var roles = await users.GetRolesAsync(user);
        var expires = DateTime.UtcNow.AddMinutes(Math.Clamp(config.GetValue("Auth:AccessTokenMinutes", 15), 1, 60));
        var claims = new List<Claim> { new("sub", user.Id.ToString()), new("email", user.Email!),
            new("jti", Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        var jwt = new JwtSecurityToken(config["Auth:Issuer"] ?? "ecommerce-identity", config["Auth:Audience"] ?? "ecommerce",
            claims, DateTime.UtcNow, expires, new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        var access = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        db.RefreshSessions.Add(RefreshSession.Create(Hash(refresh), user.Id, family,
            DateTime.UtcNow.AddDays(Math.Clamp(config.GetValue("Auth:RefreshTokenDays", 14), 1, 90))));
        await db.SaveChangesAsync(ct);
        return new(access, refresh, expires, new(user.Id, user.Email!, user.DisplayName, roles.ToArray(), user.IsActive));
    }
    private async Task<ApplicationUser> User(Guid id) => await users.FindByIdAsync(id.ToString()) ?? throw new ServiceException(404, "User not found.");
    private async Task<IDbContextTransaction> BeginUserAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var tx = await db.Database.BeginTransactionAsync(ct);
        try { var key = "identity:" + id; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct); return tx; }
        catch { await tx.DisposeAsync(); throw; }
    }
    private async Task RevokeAllAsync(Guid id, CancellationToken ct) =>
        await db.RefreshSessions.Where(x => x.UserId == id && x.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
    private async Task<UserProfile> Profile(ApplicationUser user) => new(user.Id, user.Email!, user.DisplayName, (await users.GetRolesAsync(user)).ToArray(), user.IsActive);
    private static string Hash(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024) throw new ServiceException(401, "Invalid refresh token.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
    private static string Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 150) throw new ServiceException(400, "Display name must contain 1–150 characters.");
        return value.Trim();
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new ServiceException(result.Errors.Any(x => x.Code.StartsWith("Duplicate")) ? 409 : 400,
            string.Join(" ", result.Errors.Select(x => x.Description)));
    }
}
