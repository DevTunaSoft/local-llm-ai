# Work Intelligence AI — production release gates and newly repaired defects

**Date:** 2026-11-01. **Source basis:** `WorkIntelligenceAI_Repair_Source_2026-11-01.zip`, not the older 72/72 service-test revision. This is a code candidate, **not production-ready**, until the gates below pass on the exact source/build.

## Fixes in this continuation

- Prevent `WORKINTELLIGENCE_DATA_ROOT` from resolving inside configured OneDrive root paths (`OneDrive`, `OneDriveCommercial`, `OneDriveConsumer`). Live SQLCipher files must stay outside OneDrive; only independently encrypted restic repository objects belong there. Guard paths against reparse/symlink ancestors before and after directory creation. New C# xUnit cases check direct, nested, sibling-prefix and symlink inputs.
- Fix scheduled backup removal: previously the `finally` clause erased DPAPI credentials even if `schtasks /Delete` failed and the task remained registered. Now credentials remain intact when task deletion fails. This is a deliberate fail-closed behavior, pending Windows integration testing of Task Scheduler's missing-task and privilege cases.
- Strengthen `Build-Windows.ps1`: reject missing files declared in vendor checksum manifest; test both WPF and production-service suites (including SQLCipher) before publishing; report unsigned/invalid signature status explicitly. Native test discovery depends on placing a verified `sqlcipher.dll` in `vendor/runtime`.
- Add `scripts/release_readiness.py`, which tracks **all 106** requirements, rejects missing runtime tools and native hash mismatches, and refuses to infer runtime acceptance from static QA.

## Must-pass gates before a production declaration

| Gate | State today | What is needed |
|---|---|---|
| C# Release compilation for this exact revision | **NOT EXECUTED** | .NET 10 on an authorized Windows runner; correct every compile error |
| xUnit tests, real SQLCipher and actual Windows process behavior | **NOT EXECUTED** | Real native Windows DLL, full Windows CI job and logs |
| Single Windows x64 EXE | **BLOCKED** | Complete vendored, licensed, pinned Windows runtime assets; clean-machine startup test |
| Real GPT-OSS/Qwen inference and model compatibility | **NOT EXECUTED** | Approved GGUF model weights, actual llama.cpp binaries, 32GB target hardware test |
| Audio/vision/OCR, exports, retrieval accuracy and source provenance | **INCOMPLETE / UNVERIFIED** | Implement missing requirement rows, execute actual fixture-based E2E checks |
| Persistent learning and corrected-knowledge quality | **PARTIAL** | Correction conflict evaluation, approval UI, stale policy authority and recall evaluation |
| OneDrive encrypted backups and restore to another computer | **UNVERIFIED** | Organizational OneDrive approval, Windows account authorization, signed-off recovery trial |
| Native supply chain, signature and license closure | **BLOCKED** | Approved vendor checksums/licenses, SBOM, trusted signing certificate and signature evidence |
| 106 registered requirements and the 99-point audit | **NOT ACCEPTED** | Per-ID proof, P0/P1 implementation, regression and owner sign-off |

## Environmental limitation observed

This execution environment is **Linux** and has no `.NET` SDK, Windows desktop runner, native Windows binaries, real GGUF models, OneDrive account, or independent Windows test PC. Outbound DNS for `dot.net` was unavailable. The previously empty private GitHub repository `DevTunaSoft/local-llm-ai` does not contain this source, so it cannot currently run the Windows workflow. This is a real deployment blocker, not a compiler success or failure.

## What was actually executed

`python scripts/static_qa.py`; `python scripts/recheck_qa.py`; `python scripts/learning_qa.py`; `python scripts/independent_round3_qa.py`; Python syntax validation; `python scripts/release_readiness.py`. Evidence logs for the exact revised source are bundled separately under `docs/qa/production-readiness-2026-11-01/`. Static/synthetic SQLite checks do not certify WPF, SQLCipher or actual inference.

## Security boundaries

Workspaces are **logical filters, not independent user access controls**. The application is intended for a single authorized user, not a multiuser tenant boundary. Approval is required for durable memory; prior-chat recall is contextual evidence, not a trusted organizational policy. The product is not cleared to ingest unauthorized employee data, patient information, or other sensitive records. Do not enable organizational OneDrive backup without work-data authorization.

## Required external inputs

1. An accessible Windows 11 build/test environment or a GitHub repository containing this complete source and configured Windows Actions runner.
2. A vetted, legally distributable Windows x64 native stack (llama.cpp, SQLCipher, PDFium, Tesseract, restic, rclone and their dependent DLLs).
3. Approved model GGUF assets and hardware for genuine inference benchmarks.
4. Organization-approved OneDrive test target plus a second authorized Windows machine/account for restore.
5. For a trusted signed release, an organization-approved publisher signing identity.

**Do not label the current ZIP an executable release.**
