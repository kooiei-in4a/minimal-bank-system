# spec-fix-001 formal run — 2026-08-02

## Run identity

- Benchmark ID: `spec-fix-001`
- Run ID: `2026-08-02-formal`
- Prompt revision: `file-first-v3`
- Input dataset: `spec-fix-001-portable-v1`
- Fixed Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Fixed Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Models: **14**
- Submission artifacts: **28 Markdown files**
- Valid / Invalid: **2 / 12**
- Winner: **GPT-5.6 Luna XHigh — 98.0 / Excellent**
- Runner-up: **ChatGPT-5.6 Sol High — 97.0 / Excellent**

## Complete evidence archive

All 28 submitted Markdown artifacts are retained in the following ZIP archive.

- Path: [`evidence/spec-fix-001-submissions-2026-08-02.zip`](evidence/spec-fix-001-submissions-2026-08-02.zip)
- Bytes: `245036`
- SHA-256: `40dbe00f58d44d035fb08037b55161065bda528c0388fe855e2d6e570bedb13c`
- Files in archive: `28`
- Structure: `models/<reviewer-slug>/repair-report.md` and `models/<reviewer-slug>/fixed-bank-system-specification.md`

The archive is the complete immutable evidence set for this run. The individual artifact names, archive paths, sizes and SHA-256 values are recorded in [`source-artifacts.csv`](source-artifacts.csv).

The files in the archive are model-generated benchmark submissions. They are **not** the authoritative product specification and must not be treated as approved changes to `docs/specs/bank-system-specification.md`.

## Verification

Linux / macOS:

```bash
sha256sum evidence/spec-fix-001-submissions-2026-08-02.zip
unzip -l evidence/spec-fix-001-submissions-2026-08-02.zip
```

PowerShell:

```powershell
Get-FileHash .\evidence\spec-fix-001-submissions-2026-08-02.zip -Algorithm SHA256
Expand-Archive .\evidence\spec-fix-001-submissions-2026-08-02.zip -DestinationPath .\spec-fix-001-submissions
```

Expected ZIP SHA-256:

```text
40dbe00f58d44d035fb08037b55161065bda528c0388fe855e2d6e570bedb13c
```

After extraction, compare each file with the corresponding `archive_path`, `bytes` and `sha256` fields in `source-artifacts.csv`.

## Other run artifacts

- [`execution-metadata.csv`](execution-metadata.csv): approximate runtime and execution method; not included in scoring
- [`scoring.csv`](scoring.csv): weighted score and Hard fail result for all 14 models
- [`scoring-report.md`](scoring-report.md): scoring method and adjudication
- [`manifest.yaml`](manifest.yaml): run-level identity and integrity metadata
- [`selected/gpt-5-6-luna-xhigh/repair-report.md`](selected/gpt-5-6-luna-xhigh/repair-report.md): expanded copy of the winning repair report for browser review

## Governance

- Refs: Parent Issue #3 and Independent Review Issue #10
- `docs/specs/bank-system-specification.md` was not modified by the benchmark run.
- F-003, F-004 and F-008 remain approval items.
- `Specification Ready` remains `NOT EVALUATED` until the approved fix is applied and independently reviewed.
