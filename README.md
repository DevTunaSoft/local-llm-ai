# Work Intelligence AI — Unencrypted Windows development candidate

> **SECURITY NOTICE:** At the user's explicit direction, this edition stores the work database, extracted text, imported document copies, memory, chat history, and backup snapshots as **PLAINTEXT**. No vault or backup password is required. Microsoft Purview sensitivity labeling is NOT automatically applied to this application's `.db`, `.bin`, or snapshot files. Do not import organizational data unless this architecture is authorized.

> **Release status: NOT PRODUCTION ACCEPTED.** Build and service tests are mandatory; Windows WPF startup, local model inference, OCR tools, OneDrive configuration, restore on a replacement computer, signing, and the remaining requirement register must still be verified. Never describe this version as complete based only on source/CI tests.

## Key design choices

- Standard Microsoft.Data.Sqlite with `SQLitePCLRaw.bundle_e_sqlite3` dependency (bundled automatically): no SQLCipher key, password, or native SQLCipher DLL. FTS5 remains available.
- Imported binary document copies in `Data/Blobs/<id>.bin`; database in `Data/knowledge.db`. Both openly readable by any process with filesystem permissions.
- Versioned plaintext snapshots: each `snap-<timestamp>-<random>` directory contains `knowledge.db`, `Blobs/`, and `manifest.json`. The manifest's SHA-256 hashes detect accidental corruption, **not malicious tampering**.
- Optional rclone OneDrive copy/check operates on plaintext snapshots. Organizational approval for this upload is required; Purview is not an automatic substitute for local app safeguards.
- Optional Windows scheduled backup saves nonsecret configuration in `Data/scheduled-backup.json`. No passphrase or DPAPI-protected credential file is created.
- Legacy SQLCipher vaults and `.enc` blobs are **never opened, overwritten, converted, or deleted** by this edition. Keep the older app and its password for a separate, verified migration. Encrypted restic repositories are not restore-compatible with the new plaintext snapshot format.
- Local LLM inference, approval-based memory learning, workspace-scoped retrieval and other application behavior remain in source; model weights remain external to the eventual EXE.

## Build and validation

```powershell
.\scripts\Validate-Source.ps1
```

On Linux:

```bash
bash scripts/setup-linux-tests.sh
bash scripts/validate-source.sh
```

The released executable must eventually include vetted native llama.cpp, PDFium, Tesseract, and rclone assets; `scripts/Build-Windows.ps1` fails closed if files, hashes, tests, or publish artifacts are missing. Even a successful publish is not Windows user acceptance.

For the original 106-feature traceability record and earlier design, see `docs/handoff/requirements.json`. Historical documents elsewhere in `docs/` describe an *obsolete encrypted design* and do not override this readme or `docs/UNENCRYPTED_STORAGE_CHANGE_2026-11-02.md`.
