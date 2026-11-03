# Work Intelligence AI acceptance review — 2026-11-02

**Decision: NOT PRODUCTION ACCEPTED. The completion mandate is not fully satisfied.**

Source reviewed: `a26f5509a1fb54afe2c061ff3be37caad0bd1b20`.
Review branch: `codex/handoff-completion-20261102`.
No application EXE/installer was packaged, signed, or released. No environment was published by the agent. Plaintext storage and backups remain the explicit architecture.

## Changes and observed evidence

| Area | Implemented and tested | Limits |
|---|---|---|
| Recovery | Verify SQLite integrity/references and attachment hashes before accepting snapshots; fall back from broken newest SQLite; reject null paths, linked manifests and unsafe trees; stage/reverify copies before no-overwrite promotion; refuse orphaned journals and preserve settings | Power loss/process termination during mult-file promotion and actual disk-full remain unverified; staging/rollback tests do not certify these |
| OneDrive/retention | Upload only completed, verified snapshot directories; exclude pending/corrupt/unrelated content; preserve remote recovery points; serialize retention with repository operations | No real account was used; remote interruption and cross-PC parity not demonstrated |
| Documents | Relationship-driven PowerPoint order, named Excel sheets/cell coordinates; fail on broken/shared-string/external relationships; searchable PDF with local embedded font and pagination; atomic DOCX Unicode validation | Native OCR and a broad real-world Office/PDF corpus remain unverified; complex text shaping/formatting and viewer interoperability remain open |
| Comparison | Compare two hash-verified stored sources in the current workspace; preserve version labels and deterministic added/removed lines | Whitespace-normalized text comparison, not automatic policy adjudication; 2,000-line and two-million-character per-source limits |
| Models/downloads | Per-model startup/response deadlines; distinguish user cancellation from timed-out partial answers; serialized resumable download; safe redirects/ranges/checksums and symlink refusal | No production 20B/27B acceptance; catalog/routing/vision and detailed memory budgeting remain open |
| Native supply chain | Pin Windows llama.cpp `b12059` archive digest `25310083fad7da14ce7788a7f3c5d7536be19b82a099d2566a805e99864a6f91`; verify every development runtime file, including transitive DLLs; reject additions/tampering | This is source-test integration, not the full vetted PDFium/Tesseract/rclone production bundle or a native license/security certification |
| Work records | Projects, milestones, actions, risks, decisions, incidents; owner/date/status/related records; immutable revision snapshots/reasons; stale-edit refusal; workspace-filtered current-record retrieval; editing existing task details | Workspace scopes are not per-user security principals; all-control/accessibility and representative work acceptance remain open |
| Migration/scheduling | Transactional schema 6→7 upgrade retains tasks/memory; work-record history survives backup/restore; preserve source DLL and custom data-root in scheduled command; validate destinations | Windows `/IT` is signed-in only; actual Task Scheduler installation/removal, sleep/reboot/logout and backoff behavior not accepted |

Commits:

- `929399a49f107e23a597d0cae6247ad3fcfee730`: recovery/native hardening, PDF export, deadlines, Office relationships.
- `ea9007e1d3f5ff854854432c5b78e6bbf071c418`: revisioned work records, source comparison, task editing, download/scheduling regressions, Windows UI coverage.
- `a26f5509a1fb54afe2c061ff3be37caad0bd1b20`: explicit data-root initialization and actual Windows launch coverage.

## Executed checks

- `bash scripts/setup-linux-tests.sh`: PASS, official checksum-pinned .NET SDK 10.0.403 and package restore. Required execution outside the filesystem sandbox for .NET system mutex access; no checks or TLS validation disabled.
- `bash scripts/validate-source.sh`: PASS, source DLL build **0 warnings/0 errors**, **173 passed / 0 failed / 2 skipped** production-service tests, plaintext checks **23/23**, learning/SQL checks **33/33**.
- The two skipped local tests require real chat/embedding GGUF fixtures. They now report SKIPPED instead of passing without executing. Required native CI remains fail-closed.
- `python3 scripts/verify-requirements.py` and `python3 -O scripts/verify-requirements.py`: PASS, all 106 IDs and review records present. Explicit checks cannot disappear under Python optimization.
- `python3 -m unittest discover -s tests/python`: PASS, 6 traceability integrity tests, including missing IDs/files, fabricated tests and unsupported completion claims.
- `dotnet list WorkIntelligence/WorkIntelligence.csproj package --vulnerable --include-transitive --format json`: PASS, no known vulnerable packages reported by the configured NuGet source. This does not audit all native binaries/licenses.
- [Windows/Linux/WPF/native consolidated run at ea9007e](https://github.com/DevTunaSoft/local-llm-ai/actions/runs/41541967866): **all five jobs succeeded**. This includes real WPF project creation/revision/history, conversation operations, genuine small GGUF chat/restart, and embeddings/retrieval.
- [Earlier consolidated run at 929399a](https://github.com/DevTunaSoft/local-llm-ai/actions/runs/41540700784): all five jobs succeeded.
- Final documentation-head CI must succeed on the exact pushed head; inspect the PR/check run, not only the earlier links above. A document cannot contain its own commit hash without changing that hash.

Portable build/test logs, TRX and advisory output: [`../qa/completion-2026-11-02/`](../qa/completion-2026-11-02/). Source tests use synthetic temporary data only. No user data, account credentials, model weights, compiled binaries or native archives are committed.

## Defects reproduced and regression coverage

| Defect/root cause | Regression evidence |
|---|---|
| Header/hash-valid but malformed SQLite blocked older recovery | `BackupTests.RestoreFallsBackWhenNewestDatabaseHasValidHashesButBrokenSqlitePages` |
| Null manifest paths escaped fallback; linked manifests were followed | `NullManifestPathIsRejectedWithoutCrashingFallback`, `SnapshotManifestSymlinkIsRejected` |
| Snapshot file list could omit DB-referenced attachment | `MissingDocumentBlobCannotBecomeAVerifiedRecoveryPoint` |
| Whole repository upload included pending/unrelated files | `RemoteUploadSelectsOnlyCompletedVerifiedSnapshots` |
| Source blob and download partial symlinks crossed file boundaries | `SymlinkAttachmentCannotBeCopiedIntoABackup`, `SymlinkPartialIsRejectedBeforeNetworkOrFileWrites` |
| EXE-only development pin omitted DLL trust | `DevelopmentRuntimeTests.TransitiveDllTamperingOrAdditionIsRejected` |
| Stalled responses had no configurable bound | `ResponseTimeoutTests` header/body stall and cancellation cases |
| Slide filename order/sheet filename labels misrepresented content | `OfficeRelationshipTests` reordered/orphaned slides, named sheets, invalid references |
| Source-only scheduled command lacked DLL/custom root | `StartupOptionsTests` plus explicit-root Windows WPF launch |
| Basic tasks hid saved details; no structured history | `TaskEditsPersistAndCannotTargetAnotherWorkspace`, `WorkRecordTests`, actual WPF record automation |
| Optional live tests returned green without real fixtures | `LiveModelFactAttribute` reports absent optional fixtures as SKIPPED; required CI flags still fail missing inputs |

## Remaining repository engineering — not external-only blockers

The 106-item JSON records remain authoritative; none is certified by a generic suite result. Material unfinished scope includes:

- **WIN-005, WIN-010:** complete first-run path/model/knowledge/backup wizard and GUI install/uninstall/data-root workflows.
- **WIN-004, SEC-007, SEC-011, DOC-003, DOC-004:** complete licensed, pinned native Windows bundle, native OCR/PDFium document acceptance, SBOM/advisory review and extraction/ACL acceptance.
- **MOD-004, MOD-008–012, MOD-014, DOC-010:** catalog, detailed KV/context memory budgeting, actual token/RAM benchmarks, explainable routing, complete architecture/template checks and multimodal projector/image flow.
- **KNOW-007, KNOW-013:** authority/effective-date/contradiction review and fixed retrieval/citation/hallucination evaluation corpus.
- **WORK-002:** principal-level authorization/export controls; current workspace scoping does not enforce organizational permissions.
- **BKP-003, BKP-007, BKP-008:** retry/backoff, calendar/quota/remote retention, backup health/alert UI and interrupted-operation acceptance.
- **QA-006, QA-007, QA-010, QA-012:** complete 99-case audit, real crash/disk-full/reboot/RAM fault evidence, independent security review and every-control/accessibility coverage.

These are not deferred by user agreement and must not be hidden behind hardware/account blockers.

## External acceptance inputs and exact actions

1. **Target models/hardware — MOD-001/002, QA-009:** authorized Windows i7-1370P/32GB CPU laptop, trusted GPT-OSS 20B and Qwen3.5 27B GGUFs and matching runtime. Run `scripts/Test-ProductionModels.ps1 -RuntimeDirectory <runtime> -GptOss20BGguf <20B.gguf> -Qwen27BGguf <27B.gguf>`. Then execute the registry's template/reasoning, long-context, 30-minute memory and throughput criteria; the existing harness only proves generation/restart. NOT EXECUTED here.
2. **Approved OneDrive and second system — BKP-004/006/009/013/014, WIN-006:** organizational authorization, dedicated approved tenant/folder and a separate Windows profile/PC. Use synthetic records, perform interactive OAuth, create/upload/check a snapshot, download/check before restore to an empty data root, compare documents/blobs/memories/chats/vectors/work-record history and settings, then disable networking and query the restored corpus. Original data must remain untouched. NOT EXECUTED here.
3. **Release authorization/signing/clean VM — WIN-001/008/009, QA-011:** user authorization for application EXE creation, vetted full bundle, publisher certificate and clean Windows 11 VM. Keep `Build-Windows.ps1` packaging/publish path unexecuted until authorized. Then verify hash/signature/SBOM, native closure, launch without installed runtimes, rollback and second-PC migration. NOT EXECUTED here.

## Eventual source-only release checklist

Do not package until internally unfinished scope is resolved, full registry/audit acceptance is recorded, native versions/licenses/hashes are reviewed, model/account/second-PC gates pass, signing assets are available, and the user explicitly authorizes EXE creation. A green source branch is reviewable engineering, not an accepted Windows release.
