# Evidence ledger — start state / do not invent PASS

> Current source review: see [2026-11-01 results](../SOURCE_REVIEW_2026-11-01.md) and the current-instance table below. The original handoff table is retained as history.

| Gate | Status as of handoff | Evidence / missing proof |
|---|---|---|
| Latest ZIP structure/integrity | PASS | Handoff generation verified ZIP CRC and extracted member integrity; see `handoff_manifest.json`. |
| Original source-level historical checks | SOURCE CHECK ONLY | Prior source logs report 71/71, 76/76, 21/21. Their specific assertions are not WPF/UI integration proof. |
| Windows restore/build (`dotnet`) | NOT VERIFIED | Prior Linux environment lacked .NET SDK; execute on Windows. |
| Windows xUnit test execution | NOT VERIFIED | Tests included, but not run under Windows .NET environment. |
| Final EXE built and launched | NOT VERIFIED | No exe or trusted native runtime shipped. |
| Native dependencies / SQLCipher runtime | NOT VERIFIED | No vetted native dependency bundle or binary manifest. |
| GPT-OSS 20B actual model inference | NOT VERIFIED | No weights or target machine benchmarking in prior environment. |
| Qwen3.5 27B actual model inference | NOT VERIFIED | No weights or target machine benchmarking in prior environment. |
| Semantic retrieval with actual embedding model | NOT VERIFIED | Code exists, model/runtime and relevance untested. |
| OCR scanned PDF on Win11 | NOT VERIFIED | Code exists, PDFium/Tesseract absent. |
| Vault encrypted at rest | NOT VERIFIED | App fail-closed code exists; native SQLCipher not executed. |
| Scheduled backup with GUI closed | NOT VERIFIED | Task registration source exists, no Windows test. |
| Authorized OneDrive upload and byte verification | NOT VERIFIED | No configured OAuth test account or remote test. |
| Separate-PC restore and offline knowledge verification | NOT VERIFIED | Not executed. |
| UI interactive full workflow | NOT VERIFIED | WPF never launched. |
| 99-point independent runtime audit | NOT VERIFIED | Source checks are not equivalent. |
| Signed release and SBOM | NOT VERIFIED | Not available. |

**Codex:** Keep this ledger current with time, machine, command, log path, output hash, requirement ID, and reviewer. No evidence = NOT VERIFIED. After any code fix, re-run impacted gates.

## Current-instance source review — 2026-11-01

Reviewer: Codex, sequential compiler, behavioral, security/path, data-integrity, recovery, concurrency and second-review passes. No separate human or independent-agent approval is implied.

Machine: Linux x64, kernel 6.18.57/glibc 2.41; SDK 10.0.403; SQLCipher 4.12.0 (SQLite 3.51.1), restic 0.18.0. Synthetic test data only. Evidence directory: `docs/qa/source-review-2026-11-01/`; exact output SHA-256 values: [SHA256SUMS.txt](../qa/source-review-2026-11-01/SHA256SUMS.txt).

| Gate / requirement aspects | Status | Exact command and artifact |
|---|---|---|
| Reusable dependencies / QA-003, QA-013 | PASS, initial and repeat setup runs | `bash scripts/setup-linux-tests.sh`; `setup.log`, `setup-repeat.log` |
| WPF-target compilation / WIN-001, QA-005 | PASS: 0 warnings/errors; cross-compilation only | `dotnet build WorkIntelligence.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:UseAppHost=false -p:SelfContained=false -p:PublishSingleFile=false`; `build.log` |
| Service behavior / QA-003, SEC-001/002, KNOW-001/002/005/012, DOC-001/002/005/007/008, BKP-001/009/011 | PASS for exercised aspects: 72/72, 0 skipped | `dotnet test tests/Core/WorkIntelligence.Core.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --logger 'trx;LogFileName=core-tests.trx' --results-directory /tmp/workintelligence-qa`; `core-tests.trx`, `behavioral-tests.log` |
| Reusable end-to-end source validation | PASS | `bash scripts/validate-source.sh`; calls preceding build/test commands with explicit test tool/data isolation |
| Three supplementary Python QA suites | SOURCE CHECK ONLY: 72/72, 77/77, 21/21 | `python scripts/static_qa.py`, `python scripts/recheck_qa.py`, `python scripts/independent_round3_qa.py`; corresponding `.log` files |
| Current NuGet advisories / QA-013 | PASS: no known vulnerable packages reported for application/original tests | `dotnet list WorkIntelligence.slnx package --vulnerable --include-transitive`; `dependency-audit.log` |
| Windows testhost / QA-004, QA-011 | NOT VERIFIED / environment blocked; aborted, not passed | `dotnet test tests/WorkIntelligence.Tests.csproj -c Release --no-build --no-restore -p:UseAppHost=false -p:SelfContained=false -p:PublishSingleFile=false`; `windows-test-attempt.log` |
| UI/chat/shutdown/scheduling corrections | SOURCE COMPILE PASS; Windows runtime NOT VERIFIED | `MainWindow.xaml.cs`, `ScheduledBackup.cs`, `App.xaml.cs`; final build plus findings table |
| Real Windows llama/embedding/OCR/native DLL behavior | NOT VERIFIED | No vetted Windows native bundle/model weights or Windows desktop available |
| OneDrive / different Windows PC / actual Task Scheduler | NOT VERIFIED | No authorized remote test account or second Windows computer used |
| Standalone EXE | NOT APPLICABLE now: user deferred packaging | No EXE built/published for this review |

A full acceptance result is not inferred from a component PASS. See [all 106 IDs](REVIEW_TRACEABILITY_2026-11-01.md) and [remaining limitations](../SOURCE_REVIEW_2026-11-01.md#remaining-work-and-boundaries). A fresh cloud task/snapshot restoration and hosted CI execution were not tested.
