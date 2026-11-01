# MASTER DEVELOPMENT DIRECTIVE — WORK INTELLIGENCE AI
**Project:** Work Intelligence AI  
**Target:** Windows 11 Pro x64; Intel Core i7-1370P, 14 cores/20 threads, 32 GB RAM, Intel integrated graphics  
**Project state at handoff:** Architecture and requirements exist; source code, Windows build, hardware benchmarking, and end-to-end restore have NOT been established as completed.  
**Purpose of this directive:** Build, debug, independently audit, test, package, and deliver a fully functioning application—not just provide recommendations, a mockup, snippets, or an untested source-code archive.

## 1. Your role and absolute mandate
Act as the accountable combined **principal software architect, experienced C#/.NET Windows engineer, llama.cpp inference engineer, RAG/retrieval engineer, database and cryptography engineer, Windows deployment engineer, threat-model/security specialist, test automation engineer, QA lead, technical researcher, and independent release auditor**. Personally own the whole system and integration between its components.

My highest priority is **complete, reliable, maintainable, secure, working code**. I do not want to repeatedly discover missing features, report obvious errors, and ask you to fix your own implementation. Anticipate errors; investigate them independently; implement durable root-cause fixes; then check and recheck. Examine failure modes from **99 different angles** using the concrete 99-point matrix below. This is an audit framework, not permission to invent test results or promise mathematically impossible zero defects.

Do not ask me to act as your build engineer, run trivial diagnostics for you, repair dependencies manually, or review incomplete features that you could verify yourself. Do not end at “it compiles.” Never claim “tested,” “error-free,” “complete,” “production-ready,” or “Windows-compatible” unless the relevant tests have actually run and their evidence is available. If an environment lacks Windows, hardware, model weights, corporate authorization, OneDrive connectivity, signing keys, or any other required facility, implement everything that can be implemented, arrange automated Windows testing where possible, explicitly classify the blocked tests as **NOT VERIFIED**, and never misrepresent the release as accepted. Work autonomously within actual available tools; do not promise to continue working in the background or deliver later.

## 2. Canonical product outcome
Build a fully local **Work Intelligence AI** desktop application used exclusively for approved professional work. It should function as a private, persistent work-knowledge assistant: local AI chat; document ingestion and analysis; source-grounded retrieval; approved long-term memories and corrections; project/decision tracking; diagnostics; model management; and encrypted versioned backup/restoration through authorized OneDrive or offline storage.

Requirements in this directive must remain enforceable without access to the original conversation. If an architecture-v2 document is attached, reconcile its deeper requirements with this directive, preserving all compatible scope and security constraints. Current instructions override older examples only where explicitly changed. Do not silently omit features because they are difficult.

**Non-negotiable boundaries:**
- AI inference, embeddings, document extraction/OCR, search, memory, and primary data storage **remain local**, with no cloud AI or hosted inference.
- Allow network access only for **explicitly authorized model downloads** and **separately authorized encrypted-backup transport** (and approved manual update checks if introduced); protect model/knowledge processes from outbound connections.
- Never index personal chat history, unrestricted personal folders, or sensitive company content automatically; require scoped authorization. Default-deny PHI/patient records, secrets, credentials, and other regulated categories unless organizational controls and approvals explicitly allow their processing.
- OneDrive is an **encrypted backup destination only**; live SQLite databases and working indexes stay on a local SSD, outside synchronized folders.
- Knowledge, memories, policies, corrections, decisions, conversations, original source references, and settings must be exportable, versioned, and restorable independently of a particular LLM or computer.

## 3. Locked hardware and model requirements
- Target Windows 11 Pro 64-bit, Intel i7-1370P, 32 GB RAM, **integrated Intel graphics only**. Do not assume NVIDIA CUDA or dedicated VRAM. Establish CPU-only operation first; try Intel-compatible acceleration only if demonstrably stable and worthwhile. Prevent swap thrashing, thermal runaway, UI freezing, runaway disk consumption, and process crashes.
- **GPT-OSS 20B** is the preferred standard model, using an appropriate verified **GGUF / MXFP4** distribution and correctly implemented Harmony chat/reasoning formatting.
- **Qwen3.5 27B Q4** is the preferred deep-analysis model, with exact compatible GGUF quantization, tokenizer/chat template, reasoning behavior, and optional verified multimodal projector where supported.
- Keep an **unlimited-capacity model catalog subject to available storage**, permitting other compatible GGUF models regardless of nominal parameter size. Enable download, manual GGUF import, validation, deduplication, selection, unloading, switching, removal, and per-model tuning.
- Default to one large generation model loaded at once. Implement a RAM-aware preflight; configurable context size (initially ~4,096–8,192 tokens), threads, memory limits, cancellation, queueing, and safe fallback; optional small model for faster responses.
- Model Manager must support user-selected directories, interrupted/resumable downloads, pinned revisions, source/license and hash verification, genuine progress, failure cleanup, offline import, compatibility checks, model benchmarking, and informative error states. No auto-download without explicit user approval.
- Distinguish text-only from vision-capable deployments; never send image payloads to a worker without the correct projector and active multimodal support.
- Model changes must **not erase or retrain** accumulated organizational knowledge. The central encrypted knowledge base is model independent. Embedding model changes trigger a controlled rebuild of derived vector indexes.
- Support **Standard**, **Deep Analysis**, and optionally tested **Automatic Router** modes, plus sequential side-by-side model comparisons. Never load both large models simultaneously by default.

## 4. Packaging and user experience contract
- Deliver **one distributable self-contained native Windows x64 application executable**, proposed name **WorkIntelligence.exe**. Bundle all application runtime/tool dependencies except separately provisioned **large LLM model assets**. There must be **no manual installation of Python, Node.js, Docker, Ollama, .NET, SQLCipher, restic, rclone, OCR binaries, or llama.cpp** for an ordinary user.
- Preferred stack: **C# / WPF / .NET 10 LTS**; native CPU llama.cpp worker; bundled local embedding + tokenizer; SQLCipher SQLite + FTS5 + vetted vector index; secure blob store; local OCR; embedded pinned restic/rclone tools. Alter a stack component only with a recorded technical reason and tests.
- Microsoft .NET single-file may require extraction of embedded native files. This is permitted **only as an implementation detail**: securely extract to application-controlled, permission-checked, hash-verified local runtime paths. Do not falsely promise one literal file in memory. Do not require a second installer for helper tools.
- Maintain separate protected paths for `Data`, `Models`, `Runtime`, `Cache`, and `Logs`; allow model folders on another drive. Use `%LOCALAPPDATA%` as a default, not a hard-coded username. Do not confuse executable portability with database portability.
- Target ~250–600 MB for the executable as a **nonbinding estimate**, prioritizing reliability over an artificial size cap. Model files are stored outside the EXE. Publish clean win-x64 self-contained single-file artifacts, release manifests, checksums, and signing where credentials are available.
- The first-run wizard must work even without a model: **Download verified model**, **Import local GGUF**, **Restore previous knowledge**, **Configure work-only authorization**, **Set secure data vault**, and optionally **Authorize OneDrive backup**. Without a model, explain that inference is unavailable while still allowing model management, restoration, and appropriate administrative functions.
- UI must be a professional, functional native desktop app: dashboard; chat; model library; knowledge library; memory manager; workspaces/projects; document studio; intelligent search; security center; system diagnostics; backup/recovery; settings. Include readable progress and errors, keyboard navigation, high contrast, scalable typography, and no voice requirement.

## 5. Chat, learning, knowledge and project intelligence
Implement all of these end-to-end—not empty buttons or UI shells:
1. Real chat streaming, stop/retry, conversation rename/search/delete/export, model/workspace selection, source citations, history persistence, bounded context and visible failures.
2. Local ingestion from approved user-selected files/folders; watched-folder updates; file fingerprinting; indexing state, duplicate detection, versioning, and cancellation.
3. Support PDF, DOCX, XLSX, CSV, PPTX, TXT, Markdown, JSON, logs, and where practical SRT/VTT. Parse headings, tables, spreadsheet formulas/cached values, workbook/sheet/cell references, document sections, PDF pages, and slides. Support scanned PDFs/images via bundled, tested OCR. Report illegible/password-protected/unsupported/corrupt files truthfully.
4. Local embedding model and **hybrid semantic + full-text retrieval**, with workspace permission filters applied **before retrieval**; source preview; provenance/version references; document authority hierarchy; deterministic ranking and traceable citation mapping.
5. **Controlled durable memory**: candidates, user approval/rejection, scoped workspaces, source origin, timestamps, version history, contradicting facts, expiration/review dates, superseded policies, and explicit unlearning. AI guesses never silently become authoritative policy.
6. Project/decision intelligence: workspaces, task and action extraction, decisions, risks, incident timelines, technical troubleshooting history, policies, procedures, and status changes—with human authorization and source verification.
7. Document Studio: compare versions, summarize, extract checklists, draft procedures, analyze logs, and export standard formats where verified. No silent modification of originals.
8. Independent and model-neutral data exports in documented **JSON/JSONL, Markdown, CSV, and original source files**, plus documented database/schema versioning. Never create a proprietary knowledge trap.
9. Evidence-grounded answers. If the retrieved material does not support an answer, disclose uncertainty instead of fabricating facts, citations, permissions, test outcomes, or company policies.
10. A complete deletion lifecycle for active documents, chunks, vectors, memory, conversations, caches, and exports; clarify that historical encrypted backups may still contain deleted information until they expire or are explicitly purged according to approved policy.

## 6. Encrypted backup, portability and recovery — mandatory for release
- **Encrypted, versioned, locally generated backups** to an organization-approved OneDrive tenant or an offline external drive. Use pinned, bundled **restic** and **rclone** or an equally verified secure implementation. Data must be encrypted **before** cloud transfer; OneDrive must not receive plaintext work documents or unencrypted backups.
- Produce application-consistent snapshots of the SQLCipher database and associated attachment manifests; never copy an active SQLite file without a safe snapshot protocol. Include memory, document metadata, encrypted source content, conversations, decisions, settings, schema, index manifests, and audit/recovery records as authorized. Model weights may be optionally backed up separately due to size.
- Daily scheduled backups by default; configurable retention (proposed 30 daily / 12 monthly / 3 annual, subject to policy); verified off-device status; bounded retries; backup retention; failure alerts; and integrity checks. **Windows scheduled-task background mode must run even with the UI closed**, when the PC and policy allow it. Resume missed backups after sleep/offline periods. No unprotected command-line recovery secret.
- Backup must remain recoverable if the old PC is destroyed and Windows DPAPI/user profile is unavailable. Provide independent passphrase/key recovery and key-rotation guidance, secure secret storage, and explicit warning that lost encryption keys can render backups unreadable.
- Test actual **second-machine restore**: authenticate, select snapshot, validate encryption/integrity, stage safely, check schema/manifest/hash, confirm before overwrite, migrate, rebuild vector indexes if needed, reopen offline, compare expected knowledge counts and representative sourced answers.
- Handle interrupted sync, duplicate jobs, corrupted repository, invalid credentials, account revocation, ransomware, full disk, bad network, incomplete restore, data conflict, and old/new application version compatibility. Never falsely mark cloud backup successful merely because a local snapshot was created.

## 7. Security threat model and guardrails
- Encrypt SQLCipher database and encrypted document attachments; protect caches, temp, logs, index files, exports, and backup staging. Recommend Windows BitLocker/device encryption for swap/dumps and other OS-level exposures; use appropriate ACLs and local credential storage.
- Prevent prompt injection in documents from becoming privileged instructions; model-generated text cannot execute arbitrary shell commands or write to production services. Parser workers need process isolation, size/time/resource limits, path validation, and safe handling of malicious PDFs/Office documents and ZIP bombs.
- Authenticate loopback inference endpoints or use private IPC; bind only to localhost where applicable. Guard against unintended LAN exposure, unapproved outbound traffic, credential exposure, path traversal, DLL search hijacking, symlink attacks, downgrade attacks, and tampered extracted helpers.
- Restrict model downloader and backup process network permissions separately. Never expose patient data, company secrets, tokens, passwords, or file contents in telemetry; disable third-party telemetry and cloud crash reporting by default. Logs and troubleshooting exports are redacted and user-reviewed.
- Check licenses, distribution rights, model terms, binary provenance, software supply chain, vulnerability alerts, and release/update signing. No secret keys in Git/source/release artifacts. Require employer authorization before storing work data on local hardware or a OneDrive tenant.
- Design secure uninstall, delete/purge, rollback, restore, and credential rotation. Explain system limitations honestly rather than invent security guarantees.

## 8. Implementation strategy — code before declarations of completion
1. **Inventory** all agreed requirements, architecture documents, source files, known historical bugs and security/privacy boundaries; turn them into a numbered requirements-to-code-and-test traceability matrix. If existing code exists, audit and repair it before rewriting. If no code exists, scaffold a maintainable complete repository.
2. **Research actual implementation constraints and known upstream failures** before choosing dependency versions. Verify the exact model GGUF variant and licensing. Pin dependency revisions and build an SBOM.
3. **Prove the inference runtime** with both selected models in a reproducible Windows environment, where available. Benchmark CPU speed/RAM/TTFT and test chat templates, streaming, stop/restart, source retrieval, and model switching. Record environment and actual limitations.
4. **Implement working vertical slices** that run end-to-end: native shell + database + model inference; then ingestion/retrieval; memory/workspaces; diagnostics and security; model management; backup/restore; packaging. Each slice must integrate with the previous ones.
5. **Test continuously** after implementation of each module, rather than postponing all quality assurance to the end. Fix all reproducible build/test failures immediately and retest the repaired component and its dependents.
6. **Run a distinct independent final QA pass** examining the code as though it came from another engineer, with adversarial and fault-injection tests, not merely the same happy-path unit tests.
7. **Publish only evidence-supported deliverables.** If Windows execution is unavailable in the current environment, create a reproducible Windows CI/build-and-acceptance workflow, state its unexecuted cases, and do not claim final Windows acceptance.

## 9. Mandatory deep web and repository research
For every nontrivial build failure, runtime crash, unresolved dependency, compatibility concern, security anomaly, or repeated symptom, conduct a focused but **genuinely deep investigation** using live, authoritative sources, not guesswork or repetitive superficial searches.

Research in this order, using the exact error strings, exception codes, timestamps, dependencies, versions, OS build, architecture, model, quantization, inference backend, and reproducible trigger:
1. Official product/developer documentation and upstream changelogs (Microsoft .NET/WPF/Windows; llama.cpp; Qwen; OpenAI GPT-OSS; SQLite/SQLCipher; ONNX Runtime; restic; rclone; OneDrive).
2. Upstream GitHub source, **issues, pull requests, discussions, commit history, regression windows, release notes, and reproducible examples**; search both open and closed issues.
3. GitLab, Codeberg, upstream mirrors and maintained forks where relevant.
4. Stack Overflow, GitHub Discussions, XDA/technical forums where relevant, and Reddit for independently corroborated practical experiences. Treat forum advice as hypotheses, not authoritative fixes.
5. Relevant vulnerability advisories, CVEs, Windows driver compatibility findings, vendor notices, and library compatibility matrices.

Use alternate search formulations and narrow versions/commit hashes; follow references in issues to root-cause patches and tests. Evaluate whether the issue is still relevant to the **pinned** version. Compare at least two plausible root causes when the symptom is ambiguous. Reproduce the error when possible; instrument logs; isolate the smallest failing case; identify the first bad version if relevant. Implement the smallest safe fix with tests that demonstrate the original failure and prove the correction. Do not blindly paste internet code or adopt an unmaintained fork. Record URLs, source dates, commit/issue IDs, relevance, and what was tested. Do not claim a web fix was verified without actually reproducing its behavior.

Specifically search for and test: GPT-OSS Harmony reasoning/parser failures; model chat-template incompatibilities; Qwen3.5 text/vision/mmproj requirements; Intel/Vulkan integrated-memory allocation errors; .NET single-file and native helper extraction; SQLCipher/FTS/index extension compatibility; SQLite WAL snapshot integrity; Windows Task Scheduler credential/session behavior; rclone OAuth/tenant/OneDrive sync errors; restic partial backups and corrupt-data detection; offline OCR/parser failures; Windows Defender/SmartScreen/code-signing concerns. Re-check upstream status and dates each time—the presence of an old issue does not prove it affects the chosen release.

## 10. The mandatory ERROR → ROOT CAUSE → FIX → RECHECK loop
**Every error must trigger the entire process below, without requiring me to ask again:**

**A. Capture:** Exact error and stack trace, environment, versions, reproduction steps, logs, input data class, expected/actual behavior, and whether this is a regression. Redact sensitive data.

**B. Classify:** Determine whether the failure is from source code, packaging, version mismatch, native ABI, runtime path, OS/ACL/permissions, model incompatibility, malformed data, concurrency, memory pressure, encryption, networking, credentials, test harness, or an external system.

**C. Isolate:** Reproduce minimally; test alternate paths; compare working and failing versions; inspect surrounding callers, interfaces, configs and lifecycle. Trace root cause rather than suppress exceptions.

**D. Research:** Consult current authoritative docs, upstream source and issue trackers, adjacent projects, and verified community findings. Examine known regressions and security implications.

**E. Repair:** Make a complete, maintainable code fix. Preserve intended functionality. Add/extend a regression test that would fail before the fix. No fake success messages, swallowed exceptions, stale mock data, UI-only patches, disabling critical security, or deleting features to make a test pass.

**F. Recheck #1:** Recompile and run targeted unit/integration tests; reproduce the original failure conditions, including edge cases.

**G. Recheck #2:** Run relevant end-to-end, cross-component, regression, negative and security tests; check that the fix did not break model loading, document ingestion, memory, encryption, backup, restoration, or Windows packaging.

**H. Recheck #3:** Conduct an independent adversarial review from another angle, including race conditions, resource exhaustion, reinstall/upgrade, interrupted operations and nearby failure paths. Re-run the affected tests.

**I. Close only with evidence:** Log root cause, actual code change, tests run and results, affected versions, remaining risk. If failures remain, loop again. Do not simply write “fixed.”

**Maintain a defect register** with severity, reproduction, root cause, affected files, reference sources, fix, tests, verification level and status. A defect is not closed by speculation, a code diff, or a green compile alone.

## 11. 99 independent verification angles — mandatory audit matrix
The phrase “99 different ways” means broad, deliberate inspection. Work through **all 99 distinct checks** and map every applicable check to a test, review artifact or explicit NOT VERIFIED status; do not fabricate evidence. Grouped into nine categories:

### A. Requirements and completeness (1–11)
1. Complete requirement inventory.  
2. Requirements-to-code traceability.  
3. Requirements-to-test traceability.  
4. No missing advertised features.  
5. No dead menus/buttons/links.  
6. No unimplemented placeholder services.  
7. No hidden cloud dependencies.  
8. Correct work-only data scope.  
9. Proper accessibility behavior.  
10. Correct defaults and configuration.  
11. User-facing documentation matches actual behavior.

### B. Build and deployment (12–22)
12. Clean solution build.  
13. Warnings evaluated, not ignored.  
14. Package dependency closure.  
15. Windows x64 single-file publishing.  
16. Native helper extraction and hashes.  
17. Clean-user first run.  
18. Restricted-user/no-admin behavior.  
19. Paths with spaces/Unicode/long names.  
20. Repair/reinstall/update.  
21. Rollback and migration reversibility.  
22. Windows Defender/signing and provenance checks.

### C. Model inference and performance (23–33)
23. GPT-OSS model load.  
24. GPT-OSS Harmony formatting.  
25. Qwen3.5 model load.  
26. Qwen chat template and reasoning.  
27. Streaming token parsing.  
28. Stop/cancel and worker cleanup.  
29. Crash/watchdog recovery.  
30. One-model-at-a-time enforcement.  
31. Low-RAM and paging protection.  
32. Intel CPU/optional GPU backend behavior.  
33. Long context, repeated turns and model switching.

### D. Ingestion, retrieval, and memory (34–44)
34. Each supported file format.  
35. Scanned PDF/OCR correctness.  
36. Spreadsheet tables and cell provenance.  
37. Corrupt/unsupported/encrypted input.  
38. Document version tracking.  
39. Duplicate import and idempotence.  
40. Keyword + semantic relevance.  
41. Citation precision and previews.  
42. Unsupported-answer abstention.  
43. Memory approve/reject/conflicts.  
44. Complete deletion and index revocation.

### E. Security, privacy, and isolation (45–55)
45. Default-deny outbound AI network.  
46. Separate download/backup network permissions.  
47. Work folder allowlists.  
48. PHI/secret/PII policy exclusions.  
49. SQLCipher at-rest encryption.  
50. Encrypted attachments/index/temp handling.  
51. Credential storage and rotation.  
52. Loopback/IPC authorization.  
53. Prompt injection and malicious documents.  
54. Path traversal, symlinks, DLL hijacking.  
55. Redacted logs, audit integrity, vulnerability/license checks.

### F. Backup and portability (56–66)
56. Consistent live database snapshots.  
57. Client-side encryption before upload.  
58. OneDrive tenant and OAuth permissions.  
59. Backup runs with UI closed.  
60. Retry after sleep/offline.  
61. Interrupted upload and safe restart.  
62. Cloud snapshot verification.  
63. Retention/prune safety.  
64. Wrong password/corrupt repository.  
65. New-PC restoration without original DPAPI.  
66. Offline post-restore knowledge and citation validation.

### G. UI and application workflows (67–77)
67. First-run setup.  
68. Model missing/download/import.  
69. Model progress/resume/error.  
70. Chat/new/rename/delete/search.  
71. Workspace isolation.  
72. Knowledge Library import/reindex.  
73. Memory approvals and history.  
74. Document Studio outputs.  
75. Search and source navigation.  
76. Diagnostics with actionable messages.  
77. Backup/restore wizard and meaningful progress.

### H. Resilience and destructive scenarios (78–88)
78. Sudden power loss.  
79. Out-of-disk-space.  
80. Memory pressure while Windows is busy.  
81. Battery saver/sleep/hibernate.  
82. Antivirus quarantine/helper unavailable.  
83. Concurrent file edits/imports.  
84. Failed DB migration.  
85. Conflicting two-computer history.  
86. Network loss/OneDrive rate limits.  
87. Malformed enormous documents and ZIP bombs.  
88. Cancel/close/restart at every long-running stage.

### I. Independent QA and final release (89–99)
89. Static analysis and nullable warnings.  
90. Unit tests and coverage review.  
91. Integration tests with real components.  
92. E2E Windows UI automation.  
93. Regression test rerun after every fix.  
94. Fault-injection and adversarial tests.  
95. Performance/thermal/long-run soak tests.  
96. Dependency/SBOM/security/licensing audit.  
97. Signed or hash-verified release package.  
98. Fresh-machine install and second-PC restore evidence.  
99. Final independent adversarial release audit with a complete pass/fail/unverified ledger.

## 12. Test architecture and quality gates
- Build automated **unit, integration, contract, end-to-end, security, regression, fault-injection, and performance** suites. Maintain a safe synthetic fixture corpus containing work-policy revisions, contradictory sources, planted prompt injections, technical logs, scanned PDF pages, spreadsheet formula cases, known citation answers and deletion requirements. Never use real sensitive company data in CI.
- Test actual process boundaries and encrypted storage; mocks alone cannot satisfy major release criteria. Include repeated model switching, multi-turn chat, unsupported GGUF, wrong vision projector, inaccessible model drive, duplicate file import, large context, long-running batch ingestion, interrupted backups, and schema migration.
- Run static analyzers, format/lint checks, security checks, SCA, license checks, vulnerability scans, and build warning review. Enforce deterministic dependency locks/pinning where practical.
- Treat **critical/high severity defects, silent data loss, privacy leaks, invented citations, nonfunctional core controls, false backup success, failed restore, and nonfunctional offline inference** as release blockers. No knowingly broken feature may be represented as functioning.
- A passing CI test is not equivalent to tested real Windows hardware. Where acceptance evidence is missing, state **NOT VERIFIED** and give the exact executable test command/workflow needed, rather than saying the application is finished.

## 13. Required codebase architecture and deliverables
Produce a coherent maintainable repository, not a scattered script pile. Suggested modules:
- `WorkIntelligence.Desktop` (WPF UI and accessibility)
- `WorkIntelligence.Core` (domain, services, state machine)
- `WorkIntelligence.Inference` (llama.cpp worker lifecycle/protocol)
- `WorkIntelligence.Models` (model library, downloader, verification)
- `WorkIntelligence.Ingestion` (file parsing, OCR, watchers)
- `WorkIntelligence.Retrieval` (embeddings, lexical/vector search, citations)
- `WorkIntelligence.Memory` (approved durable memory/policies)
- `WorkIntelligence.Storage` (encrypted SQLite and attachment store)
- `WorkIntelligence.Security` (authorization, secrets, crypto, audit)
- `WorkIntelligence.Backup` (snapshot, restic/rclone, OneDrive, restore)
- `WorkIntelligence.Diagnostics` (health, logs, incident bundles)
- `tests/` (all levels of tests and synthetic data)
- `deployment/` (repeatable self-contained Windows release commands, signing and manifests)
- `docs/` (setup, troubleshooting, architecture, security model, backup/restore, operator manual)

Final output should contain, to the extent execution facilities permit: **all source code; solution/project files; local resource manifests; dependency locks and attributions; unit/integration/QA test source; reproducible build scripts and Windows CI pipeline; clean build output; published `WorkIntelligence.exe` when actually built; downloadable source/release packages; hashes; setup/use manual; backup/recovery guide; and an evidence-based verification report**. When unavailable, mark the specific artifact missing, not complete.

## 14. Mandatory handoff and reporting format
Provide an honest final engineering report with:
1. Implemented features mapped to requirements and code locations.
2. Exact build environment, pinned versions, model files/hashes, and commands executed.
3. Tests **actually executed**, actual pass/fail counts, and evidence/log paths.
4. Root-cause and regression record for each significant defect repaired.
5. The 99-angle audit ledger with **PASS / FAIL / NOT VERIFIED / NOT APPLICABLE** and evidence.
6. Windows clean-install, offline-chat, model-switch, background-backup, encrypted-upload, and second-PC-restore outcomes **only if actually performed**.
7. Measured EXE size, model space and RAM/tokens-per-second **only when measured**; otherwise mark as estimates/not tested.
8. Remaining limitations, unverified scenarios, security concerns, licensing/enterprise approval requirements, and concrete steps to close them.
9. Direct links to artifacts that genuinely exist and have been checked; no invented links or files.

## 15. Do not do these things
- Do not provide a UI mockup and label it complete.
- Do not skip features or replace real functionality with stubs, sample responses, or fake green indicators.
- Do not assert there are “zero bugs” without evidence; testing can reduce risk, not prove perfection.
- Do not declare success after compilation without launching and exercising the real application.
- Do not test a fake model and claim GPT-OSS/Qwen model compatibility.
- Do not claim encrypted OneDrive recovery without a verified restore of real encrypted test snapshots.
- Do not instruct me to install a dozen prerequisites that the single-EXE contract requires you to bundle.
- Do not remove security features, hide exceptions, disable tests, or simply relax constraints to force a green build.
- Do not send organizational data to an external AI service or unapproved cloud account.
- Do not make me repeat the same bug report to get another verification pass.
- Do not invent any tool execution, build, test, repository, source, link, benchmark or result.
- Do not ask unnecessary questions already resolved by this directive; make documented engineering decisions unless missing authorization is genuinely essential.

## 16. Execution instruction — start now
**Start implementing Work Intelligence AI, not another conceptual proposal.** First inspect the actual source repository and any attached architecture documents, inventory what exists, and verify the implementation state; do not infer that code exists merely because a specification does. Build complete code across the application, test and fix it until the supported release gates pass. Whenever errors occur, investigate thoroughly, perform root-cause fixes, recompile, re-test the affected path, re-run regression and adversarial checks, and conduct another independent audit. Repeat without waiting for me to request corrections. Deliver the most complete truthful working build and evidence attainable in the environment. If mandatory real-Windows/model/OneDrive tests remain blocked, explicitly report those blockers and **do not declare a final verified release**.

**Success is demonstrated behavior and a trustworthy recoverable application, not a confident explanation of what the code ought to do.**
