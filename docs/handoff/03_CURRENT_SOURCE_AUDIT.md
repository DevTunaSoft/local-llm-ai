# Baseline source audit — November 1, 2026

## Source actually included
The ZIP is the latest `WorkIntelligenceAI_Repair_Source_2026-11-01.zip` source package. It contains the WPF project, tests, scripts, CI and historical docs. The C# code is **not changed** by the Codex handoff; these added documents describe and gate further development. Confirm source version by SHA in `START_HERE_CODEX.md`.

### Code paths present in source (not runtime proof)
- UI: `WorkIntelligence/MainWindow.xaml` and `.xaml.cs`, `App.xaml.cs`.
- Vault and database: `Services/Vault.cs`, `KnowledgeStore.cs`, `KnowledgeStore.Work.cs`; schema version 3; SQLCipher pragma check, encrypted blobs, FTS5, chunk vectors, memories, chats, workspaces/tasks/proposals.
- Models: `ModelRuntime.cs`, `EmbeddingRuntime.cs`, `ModelDownloader.cs`, `HardwareProbe.cs`, `RuntimeBundle.cs`.
- Documents: `DocumentExtractor.cs`, `OcrService.cs`, `PdfPageOcr.cs`, `DocumentExporter.cs`.
- Backup and network: `BackupService.cs`, `RemoteBackupService.cs`, `ScheduledBackup.cs`, `AuditLog.cs`.
- Windows packaging: `WorkIntelligence.csproj`, `scripts/Build-Windows.ps1`, `vendor/README.md`, `.github/workflows/windows-build.yml`, `.slnx`.
- Tests: `tests/Tests.cs`, three Python source QA scripts; historical logs under `docs/qa/`.

### Known missing or partial requirements / must-check source risks
1. **No actual EXE**. No .NET SDK / WPF desktop compilation or execution result from previous environment. Restore/package may fail; all warnings errors.
2. **Native dependency gap**. No vendor runtime binaries, approved hashes/licenses, or `runtime.zip`. Script blocks distribution. Verify all dependent DLLs for llama, SQLCipher, OCR/pdfium and restic/rclone and their secure runtime extraction.
3. **SQLCipher provider migration**. Latest source references `SQLitePCLRaw.provider.sqlcipher` and loads `sqlcipher.dll`; linkage/interoperability with bundled native binary is NOT verified. Explicitly test `PRAGMA cipher_version`, encrypted on-disk header, credentials, migrations, fail-closed behavior.
4. **Two primary models untested**. Generic GGUF integration is not evidence of correct GPT-OSS Harmony or Qwen3.5 prompt/template/reasoning. Need 32GB RAM and sequential lifecycle benchmarks; SSE behavior test.
5. **Semantic search code exists but end-to-end untested**. A separate embedding model setup is necessary; validation, source grounding, retrieval quality and workspaces need tests. Ensure vectors are never mixed across embedding revisions.
6. **PDF/OCR code exists but depends on unbundled PDFium and Tesseract**. P/Invoke resolver maps `wi_pdfium` to `pdfium.dll`; verify native ABI, renderer DPI, language assets, images and scanned/mixed PDFs, temp data protection. OCR feature is NOT certified.
7. **Folder monitoring is scan-based and user-triggered in UI**, not clearly truly automatic. Test change, delete, rename and permission loss. Add dependable scheduler/watcher.
8. **Knowledge approvals are rudimentary**; no full automatic proposals from chat, contradiction handling, effective dates, provenance reconciliation, structured policy supersession.
9. **Workspace/task features are minimal**; likely missing owners, deadlines, risks, milestones, permissions, decisions and real workspace-filtered search.
10. **Document Studio currently exports DOCX**. PDF export, robust formatting and citations remain to be built/tested.
11. **Model benchmark/compare handlers exist** but no real speed verification or hardware tuning; automatic task-based model choice not implemented.
12. **ScheduledBackup.cs registers `/IT` Windows Task Scheduler task**. `/IT` means it operates only when that user is signed in; it does not satisfy a blanket logged-out guarantee. Missed-run/catch-up, shutdown, sleep and backup success reporting are not verified.
13. **RemoteBackupService.cs assumes an already configured `rclone` remote**. No OneDrive OAuth onboarding wizard; no actual authorized remote test or fresh-PC recovery; rclone config/credentials continuity needs careful security design.
14. **Backup retention / key rotation / secure deletion of historical snapshots** are not complete. Need enforceable documented policies and safety checks.
15. **Windows test coverage limited**. Existing tests mostly chunking, small parsers, unsafe URL rejection and source-bundle guardrails. Add integration tests for every system boundary.
16. **Privacy adversarial testing absent**. Audit on-disk temp caches, logs, SQLCipher provider, prompt injection, SSRF, local loopback API, permissions, model files, rclone tokens and secret lifecycle.
17. **Release versioning and signed updates/rollback** have not passed any acceptance test; verify CI actions support and change compatibility.
18. **Potential unsafe assumptions in historical docs**: Old `README.md`, `RELEASE_GATES.md`, `vendor/README.md` contain pre-repair statements such as scanned-PDF OCR being absent or old SQLCipher provider being used. Do not use these historical notes as the current truth; code and newer status + tested evidence govern. Reconcile and update them during implementation.

### Specific tests to initiate first on Windows
```
dotnet --info
dotnet restore WorkIntelligence.slnx
dotnet build WorkIntelligence.slnx -c Release --no-restore
dotnet test tests/WorkIntelligence.Tests.csproj -c Release --logger 'console;verbosity=normal'
```
Fix compile errors before expanding scope. The release packaging script only runs once all vetted native assets and `vendor/SHA256SUMS.txt` are present. A first successful build is *not* Windows acceptance.

### Source QA provenance
The last repair source reported Python static 71/71, recheck 76/76 and independent 21/21. No credible claim was made that these source scans proved working WPF, SQLCipher, llama.cpp, OneDrive or cross-PC restore. Do not conflate them.
