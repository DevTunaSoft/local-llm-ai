# Backup, OneDrive, privacy and replacement-PC requirements

## Desired sequence
Local vault (encrypted SQLCipher plus encrypted source blobs) → consistent snapshot while application is active → locally encrypted and versioned restic repository → separately authorized OneDrive via bundled rclone or supported OneDrive sync of *encrypted files only* → remote verification → fresh-device restore test → normal local offline operation.

## Non-negotiable details
- Data and DB live on local SSD outside OneDrive. Never synchronize a live SQLite vault through OneDrive.
- Back up **conversation history, approved memories, policy decisions, projects, source documents and versions, encryption/vault metadata, settings and indexing state**. Version identifiers and source hashes must survive. Derived embeddings can be rebuilt if changed, but raw knowledge cannot be lost. Models may optionally be included by informed user choice, otherwise redownload/import.
- Distinct recovery passphrase for restic and local vault; stored in a user-controlled secure password manager or equivalent; recovery must not depend solely on the old PC's DPAPI profile. Never save recovery password inside the restic archive.
- Encryption before network transfer, independent cryptographic integrity verification, documented retention (e.g. 30 daily/12 monthly/3 yearly only if authorized), space limits, secure token handling and model migration support.
- Backups should run with main UI closed. Distinguish Windows current-user interactive scheduled task from real logged-off operation; use a supported secure task/service model with explicit authorization. Catch up missed jobs, avoid overlapping writes, preserve last good snapshot, alert failures.
- OneDrive onboarding must be GUI-friendly; no manual third-party rclone install. Safely handle OAuth, refresh tokens, selection/verification of appropriate organizational tenant/location. Remote verification must not rely on OneDrive sync icon alone.
- Test upload cancellation, offline network, power failure, token expiration, wrong passphrase, corrupted archive, insufficient disk, quota exhaustion, older snapshot restore, and backup retention after deletion requests.
- Restore onto clean Windows PC and different Windows login. Confirm encrypted contents, source hashes, memory counts and citations, then run with internet disabled. If destination already contains data, refuse overwrite by default and provide safe controlled reconciliation.
- If user asks to erase knowledge, distinguish deletion from current vault vs historical snapshots and implement a compliant purge/expiration policy. Never claim immediate deletion of previously created immutable backups without actually pruning/testing.

## Current source-specific blockers
- `RemoteBackupService` requires manually preconfigured rclone remote (no OAuth wizard or tested token backup policy).
- `ScheduledBackup` task is registered with `/IT` (interactive-user sessions) and stores credentials protected by *current Windows account* DPAPI; credentials are not transferable, although backup archive can be with separate password.
- `BackupService` restic verification is code-only; real vendor binary is absent and no remote recovery test has been performed.
- Retention, rotation, alerting, interrupted-transfer handling and recovery on second machine not evidenced.

## Organizational gate
Never place real company data into a personal OneDrive or development cloud merely because it is encrypted. Obtain employer authorization for the backup destination, data scope and retention. No PHI, personal employee details, passwords or privileged credentials in dev test fixtures. Use synthetic sample data.
