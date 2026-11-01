# Source review and regression results — 2026-11-01

This review implements the user's current request: inspect the uploaded source from multiple angles, fix defects, and recheck the fixes. **Do not package an executable yet.** The older handoff's instruction to deliver an EXE is deferred by that request. This is a tested source candidate, not an assertion that the application has no remaining issues or that all 106 product requirements are complete.

## Verified result

| Check | Result | Evidence under `docs/qa/source-review-2026-11-01/` |
|---|---|---|
| WPF application and original test assembly compilation, Release | PASS: 0 warnings, 0 errors | `build.log` |
| Production services exercised on Linux with real SQLCipher/restic | PASS: 72 executed, 72 passed, 0 failed, 0 skipped | `core-tests.trx`, `behavioral-tests.log` |
| Original unit cases in the Linux service harness | PASS: all 14 cases executed | Included in the 72 above, not additional cases |
| Source structure QA | SOURCE CHECK: 72/72 | `static_qa.log` |
| Source/SQLite smoke QA | SOURCE CHECK: 77/77 | `recheck_qa.log` |
| Independent historical QA script, updated for exact snapshot IDs | SOURCE CHECK: 21/21 | `independent_round3_qa.log` |
| NuGet advisory check, application and original test project, transitive packages included | PASS: no known vulnerable packages reported by the configured source | `dependency-audit.log`; this is not a security certification |
| Reusable installation script, initial run and repeat run | PASS | `setup.log`, `setup-repeat.log` |
| Original Windows test runner on Linux | BLOCKED / NOT VERIFIED: testhost aborted because `Microsoft.WindowsDesktop.App` is unavailable on Linux | `windows-test-attempt.log`; no Windows tests are claimed as passed |
| Windows EXE packaging | NOT APPLICABLE to this review: deferred by the user | No publish/packaging command executed; no EXE in the source delivery |

Environment: Linux x64, kernel 6.18.57, glibc 2.41; official .NET SDK 10.0.403; SQLCipher 4.12.0 from commit `6404ee168a2ae53fd029f6e6775fb70a44f11535` (SQLite core 3.51.1), FTS5 enabled; official restic 0.18.0 Linux x64. Linux dependencies are for development tests only and are not a vetted Windows runtime bundle.

## Reproduction and repair cycles

The original static script passed **71/71** while the actual initial compiler run failed. Compiler fixes were made first, then a separate service test harness linked the **actual production `.cs` files** rather than reimplementing the services in Python or replacing SQLCipher with plaintext SQLite. The encrypted-storage tests use newly created temporary vaults; only synthetic fixtures and synthetic passwords are used.

| Cycle | Before repair | After repair / final evidence |
|---|---|---|
| Actual compilation | Missing WPF implicit imports, unreachable exception catch, unawaited dispatcher invocation, async `EndOfStream` analyzer failure | Final Release build succeeds with warnings treated as errors |
| First behavioral regressions | 13 failed / 35 executed | Same 35 passed after repairs; `baseline-regressions.*` and `first-fixed-regressions.*` |
| Second perspective: changed paths and PDF fixtures | 3 failed / 43 executed | Folder replacement and short/blank PDF cases pass in final suite; `second-pass-before.*` |
| Real restic recovery with a newer foreign snapshot | 1 failed / 54 executed | Recovery selects the tagged application snapshot; `backup-before.*` and final suite |
| Added adversarial and lifecycle coverage | Extra cases cover archive paths, KDF parameters, OS locks, child-process cancellation, schema refusal, and memory approval | Final total: 72/72 |

These counts describe evolving suites, not a count of distinct defects. Two initial test assumptions were corrected before the recorded 13-failure baseline: SQLCipher may reject a wrong key during `Open()`, and `AuthenticationTagMismatchException` is a valid `CryptographicException` subtype. Neither was treated as an application defect.

## Corrections

| Finding | Root cause and correction | Verification |
|---|---|---|
| Failed import could delete an existing encrypted attachment (high) | Cleanup deleted the intended blob path even when `FileMode.CreateNew` failed because that file already existed. Track successful file creation and remove only files created by the current import. Preserve SQL rollback. | `FailedImportNeverDeletesAPreexistingAttachment`, real encrypted DB |
| Reimport after deleting newest version could leave a document unsearchable | Identical-hash short circuit ignored `active=0`. Only skip an unchanged **active** version; an inactive retained version can be reimported as an active revision. | `ReimportAfterDeletingNewestVersionRestoresSearchability` |
| Workspace assignments were lost on source revision; unchanged monitored files remained unassigned | Copy prior revision's workspace links within the import transaction. Assign scanned documents even when content import is skipped as unchanged. | `RevisedDocumentRetainsWorkspaceMembership`, `ScanningAlreadyImportedFileAssignsItToApprovedWorkspace` |
| Approved folder could be replaced by a symlink to unauthorized data (high) | Approval checked the directory once. Validate the directory and ancestors again on each scan, reject reparse points, and report unavailable/unsafe folders without silently ingesting them. | `ReplacedApprovedFolderDoesNotFollowASymlink` |
| Short searchable and blank PDF pages unnecessarily required native OCR | A text-length threshold triggered OCR even with no image content. Invoke the short-text OCR fallback only on pages containing images; omit empty page markers so a wholly blank document cannot masquerade as meaningful indexed text. | Actual PdfPig-built short and blank PDF fixtures |
| Unicode and DOCX structural text were mishandled | Strip UTF-8 BOM, strictly decode BOM-marked UTF-16/UTF-32, reject malformed/binary text, preserve DOCX tabs and line breaks. | Unicode theories, malformed text rejection, DOCX formatting and export/import round trip |
| Truncated or failed responses appeared complete (high for answer reliability) | Old line-by-line SSE parser silently accepted EOF/error events and could not handle multiline data. Assemble SSE events, require `[DONE]`, reject error/abnormal finish events, bound event length, and observe cancellation. | Seven stream tests: Unicode/comments, multiline data, EOF, server error, token limit, cancellation, oversize |
| A response could be written to a different chat; preparation failure could leave Send disabled | Capture chat ID before awaits; include retrieval and preparation in the cleanup scope; prevent chat create/delete/selection changes while sending; restore controls even if persistence fails. | Source inspected and WPF-compiled; interactive Windows UI remains NOT VERIFIED |
| Closing could dispose keys while imports/scans or partial-response persistence were running | Track the active UI operation, serialize periodic scans with UI operations, cancel on shutdown, await operation completion before disposing vault keys, and propagate shutdown cancellation into long operations. | Source inspected and WPF-compiled; actual closing/UI interaction remains NOT VERIFIED |
| Native commands could still be running after cancellation returned | Shared termination logic kills the process tree and awaits OS exit before releasing locks/resources; cancellation callbacks tolerate unstarted/disposed processes and native exit races. Model switching also waits for termination. | Real `/bin/sh` child-process cancellation/exit tests; Windows/native-model behavior remains NOT VERIFIED |
| Recovery selected the repository's latest snapshot regardless of ownership (high) | Restrict recovery to `work-intelligence`-tagged snapshots and verify restored bytes. Backup verification now uses the exact snapshot ID returned by restic's JSON summary, with fail-closed validation of that summary. | Real restic foreign-snapshot reproduction, round-trip recovery and wrong-password tests; summary tests |
| Per-instance document lock did not coordinate GUI and scheduled processes | Retain OS file locks around blob/DB snapshots and mutations, and around each repository's local backup/remote copy/check. Lock files remain stable between operations; waits are cancelable and bounded. | Separate Linux process cannot acquire a held lock, can acquire after release; cancellation and repository-path normalization tests. Windows scheduling remains NOT VERIFIED |
| Runtime extraction could follow existing reparse directories or ambiguous archive paths | Reject reparse paths, traversal with either path separator, drive/alternate-stream paths and duplicate case-insensitive names; validate requested component filename. Keep embedded checksum validation and atomic hash-verified writes. | Eleven runtime/archive cases, including repeated extraction and tamper repair |
| Malformed vault metadata could request excessive key-derivation work | Bound metadata size and iteration count; validate salt/nonce/verifier lengths before deriving keys. Existing schema-1, 500,000-iteration vaults remain compatible. | Malformed parameter and excessive-KDF tests; existing vault encryption/authentication tests |
| Failed scheduled-task reconfiguration deleted prior working credentials | Promote newly protected settings only after Task Scheduler registration succeeds; registration failure leaves existing secrets in place. | Source inspected and WPF-compiled only; DPAPI/Task Scheduler behavior remains NOT VERIFIED |
| Loopback HTTP could inherit proxy/redirect settings | Disable proxies and redirects specifically for authenticated loopback chat/embedding clients; add localhost CORS policy to the embedding worker. Model downloads continue using their separate verified HTTPS path. | Source inspected and compiled; network capture with real Windows model workers remains NOT VERIFIED |

## Reproduction commands

From the checkout root on Linux x64 (Python 3.12+, GCC, make, Tcl and OpenSSL headers):

```bash
bash scripts/setup-linux-tests.sh
bash scripts/validate-source.sh
```

The setup script verifies published/pinned SDK, SQLCipher source and restic artifact checksums with TLS verification enabled. Tools are retained outside the checkout in `../.tools`, overridable through `WI_TOOLS_ROOT`. QA results default to `/tmp/workintelligence-qa`, overridable through `WI_QA_RESULTS_DIR`. It uses a separate Python QA virtual environment. Do not use an existing work vault as a test fixture; the core test module always selects a fresh temporary data root.

The exact Windows-target cross-compilation command exercised here is:

```bash
dotnet build WorkIntelligence.slnx -c Release --no-restore -m:1 -nr:false \
  -p:UseSharedCompilation=false -p:UseAppHost=false -p:SelfContained=false -p:PublishSingleFile=false
```

`./scripts/Validate-Source.ps1` supplies the corresponding Windows source-build/unit-test route. It is provided for the next Windows check and is **not represented as executed here**. The updated GitHub workflow has a Linux regression job and preserves the Windows unit-test job; the action tag SHAs were checked against the official repositories. **No hosted CI run was triggered or observed.**

## Remaining work and boundaries

The existing 106-item requirement register is retained, including 36 P0 and 70 P1 entries. [The review traceability supplement](handoff/REVIEW_TRACEABILITY_2026-11-01.md) lists every ID and evidence for the aspects exercised here; a passing component test does not close its whole product requirement.

The following are still material limitations:

- Windows WPF startup, interaction, shutdown, DPAPI, actual SQLCipher DLL resolution and all Windows native packaging/ACL behavior remain unverified. The source build is not an interactive Windows acceptance run.
- GPT-OSS/Qwen model inference, prompt templates, reasoning display, real SSE/network behavior, embedding quality, CPU/RAM benchmarks and native OCR/PDFium have not been executed. No model weights were downloaded.
- OneDrive OAuth/account configuration, remote byte verification, scheduled jobs in supported Windows session states, and recovery on another computer were not tested. Local restic recovery on the same Linux machine does not establish cross-PC recovery.
- Workspace membership preservation is repaired; **workspace authorization/isolation is not implemented**. Retrieval and conversation/memory storage are currently global. Do not rely on workspace selection as a confidentiality boundary.
- Folder scanning still does not reconcile deleted/renamed source files; policy supersession, complete per-model settings, vision, PDF export, retention/key rotation, and other missing/partial requirements remain open. The earlier handoff documents these gaps; none are silently declared complete here.
- Path tests do not constitute a complete Windows security review or protection against a malicious process already running as the same user. File-lock coordination applies to cooperating application processes using the same configured data root; external tools and aliases require operational care.
- The scheduled-settings ordering change, loopback client policy and UI lifetime changes have compiler/source evidence, not completed Windows runtime acceptance.

No real credentials, real work documents, model files, databases, native release binaries, dependency caches or compiled output are included in the source delivery. The original upload remains preserved. The older handoff manifest describes that original upload, not the modified source; the delivery includes a new file checksum manifest.

## Official sources consulted

- [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json): official SDK URL and SHA-512.
- [SQLCipher build instructions](https://github.com/sqlcipher/sqlcipher/tree/v4.12.0): codec, temporary-store, threading and crypto build requirements; source commit pinned in setup.
- [WHATWG SSE parsing specification](https://github.com/whatwg/html/blob/main/source#parsing-an-event-stream): multiline `data` handling, blank-line dispatch, incomplete-event EOF behavior. Accessed through the official GitHub source when the standalone site was outside the network allowlist.
- [llama.cpp server documentation](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md): local authentication, CORS and completion/reasoning interfaces. No claim of native compatibility testing with a particular binary build.
- [restic 0.18.0 backup JSON](https://github.com/restic/restic/blob/v0.18.0/internal/ui/backup/json.go) and [restore implementation](https://github.com/restic/restic/blob/v0.18.0/cmd/restic/cmd_restore.go): full snapshot ID summaries, tag filtering and `--verify`.
- [SQLite B-tree implementation](https://github.com/sqlite/sqlite/blob/master/src/btree.c): row ID allocation/reuse context for failed-import/orphan handling.

Evidence hashes and current gate status are recorded in [EVIDENCE_LEDGER.md](handoff/EVIDENCE_LEDGER.md).
