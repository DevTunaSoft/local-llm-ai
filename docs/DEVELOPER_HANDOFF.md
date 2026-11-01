# Work Intelligence AI — No-Loss Development Handoff

## Objective

Continue from the actual source in this package. Preserve all requirements from `MASTER_DEVELOPMENT_PROMPT.md` and architecture v2. Do **not** restart at architecture-only planning or declare this complete without running real tests.

## Current environment and evidence

The source was authored in a Linux sandbox on 2026-10-31. There was **no .NET SDK**, no WPF Windows runtime, no native llama.cpp/restic Windows binaries, no models and no authorized OneDrive account in the environment. Attempted .NET installer download failed due to unavailable network. Consequently there is **no compiled Windows binary and no functional unit-test execution**. The available source-level checks passed as recorded in `docs/99_POINT_AUDIT.md`.

## Immediate next development operations

1. Run `dotnet restore WorkIntelligence.slnx`, `dotnet build -c Release` and `dotnet test -c Release` on a Windows 11 x64 development machine or GitHub Actions Windows runner. Diagnose every warning/error; fix and rerun all affected tests.
2. Execute app initial vault creation with a *synthetic test passphrase* and synthetic sample documents. Confirm on-disk database is SQLCipher-encrypted; verify wrong-passphrase rejection, restart and document version replacement.
3. Verify launcher `llama-server.exe` CLI flags against pinned release, package all DLLs, then load a small model first. Assert health, real streamed answer, cancellation, restart and isolation. Only then benchmark GPT-OSS 20B and Qwen3.5 27B on target hardware.
4. Execute restic init/backup/check/restore using synthetic data. Validate no plaintext temporary files or accidental backup repository inside active data directory. Test recovery on a second computer with no original Windows credentials.
5. Implement all missing critical features in `docs/RELEASE_GATES.md`, beginning with embeddings/hybrid search, document OCR, workspace/provenance controls, unattended backups, remote OneDrive verification, and secure update/recovery.
6. Complete 99-point QA with specific test evidence. Publish final EXE only if all mandatory release gates are met.

## Discovered upstream integration details

- llama.cpp uses `LLAMA_API_KEY` (not `LLAMA_ARG_API_KEY`) for its API key environment variable; the source was corrected.
- llama.cpp readiness is determined using `GET /health`, returning 503 while loading and 200 when ready.
- .NET single-file packaging on Windows may extract native DLLs under a user-specific folder; actual release packages need testing on clean Windows.
- Use `Microsoft.Data.Sqlite.Core` plus `SQLitePCLRaw.bundle_e_sqlcipher` rather than the default plaintext SQLite package.
- PDF text uses PdfPig, which does not provide OCR on its own.
- restic protects a local repository; merely putting it under OneDrive does **not** prove cloud sync or restore success.

## Source code change rules

- Preserve user-data encryption boundaries, source provenance, and all saved knowledge.
- Do not log raw prompts, work text, tokens, passwords, or entire error payloads.
- Never silently install or upload third-party executables. Bundle only pinned, verified native releases.
- Resolve errors at root cause and run full relevant regression tests after every change.
- Maintain an explicit `NOT VERIFIED` state for all Windows, end-to-end, hardware and remote sync tests that have not actually been performed.
- Prioritize real functionality over fake placeholder UI and premature release claims.
