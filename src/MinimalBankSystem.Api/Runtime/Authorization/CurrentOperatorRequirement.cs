using Microsoft.AspNetCore.Authorization;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// Requires the presented JWT subject to resolve to a current active Operator with a matching
/// authorization-state version. A failing resolution means the presented authentication state is
/// no longer valid and is answered with HTTP 401 (ADR-0007).
/// </summary>
public sealed class CurrentOperatorRequirement : IAuthorizationRequirement;
