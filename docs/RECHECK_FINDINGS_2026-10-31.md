# Independent code recheck — 2026-10-31

## Verdict

**Source engineering revision only. NOT a compiled Windows executable or production release.**

The original 53 structural checks passed, but a separate code audit identified actual defects. The corrected package is distinct from the earlier source archive. This report does not claim that code changes are runtime-verified.

## Confirmed source defects found and corrected

| ID | Severity | Finding | Corrective change | Verification |
|---|---|---|---|---|
| WI-001 | Critical | Runtime loader required `WorkIntelligence.runtime.sha256`, while project embedded only `runtime.zip`; a correctly packaged EXE could not load native tools | Register both resources in `.csproj` | XML, source assertion and xUnit pairing test authored; packaged Windows launch unverified |
| WI-002 | High | Client sent `model: local`, while server advertised model path by default; model routing could fail | Pass `--alias work-intelligence-local` and use that name in chat payload; restrict CORS | Source asserted; llama-server integration unverified |
| WI-003 | High | Stopping a model during startup could race disposal of the child process | Serialize lifecycle with a semaphore; cancel startup through UI token | Source asserted; concurrent Windows test pending |
| WI-004 | High | AI start failure before readiness block could leave native process / HTTP resources partially initialized | Cleanup on all early exits in lifecycle finally | Source asserted; fault injection pending |
| WI-005 | Medium | Five-minute startup timeout could incorrectly reject large CPU-only models | Fifteen-minute cancellation-aware timeout | Source asserted; performance validation pending |
| WI-006 | High | Backups copied the DB and blob files without a shared write lock | Added `CreateBackupSnapshot` gate shared with document import/delete | Source asserted; backup consistency integration test pending |
| WI-007 | High | Backup repository could be created inside other application directories, potentially causing recursion/unsafe backup state | Reject any repository beneath the application root | Source asserted; negative-path test pending |
| WI-008 | High | Database `user_version` was reset to 1 unconditionally, even on newer vaults | Refuse newer unsupported schema before any schema updates | Source asserted; migration tests pending |
| WI-009 | Medium | DB connection could remain undisposed if SQLCipher provider initialization failed | Dispose opened connection on error | Source asserted; SQLCipher fault test pending |
| WI-010 | Medium | Partial model responses could be lost or recorded inconsistently after interruption | Persist incomplete response once with interruption marker | Source asserted; UI interruption test pending |
| WI-011 | Medium | Refreshing list sources could drop model selection | Preserve model selection by stable model ID | Source asserted; UI test pending |
| WI-012 | Medium | Native extraction unbounded per file and lacked symlink checks | Add archive/member size caps, reject symlinks, verify extracted bytes, retain traversal check | Source asserted; malicious archive tests pending |
| WI-013 | Medium | Backup error could display arbitrary raw stderr containing confidential file paths | Suppress raw stderr in end-user message | Source asserted; failure simulation pending |

## High-risk unresolved findings — do not claim the release is complete

1. **Deprecated encryption dependency:** `SQLitePCLRaw.bundle_e_sqlcipher` is officially deprecated and its native SQLCipher builds are no longer maintained. A maintained SQLCipher integration or approved native source build is required, followed by migration/readability testing. Do not silently swap database engines because existing encrypted data must remain readable. Upstream maintainer: https://github.com/ericsink/SQLitePCL.raw/wiki/SQLite-encryption-options-for-use-with-SQLitePCLRaw . Official SQLCipher vendor: https://www.zetetic.net/sqlcipher/sqlcipher-for-dotnet/ .
2. **No Windows compilation:** The available workspace has no `dotnet` executable, Windows runtime, or downloaded NuGet dependencies. Compile failures, API mismatches, XAML compile failures, and native packaging problems cannot be ruled out.
3. **No Windows execution:** Neither GPT-OSS 20B nor Qwen3.5 27B was launched or benchmarked on the target laptop; GGUF chat templates/reasoning have not been verified.
4. **Incomplete scope:** Vector retrieval, OCR, scanned-PDF parsing, workspaces, project tracking, export studio, monitoring folders, model comparison, scheduler, verified OneDrive remote sync, key rotation, backup retention and secure deletion, model download resume UI, and cross-computer restoration are still missing or incomplete.
5. **Backup validation insufficient:** `restic check` validates repository structure/metadata but does not prove the latest snapshot is readable, remote synchronization completed, or the vault can be restored on another PC.
6. **Model lifecycle context limits:** Prompt budgets, max tokens and reasoning format need actual tokenization and per-model tests. A large document can still exceed context.
7. **Runtime extraction security:** Writable extraction roots and race conditions require ACL review, signing, and on-machine adversarial tests.
8. **Application error handling:** Workload cancellation, closing during async work, and application crash recovery need real UI/state tests.
9. **Data governance:** Sensitive document classification, authorized OneDrive location, record deletion from retained backups, and actual security review remain necessary before business use.
10. **Release supply chain:** Official native files, hashes, licenses, SBOM and executable signing must be acquired and verified in a controlled Windows build.

## Test evidence

- `python scripts/static_qa.py`: **53/53 source structure checks PASS**.
- `python scripts/recheck_qa.py`: **57/57 source and SQLite semantics smoke checks PASS**. A first run correctly failed 2/57 and drove another correction of resource metadata and enabling SQLite foreign keys in the test fixture.
- SQLite smoke tests use Python's system SQLite, **not SQLCipher/.NET**. They validate FTS insertion/search and cascading deletion semantics only.
- `.csproj` / `.slnx` / XAML parsed as valid XML and source handlers were checked for named methods.
- Unit tests have been **authored but NOT executed**; the Windows CI configuration has **not been run** in this environment.
- **Not executed:** `dotnet restore`, `dotnet build`, `dotnet test`, WPF UI tests, native DLL loading, llama-server integration, remote backup, or restore verification.

## Next verification gates

1. Install/locate approved .NET 10 SDK on Windows, run restore/build/xUnit tests, fix errors and rerun.
2. Resolve legacy SQLCipher provider and verify encrypted DB initialize/read/write/backup/restore across upgrade.
3. Build vetted native runtime bundle, publish the executable, launch on a clean Windows PC.
4. Run representative real models on target i7-1370P / 32GB, verify prompts, SSE, reasoning output, cancellation, large contexts, and RAM limits.
5. Implement remaining master specification capabilities and run the complete 99-angle acceptance matrix, including recovery on a second authorized computer.
