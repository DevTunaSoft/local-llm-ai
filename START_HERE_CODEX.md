# START HERE — MASTER CODEX PROJECT HANDOFF

**Work Intelligence AI | 2026-11-01 | Complete source + consolidated requirements + test contract**

> **Current phase:** schema 5 learning/workspace continuation is documented in `docs/LEARNING_WORKSPACE_CONTINUATION_2026-11-01.md` and must be recompiled/tested on Windows. Source repairs and rechecks only; the user has deferred executable packaging. The current code compiles against WPF on Linux and 72 Linux service regression tests pass. Windows interaction/native acceptance remains unverified. Start with [the current source review](docs/SOURCE_REVIEW_2026-11-01.md) and evidence ledger before relying on the historical baseline below.

## Immediate assignment
Finish the Windows desktop application described by these files. This is a **source engineering candidate, not a working verified product**. Codex should read the full context, trace every requirement to code/tests, diagnose actual compiler and runtime failures, implement missing functionality, perform iterative QA, and deliver a verified self-contained Windows executable only when the acceptance gates pass.

### Required reading order
1. `AGENTS.md` — standing Codex instructions (automatically discovered when Codex opens repo).
2. `docs/handoff/01_CANONICAL_PRODUCT_SPEC.md` — newest user requirements and decisions.
3. `docs/handoff/02_REQUIREMENTS_TRACEABILITY.md` + `requirements.json` — exhaustive feature and test register.
4. `docs/handoff/03_CURRENT_SOURCE_AUDIT.md` — source inventory, confirmed unverified work, known source risks.
5. `docs/handoff/04_RELEASE_TEST_PLAN.md` — compiler, native packaging, models, security, recovery, and QA gates.
6. `docs/handoff/05_BACKUP_PORTABILITY_AND_SECURITY.md` — OneDrive architecture and recovery.
7. `docs/handoff/06_MODEL_ENGINE_AND_RESOURCES.md` — models, 32 GB memory, embedding and multimodal behavior.
8. `docs/handoff/07_CONVERSATION_DECISIONS.md` — reconstructed product discussion and historical corrections.
9. `docs/handoff/08_DEFECT_REGISTER_AND_PRIOR_QA.md` — historical fixes, open defects, audit caveats.
10. `docs/MASTER_DEVELOPMENT_PROMPT.md`, `docs/ARCHITECTURE_v2.md`, `docs/99_POINT_AUDIT.md` — original governing specifications; retain, do not replace.
11. `docs/handoff/09_CODEX_START_PROMPT.md` — the exact first instruction to Codex.

### Baseline and provenance
- Base source: `WorkIntelligenceAI_Repair_Source_2026-11-01.zip` (unmodified C# source copied into this handoff).
- Source file SHA-256: `2704c97f2422ccd4b68f00caa5505089841f9a90954d38b36ec3b6bb6094223d`.
- Previous QA claims: 71/71 structural, 76/76 source/SQLite, 21/21 independent source checks. These are **historical source checks only**. No .NET SDK, Windows WPF desktop, actual GPT-OSS 20B / Qwen3.5 27B runtime, authorized OneDrive account, or second-PC restore was available in the prior testing environment.
- Native third-party executables are **not** in this ZIP. `scripts/Build-Windows.ps1` intentionally rejects an incomplete native bundle.
- A package containing `WorkIntelligence.exe` is **not** currently included.

### Required first actions
1. Record current platform/SDK versions and `git status`; make an isolated branch/checkpoint.
2. Run real Windows `dotnet restore/build/test`; capture errors and **fix each actual failure**.
3. Audit source with architecture, requirements JSON and 99-check matrix; classify each requirement as implemented+tested, partial, missing, blocked, or not verified, with evidence.
4. Investigate SQLCipher linkage; native DLL bundling and PDFium; llama.cpp chat/embedding templates; scheduled backups and rclone OneDrive authentication. Implement missing end-to-end workflows and tests.
5. Repeat build + tests + runtime tests after each correction. Do not conceal missing features behind UI buttons.
6. Final acceptance requires clean-machine run, measured model behavior, encrypted storage, encrypted OneDrive remote restore to separate authorized Windows PC, and 99-point matrix with evidence. If a test cannot be performed, clearly say **NOT VERIFIED**.

### User expectations
The user repeatedly insisted: complete, clean, functional code; proactively find and fix issues without repeated reminders; investigate defects from many independent angles and research official sources and relevant repositories in depth; preserve ALL prior scope; no fake completion claims, placeholders, or unverified Windows release.

### Privacy boundary
This repo is the **software project**, not a dump of real organizational work knowledge. Do not gather or expose proprietary operational files or personal records. Use synthetic fixtures until employer authorizes data processing and backup destinations.
