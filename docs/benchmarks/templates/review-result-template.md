# Independent Review Result

## Target

- Benchmark ID: `<benchmark-id>`
- Run ID: `<run-id>`
- Repository: `kooiei-in4a/minimal-bank-system`
- Issue: `#<issue>`
- PR: `#<pr>`
- Base SHA: `<40-char-sha>`
- Head SHA: `<40-char-sha>`
- CI target SHA: `<40-char-sha or N/A>`

## Reviewer

- Model: `<model>`
- Harness: `<harness>`
- Effort: `<effort or N/A>`
- Reviewer slug: `<model-harness-slug>`
- Attempt: `<n>`

## Target verification

- Repository: PASS / FAIL
- PR: PASS / FAIL
- Base SHA: PASS / FAIL
- Head SHA: PASS / FAIL
- CI SHA: PASS / FAIL / N/A

If the requested target cannot be verified, state `wrong_target` or the applicable failure outcome instead of silently reviewing another checkout.

## Verdict

`APPROVE | REQUEST CHANGES | BLOCKED | INCOMPLETE`

- Blocker: `<n>`
- Major: `<n>`
- Minor: `<n>`
- Nit: `<n>`

## Findings

### F-01 — <Severity> — <Title>

- Blocking: `true | false`
- Affected component: `<path/component or N/A>`
- Description: <concise description>
- Evidence:
  - `<file:line / runtime probe / CI / authoritative source>`
- Proposed root-cause key: `<G-xx if known by collector later; reviewer should normally leave N/A>`

Repeat only for meaningful findings. Do not add speculative improvement requests as merge blockers.

## Verification performed

- CI independently checked: YES / NO
- Local build/test/probe performed: YES / NO
- Summary: `<what was actually run or inspected>`

## Scope assessment

- Scope drift: YES / NO
- Out-of-scope implementation detected: `<details or none>`

## Notes

- `<important limitations, incomplete evidence, or harness constraints>`

---

A matching structured JSON file conforming to `../schemas/review-result.schema.json` must be emitted for benchmark collection.
