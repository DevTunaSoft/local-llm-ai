> **PRODUCTION STATUS (2026-11-01): NOT ACCEPTED.** The latest release blockers and fixes are in [`docs/PRODUCTION_BLOCKERS_2026-11-01.md`](docs/PRODUCTION_BLOCKERS_2026-11-01.md). Run `python scripts/release_readiness.py` for an explicit fail-closed blocker report. Source QA passing is not a Windows EXE or model acceptance test.

> **NOVEMBER 1 FOLLOW-UP REPAIR: SCHEMA 6.** The latest patch fixes repeated memory-command failures, adds local prior-conversation recall, retires missing watched-folder files from active retrieval, and adds new xUnit test cases. See [repair report](docs/REPAIR_CONTINUATION_2026-11-01.md). **This exact revision has not been compiled or executed on Windows, and it does not satisfy all 106 registered requirements.** Previous test results are historical and must not be used as release approval.

# Work Intelligence AI — Reviewed Windows Source Engineering Candidate

> **SOURCE REVIEW 2026-11-01: NOT A USER-READY RELEASE.** The current source compiles against Windows WPF using .NET 10 on Linux. New executable regression tests exercise the production services with real Linux SQLCipher and restic. Windows UI, Windows native integrations, real model inference, OneDrive and separate-PC recovery remain unverified. **The user's current instruction is source fixes and tests only: do not package an EXE.**

Current findings, exact test results and remaining gaps are in [the source review](docs/SOURCE_REVIEW_2026-11-01.md). Earlier audit sections below are historical; the new review and evidence ledger take precedence where they differ. The old SQLCipher bundle has already been replaced in source by `SQLitePCLRaw.provider.sqlcipher`; vetted Windows linkage is still unverified.

Development checks without executable packaging:

```bash
# Linux x64: Python 3.12+, GCC, make, Tcl, OpenSSL development headers
bash scripts/setup-linux-tests.sh
bash scripts/validate-source.sh
```

On Windows with .NET 10 SDK, run `./scripts/Validate-Source.ps1` to compile and run the Windows unit tests without publishing an executable. That PowerShell script has not been executed on Windows in this review. `Build-Windows.ps1` remains a separate, deferred packaging operation.

`WORKINTELLIGENCE_DATA_ROOT` optionally selects an absolute development data directory. Normal application use defaults to local app data. Portable tests always override this setting with a new temporary directory and never use an existing work vault. The Linux native tools installed by setup are **test-only dependencies**, not approved Windows release assets.

## Latest independent code recheck

The original source-only packaging and code review found defects that were not detected by the first 53-check structural validator. A revised candidate fixes the missing embedded SHA-256 manifest, model API alias mismatch, model-startup process lifecycle race, unsafe backup snapshot coordination, inconsistent chat interruption persistence, and several robustness issues. The review also found a **deprecated, no-longer-maintained SQLCipher native package**; it remains a security/release blocker pending a tested migration to a maintained encryption provider. See `docs/RECHECK_FINDINGS_2026-10-31.md` for a complete distinction between corrected code, source-only checks, and unverified Windows acceptance tests.

Run source and SQLite semantics smoke checks: `python scripts/static_qa.py` and `python scripts/recheck_qa.py`. These do not replace actual .NET compilation, Windows UI tests, SQLCipher encryption validation, model inference, or recovery testing.

## Goal

A local-only, work-focused Windows 11 application targeting an Intel Core i7-1370P with 32 GB RAM. The intended distribution is a **single, self-contained `WorkIntelligence.exe`** containing all application/runtime components except separately managed GGUF model files. Knowledge, chats, and approved memories are local, encrypted and portable; backups are encrypted locally before entering a OneDrive-synchronized folder.

**Additional source recheck:** the new audit includes an opt-in local GGUF embedding engine with encrypted vector records and hybrid retrieval, an image OCR path using bundled Tesseract over stdin/stdout, a DPAPI-protected scheduled-backup code path, and optional rclone OneDrive byte-comparison and remote restore operations. These are SOURCE IMPLEMENTATIONS only: new native integrations, authentication, Task Scheduler registration, Win-x64 build, and cross-PC restore have NOT been executed or verified. Image-only PDFs remain explicitly unsupported rather than silently indexed as empty text. The legacy SQLCipher provider remains a release blocker. See docs/RECHECK_ROUND3_2026-10-31.md.

The source was constructed from the user's master prompt (`docs/MASTER_DEVELOPMENT_PROMPT.md`) and the system architecture (`docs/ARCHITECTURE_v2.md`). No personal documents were used as development test data.

> **Latest source-only continuation (2026-11-01):** see [Learning and Workspace Continuation](docs/LEARNING_WORKSPACE_CONTINUATION_2026-11-01.md). The new schema 5 adds approved memory corrections, scoped chat and retrieval, and new QA. This continuation has NOT been compiled or tested on Windows and is NOT production-ready; the legacy table below is historical, not updated runtime evidence.

## What is implemented in source

| Capability | Source implementation | Actual runtime verified? |
|---|---|---|
| WPF desktop layout with tab navigation | Yes | **No** |
| SQLCipher encrypted SQLite with passphrase-derived keys | Yes | **No** |
| Authenticated encryption for imported source-file copies | Yes | **No** |
| Chat history, create/delete/export | Yes | **No** |
| Approved memories, remove and JSON export | Yes | **No** |
| FTS5 keyword retrieval with source/version references | Yes | **No** |
| Text/Markdown/CSV/JSON/log/PDF/Office Open XML import | Yes | **No** |
| Model GGUF import, checksum tracking, basic memory guard | Yes | **No** |
| User-initiated HTTPS model download with required SHA-256 | Yes | **No** |
| Local llama-server subprocess, loopback-only API key and SSE streaming | Yes | **No** |
| Restic encrypted repository snapshot, check and empty-vault restore | Yes | **No** |
| Source-only Windows CI build/test workflow | Yes | **No — not executed** |
| Source structure/XAML handler/policy checker | Yes | **Yes, 53/53 source-level checks at generation time** |

## Features not yet implemented

These are **required** in the master specification but *not present as verified working implementations*: semantic vector embeddings and hybrid retrieval (new code, integration unverified); scanned-PDF OCR (not implemented); image OCR (new code, native runtime unverified); vision input; full workspaces/authorization model; project and decision tracking workflows; Document Studio Word/PDF export; folder watchers; model benchmarking and automatic selection; model comparison; background scheduled backups (new Task Scheduler/DPAPI code, Windows execution unverified); remote OneDrive transfer and byte verification (new rclone code, OAuth and runtime unverified); end-to-end cross-PC restore verification; encryption key rotation; retention-policy enforcement and secure deletion across backup history; automatic updates and rollback; an audited, signed native runtime bundle; automated cross-machine hardware performance testing; full source-backed citation verification; full 99-case runtime QA.

Do not interpret absent UI controls or services as completed features. This package must not be represented as having fulfilled the user's full acceptance contract.

## Source layout

- `WorkIntelligence/` — Windows WPF app, model runtime, vault, encrypted DB, import, backup and UI
- `tests/` — xUnit regression tests for chunking, supported parsers, malformed files and unsafe download attempts
- `scripts/static_qa.py` — reproducible source-structure validations
- `scripts/Build-Windows.ps1` — native-bundle verification, test, and Windows single-file publish
- `vendor/README.md` — official native-runtime acquisition, checksum and licensing gate
- `.github/workflows/windows-build.yml` — Windows-hosted source build and unit tests (no artifact uploads)
- `docs/` — governing development requirements, audit reports and release gates

## Build on an authorized Windows development computer

Prerequisites **for the developer/build environment only**: Windows x64 with .NET 10 SDK and network access to restore pinned NuGet packages. The **end user should not need these** after the final fully bundled release has passed acceptance tests.

```
dotnet restore WorkIntelligence.slnx
dotnet build WorkIntelligence.slnx -c Release
dotnet test tests/WorkIntelligence.Tests.csproj -c Release
```

For a **candidate single-EXE release**, first obtain approved official Windows x64 CPU `llama-server.exe`, `restic.exe`, accompanying DLLs and their independently verified SHA-256 hashes as documented in `vendor/README.md`. Then:

```powershell
./scripts/Build-Windows.ps1
```

The release packaging script refuses to build a full EXE if the native tools or checksums are missing. It will not download arbitrary binaries or silently substitute unverified components. Even a successful candidate EXE still requires all Windows acceptance tests before release.

## First-run intended workflow

1. Open `WorkIntelligence.exe`, create and securely retain an encryption passphrase (16+ characters).
2. On the Model Manager tab, import a local verified `.gguf` model or enter a Hugging Face HTTPS download URL, filename, and independently verified SHA-256 checksum.
3. Select the model and load it. Local inference runs in a restricted loopback listener with a random API key. The native `llama-server.exe` comes from the signed/vetted bundle inside the release EXE.
4. Import **only authorized** work documents. Original files stay where they are; imported snapshots, extracted text and search indexes live in the encrypted vault.
5. Approve any permanent operational memories manually.
6. On Backup / Recovery, select an organizationally approved OneDrive-synchronized local folder or external-drive folder and supply a separate backup password.
7. Perform a test restore on a separate authorized test Windows computer before trusting backup recovery.

## Important encryption and recovery facts

- The vault passphrase and restic backup passphrase are **different** and both must be retained securely.
- Database encryption is dependent on a working native SQLCipher provider. The application refuses to initialize if it does not detect SQLCipher (`PRAGMA cipher_version`).
- Backup repository encryption happens locally with restic. The app currently checks the **local repository** only. OneDrive remote sync completion and actual restoration are not yet verified.
- The application currently requires manual backups while it is open. This is **not** a substitute for unattended daily backups.
- Downloaded models can be huge and are stored outside the EXE. The UI supports importing additional models, but GPT-OSS 20B and Qwen3.5 27B were **not actually benchmarked** on the target machine.
- Removing a record from the active vault does not remove older copies in existing backup snapshots.
- Plaintext export creates user-authorized unencrypted files; store them only in approved secure locations.

## Real release blockers

**Do not publish as production-ready** until Windows compilation, install/launch, native DLL packaging, model readiness/streaming, persistence, memory pressure, document parsing, SQLCipher encryption, restic restore, OneDrive synchronization and security review are completed with recorded evidence. Refer to `docs/99_POINT_AUDIT.md` and `docs/RELEASE_GATES.md`.

## Upstream documentation used

- Microsoft .NET 10 single-file: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Microsoft SQLCipher integration: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption
- llama.cpp server API and CLI: https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md
- PdfPig text extraction: https://github.com/UglyToad/PdfPig
- Restic: https://restic.readthedocs.io/en/stable/
- Actions .NET setup: https://github.com/actions/setup-dotnet

These were consulted for design and source corrections; only the checks reported in the QA log were actually executed.
