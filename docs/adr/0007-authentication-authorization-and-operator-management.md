# ADR-0007: Authentication, authorization and operator management

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-011

## Context

Every operator requires an individual login. The product has three fixed roles, administrator-only operator management, active/disabled state, API-side authorization, last-administrator protection and no requirement for an external identity provider.

## Proposed decision

Use ASP.NET Core Identity backed by the same PostgreSQL database.

- Local username and password authentication
- ASP.NET Core Identity password hashing and security-stamp facilities
- Short-lived JWT bearer access tokens for REST API calls
- No refresh token in v0.1.0; operators authenticate again after expiry
- Signing key supplied outside the repository through environment or Docker secret
- Every authenticated request resolves the current Operator and verifies that it remains active
- Authorization policies represent the fixed administrator, counter-clerk and viewer roles
- Controllers use policy-based authorization; UI visibility is never treated as authorization

Operator management uses application services, not direct Identity endpoints exposed publicly.

- Only administrators may list, inspect, create, enable, disable or change fixed roles
- Public self-registration is disabled
- The last active administrator cannot be disabled or demoted
- An administrator cannot disable their own account
- Initial administrator creation is a separate bootstrap command or one-time startup procedure using secret input; the final procedure is documented before Release Ready
- Role and active-state changes update the security stamp so existing access is invalidated as soon as practical; active-state verification on each request provides immediate enforcement

## Consequences

### Positive

- Password storage and core account behavior use maintained framework components.
- Bearer authentication is natural for a REST API and automated tests.
- Database-backed active-state checks enforce disabled users immediately.

### Negative

- JWT signing-key management and token validation require operational care.
- Per-request Operator lookup adds a database read unless safely cached.
- No refresh token means more frequent login, acceptable for the internal demo.

## Rejected alternatives

- External identity provider: outside the selected self-contained stack.
- Shared administrator credential: violates individual login and auditability.
- Long-lived JWT without database state check: disabled users could retain access.
- Cookie session: viable, but adds browser-oriented CSRF concerns to an API-only initial scope.

## Verification

- API tests prove 401 versus 403 behavior for every role.
- Disabled operators lose API access.
- Last-administrator and self-disable rules are tested under concurrency.
- Secrets and password material never appear in logs or repository files.
