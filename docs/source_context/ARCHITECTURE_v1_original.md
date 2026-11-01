# Work Intelligence AI
## Architecture, Product Requirements, Security Design, Implementation Plan, and Release Acceptance Criteria

**Specification:** v1.0 — October 31, 2026  
**Status:** Architecture proposal; implementation and Windows acceptance testing have not yet been performed.  
**Target machine:** Windows 11 Pro x64, Intel Core i7-1370P (14 cores/20 threads), 32 GB RAM, Intel integrated graphics.  
**Primary aim:** A fully local, work-only desktop intelligence application whose knowledge can be backed up in encrypted form to OneDrive, independently exported, and recovered on another authorized Windows PC.

## 1. Non-negotiable requirements

- **R-001: Offline inference.** Every model inference, embedding calculation, retrieval, document parsing operation, and memory update works without internet access after authorized installation.
- **R-002: Explicit boundary.** No work document, user prompt, search index, memory, or model request can be sent to an external AI provider. Default network-deny for the intelligence subsystem.
- **R-003: Work-only knowledge.** Only user-selected, authorized work folders/files and approved corrections can become knowledge. Personal chat history is not imported automatically.
- **R-004: Portable recovery.** Restore the knowledge, approved memories, records, document references, and settings onto a replacement PC; do not bind the only readable copy of data to one computer or Windows account.
- **R-005: Independently encrypted backup.** Encrypt before sending to OneDrive; do not rely solely on OneDrive's server-side encryption. OneDrive storage is backup only.
- **R-006: Source-grounded answers.** Every claim sourced from company material links back to document version and page, section, or spreadsheet location when possible. Unsupported claims are labeled as such.
- **R-007: Controlled memory.** New authoritative facts and corrections have provenance, scope, review status, and version history; an LLM cannot silently update official policy.
- **R-008: Self-contained Windows package.** An authorized end user can launch the app without separately installing .NET, Python, Node, llama.cpp, or dependencies. Installer bundles pinned app/runtime libraries; large model assets are bundled in an offline model pack or pre-provisioned, with no runtime cloud downloads.
- **R-009: Testable release.** Automated tests plus actual Windows acceptance tests must pass; no claim of a complete or defect-free build without executing them.
- **R-010: Governance.** Any organizational data processing, especially protected health information (PHI), patient identifiers, payment information, personnel records, or internal credentials, must be explicitly authorized and handled under applicable employer policies.

## 2. Hardware resource and model strategy

**Primary baseline:** Qwen3-8B GGUF Q4_K_M (about 5.03 GB on disk). **Alternate evaluation:** Qwen3.5-9B Q4 GGUF (about 5–6+ GB, varies by publisher) and Qwen3-14B Q4_K_M (about 9 GB). Only one generation model loaded at a time by default. A final selection is contingent on benchmark results on the actual i7-1370P machine.

**Engine:** Pinned version of llama.cpp, local CPU AVX2 build and CPU fallback. Evaluate Intel iGPU acceleration via supported backends only if real-machine benchmarks show a reliable improvement with acceptable RAM/driver demands. No claim that integrated GPU is materially faster until tested.

**Context:** Initially 4k–8k tokens for predictable CPU/RAM behavior, with configurable higher limits after load testing. The model's advertised maximum context is not a practical target on this laptop. CPU inference and long documents may be slow; use retrieval and task splitting.

**Embedding model:** BAAI/bge-small-en-v1.5, 384-dimensional, CPU ONNX inference, pinned with local tokenizer. Later allow model changes with full vector-index rebuild; original documents and human-approved memory survive.

**Resource governor:** one active text-generation job; concurrent ingestion throttled while answering; memory and queue monitoring; configurable CPU threads and context; cancel/pause; low-disk and battery-aware scheduling; explicit per-component health and timeouts.

**Storage:** prefer SSD with at least 60–100 GB free for application, models, indexed documents, backups/staging and future growth; actual capacity and room still need to be measured. Track size and enforce quotas/alerts. Quantized weights size is not the entire runtime RAM cost.

## 3. Technology stack

| Layer | Chosen baseline | Notes |
|---|---|---|
| Desktop UI | WPF, C# on .NET 10 LTS | Windows-native, accessible controls, self-contained x64 publish |
| Domain and orchestration | C# libraries, async/cancelable jobs | Strong separation from UI and models |
| Local inference | Pinned llama.cpp | Bundled binaries, loopback-only authenticated endpoint or direct library binding; never public bind |
| Document embeddings | BGE-small-en-v1.5 + pinned ONNX Runtime CPU | Independent of the generation LLM |
| Metadata/knowledge | SQLCipher SQLite with FTS5 | Encrypted local structured database, durable WAL |
| Vector retrieval | sqlite-vec compatibility pilot | If unsatisfactory, separate rebuildable and encrypted index; no unencrypted document vectors at rest |
| File storage | Content-addressed encrypted files | Authenticated encryption, checksums, version tracking |
| Export | JSON/JSONL, Markdown, CSV, original source files | Documented schema and migrations |
| Backup | Pinned restic + bundled rclone | Restic client-side encrypted repository; OneDrive via rclone; offline removable-drive alternative |
| Packaging | Self-contained Windows installer + portable offline model pack | No user-installed development environment |
| Monitoring | Local health events and structured logs | No telemetry upload; redact sensitive data |

Technology choices are candidates until integration and licensing checks pass. Record licenses, notices, SHA-256 artifact hashes, and software bill of materials (SBOM).

## 4. Logical components and boundaries

1. **WPF Desktop Shell:** dashboard, chat, knowledge library, memory editor, decisions/projects, document analysis, model management, diagnostics, backup/restore, settings, accessibility.
2. **Orchestrator:** all tasks run through a finite-state, cancelable queue; requests carry scopes and authorization; model activity does not have arbitrary filesystem/network access.
3. **Inference Worker:** only approved local model files; no automatic HTTP retrieval; loopback + ephemeral token if a localhost server is needed; process lifecycle watchdog, maximum memory/context, timeout, graceful shutdown.
4. **Ingestion Worker:** allowlisted paths; file-type/size limits; parser isolation; text extraction and document fingerprints; local OCR for scanned PDFs as an explicitly bundled optional capability.
5. **Retrieval Engine:** hybrid lexical FTS5 + vector search; deterministic ranking, deduplication, optional rerank after benchmark; source sections and page numbers preserved; top-k limits and token budget.
6. **Memory Engine:** proposed facts and decisions, user approval, confidence and provenance, versioned supersession, contradiction detection, retention/removal, audit history.
7. **Data Vault:** SQLCipher DB plus encrypted file blobs. Unencrypted temp and crash artifacts are prohibited; Windows disk encryption also recommended to protect swap and other OS-managed plaintext exposure.
8. **Backup Helper:** the only component normally allowed outbound to the configured OneDrive target, with tightly scoped credentials; no chat or model requests. Can also back up offline without any network access.
9. **Diagnostics:** integrity probes, resource telemetry, index counts, queue health, backup health, exception categories, reproducible local troubleshooting bundles with sensitive data redaction.

## 5. Knowledge and memory model

Records include: `document`, `document_version`, `source_location`, `chunk`, `embedding_manifest`, `topic`, `system`, `project`, `incident`, `decision`, `procedure`, `policy`, `memory_candidate`, `approved_memory`, `conversation`, `citation`, `ingestion_job`, `audit_event`, `backup_manifest`, and `migration_version`.

Every factual memory contains: text/structured fact; category; workspace/visibility scope; origin and exact source reference; approval state (suggested, approved, disputed, rejected, superseded); author and approval time; created/updated timestamps; expiry/review date; priority; version; conflict links. Company policies always require explicit approval. Conversation messages are not accepted as policies by default.

### Learning pipeline

1. User authorizes a source directory/file or enters a fact.
2. File scanner fingerprints and classifies the input; reject unknown/unsafe format and oversized files.
3. Parser extracts text, tables, headings, dates, source identifiers and file version.
4. Worker chunks material with overlap and stable source references, stores content encrypted, computes local embeddings, updates hybrid indexes.
5. Search returns candidates and citation metadata; answering model receives only relevant, authorized excerpts.
6. App distinguishes a sourced answer from a suggestion; user corrections become memory proposals rather than silent canonical rewrites.
7. User approves or rejects proposed memory; conflicts require review; prior versions remain auditable until retention/deletion rules require removal.
8. Re-index on changed content; retain document revisions and support rollback; mark stale facts when the original authority has changed.

Do not describe this as continuous model-weight training: the initial release uses RAG + approved persistent memory. True fine-tuning on this CPU-only laptop is an optional future research project, not a phase-one feature.

### Retrieval trust rules

- Documents and retrieved text are *untrusted input*, never instructions to override system safety or exfiltrate content.
- Answers preserve citations and provide an explicit no-answer/uncertainty outcome where source support is missing.
- Workspace and document access filters are applied before retrieval, not only after generation.
- Deleted or revoked documents are removed from current retrieval and embeddings; recoverable backup retention must be separately disclosed and governed.
- Test adversarial prompts embedded inside PDFs and spreadsheets.

## 6. Product functions and user experience

**Dashboard:** model installed/loaded, local/offline status, knowledge counts, recent workspaces, failed ingestion, backups and alerts.

**Chat:** new/rename/delete conversation, streaming answers, stop/retry, save/print/export, cite sources, select workspace, model mode selector, find related prior decisions, optional chat retention policies. Clearly label generated statements vs cited evidence.

**Knowledge Library:** drop files and folders; watch approved folders; manual/automatic scan; import progress, duplicates, versions, metadata, tags, search preview, delete/unlearn, reindex. Support PDF, DOCX, XLSX, CSV, TXT, Markdown, JSON, and logs initially; avoid Office macro execution. Scanned images/PDFs need local OCR support to be explicitly validated.

**Memory & Policies:** view/edit/approve/reject memory suggestions; classify facts vs procedures vs preferences; source authority hierarchy; contradiction and supersession handling; targeted deletion and export; canonical work vocabulary.

**Project Intelligence:** project workspaces, timelines, decisions, open risks, tasks, root-cause reports, known issues, and change logs. No autonomous writes to business platforms in initial release.

**Document Studio:** summarize, compare versions, extract checklists, create procedure drafts, review technical logs, show spreadsheet row/cell source lineage when possible, export DOCX/PDF where locally supported; never quietly overwrite originals.

**Search:** exact keyword, natural-language semantic, filters by date/system/project/author/source status, citation preview. Relevance and false-positive testing with a labeled corpus.

**Security Center:** encryption, workspace permissions, source allowlist, backup scope, network state, log review, retention, and export controls.

**Diagnostics:** one-click local health report; model hash/version, startup issues, inference speed, parser failures, DB integrity checks, indexing coverage, storage capacity, backup status, and repair actions requiring confirmation for destructive steps.

**Backup and Recovery:** back up now, schedule, retention, health/history, check repository, recovery-key readiness, restore preview, dry run, new-machine restore, offline export and recovery.

**Accessibility:** keyboard navigation, readable status text, resizable text, high contrast, accessible progress/report messages; no voice interaction required.

## 7. Security and privacy controls

- **Network boundary:** the AI application and workers never contact external AI systems; optional backup helper explicitly permitted to reach Microsoft OneDrive via rclone authorization. Local loopback interfaces are authenticated and not accessible from LAN.
- **Encryption:** encrypted SQLCipher datastore at rest; content-addressed files authenticated-encrypted; restic encrypts backup content before uploading. Store cloud tokens/keys in OS-protected vault; portable recovery secret or passphrase kept outside the machine. Avoid machine-only DPAPI protection for the sole portable backup key.
- **Windows protection:** BitLocker/device encryption, screen lock and local Windows user boundary. Be explicit that BitLocker also helps with temporary files, swap and crash artifacts not covered by database encryption.
- **Authorization:** no bulk ingestion of employee information, patient records, internal credentials, or fleet/customer data without proper company authorization and defined classification, access, retention and audit controls.
- **Secrets:** never index passwords, access tokens, API keys, private keys or recovery phrases; implement detection/redaction and manual review.
- **External content:** malicious documents may contain instructions; parsers run with restricted privileges and resource quotas; model cannot invoke arbitrary shell commands, network requests or upload tools.
- **Updates:** signed/pinned release packages, dependency vulnerability scan, SBOM and license scan; no auto-update network calls unless user or IT authorizes and a safe channel is configured.
- **Data lifecycle:** purge controls for original documents, extracted text, vectors, memory, chats, logs, and cached exports; clearly state the impact of snapshot retention and cloud deletion.

## 8. Backup, encryption, restoration

### Backup workflow

1. Pause/co-ordinate writes and capture an application-consistent view of file manifests plus SQLCipher database using the SQLite Online Backup API (or equivalent validated encrypted snapshot mechanism). A raw copy of a live `.db` alone is prohibited.
2. Build a versioned backup staging manifest containing schema version, application version, model/embedding manifests, file hashes, source attachment versions, authorized memory, settings, local export and audit information; never include plaintext credentials.
3. Restic takes a deduplicated, client-side encrypted snapshot of the consistent staging content. Store in a configured OneDrive directory by `restic` + `rclone` backend, not by relying on simultaneous OneDrive sync of live SQLite files.
4. Verify command exit status, snapshot presence, repository checks, and periodic sample data blocks. Report 'successful' only after upload and validation; a successful local snapshot is not a successful cloud backup.
5. Enforce configurable snapshot retention; add an independent encrypted offline copy (external drive) for resilience against account loss, ransomware, cloud deletion and backup credentials compromise.

**Default proposed schedule:** daily backup, weekly repository metadata check, rotating data checks, monthly test restore into a disposable location and quarterly new-PC drill. Example retention: 30 daily, 12 monthly, 3 yearly, subject to company policy and actual backup size.

### Recovery workflow

1. On a clean authorized Windows x64 PC, install the self-contained application and required locally packaged model(s).
2. Authenticate to approved OneDrive account (or plug in encrypted external backup), supply portable recovery password/key, list snapshots without restoring over active data.
3. Choose snapshot and verify integrity; restore to staging and check hashes, schema, origin, and policies.
4. Stop workers, take safety snapshot of destination, atomically move validated records to the new app location, perform forward schema migrations and rebuild embeddings if models changed.
5. Launch offline and validate document count, approved memory, conversation history, source references and representative queries. Complete a test question for every major workspace.
6. Require explicit confirmation before replacing existing data. Warn on conflicting/divergent computer histories; do not silently merge divergent memories.

**Recovery credentials:** protect the restic repository password and separately export a recovery code and recovery instructions. If all recovery credentials are lost, client-side encryption deliberately makes backups unreadable.

**RPO goal:** at most 24 hours if daily backup has completed; **RTO goal:** restore workflow straightforward on a suitable replacement PC, with elapsed time dependent on backup size, internet and model availability. These are goals, not demonstrated service levels.

## 9. Packaging and distribution

- Windows x64 signed installer + a portable self-contained application distribution for authorized users.
- Include pinned .NET 10 runtime, WPF app, local inference runtime, embedding worker, database libraries, document parsers, restic, rclone, diagnostics and required licenses/notices. Use self-contained publish and include native dependencies explicitly.
- Do not require Python, Node.js, docker, CLI setup, model downloads, or administrator access during normal operation, subject to enterprise installation restrictions.
- Provide an **offline model pack**: at minimum Qwen3 8B quantized weights, embedder, tokenizers, checksum manifest; optionally Qwen3.5 9B and Qwen3 14B. An installer containing all model weights will be multiple gigabytes; don't falsely market a tiny executable as including them.
- Keep app binaries immutable and app data separate; upgrades are transactional, preserve data, and support rollback. Version and verify the model runtime, GGUF, tokenizer, embedding model, DB schema and backup format independently.
- Treat new-PC restore, not just executable copying, as the portability test.

## 10. Proposed directory structure

```text
WorkIntelligenceAI/
  src/
    WorkIntelligence.Desktop/      # WPF GUI
    WorkIntelligence.Core/         # domain types, application services
    WorkIntelligence.Inference/    # llama.cpp host/bridge
    WorkIntelligence.Ingestion/    # parsers, watchers, OCR
    WorkIntelligence.Retrieval/    # lexical/vector search and citation grounding
    WorkIntelligence.Memory/       # approvals, conflict resolution and decisions
    WorkIntelligence.Storage/      # SQLCipher and encrypted objects
    WorkIntelligence.Security/     # data policy and local key management
    WorkIntelligence.Backup/       # restic/rclone adapter and recovery
    WorkIntelligence.Diagnostics/  # health, watchdog, local logs
  tests/
    Unit/ Integration/ Security/ FaultInjection/ Regression/ WindowsAcceptance/
  runtime/                       # bundled pinned native binaries
  models/                        # separately provisionable offline model packs
  deployment/                    # installer, signing and release manifests
  docs/                          # operating guide, recovery guide, threat model
```

**Data** resides in a protected per-user Windows application data directory, separate from source/app binaries and separate from the live OneDrive sync folder.

## 11. Implementation order and release gates

### Phase 0 — Feasibility and measured baselines
- Confirm exact Windows build, CPU instruction set, usable SSD space, power/thermal constraints, Intel graphics support, organizational permission and backup tenant policy.
- Run pinned llama.cpp CPU model on real i7-1370P; benchmark Qwen3 8B and Qwen3.5 9B (optionally Qwen3 14B) for tokens/sec, time-to-first-token, prompts/sec, RAM peak, temperature, laptop responsiveness and accuracy on representative company-free synthetic tasks.
- Verify SQLCipher + SQLite FTS5 + sqlite-vec extension loading on Windows x64; determine fallbacks; verify offline PDF/DOCX/XLSX extraction.
- Exit gate: select stable model/runtime and measured resource envelope; documented licensing/security feasibility.

### Phase 1 — Core working desktop app
- Native chat UI, self-contained runtime, model switching/stop and graceful restart; no external AI calls.
- Encrypted local data vault and structured conversation database; local health/readiness and crash recovery.
- Exit gate: installed Windows app works without Python/Node/Docker, without internet; cold start, 100-round chat stability and restart persistence tests passed.

### Phase 2 — Knowledge ingestion and source-grounded answers
- Approved-file import, watcher, PDF/DOCX/XLSX/CSV/TXT/Markdown/JSON/logs; versioning; local embeddings, hybrid search, citation previews.
- Duplicate avoidance, metadata validation, limited re-index, unlearn/delete, malicious text and parser-fault hardening.
- Exit gate: source answers verifiably grounded with citations; no unauthorized file ingestion; tests against corpus of supplied synthetic documents.

### Phase 3 — Memory and work intelligence
- Proposed-and-approved memories, decision history, project workspaces, policy authority hierarchy, conflict detection, review queue and audit history.
- Technical incident analysis, compare changes, output export, stored work context.
- Exit gate: approved memory survives restart/model swap; rejected correction does not leak into answers; conflicting and revoked memories handled correctly.

### Phase 4 — Encrypted OneDrive backup and migration
- Restic+rclone setup (only if approved), offline snapshot, scheduled encrypted backup, retention, health, recovery keys, export/import and full restoration wizard.
- Exit gate: restore onto a second Windows PC from OneDrive, then disconnect internet and successfully query restored knowledge and memories. Corrupt repository, bad password and interruption are correctly reported.

### Phase 5 — Hardening, diagnostics, installer and release
- Full static/dependency/license scans, unit/integration/fault injection/security/privacy/performance/Windows UI tests; repair and repeat.
- Clean Windows 11 Pro install test, interrupted update and rollback, malformed file imports, extreme memory pressure, disk full, sudden power loss and simulated network outage.
- Exit gate: all mandatory acceptance tests documented with evidence, release notes, SHA-256 hashes, SBOM, signed installer if signing infrastructure available, operational/recovery manual. No outstanding critical/high-severity defects or misleading claims of complete functionality.

## 12. Test matrix and specific acceptance criteria

| Test ID | Scenario | Expected result |
|---|---|---|
| A-01 | Completely disconnect PC from network; chat/ingest/query | Full intelligence path works, no external dependency |
| A-02 | Inspect outbound connections during AI operation | No external AI/API requests; network denied by default |
| A-03 | Import known work-free test docs with precise facts | Exact citations to correct file/version/location; abstain for absent facts |
| A-04 | Modify source document | Old and new versions tracked; retrieval uses authorized latest version |
| A-05 | Approve/reject/correct memory | Only approved and current memories affect canonical answers |
| A-06 | Remove source and indexes | No longer retrievable; retention in backups clearly explained |
| A-07 | Restart, crash, shutdown, re-open | Consistent encrypted database; worker recovery without lost committed data |
| A-08 | Try prompt injection embedded in source file | Source cannot override policy or perform external action |
| A-09 | Read app storage after closing without key | Protected work content cannot be read from database/object files |
| A-10 | Backup while DB is receiving updates | Snapshot is consistent, complete, encrypted and restorable |
| A-11 | Interrupt OneDrive transfer | Backup failure is explicit; prior snapshots remain usable |
| A-12 | Restore onto new authorized Windows PC | Documents, decisions, memory, settings and chats recovered and usable offline |
| A-13 | Model/embedding upgrade | Knowledge survives; embedding index re-created and search still works |
| A-14 | Damage backup data or supply wrong key | Integrity/authentication failure reported; do not claim success |
| A-15 | Fill disk, use huge/malformed file, deny permissions | Graceful handling, correct diagnostics, no corrupt database |
| A-16 | Installer runs on clean Windows 11 x64 | No manual installation of developer runtimes/tools |
| A-17 | Resource-heavy import plus generation | Responsive interface; throttling, cancellation, no unbounded memory use |
| A-18 | PII/PHI-like synthetic test document | Classification and prevention mechanisms behave per approved company policy |
| A-19 | Restore older DB schema to updated app | Safe migration and rollback or explicit incompatibility message |
| A-20 | Simulate conflicting histories from two PCs | Explicit conflict resolution, never silently overwrite newer approved knowledge |

**QA practice:** inventory requirements before coding; unit + integration tests every change; run linters and static/security scanners; fix failures and rerun dependent suites; add regression test for each defect; execute fault injection and independent review; then perform real Windows acceptance tests. A successful compile alone is not release proof.

## 13. Deferred and explicitly excluded work

- **Not in v1:** heavyweight model training on the laptop; unattended autonomous changes to company systems; web browsing by the LLM; automatic access to inboxes, production databases, employee/clinical systems; multi-PC live concurrent sync; cloud AI fallback; unapproved remote monitoring.
- **Possible future modules:** local transcription with permission/consent, deep technical log analysis, connectors to explicitly authorized company platforms, audited and reviewed automation, multiuser deployment with role-based access, synchronized editing with conflict resolution, local multimodal OCR/vision.
- **Important:** Backup synchronization is **not** a distributed live database or automated two-way knowledge merge.

## 14. Reference sources reviewed October 31, 2026

- llama.cpp: https://github.com/ggml-org/llama.cpp and https://github.com/ggml-org/llama.cpp/blob/master/docs/build.md
- Qwen3 8B official GGUF: https://huggingface.co/Qwen/Qwen3-8B-GGUF
- Qwen3 14B official GGUF: https://huggingface.co/Qwen/Qwen3-14B-GGUF
- Qwen3.5 9B family: https://huggingface.co/Qwen/Qwen3.5-9B
- BGE small embedding: https://huggingface.co/BAAI/bge-small-en-v1.5
- SQLCipher: https://github.com/sqlcipher/sqlcipher
- sqlite-vec: https://github.com/asg017/sqlite-vec
- SQLite online backup: https://sqlite.org/backup.html
- restic backend setup: https://restic.readthedocs.io/en/stable/030_preparing_a_new_repo.html
- restic integrity checks: https://restic.readthedocs.io/en/stable/045_working_with_repos.html
- rclone OneDrive: https://rclone.org/onedrive/
- .NET self-contained single-file: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Microsoft .NET support lifecycle: https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core

## 15. Decision record

**Architecture preferred:** WPF/.NET 10 local desktop host; pinned llama.cpp model worker; encrypted SQLite/FTS and local vector retrieval; approved long-term memory; encrypted restic snapshots via rclone into authorized OneDrive; separate offline recovery pack; CPU-first hardware baseline.

**Critical decisions pending measurement rather than guesswork:** exact installed GPU/SSD and available capacity; model choice between Qwen3 8B and newer Qwen3.5 9B; verified SQLCipher/vector-extension compatibility; backup policy authorization; actual latency/thermal benchmark; release signing and company deployment policy.

**Implementation status:** architecture defined. No complete application, installation package or Windows run/test evidence exists as a result of this specification alone.
