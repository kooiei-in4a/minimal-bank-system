# ADR-0007: Authentication, authorization and operator management

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-011

## Context

Every operator requires an individual login. The product has three fixed roles, administrator-only operator management, active/disabled state, API-side authorization, last-administrator protection and no requirement for an external identity provider.

A short-lived JWT alone cannot guarantee immediate role-change enforcement because an older token may still contain the former role. The design therefore needs a per-request authorization-state check.

## Proposed decision

Use ASP.NET Core Identity backed by the same PostgreSQL database.

- Local username and password authentication
- ASP.NET Core Identity password hashing and security-stamp facilities
- Short-lived JWT bearer access tokens for REST API calls
- No refresh token in v0.1.0; operators authenticate again after expiry or invalidation
- Signing key supplied outside the repository through environment or Docker secret
- JWT contains the Operator identifier and a versioned authorization-state value
- A JWT role claim may be present for diagnostics, but it is not authoritative for authorization
- Every authenticated request loads the current Operator and verifies active state and authorization-state version
- Authorization policies use the current database role loaded after token validation
- Controllers use policy-based authorization; UI visibility is never treated as authorization

### Immediate invalidation

Role changes, disablement and re-enablement atomically update the Operator authorization-state version and the ASP.NET Core Identity security stamp.

For each authenticated request:

1. validate JWT signature, issuer, audience and expiry;
2. load the current Operator using the token subject;
3. reject the token if the Operator does not exist or is disabled;
4. compare the token authorization-state version with the current database value;
5. reject a mismatch; and
6. authorize using the current database role, not the stale token role.

A disabled Operator or stale authorization-state token receives HTTP 401 because the presented authentication state is no longer valid. A currently valid Operator whose current role lacks the required policy receives HTTP 403.

This means:

- a demoted administrator cannot use an older administrator token;
- a promoted Operator does not gain the new authority through an older token and must authenticate again; and
- disabling an Operator invalidates previously issued tokens on the next request without Redis or a distributed revocation service.

Operator management uses application services, not direct Identity endpoints exposed publicly.

- Only administrators may list, inspect, create, enable, disable or change fixed roles
- Public self-registration is disabled
- The last active administrator cannot be disabled or demoted
- An administrator cannot disable their own account
- Initial administrator creation is a separate bootstrap command or one-time startup procedure using secret input; the final procedure is documented before Release Ready

## Consequences

### Positive

- Password storage and core account behavior use maintained framework components.
- Bearer authentication is natural for a REST API and automated tests.
- Role changes and disablement invalidate older JWTs immediately at the next request.
- Authorization decisions use current database state.

### Negative

- JWT signing-key management and token validation require operational care.
- Every authenticated request requires an Operator lookup unless a future cache preserves immediate invalidation semantics.
- No refresh token means more frequent login, acceptable for the internal demo.

## Rejected alternatives

- Trust the JWT role claim until expiry: permits stale elevated authority after demotion.
- Add Redis or a distributed token-revocation service: unnecessary infrastructure for the internal demo.
- External identity provider: outside the selected self-contained stack.
- Shared administrator credential: violates individual login and auditability.
- Long-lived JWT without database state check: disabled users could retain access.
- Cookie session: viable, but adds browser-oriented CSRF concerns to an API-only initial scope.

## Verification

- API tests prove 401 versus 403 behavior for every role.
- A token issued before Operator disablement is rejected on the next request.
- A token issued before administrator-to-viewer demotion cannot call administrator APIs.
- A token issued before viewer-to-administrator promotion does not gain administrator authority until reauthentication.
- Last-administrator and self-disable rules are tested under concurrency.
- Secrets, JWTs and password material never appear in logs or repository files.
