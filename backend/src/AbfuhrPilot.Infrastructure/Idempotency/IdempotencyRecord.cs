namespace AbfuhrPilot.Infrastructure.Idempotency;

/// <summary>
/// Stored result of a mutating request executed under an <c>Idempotency-Key</c>.
/// Replaying the same company+user+key with the same request hash returns the
/// stored response instead of re-executing (safe offline/native retries);
/// the same key with a different request hash is rejected with 409.
/// Only successful (2xx) responses are stored; error responses re-execute on
/// retry. Records expire (default 24 h) and are removed by the cleanup worker.
/// Response bodies never contain secrets — auth/token endpoints are excluded
/// from idempotency by design.
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    /// <summary>SHA-256 over method + path + body (multipart: method + path marker only).</summary>
    public string RequestHash { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
}
