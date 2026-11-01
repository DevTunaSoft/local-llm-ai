# Repository instructions for Codex — Work Intelligence AI

> **Latest code continuation:** `docs/LEARNING_WORKSPACE_CONTINUATION_2026-11-01.md` and
> `docs/qa/learning-continuation-2026-11-01/STATUS.md` describe the schema 5
> learning and workspace changes. Source/SQLite checks passed, but changed C#
> has not yet been compiled or executed in this session. Run Windows and native
> regression gates before stating any release is functional.

## Current phase — user instruction dated 2026-11-01

Work on source quality, reproduce/fix defects, and rerun meaningful checks. **Do not package or publish an EXE until the user explicitly requests that later.** The eventual distribution requirements below do not authorize packaging during this phase. Read `docs/SOURCE_REVIEW_2026-11-01.md` and the updated evidence ledger for current results and open limitations. Use `scripts/validate-source.sh` for Linux source/service validation or `scripts/Validate-Source.ps1` for Windows source checks; defer `Build-Windows.ps1`. Cloud tasks already have isolated checkouts: use the existing checkout and do not create a worktree unless the user asks.

Read **`START_HERE_CODEX.md`**, **`docs/handoff/01_CANONICAL_PRODUCT_SPEC.md`**, **`docs/handoff/02_REQUIREMENTS_TRACEABILITY.md`**, **`docs/handoff/03_CURRENT_SOURCE_AUDIT.md`**, and **`docs/handoff/04_RELEASE_TEST_PLAN.md`** before changing code. The full historical specifications are at `docs/MASTER_DEVELOPMENT_PROMPT.md` and `docs/ARCHITECTURE_v2.md`. If they conflict with the consolidated handoff, preserve the user's latest requirements, investigate discrepancies, and record a resolution. Do not silently drop requirements.

## Product contract
- Windows 11 Pro x64, i7-1370P, 32 GB RAM, integrated Intel GPU; **CPU-only must work**.
- Ship **one self-contained `WorkIntelligence.exe`** with application code, bundled native tools, .NET runtime and local support assets; LLM `.gguf` model weights and mutable encrypted data remain external. Native extraction to protected app-owned directories is acceptable. No separate Python, Node, Ollama, Docker or .NET runtime install for end users.
- GPT-OSS 20B primary; Qwen3.5 27B advanced; support unlimited compatible locally imported/downloaded GGUF models, with memory protection, benchmarks and comparison.
- All inference, embeddings, retrieval, OCR and work information local; authorized model download / encrypted OneDrive backup are carefully restricted outbound exceptions. Never upload actual workplace data to any development or AI service. Only authorized, synthetic test fixtures in the repo.
- Persistent encrypted knowledge and source-grounded answers; approved corrections and policy supersession; complete document workflows, scanned-PDF OCR, workspace/projects, scheduled encrypted backups, actual OneDrive authentication and verified recovery to another machine.
- Always secure organizational approval before importing real company, regulated, patient, employee or confidential records; exclude such data by default.

## Quality standard — never replace functionality with placeholders
1. Inventory every requirement ID in `docs/handoff/requirements.json` and map to source and test; keep the matrix current with links to evidence.
2. Fix all build/runtime/security errors: **reproduce → root cause → check official docs / upstream issues → implement → unit test → integration test → regression test → independent recheck**; repeat on failure.
3. Prefer official Microsoft, llama.cpp, model publisher, SQLCipher, restic, rclone documentation and relevant tracked issues. Do not treat forum answers as proof.
4. Run `dotnet restore WorkIntelligence.slnx`, `dotnet build WorkIntelligence.slnx -c Release --no-restore`, `dotnet test tests/WorkIntelligence.Tests.csproj -c Release`, the source QA scripts, and finally `scripts/Build-Windows.ps1` once verified native binaries + manifests exist. Add more meaningful automated tests, not merely string matching.
5. Verify Windows WPF startup on clean machine; actual model inference, SSE, cancellation, memory limits; SQLCipher encryption, corrupted/malicious documents, OCR; backups with an authorized OneDrive test account, retention, restore, and offline validation on another PC. Capture logs/commands/versions/outputs; redact sensitive information.
6. Keep `docs/handoff/EVIDENCE_LEDGER.md` updated with **PASS / FAIL / NOT VERIFIED / NOT APPLICABLE**, exact test commands, environments and artifacts. **No claims of passing tests that were not actually run.** Build success != application acceptance.
7. Deliver complete source, reproducible build, verified standalone EXE (when test environment supports it), SBOM/licenses, source/feature matrix, test and defect evidence, restoration playbook, and current limitations.

## Safeguards
- Never push real work data, OneDrive credentials, model weights, recovery keys, local knowledge DB, or backups into GitHub/cloud Codex. Avoid collecting secrets in issue comments/logs.
- Use explicit permissions for downloads and live OneDrive tests. No invasive system changes / task scheduling without authorized Windows testing environment.
- Preserve migration and backup compatibility; do not modify user data destructively to pass tests. Use safe snapshots and rollback.
- Historical static check counts are **not** runtime verification. This repo is a development candidate with **no verified Windows EXE**.
