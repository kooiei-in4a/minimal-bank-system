namespace MinimalBankSystem.Infrastructure.Identity;

/// <summary>The two fixed Operator states from ADR-0006 (`active` / `disabled`).</summary>
public enum OperatorState
{
    /// <summary>有効 (Active). Permits login and business API use.</summary>
    Active = 0,

    /// <summary>無効 (Disabled). Login and business API use are denied.</summary>
    Disabled,
}
