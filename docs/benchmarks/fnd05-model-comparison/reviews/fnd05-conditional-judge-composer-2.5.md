# FND-05 Conditional Judge Result

## TRIGGER

```yaml
TRIGGERED: true
PRIMARY_TRIGGER: blocker_or_major_disagreement
TRIGGER_REASONS:
  - blocker_or_major_disagreement
  - root_cause_disagreement
  - required_fix_direction_disagreement
  - merge_readiness_disagreement
TARGET_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
DISPUTE_SCOPE:
  - M-02 mandatory mutation validity
  - M-06 mandatory mutation validity
```

## JUDGE_IDENTITY

```yaml
MODEL: Composer 2.5
HARNESS: Cursor
EFFORT: default
CONTEXT: Fresh Context
ROLE: Conditional Heavy Review Judge
PROMPT_REVISION: fnd05-conditional-judge-v2
PRODUCER_SLOT: J
```

## TARGET_VERIFICATION

```yaml
PR: 153
FINAL_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
TRIGGER_LOCK_COMMIT: 453b319b278b0df145cb3493d634dfc8478bdd68
RUN_REGISTRY_SHA256: 06bd4d8e7a80b0d7939edd7ff2db88aa7a62dc6fe03aa4c133195b427f212e54
ENTRY_CONDITIONS: PASS
```

## PHASE_A_REFERENCE

```yaml
artifact: docs/benchmarks/fnd05-model-comparison/reviews/fnd05-conditional-judge-phase-a-composer-2.5.md
sha256: 824ee163cb543a42f80bbe23c3549c9b5a6d7aad143ffd075afdce9c1f9ae047
producer_commit: b01d11759f343c1a04ca84c7ba71ae57ff6c3731
```

## PHASE_A_SUMMARY

Independent reference at FINAL_HEAD found both disputed mutations invalid as mandatory kills:

- **M-02:** `failure_oracle` emits `migrator-nonzero-masked` for any Migrator exit 0 without first establishing intended real Migrator failure. Probe confirmed identical signature for mask-only (natural success) and mask-and-fail (intended failure) cases.
- **M-06:** `run_m06` self-confirms mutation via inline config jq only; never invokes `static-gate.sh` or any lifecycle oracle; no baseline GREEN, restore GREEN, or residue 0.

`PHASE_A_MERGE_READY: NO`

## PHASE_B_ARTIFACT_VERIFICATION

```yaml
H1:
  path: docs/benchmarks/fnd05-model-comparison/reviews/fnd05-heavy-h1-sol-gpt-5.6-sol-codex.md
  producer_commit: 4583a32cdb6f6c788a56ce7fd43fba5266d5e5da
  sha256: 5de83bc37594896da2c681c92da89037c9c5dae3abec9b5a415362dd51967686
  SHA_MATCH: YES
H2:
  path: docs/benchmarks/fnd05-model-comparison/reviews/fnd05-heavy-h2-opus-claude-opus-5-claude-code.md
  producer_commit: 4ca962b4a8f0dd9faeacc1a494ed86f919f5536a
  sha256: cc0e996707f83f4b9c338b3ecc5033d0829646c0976843aec30de39b3a275425
  SHA_MATCH: YES
```

## H1_POSITION

```yaml
VERDICT: APPROVE
MAJORS: 0
M-02: treated as valid kill; claims M-01 through M-10 kills at exact Final Head
M-06: treated as valid kill
MERGE_READY: YES (architecture/contract only; not merge authorization)
```

H1 did not file discrete findings on M-02 or M-06 mutation validity.

## H2_POSITION

```yaml
VERDICT: CHANGES_REQUIRED
MAJORS:
  - H2-MAJ-01: M-06 not a valid kill (oracle never executed)
  - H2-MAJ-02: M-02 signature non-discriminating; precondition unverified
MERGE_READY: NO
```

## M02_ADJUDICATION

```text
SOURCE_FINDING: H2-MAJ-02 — M-02's expected failure signature cannot distinguish a real masked failure from a no-op mutation
NORMALIZED_ROOT_CAUSE: failure_oracle short-circuits on exit code 0 alone; intended-failure marker and FND05_M02_MASKED_NONZERO are never consumed on the masked path; D-06 precondition "intended real Migrator failure reached" is unverified before counting kill
PHASE_A_REFERENCE: M02_VALID_KILL=NO; M02_SIGNATURE_DISCRIMINATES_INTENDED_PATH=NO; probe M02-DISC-01 confirms identical signature for mask-only vs mask-and-fail
H1_POSITION: no Major; asserts all mandatory mutations including M-02 are valid kills
H2_POSITION: Major; precondition unrecorded/unverifiable; signature identical on success and masked-failure branches
ADJUDICATION: UPHELD
NORMALIZED_SEVERITY: Major
ACTUAL_IMPACT: CI can report M-02 KILLED while providing no evidence that exit-code masking of a real migration failure is detected; a broken or removed precondition fixture would leave the protected contract unguarded with green mutation suite
CONTRACT_BASIS: mandatory-mutations.md §3, §13; mutation-determinism-contract.md §3, §9; pre-run-decision-locks.md D-06 M-02; Issue #43 AC migration failure must not start API
EVIDENCE: FINAL_HEAD verify-mutations.sh run_m02/failure_oracle; independent probe M02-DISC-01 (mask_only and mask_and_fail both emit migrator-nonzero-masked)
REQUIRED_FIX: Machine-readably confirm intended real Migrator failure before masking assertion; failure signature must differ when intended failure path is not reached while masking wrapper remains; consume precondition marker or equivalent external evidence
MERGE_BLOCKING: YES
RE_REVIEW_REQUIRED: YES
RE_REVIEW_SCOPE: M-02 path in verify-mutations.sh + targeted mutation verifier re-run (not full H1/H2 heavy review)
```

## M06_ADJUDICATION

```text
SOURCE_FINDING: H2-MAJ-01 — M-06 mandatory mutation is not a valid kill; it verifies its own mutation instead of the oracle
NORMALIZED_ROOT_CAUSE: run_m06 only checks rendered config via inline jq (mutation self-confirmation); static-gate.sh volume-policy oracle is never invoked; no baseline GREEN, expected product-oracle RED, restore GREEN, or residue 0
PHASE_A_REFERENCE: M06_VALID_KILL=NO; product oracle RED confirmed by probe M06-ORACLE-01 when static-gate invoked against mutated compose, but M-06 harness does not invoke it
H1_POSITION: no Major; treats M-06 as valid kill
H2_POSITION: Major; EXPECTED_RED_OBSERVED=NO; oracle bypass proven by P-1/P-2 probes in H2 artifact
ADJUDICATION: UPHELD
NORMALIZED_SEVERITY: Major
ACTUAL_IMPACT: Named-volume contract can be removed from static-gate.sh while M-06 still prints KILLED and mutation suite stays green; PostgreSQL data volume policy becomes unguarded
CONTRACT_BASIS: mandatory-mutations.md §7, §13; mutation-determinism-contract.md §3, §9; pre-run-decision-locks.md D-06 M-06; Issue #43 AC PostgreSQL data uses named volume
EVIDENCE: FINAL_HEAD verify-mutations.sh run_m06; static-gate.sh jq volume assertion; probe M06-ORACLE-01 (inline KILLED, static-gate exit 1 on mutated compose); H2 P-1 oracle-regression probe corroborates
REQUIRED_FIX: Wire M-06 through shipped volume-policy oracle (static-gate and/or lifecycle test): baseline GREEN → apply named-volume violation → observe expected RED with discriminating signature → restore GREEN → residue 0
MERGE_BLOCKING: YES
RE_REVIEW_REQUIRED: YES
RE_REVIEW_SCOPE: M-06 path in verify-mutations.sh + static-gate invocation + targeted mutation verifier re-run
```

## NORMALIZED_ROOT_CAUSES

1. **M-02:** Mandatory mutation kill counted without verified D-06 precondition and with a non-discriminating failure signature (`migrator-nonzero-masked` on any exit 0).
2. **M-06:** Mandatory mutation kill counted via mutation self-check only; product oracle never executed; full mutation contract cycle (baseline → RED → restore → residue) absent.

## FINAL_VERDICT

```yaml
FINAL_VERDICT: CHANGES_REQUIRED
MERGE_READY: NO
```

Per `mandatory-mutations.md` §13, invalid mandatory mutation kills in Final Synthesis are merge-blocking Major. Two such defects are independently confirmed.

## REQUIRED_FIX

```yaml
M-02:
  observable_properties:
    - intended real Migrator failure is machine-readably confirmed before masking assertion
    - failure signature differs when intended failure path is not reached (masking wrapper alone must not produce kill signature)
    - masked non-zero is observable (e.g., log marker or external state) and tied to oracle RED
M-06:
  observable_properties:
    - baseline GREEN via shipped volume-policy oracle before mutation
    - named-volume violation causes static-gate and/or lifecycle oracle RED (not inline self-check only)
    - restore GREEN and residue 0 after mutation revert
```

## RE_REVIEW_SCOPE

```yaml
scope: targeted
rationale: >
  Both findings are test-oracle defects in verify-mutations.sh only; product
  runtime at FINAL_HEAD was not adjudicated defective. No adjacent architecture
  change is implicated.
required:
  - implement M-02 and M-06 fixes in tests/fnd05/verify-mutations.sh (and static-gate signature if needed for M-06)
  - run fnd05-mutations CI or equivalent local mutation verifier
  - one targeted re-review pass on mutation evidence (finding owner / lightweight mutation verifier)
not_required:
  - full Sol + Opus heavy architecture review rerun
  - product compose/runtime changes unless fix implementation requires them
```

## UNVERIFIED

- Full local re-run of entire `verify-mutations.sh` suite (CI SUCCESS at FINAL_HEAD used for baseline evidence).
- Runtime `docker volume inspect` under M-06 (config-level evidence sufficient).

## ARTIFACT_LOCK

```yaml
status: pending_lock_commit
prompt_revision: fnd05-conditional-judge-v2
target_head_sha: 59aa87f9c6c4c581a56257caef738318e8d09ec3
```
