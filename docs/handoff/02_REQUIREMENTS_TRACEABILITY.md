# Requirements traceability — authoritative reconciliation register

**106 uniquely identified requirements** across 9 workstreams.

> SOURCE STATUS IS AN ESTIMATE FROM SOURCE INSPECTION, NOT PROOF OF OPERATION. Codex must update each row with real code references and executed test evidence. Historical source-check counts cannot be substituted for acceptance tests.

| ID | Requirement | Baseline source status | Starting code reference | Required acceptance |
|---|---|---|---|---|
| WIN-001 | Single self-contained Windows x64 WorkIntelligence.exe including .NET and all operational native tools | scaffold | WorkIntelligence.csproj; scripts/Build-Windows.ps1 | Build and launch one EXE on clean Win11 without developer prerequisites. |
| WIN-002 | Store large LLM GGUF files outside executable in user-selectable folders | partial | Services/LocalPaths.cs; ModelDownloader.cs | First run choose import/download destination and recover from a new path. |
| WIN-003 | Store encrypted data, documents, caches and settings external to EXE and separate from OneDrive live sync | scaffold | Services/LocalPaths.cs; KnowledgeStore.cs | Upgrade EXE while retaining data without live DB sync conflicts. |
| WIN-004 | Bundle llama.cpp, SQLCipher, PDF renderer, Tesseract, restic, rclone, OCR assets and embedding runtime without separate install | blocked | scripts/Build-Windows.ps1; vendor/README.md | All dependencies signed/pinned/hash-verified and packaged correctly. |
| WIN-005 | First-run wizard configures vault, directories, model, knowledge, authorized backup | partial | MainWindow.xaml | End-to-end GUI walkthrough on clean Windows. |
| WIN-006 | Portable restore to replacement Windows machine without losing knowledge | blocked | BackupService.cs; RemoteBackupService.cs | Second machine restore uses backup password and works offline. |
| WIN-007 | Safe application upgrades, schema migrations, rollback and old-version safeguards | partial | KnowledgeStore.cs | Test forward and rollback migrations on copied vaults. |
| WIN-008 | Signed publisher and checked release / verifiable SBOM/licenses | missing | vendor/README.md | Authenticode/signature and SBOM/license scan verified where appropriate. |
| WIN-009 | Clean machine no Python, Node, Docker, Ollama or .NET runtime required | blocked | Build-Windows.ps1 | Install and use on clean Win11 VM. |
| WIN-010 | User-controlled local app data location, resource limits, safe installation and uninstall | partial | LocalPaths.cs | UI config, permissions and preserve/delete options tested. |
| MOD-001 | GPT-OSS 20B default with correct GGUF/MXFP4, Harmony tool/reasoning templates | unverified | Services/ModelRuntime.cs | Real test with licensed official model; correct reasoning/display separation. |
| MOD-002 | Qwen3.5 27B advanced with verified Q4 GGUF chat template and reasoning | unverified | Services/ModelRuntime.cs | Real inference and sustained RAM tests. |
| MOD-003 | Unrestricted compatible GGUF library; custom import, custom directories and SHA256 pinning | partial | Services/ModelDownloader.cs; KnowledgeStore.cs | Import/detect incompatible, corrupt and moved model files. |
| MOD-004 | Verified official model catalog, optional download wizard and progress | partial | MainWindow.xaml; ModelDownloader.cs | Trusted versioned download with progress, resume, checksum and fail handling. |
| MOD-005 | Resume/cancel interrupted downloads; avoid partial file corruption and malicious redirects | partial | Services/ModelDownloader.cs | Network loss and hostile redirect simulation tests. |
| MOD-006 | Switch models without restart; unload old model and preserve all knowledge | partial | Services/ModelRuntime.cs | Repeated switching without leaks and DB loss. |
| MOD-007 | One large loaded model by default; CPU-based on integrated Intel | scaffold | Services/HardwareProbe.cs; ModelRuntime.cs | 32 GB real RAM pressure and no GPU tests. |
| MOD-008 | Model-specific context, threads, prompt/temperature, max tokens, timeouts, memory settings | missing | Services/ModelRuntime.cs | UI settings persist and are applied to llama runtime. |
| MOD-009 | Model benchmarks with latency/token rate/RAM/load time and progress | partial | MainWindow.xaml.cs | Repeatable measured benchmark traces. |
| MOD-010 | Sequential model side-by-side compare of same question, sources and preferred answer | partial | MainWindow.xaml.cs | Compare answers with citations without two models loaded at once. |
| MOD-011 | Optional automatic model choice based on task and available memory | missing | none | Configurable and explainable routing; user override. |
| MOD-012 | Support compatible future GGUF architectures with compatibility checks | partial | ModelRuntime.cs | Versioned capability/format tests and helpful refusal for unsupported models. |
| MOD-013 | Offline local embedding GGUF supported independently of chat LLM | partial | Services/EmbeddingRuntime.cs | Embedding model setup and real vector indexing without network. |
| MOD-014 | Vision input support when model and projector compatible | missing | none | Image question with multimodal model verified and local-only. |
| MOD-015 | Chat streaming, reasoning formatting, Unicode, retries, cancellation and model crash recovery | partial | ModelRuntime.cs; MainWindow.xaml.cs | SSE streams and stop/retry/interrupt test on two models. |
| MOD-016 | Reliable process lifecycle, port isolation/API key, health, cleanup and protected runtime | partial | ModelRuntime.cs; RuntimeBundle.cs | Repeated start/stop, failed launch, port race, and process-tree cleanup. |
| CHAT-001 | Conversation histories persist across restarts; create, rename, delete, search and export | partial | Services/KnowledgeStore.cs; MainWindow.xaml | All controls function and persist; no sensitive data leakage. |
| CHAT-002 | Assistant distinguishes source evidence from generated reasoning and uncertainty | partial | KnowledgeStore.cs; MainWindow.xaml.cs | Citations correspond to actual chunks, abstains on no evidence. |
| CHAT-003 | User attaches individual authorized files to chats and compares documents | partial | DocumentExtractor.cs; MainWindow.xaml | File attachment in chat, referenced safely. |
| CHAT-004 | Session stop/retry/resume, no lost partial messages, clear status and errors | partial | MainWindow.xaml.cs | Cancel/resume and crash save behavior exercised. |
| CHAT-005 | Chat grounded in current workspace with leakage protection | partial | KnowledgeStore.Work.cs; MainWindow.xaml.cs | Cross-workspace access tests and filtered search. |
| CHAT-006 | Work-only purpose; prevent personal and prohibited sensitive file ingestion by default | partial | none | Clear permission and data-scope guardrails. |
| CHAT-007 | Plain language professional document drafting, IT support, policy comparison and operations use | partial | MainWindow.xaml | Synthetic multi-turn tasks with sources and exports. |
| KNOW-001 | Local encrypted source files and extracted content with hash, version and provenance | partial | Services/KnowledgeStore.cs | Import/update/delete and source history tests. |
| KNOW-002 | FTS keyword search by text/system/file/date/source | partial | Services/KnowledgeStore.cs | Relevance and negative search fixtures. |
| KNOW-003 | Semantic embeddings and hybrid lexical-vector retrieval | partial | EmbeddingRuntime.cs; KnowledgeStore.cs | Real embedding model, vector consistency and ranked recall tests. |
| KNOW-004 | Source citations precisely map to document and page/section/version | partial | KnowledgeStore.cs | Every sourced claim links to verified excerpt. |
| KNOW-005 | Propose, approve, reject, revise, delete and export memories | partial | KnowledgeStore.Work.cs; KnowledgeStore.cs | Approval decisions and deletions persist. |
| KNOW-006 | Conversation-derived memory candidate extraction with consent | partial | KnowledgeStore.Work.cs | Automatic suggestions surfaced, not silently approved. |
| KNOW-007 | Detect contradictory rules, superseded policies, source authority and effective dates | missing | none | Conflict test suite identifies current approved rule. |
| KNOW-008 | Corrections captured and applied across subsequent conversations and LLM swaps | partial | KnowledgeStore.cs | Regression task before/after model swap. |
| KNOW-009 | Watch approved local folders for changes and reindex with safe debounce/retries | partial | KnowledgeStore.Work.cs | Windows FileSystemWatcher or dependable scan scheduler with changed/deleted files. |
| KNOW-010 | Protect against stale, malicious or prompt-injection content in retrieved documents | missing | none | Attack fixtures cannot instruct file/network/credential operations. |
| KNOW-011 | Model-agnostic storage, embedding version compatibility and rebuildable indexes | partial | KnowledgeStore.cs | Switch embedding model and rebuild without deleting sources. |
| KNOW-012 | Delete derived vectors/FTS and handle retention of historical backups | partial | KnowledgeStore.cs | Delete item then verify active/search/caches and documented backup expiry. |
| KNOW-013 | Quality evaluation dataset for retrieval relevance, source citation and hallucination rate | missing | tests | Repeatable answer/source acceptance metrics; no invented citations. |
| KNOW-014 | Versioned records for company decisions, incidents, approved procedures and owners | partial | KnowledgeStore.Work.cs | Lifecycle and provenance throughout updates. |
| KNOW-015 | Portable exports for knowledge, memory and documents in documented nonproprietary formats | partial | KnowledgeStore.cs; DocumentExporter.cs | Re-import or independently read export without app. |
| KNOW-016 | All knowledge and memory remain shared across every selected LLM | partial | KnowledgeStore.cs; ModelRuntime.cs | Model swap and restore test with same questions. |
| DOC-001 | Plain text, Markdown, JSON, XML, CSV, logs and programming documentation ingestion | partial | DocumentExtractor.cs | Fixtures contain expected content and encoding edge cases. |
| DOC-002 | PDF text extraction including page metadata | partial | DocumentExtractor.cs | Scanned/structured PDF test cases and page-aware citations. |
| DOC-003 | Scanned and mixed PDF OCR using vetted bundled PDFium and Tesseract | partial | PdfPageOcr.cs; OcrService.cs | Actual Windows OCR produces accurate text from known 10-page fixture. |
| DOC-004 | PNG/JPEG/BMP/TIFF OCR; screenshots and software error text | partial | OcrService.cs | Windows OCR accuracy/corruption and resource-limits tests. |
| DOC-005 | DOCX, XLSX, PPTX extraction with sheets/slide/paragraph structure | partial | DocumentExtractor.cs | Complex fixtures including formulas and tables. |
| DOC-006 | Document Studio: drafting, editing, comparing, summaries and citations | partial | MainWindow.xaml; DocumentExporter.cs | Complete UI workflow with source versions. |
| DOC-007 | Export results to Word and PDF, preserving expected formatting | partial | DocumentExporter.cs | Open exported DOCX and PDF on clean Windows and validate content. |
| DOC-008 | Document import validation for zip bombs, oversized/corrupt files, macros and embedded threats | partial | DocumentExtractor.cs | Fuzz/resource controls and malicious document tests. |
| DOC-009 | Original document not silently altered; secure temporary file lifecycle | partial | DocumentExtractor.cs; Vault.cs | File hashes unchanged; no plaintext temp secrets. |
| DOC-010 | Optional vision Q&A when supported, with explicit model/projector setup | missing | none | Actual local multimodal benchmark. |
| DOC-011 | Source comparison: versions, policies, spreadsheets and technical logs | partial | MainWindow.xaml | Correct differences explained with citations. |
| WORK-001 | Separate workspaces and assignment of documents/knowledge | partial | KnowledgeStore.Work.cs; MainWindow.xaml | Add/select/scope workspace and persist. |
| WORK-002 | Workspace-level permission/authorization boundary and export controls | missing | none | Negative access/cross-scope tests. |
| WORK-003 | Projects, milestones, actions, risks, owners, deadlines, status and decisions | partial | KnowledgeStore.Work.cs | CRUD UI with persisted all fields. |
| WORK-004 | Decision history and superseded decision reasoning | missing | none | Historical as-of queries and provenance. |
| WORK-005 | Technical incident records, resolution steps and linking prior cases | missing | none | Search and trace incident-to-fix. |
| WORK-006 | Optional future local work systems connections only if individually authorized | deferred | none | No automatic data capture or external API use. |
| SEC-001 | SQLCipher encrypted SQLite database with maintained supported provider | blocked | KnowledgeStore.cs; App.xaml.cs; WorkIntelligence.csproj | Native provider loads, wrong password denied, disk header ciphertext and no plaintext fallback. |
| SEC-002 | AES-GCM encrypted imported attachments, controlled keys and passphrases | partial | Vault.cs | Decrypt/authentication failure behavior tested. |
| SEC-003 | Encrypt embeddings, search indexes, logs and temporary document data | partial | KnowledgeStore.cs; AuditLog.cs | No unprotected copied work data on disk in typical operations. |
| SEC-004 | No cloud AI, telemetry or unauthorized outbound traffic | partial | ModelRuntime.cs; EmbeddingRuntime.cs | Monitor actual Win11 network traffic in offline mode. |
| SEC-005 | Only authorized model downloads and encrypted OneDrive transfer can reach internet | partial | ModelDownloader.cs; RemoteBackupService.cs | Allowlist and SSRF/redirect/localhost tests. |
| SEC-006 | Prompt injection, imported file and malicious model handling / sandboxing | missing | none | Threat modeling and adversarial tests. |
| SEC-007 | Windows ACLs, least privilege and native DLL extraction tamper protection | partial | RuntimeBundle.cs; LocalPaths.cs | Cross-user extraction and writable path negative tests. |
| SEC-008 | Never expose secrets in process arguments, environment logs, diagnostics, backups | partial | BackupService.cs; ScheduledBackup.cs; AuditLog.cs | Redaction and inspection of process lists/logs. |
| SEC-009 | Organizational approval and retention controls; PHI/employee records excluded by default | missing | none | First-run authorization, restrictions and audit evidence. |
| SEC-010 | Secure recovery password management, key rotation and future cryptographic agility | missing | Vault.cs; BackupService.cs | Rotation and independent-machine restore tests. |
| SEC-011 | Threat model, SBOM, dependency/license checks and security update policy | partial | vendor/README.md; .github/workflows/windows-build.yml | Supply-chain report and no known unmitigated severe vulnerabilities. |
| SEC-012 | Secure deletion policy covering active data, search artifacts, exports and backup retention | missing | none | Deletion semantics documented/tested, no false instant backup erasure claims. |
| BKP-001 | Encrypted local restic snapshots of consistent vault/documents/settings | partial | BackupService.cs | Restic execution against active writable vault with integrity restore. |
| BKP-002 | Scheduled unattended backup when window closed, robust after logout/reboot where policy allows | partial | ScheduledBackup.cs | Windows Task Scheduler test in each supported session/power state. |
| BKP-003 | Retry missed backups / catch-up after sleep, shutdown and network interruption | missing | ScheduledBackup.cs | Simulated missed jobs self-heal without duplicate/corruption. |
| BKP-004 | Explicit approved OneDrive OAuth account setup and remote selection wizard | missing | RemoteBackupService.cs | Non-CLI setup with authorized work account. |
| BKP-005 | Copy only locally encrypted restic repository to OneDrive; never live databases | partial | RemoteBackupService.cs | No plaintext in remote test account; inspect traffic. |
| BKP-006 | Remote backup byte/content verification and evidence of upload completion | partial | RemoteBackupService.cs | rclone check, remote inventory and actual recovery. |
| BKP-007 | Configurable daily/monthly/yearly retention and quota management | missing | none | Snapshot prune/forget and backup restore at oldest retained point. |
| BKP-008 | Backup alerts, health, activity log, storage, missed-job indication | partial | AuditLog.cs; MainWindow.xaml | UI accuracy and failure notifications. |
| BKP-009 | Second-machine restore to empty vault with independent recovery password | partial | BackupService.cs; RemoteBackupService.cs | Fresh Windows PC restore and offline question retrieval. |
| BKP-010 | Export portable knowledge independently of application, and install model separately | partial | KnowledgeStore.cs | Migration preserving hashes, provenance and corrections. |
| BKP-011 | Interrupted/corrupt repository, wrong key, partially restored data recovery | partial | BackupService.cs | Fault injection, rollback and no corrupt active vault. |
| BKP-012 | Never overwrite divergent knowledge; merge or guided conflict resolution | partial | BackupService.cs | Refuse overwrites and support controlled optional reconciliation. |
| BKP-013 | OneDrive backup privacy/account-authorization and escrow/recovery policy | missing | none | Approved tenant, encrypted before upload and authorized retention. |
| BKP-014 | Repair/restore verification independent of original PC and Windows user login | unverified | BackupService.cs | Cross-device with separate Windows account and credentials. |
| QA-001 | Functional diagnostics dashboard for models, SQLCipher, indexing, OCR, backups, memory | partial | MainWindow.xaml.cs; AuditLog.cs | Actual failing component detection and actionable messages. |
| QA-002 | Progress reporting for long model loads, downloads, embeddings, OCR and backups | partial | MainWindow.xaml.cs | Feedback at runtime and cancellability. |
| QA-003 | Automated C# unit tests beyond smoke tests and static string checks | partial | tests/Tests.cs | Meaningful assertions, coverage and repeatable CI. |
| QA-004 | Actual Windows WPF UI automated interaction tests | missing | none | Clean Win11 end-to-end clicks and screenshot evidence. |
| QA-005 | Windows GitHub Actions build/test plus release packaging and artifact review | partial | .github/workflows/windows-build.yml | Verified green workflow, downloadable artifact within constraints. |
| QA-006 | 99-point independent audit matrix executed with individual evidence | unverified | docs/99_POINT_AUDIT.md | Each case PASS/FAIL/NOT VERIFIED/N/A with logs. |
| QA-007 | Fault injection for full disk, corrupted vault, cancelled tasks, reboot, low RAM | missing | none | Graceful recovery, no data loss. |
| QA-008 | Defect tracker with root cause, patch, reproduction, regression evidence | partial | docs/RECHECK_FINDINGS_2026-10-31.md | All severe defects closed with evidence. |
| QA-009 | Memory/context benchmarks for 20B/27B on i7-1370P 32GB CPU | unverified | Services/HardwareProbe.cs | Real 30-min benchmark without unsafe paging. |
| QA-010 | Independent security review including net isolation and supply chain | unverified | docs/RELEASE_GATES.md | Signed report and executed negative tests. |
| QA-011 | Clean VM standalone EXE run and second PC migration | unverified | scripts/Build-Windows.ps1 | No external runtimes, full restore, offline Q&A. |
| QA-012 | All UI controls functional; no disguised placeholders or silent exception swallowing | unverified | MainWindow.xaml.cs | Every control covered by runtime test. |
| QA-013 | Fresh dependency security advisories and version compatibility | unverified | WorkIntelligence.csproj; vendor/README.md | Documented upstream evidence per dependency. |
| QA-014 | Graceful error handling and safe resource cancellation on exit | partial | App.xaml.cs; ModelRuntime.cs | Tests prevent stuck processes, data corruption or UI freeze. |

## Exit rule

Do not call this project feature-complete until EVERY P0 and agreed P1 requirement has a concrete implementation, test/evidence reference, and no unresolved material defects. Mark environment-dependent tests `NOT VERIFIED`, not `PASS`. Any proposed deferral must be explicitly agreed by the user; features may not be silently cut.

## Current review supplement — 2026-11-01

The rows above retain baseline scope/status. Current source fixes and executed evidence for all 106 IDs are tracked in [REVIEW_TRACEABILITY_2026-11-01.md](REVIEW_TRACEABILITY_2026-11-01.md). Component tests do not close untested Windows/product acceptance gates. EXE packaging is deferred by the current user request.
