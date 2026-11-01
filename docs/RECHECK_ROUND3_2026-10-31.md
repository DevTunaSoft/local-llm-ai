# Work Intelligence AI — independent third code review
**Date:** 2026-10-31
**Revision:** Source Engineering Candidate Round 3, not a Windows release

## Objective and scope
This review explicitly revisits the source archive's four known missing capabilities: semantic retrieval, OCR, unattended backups and verified OneDrive recovery. It also examines the previous misleading source-only PASS counts and prior lifecycle/backup findings. This review does not certify a complete implementation or production readiness. It was conducted in a Linux container without `dotnet`, Windows WPF, a configured Microsoft OneDrive account, packaged native Windows components or the selected full-sized models.

## New implementation work
1. `EmbeddingRuntime.cs`: adds a separate authenticated loopback-only llama.cpp embedding worker using a user-selected GGUF embedding model. `KnowledgeStore.cs`: new V2 vector table, semantic indexing, cosine retrieval with reciprocal-rank fusion and model-hash isolation. The WPF Knowledge Library gains explicit **Load Semantic Model** and **Build Semantic Index** controls. Both Chat and document search call hybrid retrieval when available. All vectors are inside the encrypted SQLite database. **Not proven against a real llama.cpp embedding server.** A separate embedding GGUF is required; no compatible embedding model is packaged in this source archive.
2. `OcrService.cs`: Tesseract image OCR through stdin/stdout without a plaintext temporary image. PNG/JPEG/BMP/TIFF import routes to this worker. The release script now requires a vetted Tesseract binary and English data files in the embedded native bundle. Synthetic English OCR command test passed under Linux. **Scanned-PDF page rasterization and OCR still unimplemented.** Image-only PDFs are now deliberately rejected, not indexed as bogus page headings.
3. `BackupService.cs`: added restic `check --read-data`, test restoration of the newest local snapshot and SHA-256 comparison of restored contents. **Cannot claim the local backup has been verified on Windows: restic was not installed or executed in this environment.**
4. `RemoteBackupService.cs`: optional rclone copy of locally encrypted restic repository to an already configured OneDrive remote; uses `rclone check --download --one-way` for remote byte comparison. Adds remote recovery via local encrypted repository download. Remote access configuration and organizational authorization must already exist. **Not executed against OneDrive; initial OAuth wizard still missing.**
5. `ScheduledBackup.cs`: Windows Task Scheduler daily job code running from the application EXE with `--scheduled-backup`, DPAPI-protected credentials scoped to the current Windows user, optional OneDrive sync. Intended to run only when that user is logged in. **Task registration, actual execution, wake/retry behavior and DPAPI flow are untested on Windows.**
6. `MainWindow.OnClosing`: replaced synchronous model Stop().GetResult() on UI thread (potential deadlock) with cancellation and async shutdown.
7. Updated unit-test expectation after adding PNG OCR, and added an invalid-image test.

## Tests ACTUALLY performed
- `python scripts/static_qa.py`: **59/59 PASS** (source/XAML wiring, file and dependency assertions; not compilation).
- `python scripts/recheck_qa.py`: **64/64 PASS** (source assertions plus SQLite schema/FTS smoke; **system SQLite, not SQLCipher**). Previously this suite failed two assertions after the V2 schema change; the test fixture was corrected and rerun. This was an expected assertion migration, not proof of final Windows behavior.
- `python scripts/independent_round3_qa.py`: **21/21 PASS** (independent check of new embedding/backup/OCR/scheduler code, SQLite schema migration and vector FK deletion).
- Linux Tesseract stdin/stdout synthetic image: **PASS**; verified recognizing `SAMSARA FLEET SAFETY WORK PROCEDURE`.
- All source/archive files separately checked for ZIP integrity in the creation process.
- These are source-oriented tests. Newly authored C# xUnit tests were **not executed**.

## Critical outstanding release blockers
- **Windows compilation has not happened.** APIs, WPF XAML binding, NuGet package resolution, native DLL dependencies and the complete EXE have not been tested. Source syntax checks are not substitute for C# compiler.
- **Deprecated SQLCipher provider remains.** `SQLitePCLRaw.bundle_e_sqlcipher` is deprecated by its maintainer. A maintained, compatible encrypted SQLite provider and actual old/new-vault migration testing are required. Do not place regulated work records in the present candidate.
- **No actual GPT-OSS 20B or Qwen3.5 27B inference test**, RAM benchmark or local embedding model test on the i7-1370P laptop.
- **Scanned-PDF OCR remains missing.** OCR of standalone images is newly coded but not Windows-tested; PDF rasterization requires an audited renderer and all native runtime dependencies and licenses.
- **OneDrive authentication is not implemented as a built-in wizard**; the new service assumes a separately configured rclone remote. No actual encrypted upload, cloud byte comparison or second-computer restoration test has been performed.
- **Scheduled backup is unverified on Windows**, depends on an interactive signed-in user, and does not yet handle full missed-task monitoring, retention pruning and unattended prompts robustly.
- **Other original master-spec scope still incomplete:** model comparison and automatic selection, full workspace permissions, document studio, project tracking, scanner/vision input, source-citation verification, granular lifecycle/indexing progress, folder monitoring, signing/SBOM, secure deletion across backup retention, key rotation, multi-PC conflict reconciliation, complete 99-angle Windows acceptance.

## Acceptance standards
No source-level pass score can be interpreted as proof of the product's operation. Remaining work must be finished and then verified by `dotnet restore`, `dotnet build`, actual xUnit execution, WPF UI testing, staged native bundle verification, model-inference benchmarks, OCR/PDF functional tests, DPAPI/Task Scheduler tests, real OneDrive OAuth/transfer/data verification, and cross-PC encrypted restore with an independent network-disconnected readback. Critical/high issues cannot be waived for production.
