namespace SMS.Infrastructure.Audit;

/// <summary>Supplies the current acting user to the audit interceptor. The API binds it to the
/// HTTP context; background/seed work falls back to "system".</summary>
public interface ICurrentUserAccessor { string? UserName { get; } }

public sealed class SystemCurrentUser : ICurrentUserAccessor { public string? UserName => null; }
