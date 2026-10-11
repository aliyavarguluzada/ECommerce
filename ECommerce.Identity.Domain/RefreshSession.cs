namespace ECommerce.Identity.Domain;
public sealed class RefreshSession
{
    public string TokenHash { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacementHash { get; private set; }
    private RefreshSession() { }
    public static RefreshSession Create(string hash, Guid userId, Guid family, DateTime expires) =>
        new() { TokenHash = hash, UserId = userId, FamilyId = family, ExpiresAt = expires };
    public void Revoke(string? replacement = null) { RevokedAt = DateTime.UtcNow; ReplacementHash = replacement; }
}
