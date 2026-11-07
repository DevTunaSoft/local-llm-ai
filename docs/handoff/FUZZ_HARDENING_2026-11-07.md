# Work Intelligence AI — Multi-round fuzzing and hardening ledger
**Date:** 2026-11-07
**Repository:** DevTunaSoft/local-llm-ai
**Verified source SHA:** `2c200b05981e4e210feef5e1c2fd1082edb0c2e2`
**Consolidated CI:** https://github.com/DevTunaSoft/local-llm-ai/actions/runs/42336900019
**Result:** five of five consolidated jobs passed (Windows build/service regressions, Linux service regressions, real Windows WPF UI smoke, real small-GGUF inference/embeddings, 106-item traceability). Passing smoke tests are not release acceptance.

## Hardening rounds (distinct execution paths)
| Round / risk | Root cause in inspected source | Permanent correction | New executable regressions | Main commits |
|---|---|---|---|---|
| 1. Corrupted snapshot manifest | Null JSON `Path` field entered filename validation and could throw an unexpected exception, preventing fallback to an older good snapshot | Fail closed with a controlled integrity exception | Five mutated manifest types: null path, null hash, duplicate record, traversal, impossible length; recovery uses older valid snapshot | `098d32ce`, `f63a7c09` |
| 2. Untrusted SSE model output | Invalid JSON shapes could throw unmanaged exceptions or silently appear to produce an empty successful answer; JSON nesting was unbounded | Validate root/choice/delta shapes, cap parser depth, normalize malformed payload failures | Malformed JSON/primitives/choices, strict error expectations, seeded 48-case malformed event and deep nesting fuzz, cancellation/EOF tests | `aba65fc8`, `06319a8e`, `87c3d52e`, `2c200b05` |
| 3. Concurrent model preference writes | A process-local `lock` could not prevent separate GUI/scheduled processes from losing changes | Shared OS lock for preference reads and writes | Forty concurrent per-model write/read transactions; verify every key remains after all writers finish | `2c80cabb`, `dfd26cc8`, `347e1ba4` |
| 4. Competing restores | Empty-vault check preceded shared serialization, so two restoration sources could race to publish different data | Vault-wide OS restore lock acquired **before** emptiness check | Eight simultaneous restores; exactly one succeeds; verify database integrity, memories and document search | `e5db80a9`, `a0295f3f` |
| 5. Model download trust and capacity | Reserved Windows filenames and overly large resumed/streaming downloads could cause unsafe device-path handling or disk exhaustion | Reject reserved device names, credentialed/wrong-origin URLs and over-64-GiB downloads with overflow-safe arithmetic | Origin/name injection tests plus seeded 150 filename mutations and 150 transfer-size boundaries | `07517e06`, `8a177839` |
| 6. Native PDF/OCR integration | Development-mode runtime rejected even hash-pinned PDFium DLL; normal 25-MiB image input limit also rejected valid high-resolution internally rendered scanned PDF pages | Permit only pinned `pdfium.dll` in dev runtime; separate bounded internally generated BMP OCR path, keeping external upload cap | DLL hash/tamper checks; large synthetic PDF bitmap, corrupted headers and 128 randomized BMP header tests | `fd94f92f`, `29564318`, `97e0164d`, `b22ebefd`, `f825d4d2` |
| 7. Native archive partial extraction | Runtime ZIP extraction began writing files before validating the last archive entries | Preflight complete archive metadata, duplicates, symlinks, bounds and paths before the first file write | Four malicious late-entry archives must leave zero extracted native components | `ee673727`, `d39eb38f` |
| 8. OCR/OneDrive subprocess output exhaustion | `ReadToEndAsync` buffered arbitrary native stdout/stderr without a size ceiling; timeout was missing for some remote operations | Bounded streaming pipe reader; kill/cancel on output overflow; bound OCR/rclone logs and remote copy/verification durations | Exact-boundary and 160 seeded output-size fuzz cases, cancellation behavior; full source/service regressions | `9fc86ac0`, `9f70c8ae`, `d39f51df`, `3c557ab3` |
| 9. Corrupt local data declared remotely verified | rclone's byte equality could succeed when both copies contain an already-corrupt snapshot | Verify every local snapshot and manifest **before** starting OneDrive upload | Corrupt database fixture must be refused before runtime/network access; source files preserved | `e3b64b6d`, `0121bd09` |

## Tested versus unverified
**Verified in GitHub Actions:** the above source-service scenarios compiled and passed across the consolidated Windows/Linux test jobs at the source SHA above. Real small-GGUF streaming and local embedding smoke and the tested WPF controls still pass. Earlier failures prompted fixes; do not count an earlier green commit as proof of a later untested change.

**Not verified:** execution of real native Tesseract/PDFium on a mixed/scanned production PDF; true random coverage-guided fuzzing, native ASAN or long-running soak; aggressive disk-full injection; authentic OneDrive OAuth, remote copy and replacement-PC restore; 20B/27B CPU-only model performance on the intended laptop; full 106 requirements and all UI functions; signed clean-machine single-file EXE. These are pending acceptance, not green checks.

## Next independent fuzzing rounds
1. Real Tesseract/PDFium fixture matrix: scanned/mixed PDFs; pages of extreme dimensions, invalid fonts and corrupt image filters; isolate native crashes with process supervision.
2. True native model fuzz/stress on target Windows laptop: GPT-OSS 20B and Qwen 27B, repeated cancellation, bad GGUF metadata, low-memory conditions and model-switch endurance.
3. Windows Task Scheduler and OneDrive fault injection with approved **synthetic-only** data: sleep/reboot, logout, expired tokens, remote quota, dropped connections, wrong account and isolated replacement-PC restore.
4. Filesystem fault injection: disk full mid-snapshot, Windows antivirus file locks, interrupted restores, SQLite WAL recovery, cleanup and retry.
5. Complete independent 106-requirement acceptance and supply-chain review before **any** final EXE packaging.

## Release policy
Keep inference entirely local. Plaintext SQLite and snapshots were explicitly chosen; warn about work-data authorization and do not claim encryption. No final EXE has been built or packaged; explicit user permission is still required. All issues in this ledger are code-level findings with post-remediation tests; not a claim of zero remaining defects or production readiness.
