# Work Intelligence AI — 106-Requirement Acceptance Crosswalk

Generated from `docs/handoff/requirements.json` on 2026-11-02. **This is not a production acceptance or completion certificate.** The legacy `source_status` field predates November 2 CI progress, and must not be confused with a verified implementation status. Evidence is explicitly limited to tested subsystems; every requirement still needs end-to-end acceptance against its full criteria.

**Baseline CI evidence:** [Consolidated Windows+Linux+GGUF run](https://github.com/DevTunaSoft/local-llm-ai/actions/runs/41515302579) (passed, 2026-11-02). **Unverified:** GPT-OSS 20B, Qwen 27B on target laptop; full OCR tools; actual OneDrive account OAuth/restore; all UI controls; production EXE and clean-PC certification. The newly added retention feature and real-hardware acceptance runner are separately under validation.

| ID | Requirement | Legacy code estimate | Verified scope / next acceptance |
|---|---|---|---|
| WIN-001 | Single self-contained Windows x64 WorkIntelligence.exe including .NET and all operational native tools | scaffold | **Full acceptance pending:** Build and launch one EXE on clean Win11 without developer prerequisites. |
| WIN-002 | Store large LLM GGUF files outside executable in user-selectable folders | partial | **Full acceptance pending:** First run choose import/download destination and recover from a new path. |
| WIN-003 | Store plaintext data, documents, caches and settings external to EXE; keep live SQLite out of OneDrive live sync | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| WIN-004 | Bundle llama.cpp, standard SQLite native provider, PDF renderer, Tesseract, rclone, OCR assets and embedding runtime without separate install | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| WIN-005 | First-run wizard configures vault, directories, model, knowledge, authorized backup | partial | **Full acceptance pending:** End-to-end GUI walkthrough on clean Windows. |
| WIN-006 | Portable restore to replacement Windows machine without losing knowledge | blocked | **Full acceptance pending:** Second machine restore uses backup password and works offline. |
| WIN-007 | Safe application upgrades, schema migrations, rollback and old-version safeguards | partial | **Full acceptance pending:** Test forward and rollback migrations on copied vaults. |
| WIN-008 | Signed publisher and checked release / verifiable SBOM/licenses | missing | **Full acceptance pending:** Authenticode/signature and SBOM/license scan verified where appropriate. |
| WIN-009 | Clean machine no Python, Node, Docker, Ollama or .NET runtime required | blocked | **Full acceptance pending:** Install and use on clean Win11 VM. |
| WIN-010 | User-controlled local app data location, resource limits, safe installation and uninstall | partial | **Full acceptance pending:** UI config, permissions and preserve/delete options tested. |
| MOD-001 | GPT-OSS 20B default with correct GGUF/MXFP4, Harmony tool/reasoning templates | unverified | **Full acceptance pending:** Real test with licensed official model; correct reasoning/display separation. |
| MOD-002 | Qwen3.5 27B advanced with verified Q4 GGUF chat template and reasoning | unverified | **Full acceptance pending:** Real inference and sustained RAM tests. |
| MOD-003 | Unrestricted compatible GGUF library; custom import, custom directories and SHA256 pinning | partial | **Full acceptance pending:** Import/detect incompatible, corrupt and moved model files. |
| MOD-004 | Verified official model catalog, optional download wizard and progress | partial | **Full acceptance pending:** Trusted versioned download with progress, resume, checksum and fail handling. |
| MOD-005 | Resume/cancel interrupted downloads; avoid partial file corruption and malicious redirects | partial | **Full acceptance pending:** Network loss and hostile redirect simulation tests. |
| MOD-006 | Switch models without restart; unload old model and preserve all knowledge | partial | **Full acceptance pending:** Repeated switching without leaks and DB loss. |
| MOD-007 | One large loaded model by default; CPU-based on integrated Intel | scaffold | **Full acceptance pending:** 32 GB real RAM pressure and no GPU tests. |
| MOD-008 | Model-specific context, threads, prompt/temperature, max tokens, timeouts, memory settings | missing | **Full acceptance pending:** UI settings persist and are applied to llama runtime. |
| MOD-009 | Model benchmarks with latency/token rate/RAM/load time and progress | partial | **Full acceptance pending:** Repeatable measured benchmark traces. |
| MOD-010 | Sequential model side-by-side compare of same question, sources and preferred answer | partial | **Full acceptance pending:** Compare answers with citations without two models loaded at once. |
| MOD-011 | Optional automatic model choice based on task and available memory | missing | **Full acceptance pending:** Configurable and explainable routing; user override. |
| MOD-012 | Support compatible future GGUF architectures with compatibility checks | partial | **Full acceptance pending:** Versioned capability/format tests and helpful refusal for unsupported models. |
| MOD-013 | Offline local embedding GGUF supported independently of chat LLM | partial | **Partial test evidence:** Real GGUF embedding worker, vector normalization, and shutdown tested. **Remaining:** Embedding model setup and real vector indexing without network. |
| MOD-014 | Vision input support when model and projector compatible | missing | **Full acceptance pending:** Image question with multimodal model verified and local-only. |
| MOD-015 | Chat streaming, reasoning formatting, Unicode, retries, cancellation and model crash recovery | partial | **Full acceptance pending:** SSE streams and stop/retry/interrupt test on two models. |
| MOD-016 | Reliable process lifecycle, port isolation/API key, health, cleanup and protected runtime | partial | **Partial test evidence:** Real llama-server startup, restart, streaming, shutdown smoke-tested; production-size model pending. **Remaining:** Repeated start/stop, failed launch, port race, and process-tree cleanup. |
| CHAT-001 | Conversation histories persist across restarts; create, rename, delete, search and export | partial | **Partial test evidence:** Create, rename, search and SQLite persistence verified in WPF/CI; export not fully accepted. **Remaining:** All controls function and persist; no sensitive data leakage. |
| CHAT-002 | Assistant distinguishes source evidence from generated reasoning and uncertainty | partial | **Full acceptance pending:** Citations correspond to actual chunks, abstains on no evidence. |
| CHAT-003 | User attaches individual authorized files to chats and compares documents | partial | **Full acceptance pending:** File attachment in chat, referenced safely. |
| CHAT-004 | Session stop/retry/resume, no lost partial messages, clear status and errors | partial | **Full acceptance pending:** Cancel/resume and crash save behavior exercised. |
| CHAT-005 | Chat grounded in current workspace with leakage protection | partial | **Full acceptance pending:** Cross-workspace access tests and filtered search. |
| CHAT-006 | Work-only purpose; prevent personal and prohibited sensitive file ingestion by default | partial | **Full acceptance pending:** Clear permission and data-scope guardrails. |
| CHAT-007 | Plain language professional document drafting, IT support, policy comparison and operations use | partial | **Full acceptance pending:** Synthetic multi-turn tasks with sources and exports. |
| KNOW-001 | Local plaintext source file copies and extracted content with hash, version and provenance | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| KNOW-002 | FTS keyword search by text/system/file/date/source | partial | **Full acceptance pending:** Relevance and negative search fixtures. |
| KNOW-003 | Semantic embeddings and hybrid lexical-vector retrieval | partial | **Partial test evidence:** Real vectors indexed, persisted and retrieved across workspaces; relevance benchmark not certified. **Remaining:** Real embedding model, vector consistency and ranked recall tests. |
| KNOW-004 | Source citations precisely map to document and page/section/version | partial | **Full acceptance pending:** Every sourced claim links to verified excerpt. |
| KNOW-005 | Propose, approve, reject, revise, delete and export memories | partial | **Partial test evidence:** Approval and persistence tested; full GUI coverage incomplete. **Remaining:** Approval decisions and deletions persist. |
| KNOW-006 | Conversation-derived memory candidate extraction with consent | partial | **Full acceptance pending:** Automatic suggestions surfaced, not silently approved. |
| KNOW-007 | Detect contradictory rules, superseded policies, source authority and effective dates | missing | **Full acceptance pending:** Conflict test suite identifies current approved rule. |
| KNOW-008 | Corrections captured and applied across subsequent conversations and LLM swaps | partial | **Full acceptance pending:** Regression task before/after model swap. |
| KNOW-009 | Watch approved local folders for changes and reindex with safe debounce/retries | partial | **Full acceptance pending:** Windows FileSystemWatcher or dependable scan scheduler with changed/deleted files. |
| KNOW-010 | Protect against stale, malicious or prompt-injection content in retrieved documents | missing | **Full acceptance pending:** Attack fixtures cannot instruct file/network/credential operations. |
| KNOW-011 | Model-agnostic storage, embedding version compatibility and rebuildable indexes | partial | **Full acceptance pending:** Switch embedding model and rebuild without deleting sources. |
| KNOW-012 | Delete derived vectors/FTS and handle retention of historical backups | partial | **Full acceptance pending:** Delete item then verify active/search/caches and documented backup expiry. |
| KNOW-013 | Quality evaluation dataset for retrieval relevance, source citation and hallucination rate | missing | **Full acceptance pending:** Repeatable answer/source acceptance metrics; no invented citations. |
| KNOW-014 | Versioned records for company decisions, incidents, approved procedures and owners | partial | **Full acceptance pending:** Lifecycle and provenance throughout updates. |
| KNOW-015 | Portable exports for knowledge, memory and documents in documented nonproprietary formats | partial | **Full acceptance pending:** Re-import or independently read export without app. |
| KNOW-016 | All knowledge and memory remain shared across every selected LLM | partial | **Full acceptance pending:** Model swap and restore test with same questions. |
| DOC-001 | Plain text, Markdown, JSON, XML, CSV, logs and programming documentation ingestion | partial | **Full acceptance pending:** Fixtures contain expected content and encoding edge cases. |
| DOC-002 | PDF text extraction including page metadata | partial | **Full acceptance pending:** Scanned/structured PDF test cases and page-aware citations. |
| DOC-003 | Scanned and mixed PDF OCR using vetted bundled PDFium and Tesseract | partial | **Full acceptance pending:** Actual Windows OCR produces accurate text from known 10-page fixture. |
| DOC-004 | PNG/JPEG/BMP/TIFF OCR; screenshots and software error text | partial | **Full acceptance pending:** Windows OCR accuracy/corruption and resource-limits tests. |
| DOC-005 | DOCX, XLSX, PPTX extraction with sheets/slide/paragraph structure | partial | **Full acceptance pending:** Complex fixtures including formulas and tables. |
| DOC-006 | Document Studio: drafting, editing, comparing, summaries and citations | partial | **Full acceptance pending:** Complete UI workflow with source versions. |
| DOC-007 | Export results to Word and PDF, preserving expected formatting | partial | **Full acceptance pending:** Open exported DOCX and PDF on clean Windows and validate content. |
| DOC-008 | Document import validation for zip bombs, oversized/corrupt files, macros and embedded threats | partial | **Full acceptance pending:** Fuzz/resource controls and malicious document tests. |
| DOC-009 | Original document not silently altered; secure temporary file lifecycle | partial | **Full acceptance pending:** File hashes unchanged; no plaintext temp secrets. |
| DOC-010 | Optional vision Q&A when supported, with explicit model/projector setup | missing | **Full acceptance pending:** Actual local multimodal benchmark. |
| DOC-011 | Source comparison: versions, policies, spreadsheets and technical logs | partial | **Full acceptance pending:** Correct differences explained with citations. |
| WORK-001 | Separate workspaces and assignment of documents/knowledge | partial | **Full acceptance pending:** Add/select/scope workspace and persist. |
| WORK-002 | Workspace-level permission/authorization boundary and export controls | missing | **Full acceptance pending:** Negative access/cross-scope tests. |
| WORK-003 | Projects, milestones, actions, risks, owners, deadlines, status and decisions | partial | **Full acceptance pending:** CRUD UI with persisted all fields. |
| WORK-004 | Decision history and superseded decision reasoning | missing | **Full acceptance pending:** Historical as-of queries and provenance. |
| WORK-005 | Technical incident records, resolution steps and linking prior cases | missing | **Full acceptance pending:** Search and trace incident-to-fix. |
| WORK-006 | Optional future local work systems connections only if individually authorized | deferred | **Full acceptance pending:** No automatic data capture or external API use. |
| SEC-001 | Standard unencrypted SQLite database with FTS5; no SQLCipher dependency; fail closed for preexisting encrypted vaults | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| SEC-002 | Unencrypted imported attachments with stable local storage and explicit work-data confidentiality warning; do not require app passphrases | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| SEC-003 | Store embeddings, search indexes and permitted logs as plaintext locally; document absence of at-rest protection | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| SEC-004 | No cloud AI, telemetry or unauthorized outbound traffic | partial | **Full acceptance pending:** Monitor actual Win11 network traffic in offline mode. |
| SEC-005 | Only authorized model downloads and explicitly approved plaintext OneDrive backup transfer may reach internet | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| SEC-006 | Prompt injection, imported file and malicious model handling / sandboxing | missing | **Full acceptance pending:** Threat modeling and adversarial tests. |
| SEC-007 | Windows ACLs, least privilege and native DLL extraction tamper protection | partial | **Full acceptance pending:** Cross-user extraction and writable path negative tests. |
| SEC-008 | Never expose secrets in process arguments, environment logs, diagnostics, backups | partial | **Full acceptance pending:** Redaction and inspection of process lists/logs. |
| SEC-009 | Organizational approval and retention controls; PHI/employee records excluded by default | missing | **Full acceptance pending:** First-run authorization, restrictions and audit evidence. |
| SEC-010 | Secure recovery password management, key rotation and future cryptographic agility | missing | **Full acceptance pending:** Rotation and independent-machine restore tests. |
| SEC-011 | Threat model, SBOM, dependency/license checks and security update policy | partial | **Full acceptance pending:** Supply-chain report and no known unmitigated severe vulnerabilities. |
| SEC-012 | Secure deletion policy covering active data, search artifacts, exports and backup retention | missing | **Full acceptance pending:** Deletion semantics documented/tested, no false instant backup erasure claims. |
| BKP-001 | Unencrypted versioned local snapshots of consistent SQLite and document files with SHA-256 integrity manifest | partial | **Partial test evidence:** Verified versioned SHA-256 snapshot creation and corruption fallback tests. **Remaining:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| BKP-002 | Scheduled unattended backup when window closed, robust after logout/reboot where policy allows | partial | **Full acceptance pending:** Windows Task Scheduler test in each supported session/power state. |
| BKP-003 | Retry missed backups / catch-up after sleep, shutdown and network interruption | missing | **Full acceptance pending:** Simulated missed jobs self-heal without duplicate/corruption. |
| BKP-004 | Explicit approved OneDrive OAuth account setup and remote selection wizard | missing | **Full acceptance pending:** Non-CLI setup with authorized work account. |
| BKP-005 | Copy verified plaintext snapshots to approved OneDrive destination; never live databases | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| BKP-006 | Remote backup byte/content verification and evidence of upload completion | partial | **Full acceptance pending:** rclone check, remote inventory and actual recovery. |
| BKP-007 | Configurable daily/monthly/yearly retention and quota management | missing | **Full acceptance pending:** Snapshot prune/forget and backup restore at oldest retained point. |
| BKP-008 | Backup alerts, health, activity log, storage, missed-job indication | partial | **Full acceptance pending:** UI accuracy and failure notifications. |
| BKP-009 | Second-machine restore to empty vault with independent recovery password | partial | **Full acceptance pending:** Fresh Windows PC restore and offline question retrieval. |
| BKP-010 | Export portable knowledge independently of application, and install model separately | partial | **Full acceptance pending:** Migration preserving hashes, provenance and corrections. |
| BKP-011 | Interrupted/corrupt repository, wrong key, partially restored data recovery | partial | **Partial test evidence:** Corrupt-latest snapshot fallback tested; interrupted remote restore unverified. **Remaining:** Fault injection, rollback and no corrupt active vault. |
| BKP-012 | Never overwrite divergent knowledge; merge or guided conflict resolution | partial | **Full acceptance pending:** Refuse overwrites and support controlled optional reconciliation. |
| BKP-013 | OneDrive backup privacy/account-authorization and escrow/recovery policy | missing | **Full acceptance pending:** Approved tenant, encrypted before upload and authorized retention. |
| BKP-014 | Repair/restore verification independent of original PC and Windows user login | unverified | **Full acceptance pending:** Cross-device with separate Windows account and credentials. |
| QA-001 | Functional diagnostics dashboard for models, plaintext SQLite integrity, indexing, OCR, backups and learning | partial | **Full acceptance pending:** Validate the updated 2026-11-02 plaintext design in Windows runtime with synthetic fixtures; obtain organizational approval; no production acceptance yet. |
| QA-002 | Progress reporting for long model loads, downloads, embeddings, OCR and backups | partial | **Full acceptance pending:** Feedback at runtime and cancellability. |
| QA-003 | Automated C# unit tests beyond smoke tests and static string checks | partial | **Partial test evidence:** C# core regressions run across Windows and Linux. **Remaining:** Meaningful assertions, coverage and repeatable CI. |
| QA-004 | Actual Windows WPF UI automated interaction tests | missing | **Partial test evidence:** WPF workspace/chat interaction tested; not all controls. **Remaining:** Clean Win11 end-to-end clicks and screenshot evidence. |
| QA-005 | Windows GitHub Actions build/test plus release packaging and artifact review | partial | **Full acceptance pending:** Verified green workflow, downloadable artifact within constraints. |
| QA-006 | 99-point independent audit matrix executed with individual evidence | unverified | **Full acceptance pending:** Each case PASS/FAIL/NOT VERIFIED/N/A with logs. |
| QA-007 | Fault injection for full disk, corrupted vault, cancelled tasks, reboot, low RAM | missing | **Full acceptance pending:** Graceful recovery, no data loss. |
| QA-008 | Defect tracker with root cause, patch, reproduction, regression evidence | partial | **Full acceptance pending:** All severe defects closed with evidence. |
| QA-009 | Memory/context benchmarks for 20B/27B on i7-1370P 32GB CPU | unverified | **Full acceptance pending:** Real 30-min benchmark without unsafe paging. |
| QA-010 | Independent security review including net isolation and supply chain | unverified | **Full acceptance pending:** Signed report and executed negative tests. |
| QA-011 | Clean VM standalone EXE run and second PC migration | unverified | **Full acceptance pending:** No external runtimes, full restore, offline Q&A. |
| QA-012 | All UI controls functional; no disguised placeholders or silent exception swallowing | unverified | **Full acceptance pending:** Every control covered by runtime test. |
| QA-013 | Fresh dependency security advisories and version compatibility | unverified | **Full acceptance pending:** Documented upstream evidence per dependency. |
| QA-014 | Graceful error handling and safe resource cancellation on exit | partial | **Full acceptance pending:** Tests prevent stuck processes, data corruption or UI freeze. |

**Release gate:** Do not package or publish a Windows EXE without explicit user authorization. Verify all outstanding evidence, hashes, SBOM, licensing and clean-machine checks beforehand.
