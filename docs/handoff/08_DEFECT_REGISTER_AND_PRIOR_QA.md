# Defect and prior QA register (historical, source-based)

## Previously reported defects and source-side corrections
Listed below as **reported code modifications, not verified Windows fixes**:

| ID | Issue | Prior reported source change | Still required |
|---|---|---|---|
| HIST-01 | Missing embedded native runtime checksum | Added resource/manifest handling | Native bundle extraction/tamper test |
| HIST-02 | llama API alias/auth/health mismatch | Updated alias and `/health`, API key handling | Run actual llama versions and both models |
| HIST-03 | AI process startup/shutdown race | Added synchronization and cleanup | Stress start/stop, failure injection |
| HIST-04 | Large CPU load timeout too short | Increased to 15-minute startup bound | Benchmark and responsive cancellation |
| HIST-05 | Backup snapshot consistency | Coordinated snapshot code | Concurrent writes + restic verify/restore |
| HIST-06 | Unsafe repository nesting | Reject local app-root nested backup | Path traversal and symlink/Windows junction tests |
| HIST-07 | Vault schema future-version mutation | Added `user_version` protection | Migration and rollback tests |
| HIST-08 | SQLCipher connection leak | Dispose failed connections | Native encryption provider actually loads |
| HIST-09 | Interrupted chat persistence | Added partial-response persistence | Crash/cancel/restart scenario |
| HIST-10 | Model selection reset | UI selection preservation | UI click test |
| HIST-11 | Native bundle ZIP traversal/size/symlink risks | Added archive integrity checks | Malicious ZIP/persistent tamper tests |
| HIST-12 | Sensitive subprocess log exposure | Redacted diagnostic lines | Inspect file, env, console, crash dumps |
| HIST-13 | Deprecated SQLitePCLRaw bundle | Replaced package reference with provider.sqlcipher | SQLCipher native DLL compatibility & actual encrypted DB tests |
| HIST-14 | No vector retrieval | Added embedding worker and encrypted vectors | Real model, retrieval quality and citations |
| HIST-15 | No OCR | Added Tesseract and later PDFium renderer source | Bundle assets and scan PDF on Windows |
| HIST-16 | No unattended backups | Added Task Scheduler code | Logged-out and missed-job requirements still open |
| HIST-17 | No OneDrive verification | Added rclone copy/check/remote restore code | OAuth/onboarding and second-PC recovery not done |
| HIST-18 | Missing workspaces/tasks/model comparison/exports | Added minimal UI/actions and DOCX export | Complete workflows, automated tests and PDF output |

## High-priority active release blockers
- **CRIT-01**: Windows compile, WPF launch, clean-machine EXE not tested. Need actual SDK and Windows runner.
- **CRIT-02**: No trusted vendor native bundle, SHA manifest, compatible DLL dependencies, license/SBOM or signed tested EXE.
- **CRIT-03**: SQLCipher provider/native linkage and encrypted file security not verified.
- **CRIT-04**: GPT-OSS 20B and Qwen3.5 27B never validated on user CPU/32GB profile.
- **CRIT-05**: OneDrive OAuth onboarding, remote backup and cross-computer restore unverified.
- **CRIT-06**: Master requirement registry still contains partial/missing items (provenance/version conflict, OCR runtime, PDF export, background jobs, permissions, auto indexing, backup retention, key rotation, etc.).
- **CRIT-07**: Static smoke tests are not a comprehensive functional suite. Independent Windows UI + integration + fault-injection tests missing.

## Source reports carried into package
`docs/RECHECK_FINDINGS_2026-10-31.md`, `docs/RECHECK_ROUND3_2026-10-31.md`, `docs/REPAIR_DELIVERY_STATUS_2026-11-01.md`, and the original source/QA logs. Historical checks are not overwritten or fabricated; read dates, source context and tested environment carefully.

## Defect handling instruction
Codex should create an issue/record for each observed error, record OS/build/exact stack trace/reproduction/affected dependencies, study upstream official issues before fixing, patch with unit/integration tests, rerun and re-audit, record residual risk. Never mark `PASS` from a code presence check.
