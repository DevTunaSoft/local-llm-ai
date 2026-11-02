# Codex — Work Intelligence AI final implementation mandate

## Operating contract
Operate autonomously within this coding task until the repository reaches the highest achievable verified state. Do not stop after each patch or green smoke test, and do not ask for confirmation on routine engineering decisions. Maintain a single working branch and a defect/acceptance ledger. Implement, test, diagnose, fix, rerun and re-audit. Do not claim production readiness from compilation or isolated smoke tests.

**Release packaging restriction:** DO NOT build, publish or distribute a final EXE or installer without explicit user authorization. Source builds and automated WPF launch tests that use non-packaged DLLs are permitted.

**Current architecture:** Windows 11 x64 WPF/.NET 10; entirely local CPU-only llama.cpp inference; selectable GGUF models (weights outside executable); per-model settings; local SQLite documents, memory and embeddings; workspaces; consent/approval-gated memory; unencrypted data and backups per user decision; authorized OneDrive as plaintext backup storage only. Do not reintroduce SQLCipher, restic encryption, secret passphrases or cloud inference.

## First actions
1. Read `README.md`, `docs/UNENCRYPTED_STORAGE_CHANGE_2026-11-02.md`, `docs/handoff/requirements.json`, `docs/handoff/requirement-evidence.json`, and the relevant source/test workflows.
2. Inventory actual code and every one of the 106 requirements. Historical `source_status` values are *not* runtime evidence. Treat the evidence crosswalk as partial and verify it.
3. Run `python3 scripts/verify-requirements.py`, Windows `scripts/Validate-Source.ps1`, and all consolidated CI workflows. Diagnose existing failures before feature work. Preserve passing behavior.
4. Produce one authoritative traceability ledger with requirement ID, implementation file/commit, unit/integration/e2e tests, actual pass/fail/unverified evidence, and blockers. Never mark a requirement complete on source inspection alone.

## Implement and validate, in priority order
### A. Application completion and native integration
- Audit and finish all visible WPF controls, first-run setup, model catalog/download/import/switching/benchmarking, conversation export, Document Studio, workspace task/incident/decision records and retrieval citations.
- Integrate and hash-pin official native Windows llama-server and its transitive DLLs, rclone, PDFium renderer, Tesseract and OCR assets; verify licenses/SBOM, archive extraction, clean lifecycle and explicit update policy.
- Exercise available source-only windows tests without releasing a package. Document any native assets requiring vendor licensing, signing or organizational approval.
- Keep signed/publish/EXE gate closed until user says to package.

### B. Unattended backups and recovery
- Verify scheduled backup operation with the app closed, missed-job catch-up, cancellation, retries, notification/health, versioned snapshots, configurable retention, and no loss of older valid versions.
- Validate OneDrive OAuth and approved backend before any plaintext data transfer; verify copies remotely byte-for-byte and prevent credentials in logs. Never use a personal/organization account or upload actual work data without explicit authorization.
- Test restore to an isolated empty vault and separate Windows profile/VM using synthetic fixtures. Verify DB, blobs, settings, memories, vectors, model pointers, and original-vault non-overwrite guarantees.
- Distinguish offline synthetic tests from real account/cross-PC acceptance.

### C. Real models and documents
- Run CPU GGUF tests for small models as already automated. For GPT-OSS 20B and Qwen 27B, obtain only vetted models from authorized sources; validate their templates, GGUF compatibility, load, streaming, stop/restart, context, memory and token throughput on relevant hardware. Existing `scripts/Test-ProductionModels.ps1` is a starting harness, not a production pass.
- Build real XLSX/PPTX/DOCX/PDF and image OCR fixtures including scanned/mixed PDFs, page/slide/sheet provenance and export round trips. Validate against actual native OCR tools; never count a stub as functioning.

### D. Security, fault injection and regression
- Test workspace isolation, unauthorized file access, organization authorization, malicious model/document payloads, prompt-injection-as-data boundaries, paths/symlinks, ZIP bombs, timeouts, cancellation, low RAM, disk full, corrupted SQLite/backups, worker crashes, and restored data privacy.
- Enforce local inference/no telemetry and explicit user-approved outbound network operations; audit rclone destination and auth state. Do not assume hashes provide authenticity or encryption.
- Rerun affected and full regression suites after every correction. Report provenance and remaining risks.

### E. Full acceptance and handoff
- Reconcile every requirement ID against proof; update `docs/handoff/requirement-evidence.json` and human-readable report accurately.
- Keep a single `docs/handoff/FINAL_ACCEPTANCE.md` with last verified SHA, CI URLs, issue status, independently runnable tests, Windows laptop/OneDrive/signer/clean-machine blockers, and exactly what remains before EXE authorization.
- Before stopping, run the full consolidated CI, verify final HEAD SHA (not an earlier green commit), and self-review changed code for correctness and consistency.
- If external services, target 32 GB laptop, real enterprise account, signing certificate or native assets prevent a test, clearly state which test was NOT run and how to run it. Do not silently skip.

## Completion criteria
One-pass completion is only achieved when the 106 items are either demonstrated against full acceptance criteria or explicitly recorded as external blockers / superseded by user-approved design. No false percentage-complete reporting, no placeholders presented as functionality, no claims of unattended coding outside the actual agent session, and no final EXE without authorization.

## Final output in the agent task
Provide: exact HEAD commit; substantial changes; five-workstream status; complete test command/results with links; 106-item coverage and unresolved requirement IDs; blockers requiring hardware/account/permission; no EXE status. Continue implementing fixable items rather than ending at the first passing build.
