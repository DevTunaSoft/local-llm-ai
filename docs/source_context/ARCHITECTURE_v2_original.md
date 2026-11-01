# Work Intelligence AI
## Architecture, Product Requirements, Security Design, Implementation Plan, and Release Acceptance Criteria

**Specification:** v2.0 — October 31, 2026  
**Status:** Revised architecture and requirements only; implementation, packaging, actual hardware benchmarks and Windows acceptance testing have not been performed.  
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
- **R-008: Single executable, separate downloaded models.** Ship a Windows x64 self-contained `WorkIntelligence.exe` with all application/runtime dependencies bundled (possibly extracting native helpers to a verified protected runtime directory). LLM weights and their optional vision projector files live separately in user-chosen directories. First-run setup provides an **explicitly authorized** HTTPS model downloader and an offline import option. No Python, Node.js, Ollama, Docker, or separate .NET installation.
- **R-009: Testable release.** Automated tests plus actual Windows acceptance tests must pass; no claim of a complete or defect-free build without executing them.
- **R-010: Governance.** Any organizational data processing, especially protected health information (PHI), patient identifiers, payment information, personnel records, or internal credentials, must be explicitly authorized and handled under applicable employer policies.

## 2. Hardware resource and model strategy

**Selected default:** GPT-OSS 20B MXFP4 GGUF (ggml-org distribution, about 12.1 GB weights on disk). **Selected deep-analysis model:** Qwen3.5 27B Q4_K_M GGUF (about 17.98 GB, specific quantizer edition to be pinned). A smaller compatible 4B–9B GGUF model is recommended as an **optional emergency/low-RAM fallback**, but not required. Many additional supported GGUF models may be imported: there is no hard-coded number or parameter-count ceiling. Only one generation model loaded at a time by default. Final usability depends on actual i7-1370P benchmarks, including RAM, paging, context, temperature and time-to-first-token.

**Engine:** Pinned version of llama.cpp, local CPU AVX2 build and CPU fallback. Evaluate Intel iGPU acceleration via supported backends only if real-machine benchmarks show a reliable improvement with acceptable RAM/driver demands. No claim that integrated GPU is materially faster until tested.

**Context:** Initially 4k–8k tokens for predictable CPU/RAM behavior, with configurable higher limits and per-model context profiles after load testing. The model's advertised maximum context is not a practical target on this laptop. CPU inference and long documents may be slow; use retrieval and task splitting.

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
| Packaging | Single self-contained Windows EXE; optionally a signed installer/wrapper | No user-installed development environment; external models selected/downloaded/imported through first-run wizard |
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
8. **Backup Helper:** separately permissioned component allowed outbound only to the configured OneDrive target, with tightly scoped credentials; no chat or model requests. Runs from signed/verified application components even when the GUI is closed via a user-scoped Windows scheduled task, where policy permits. Can also back up offline without any network access.
9. **Model Download Broker:** separate, **user-initiated only** outbound HTTPS component restricted to allowlisted verified model repositories, manifests and hashes. No inference, documents, conversations or work data are ever sent by this component.
10. **Diagnostics:** integrity probes, resource telemetry, index counts, queue health, backup health, exception categories, reproducible local troubleshooting bundles with sensitive data redaction.

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

**Security Center:** encryption, workspace permissions, source allowlist, approved OneDrive destination and tenant, independent model-download network permission, log review, retention, export controls and explicit authorization status.

**Diagnostics:** one-click local health report; model hash/version, startup issues, inference speed, parser failures, DB integrity checks, indexing coverage, storage capacity, backup status, and repair actions requiring confirmation for destructive steps.

**Backup and Recovery:** back up now, schedule, retention, health/history, check repository, recovery-key readiness, restore preview, dry run, new-machine restore, offline export and recovery.

**Accessibility:** keyboard navigation, readable status text, resizable text, high contrast, accessible progress/report messages; no voice interaction required.

## 7. Security and privacy controls

- **Network boundary:** inference, embedding, retrieval, parsing and memory never contact external AI systems. Two explicit network exceptions are permissioned separately: the backup helper for the approved OneDrive account and the *manual* model-download broker for allowlisted verified model files. Neither sends work prompts, documents, indices or conversations. Local loopback interfaces are authenticated, restricted to the local user and not accessible from LAN.
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

- **Canonical deliverable:** `WorkIntelligence.exe`, self-contained Windows x64. Signed installer is optional deployment convenience; it must not be necessary for dependency setup. No manually installed Python, Node, .NET, Ollama, llama.cpp, restic, or rclone.
- Include pinned .NET 10 runtime, WPF app, local inference runtime, embedding worker, database libraries, document parsers, restic, rclone, diagnostics and required licenses/notices. Use self-contained publish and include native dependencies explicitly.
- Do not require Python, Node.js, Docker, CLI setup, separately installed runtimes, or administrator access during ordinary operation, subject to enterprise installation restrictions. A one-time model **download or local import is required** before inference. Model updates/downloads always require a deliberate user action.
- **LLMs must remain external** to the EXE. On first launch provide: download selected verified GGUF (GPT-OSS 20B or Qwen3.5 27B), import existing model directory, or offline import from a removable drive. Include/resume checks, optional offline file hash manifest, free-space preflight, and optional multimodal projector download. The smaller embedding model, tokenizer and document processors are bundled as executable resources/native helpers, subject to distribution-license verification. Do not suggest the EXE can answer questions before a supported model has been provisioned.
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
- Confirm exact Windows build, CPU instruction set, usable SSD space, power/thermal constraints, Intel graphics support, organizational permission, backup tenant policy and scheduled-task restrictions.
- Run pinned llama.cpp CPU models on real i7-1370P; benchmark **GPT-OSS 20B MXFP4 and Qwen3.5 27B Q4** plus an optional smaller fallback. Measure tokens/sec, time-to-first-token, prompts/sec, peak RAM and page faults, temperature, laptop responsiveness and answer quality on synthetic test tasks. Validate Harmony for GPT-OSS and exact Qwen chat-template/thinking output; test model loads independently of the UI.
- Verify SQLCipher + SQLite FTS5 + sqlite-vec extension loading on Windows x64; determine fallbacks; verify offline PDF/DOCX/XLSX extraction.
- Exit gate: select stable model/runtime and measured resource envelope; documented licensing/security feasibility.

### Phase 1 — Core working desktop app
- Native chat UI, self-contained runtime, external model manager with import/download/verification, model switching/stop and graceful restart; no external AI calls or unauthorized model downloads.
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
- Restic+rclone setup (only if approved), offline snapshot, **Windows scheduled backup when GUI is closed**, missed-run recovery, encrypted snapshots, retention, health, recovery keys, export/import and full restoration wizard.
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
| A-21 | Launch app without LLM files | Model Manager opens, chat clearly unavailable; backup/restore and other non-inference tasks usable |
| A-22 | Download selected model, interrupt twice, resume, tamper with downloaded bytes | Resume safely; reject wrong checksum; do not activate partial model |
| A-23 | Try unapproved GGUF/new architecture | Present compatibility warning, no untrusted code execution, preserve prior working model |
| A-24 | Test GPT-OSS Harmony reasoning vs output and Qwen3.5 chat format | Valid turns and cancellation; reasoning traces not accidentally surfaced as authoritative answer |
| A-25 | Simulate 27B load with 32 GB system already busy | Safe preflight, warnings and refusal/fallback where needed; no prolonged uncontrollable paging |
| A-26 | Close GUI while computer stays on; scheduled backup due | Scheduled encrypted backup completes according to configuration; verifiable status next launch |
| A-27 | Sleep/offline during backup window, then resume | Missed run detected, retried safely, no false success report |
| A-28 | Lose device, recovery credential remains separate | Recover to a clean Windows PC without original DPAPI keys |
| A-29 | Compromise/rotate OneDrive OAuth token | Token revocation/reconnection supported; existing encrypted backups remain encrypted |
| A-30 | Update/reinstall EXE with old DB and divergent backup versions | Signed package/hash checked, migration rollback and backup restore verified |
| A-31 | Import PDF screenshots, scanned documents and complex spreadsheets | Correct OCR/image fallback, table/cell provenance, explicit extraction warnings |
| A-32 | Process huge document and chat history exceeding model context | Source-aware retrieval and context summarization, no silent truncation of crucial evidence |
| A-33 | Try document containing secrets, malicious URLs and prompt injection | Secrets prevented from indexing by policy; no unauthorized network/actions |
| A-34 | Delete a memory/document with derived embeddings, caches and backup copies | Current data and derived data purged or tombstoned; exact backup-retention limits disclosed |
| A-35 | Bundled native helper executable tampered in extraction directory | Launch blocked; hash/signature check and repair required |
| A-36 | Verify app and subprocess network while models downloaded and after | Only approved manually invoked downloader, or approved backup, connect externally; AI content never leaves device |
| A-37 | Run same prompt sequentially through both selected models | Independent reproducible configuration, citations and response comparison; no simultaneous overcommit |
| A-38 | Revoke folder/source authorization | All retrieval respects scope before generation; knowledge from revoked source no longer shown |
| A-39 | Laptop remains asleep/off for several days | No claim of successful daily backup; on next eligible run perform catch-up and alert about recovery point |

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

**Architecture preferred:** one self-contained WPF/.NET 10 `WorkIntelligence.exe`; GPT-OSS 20B MXFP4 default and Qwen3.5 27B Q4 deep analysis with user-selected external model directories; pinned llama.cpp model worker; encrypted SQLite/FTS and local vector retrieval; approved long-term memory; encrypted restic snapshots via rclone into authorized OneDrive; separate offline recovery pack; CPU-first hardware baseline.

**Critical decisions pending measurement rather than guesswork:** available SSD capacity, actual CPU-only 20B/27B throughput and peak memory, multimodal projector compatibility, verified SQLCipher/vector-extension compatibility, backup tenant/company approval, Task Scheduler policy and service-account behavior, latency/thermal benchmark, code signing, and company deployment policy.

**Implementation status:** architecture defined. No complete application, installation package or Windows run/test evidence exists as a result of this specification alone.


## 16. v2 gap remediation: additional non-negotiable requirements

The following items add to R-001 through R-010 and reconcile model and packaging choices made after v1.

- **R-011: Model registry and compatibility profiles.** Each locally installed model has manifest metadata: publisher/repository, immutable revision and GGUF checksum, quantization, tokenizer, architecture, license, context cap, validated inference-engine version, chat template, reasoning delimiters, tool-call capability and optional vision projector. A GGUF file merely opening is *not* sufficient proof of compatibility. GPT-OSS must use its Harmony format. Model-specific settings are versioned and can be reset. Import from an arbitrary path is permitted only after format and compatibility validation.
- **R-012: Separate model files and managed download.** Bundled EXE must supply the full GUI, engine, embedding runtime, parsers, encryption, diagnostics and backup helpers. Large LLM `.gguf` files live in user-selected directories. Explicit opt-in download only, with source/revision validation, resume, size and disk checks, SHA-256 verification, partial-download quarantine and atomic activation. Offline import remains fully supported; network availability is not needed during normal inference.
- **R-013: Correct network split.** Model Download Broker is not the model inference process, and Backup Broker is not the document/memory process. Different credentials, scoped egress rules, auditing and user controls. No general "online mode" for the LLM, web search, tool calls or prompt transmission.
- **R-014: Resource guardrails.** Before starting 20B or 27B, estimate peak committed memory including model weights, KV cache, native runtime, system reserved RAM and other active apps. Offer context reduction, unload a prior model, suspend indexing, or refuse loading gracefully. Expose time-to-first-token and token/sec after measured benchmarking; never promise arbitrary model speed. One big model resident at a time; comparison sequential by default.
- **R-015: Chat reproducibility.** Record model revision/checksum, runtime revision, per-model prompt-template revision, inference settings, context size, retrieval query and cited source versions for each result. Maintain conversation context via bounded window with user-visible summarization/retrieval; never silently treat truncated context as authoritative. Clearly distinguish model reasoning artifacts from user-visible answers.
- **R-016: Structured task capabilities.** Add optional locally validated structured outputs and action-item extraction; model output must be schema-validated, never executed as an instruction to delete or mutate files. Any future tool execution is sandboxed and requires explicit permissions/confirmation. Zero automatic production-system modification in v1.
- **R-017: Background backups.** Register a per-user Windows Task Scheduler invocation, where allowed, which runs a *bundled signed/verified application background mode* without opening chat UI. Backup only runs when the PC is on/available with required authorization. Detect missed runs after sleep/offline, retry with jitter/backoff, show last **verified off-device** backup, and do not claim a successful backup if transfer or verification fails. Provide manual/fallback backup for restricted devices. Avoid storing recovery passphrase in the OneDrive repository or plaintext task arguments.
- **R-018: Threat-modelled encryption and keys.** Document encryption key hierarchy, recovery key versus unattended-backup credential handling, rotation, OS credential storage, protected temporary working files, memory dumps, unencrypted logs, Windows search indexing, and BitLocker requirement/recommendation. Handle app data + DB + embeddings + FTS + attachments + exported output and backup staging; test actual at-rest protection. Recovery credentials must be portable and separately secured, with rehearsal of new-machine restore.
- **R-019: Immutable/recoverable updates.** Verify EXE and embedded native helpers, pin versions/hashes and release signature where possible, maintain SBOM. Native helpers extracted to protected per-user paths with ACL checks and integrity validation before launch. Stage app/database migrations with rollback. Atomic or validated upgrade, no silent data overwrite.
- **R-020: Ingestion quality.** Include local OCR for scanned PDF/images, rich-text handling, presentation files (PPTX), tables and spreadsheet formulas/values, logs, and optional caption/transcript formats (SRT/VTT). The OCR implementation and language packs must be bundled into the EXE or explicitly installed as **optional external model assets** with user consent; no runtime dependency downloads. Detect password-protected, encrypted, garbled and corrupt files; do not fabricate extraction success. Preserve page/slide/sheet/cell-level references where available.
- **R-021: Multimodal correctness.** Qwen3.5 27B has vision-capable variants, but a selected GGUF may be text-only without an appropriate separate multimodal projector. Detect and mark vision capabilities honestly; download/import verified projector when required and compatible. Provide local OCR independently, so scanned text remains available even when the active chat model has no vision processing.
- **R-022: Evaluation and QA datasets.** Build a synthetic, non-company-sensitive golden set covering exact fact retrieval, policy supersession, conflicting sources, citations, numeric spreadsheet interpretation, abstention, search recall, model switching, prompt injection and restoration. Record pass/fail metrics per model, parser and software revision. Repeat after changes; report unresolved risks instead of promising 100% accuracy.
- **R-023: Full lifecycle and deletion.** Define which imported items are copies versus watched references, source/backup retention, compaction, revocation, purge from current DB/FTS/vector index/derived caches, and scheduled expiration of encrypted backup copies. Backups cannot magically forget data still present in older retained snapshots. Include export into standard formats and a safe uninstall leaving an explicit choice to retain or wipe the encrypted data vault.
- **R-024: Enterprise controls.** Before importing internal work data confirm employer authority, data class permitted on local device and specifically on chosen OneDrive tenant, backup retention, allowed local processing, and software redistribution permissions. Default deny PHI, credentials and regulated content. Key material and OneDrive OAuth refresh tokens are stored outside model-accessible knowledge; enforce least privilege, Windows account access checks and tamper-evident local audit logs.
- **R-025: Accessibility and resilient UX.** Keyboard-first navigation, text status/progress, scalable typography, accessible errors and diagnostics. Progress indicators must distinguish "importing", "extracting", "indexing", "retrieving", "generating", "uploading", "verified", and "failed". Cancel operations safely; avoid showing a completed checkmark for queued or partial work.
- **R-026: Observability without leaks.** Local diagnostics include queued-job state, worker health and memory, model load and cache pressure, OCR/extraction failures, retrieval traces without secrets, backup status and storage totals. Opt-in troubleshooting package with explicit redaction preview. No telemetry or crash-report upload by default.

## 17. First-run UX and data directories (v2)

1. Launch `WorkIntelligence.exe`; verify signed/hashed embedded native components and extract to a protected application-specific runtime directory if necessary. An app that unpacks a helper is still distributed as one EXE; after first run, several internal files will exist on disk.
2. Run offline-first health check and check available SSD/RAM. Do not initiate internet access automatically.
3. Choose **Download verified model** (separate network consent, select source/model/revision and install path), **Import local GGUF** (zero network), or **Restore from backup**. GPT-OSS 20B MXFP4 and Qwen3.5 27B Q4_K_M are curated recommendations, not a hard parameter limit.
4. Create/unlock protected local data vault and recovery passphrase; choose authorized source folders and confirm appropriate company data use.
5. Optionally authorize the specific approved OneDrive target via Microsoft OAuth; configure unattended backup credential handling and schedule. Offer encrypted external-drive backup without a Microsoft account.
6. Present dashboard with model installed/loaded, offline inference readiness, last verified backup, scope and current ingestion status. Users can inspect the knowledge library and restore prior backups even without an LLM installed.

Suggested per-user storage roots (actual path resolves to logged-in Windows user; not fixed username):

```text
%LOCALAPPDATA%\WorkIntelligence\Data\          encrypted DB, blobs, audit/events, safe staging
%LOCALAPPDATA%\WorkIntelligence\Models\        default user-selectable GGUF/model packs
%LOCALAPPDATA%\WorkIntelligence\Runtime\       verified extracted native helpers
%LOCALAPPDATA%\WorkIntelligence\Cache\         controlled transient cache (protected/purged)
%LOCALAPPDATA%\WorkIntelligence\Logs\          minimal redacted operational logs
```

Do **not** store a live SQLite database inside OneDrive's synced directory or in a shared writable runtime extraction folder. If the app is run from a removable drive, recover data-root configuration by explicit user selection rather than assuming proximity to `WorkIntelligence.exe`.

## 18. Scope decisions and dependencies (v2)

**Launch-critical:** core chat, selected models, model download/import, secure native runtime, encrypted store, authorized ingestion and OCR, retrieval/citations, approved memories, usability and diagnostics, scheduled encrypted backup/restore, single EXE with external models, offline testing, failure recovery and source export.

**Optional after stable launch:** model auto-router (once accuracy tested), benchmarking dashboard, sequential answer comparison, optional multimodal vision projector and image-aware Qwen input, optional fine-tuning on other hardware, integration with employer-approved systems, advanced document authoring, transcription import improvements. We may include comparison UI in v1 if low-risk, but it is never a precondition for safe backup and accurate retrieval.

**Explicitly excluded from v1:** autonomous writes to Traumasoft or other production platforms, unattended browsing, automatic access to work email, cloud AI processing, automatic ingest of personal chat history, distributed live multi-PC writes, unrestricted arbitrary code execution, and model-weight training on the laptop.

**Important design tradeoff:** "Single EXE except LLM" can mean a single **distributed executable**, not a single in-memory file with no runtime extraction or persistent storage. Native helpers and small embedding/OCR resources may be embedded and extracted to an integrity-checked private folder. Do not promise a true zero-extraction binary unless the complete dependency set is proven to support that. A single EXE also does not guarantee antivirus trust without code signing and enterprise deployment approval.

## 19. Additional references for v2 decisions (checked October 31, 2026)

- GPT-OSS model card / Harmony requirements: https://huggingface.co/openai/gpt-oss-20b
- llama.cpp-maintained 20B MXFP4 GGUF: https://huggingface.co/ggml-org/gpt-oss-20b-GGUF
- Qwen3.5 27B model card (includes vision architecture): https://huggingface.co/Qwen/Qwen3.5-27B
- Qwen3.5 27B Q4 GGUF quantization manifest: https://huggingface.co/bartowski/Qwen_Qwen3.5-27B-GGUF
- llama.cpp tool/format support: https://github.com/ggml-org/llama.cpp/blob/master/docs/function-calling.md
- llama.cpp multimodal projector: https://github.com/ggml-org/llama.cpp/blob/master/docs/multimodal.md
- .NET single-file/native extraction limitations: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Rclone OneDrive configuration and OAuth: https://rclone.org/onedrive/
- Restic integrity checks and repair: https://restic.readthedocs.io/en/stable/077_troubleshooting.html
