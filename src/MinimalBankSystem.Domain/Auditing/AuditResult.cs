namespace MinimalBankSystem.Domain.Auditing;

/// <summary>The durable outcome snapshot recorded for one authenticated operation.</summary>
public enum AuditResult
{
    Success = 1,
    Failure = 2,
}
