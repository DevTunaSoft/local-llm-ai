# Unencrypted storage architecture change — November 2, 2026

## Explicit decision
User requested removal of **all application-managed data-at-rest and backup encryption**, including SQLCipher, AES-GCM attachments, vault passphrases, encrypted restic snapshots, and DPAPI-wrapped backup passwords. We preserved checksums, HTTPS connections, session-scoped local model authentication, and filesystem guards; these are not storage encryption. Database and backup contents are sensitive plaintext.

## Changed components
- Database uses Microsoft.Data.Sqlite with the standard bundled SQLite native engine (FTS5).
- Imported file bytes stored under `Data/Blobs/<documentId>.bin`, with extracted text and memory openly readable in `Data/knowledge.db`.
- Plaintext, incrementally versioned snapshot directories with a SHA-256 manifest; restore to empty data root only, verifying each byte before promotion.
- Optional rclone remote sync and download/check; it sends plaintext to the configured remote, using whatever transport the provider supplies.
- Scheduled backup stores only repository/remote settings in a JSON file, not secrets.
- UI removes all passphrase controls and gives explicit plaintext warnings before backup/upload.
- Old `vault.json`, encrypted binary attachments and non-SQLite DB headers cause explicit failure, leaving old data intact. Existing restic encrypted snapshots are not migrated.

## Risk and Microsoft Purview
Microsoft Purview labels in Office and supported file types are **not automatically applied** to this application's SQLite database or arbitrary `*.bin` attachments. Applying a generic encryption label to an unknown file type may make it unreadable by an app. For work use, seek organizational approval and configure endpoint/file access controls, restricted account privileges, and approved OneDrive locations. Purview alone is not a substitute for application-specific local encryption.

## Limitations and acceptance
This is a change in security posture, not a declaration of readiness. Validate `dotnet restore/build/test`, real Windows WPF startup, SQLite persistence and migrations, unencrypted backup/restore and tamper refusal, OneDrive and scheduled tasks, native model/OCR execution, and the 106-feature acceptance register. A checksum is not an integrity signature; an attacker who can edit data and manifest can forge the hashes. No real user data used in the tests.
