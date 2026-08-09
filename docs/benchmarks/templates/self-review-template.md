# Formal Self-Review Result

## Identity

- Benchmark ID: `<benchmark-id>`
- Run ID: `<run-id>`
- Candidate: `<model> / <harness>`
- H0 Head: `<40-char-sha>`
- PR: `#<pr>`
- H0 CI: `<run or N/A>`
- Self-review attempt: `<n>`
- Fresh context: `YES / NO`

## Rules

- Review H0 exact Head only.
- Do not modify code during this phase.
- Do not read other candidates, external reviewer results, Gold, rankings, or scores.
- Treat implementation notes as secondary evidence.

## Findings

### SR-01 — <Severity> — <Title>

- Blocking: `true | false`
- Confidence: `high | medium | low`
- Affected component: `<path/component or N/A>`
- Description: `<root cause and impact>`
- Evidence:
  - `<file:line / runtime probe / CI / authoritative source>`
- Recommended fix: `<concise fix or N/A>`

Repeat only for meaningful findings.

## Self-review verdict

`NO CHANGE | FIX REQUIRED | BLOCKED | INCOMPLETE`

## Notes

- `<limitations or none>`

---

Emit a matching JSON result conforming to `../schemas/self-review-result.schema.json`.
