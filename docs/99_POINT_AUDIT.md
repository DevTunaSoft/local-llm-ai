# Work Intelligence AI — 99-Angle Audit Matrix

> **Audit date:** 2026-10-31. A **PASS (SOURCE)** means a source-level structure or presence check succeeded, **not** that the feature works in a compiled Windows application. **NOT VERIFIED** means runtime or independent tests are outstanding. **NOT IMPLEMENTED** denotes missing functionality. These results must not be used as production release certification.

Automated source structure: **53/53 passed** with `python scripts/static_qa.py` in the Linux environment. C# build, unit tests, Windows EXE, native inference and backup restore **not executed**.

## 01 Requirements & architecture

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 1 | User requirement completeness vs supplied master prompt | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 2 | Target hardware recorded accurately | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 3 | Local inference separation from backup transport | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 4 | Single-EXE application strategy | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 5 | External GGUF model storage | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 6 | Vault data outside EXE | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 7 | Encrypted OneDrive recovery design | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 8 | Source provenance and feature traceability | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 9 | Unimplemented requirements flagged honestly | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |

## 02 Build & packaging

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 10 | WPF XAML parses as XML | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 11 | WPF event names resolve in code | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 12 | C# compiles with warnings as errors | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 13 | Pinned NuGet packages restore cleanly | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 14 | Windows build works on clean runner | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 15 | Single EXE contains all non-model dependencies | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 16 | Native extracted DLLs load securely | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 17 | No separate .NET/Python/Node/Ollama installation needed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 18 | EXE runs on clean Windows 11 Pro | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 03 Security & privacy

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 19 | SQLCipher-only provider configured | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 20 | SQLCipher crypto verified at runtime | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 21 | Vault passphrase derivation checked | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 22 | Authenticated imported file encryption checked | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 23 | Sensitive text excluded from logs | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 24 | External AI-provider endpoints absent | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 25 | Local llama endpoint bound to loopback | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 26 | Local llama endpoint authentication effective | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 27 | Prompt injection red-team tests completed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 04 Model engine & resources

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 28 | GGUF signature verified on import | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 29 | GGUF SHA256 verified on load | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 30 | GPT-OSS 20B actually loads | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 31 | Qwen3.5 27B actually loads | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 32 | Harmony and Qwen chat templates correct | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 33 | Streaming SSE outputs all expected content | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 34 | Cancel and model switching correctly recover | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 35 | RAM and CPU guard effective on i7-1370P | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 36 | Larger compatible GGUF models safe to add | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 05 Retrieval & memory

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 37 | FTS5 schema and indexes defined | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 38 | FTS5 returns relevant document excerpts | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 39 | Vector embeddings integrated | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 40 | Hybrid ranking validated | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 41 | Approved memory persists across restart | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 42 | Memory correction and conflict resolution | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 43 | Document versions supersede old active version | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 44 | Citation IDs always map to real source | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 45 | Knowledge is preserved when switching LLMs | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 06 Document processing

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 46 | Plain UTF-8 text/CSV/JSON imports | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 47 | PDF extraction preserves meaning | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 48 | DOCX extraction including tables | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 49 | XLSX numerical and shared-string extraction | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 50 | PPTX slide text extraction | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 51 | Scanned-PDF OCR implemented and tested | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 52 | Image/screenshot understanding implemented | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 53 | Malformed ZIP/XXE/oversized file blocked | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 54 | Document deletion and backup retention policy | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 07 Backup and recovery

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 55 | Restic local repository encryption configured | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 56 | Consistent SQLCipher backup snapshot | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 57 | Document files included with snapshot | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 58 | Backup metadata integrity checked | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 59 | Actual backup test restoration completed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 60 | OneDrive sync completion independently confirmed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 61 | Scheduled unattended backups when UI closed | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 62 | Interrupted upload/retry/fault recovery | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 63 | Restore on different Windows PC without old credentials | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 08 User interface

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 64 | Vault unlock/create controls wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 65 | Model import and manager controls wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 66 | Local chat send/stop wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 67 | Document import/delete/search controls wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 68 | Memory add/delete/export controls wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 69 | Backup and restore controls wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 70 | Diagnostics controls wired | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 71 | Every UI button functionally exercised | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 72 | Keyboard/accessibility/WPF UI automation passed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 09 Reliability & fault injection

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 73 | Wrong vault passphrase handled | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 74 | Model file corruption handled | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 75 | Native tool tampering detected | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 76 | Untrusted download redirect blocked | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 77 | Checksum mismatch removes bad model file | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 78 | Disk full during document import rollback | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 79 | Unexpected shutdown and restart recovery | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 80 | Concurrent writes and UI race resilience | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 81 | Cancel during backup and restore safely | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## 10 Updates & portability

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 82 | Independent JSON/Markdown export | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 83 | Original documents safely encrypted | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 84 | Backups allow recovery without source computer | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 85 | Database migrations versioned and tested | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 86 | Key rotation and credential recovery | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 87 | Model architecture upgrades do not lose content | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 88 | Native runtime upgrade rollback | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 89 | Backup retention and secure deletion | NOT IMPLEMENTED | Required source feature absent or incomplete. |
| 90 | Versioned signed executable update process | NOT IMPLEMENTED | Required source feature absent or incomplete. |

## 11 Release quality

| # | Verification angle | Result | Evidence / blocker |
|---:|---|---|---|
| 91 | Source-only QA script passes | PASS (SOURCE) | Code/structure or manual source review; Windows behavior not tested. |
| 92 | All xUnit tests executed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 93 | Security dependency and license audit done | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 94 | Windows model benchmark documented | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 95 | Windows UI end-to-end tests executed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 96 | 99-point matrix supported by test evidence | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 97 | Critical and high defects all resolved | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 98 | Verified EXE and source archives produced | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |
| 99 | Independent second-PC restore certification passed | NOT VERIFIED | Needs Windows .NET build, runtime, real hardware, external assets or independent test. |

## Assessment

**99 checks enumerated:** 28 source-only checks, 12 not implemented, and 59 awaiting verification. All application-level release gates remain open.
