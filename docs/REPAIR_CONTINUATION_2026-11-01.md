# Work Intelligence AI — source repair and QA report

**Date:** 2026-11-01. **Input:** latest `WorkIntelligenceAI_Learning_Workspace_Source_2026-11-01.zip` and the user's independent audit. **Output:** updated source only, not an EXE. **Status:** **SOURCE-LEVEL REPAIRS COMPLETE FOR THE LISTED ISSUES; FULL PRODUCT ACCEPTANCE OPEN**.

## Source changes implemented

1. **Idempotent user-approved memory suggestions.** Added `KnowledgeStore.QueueMemoryIfNew`, which checks active approved and pending proposals within the same workspace before enqueuing. Updated `Send_Click` to use this non-throwing duplicate path; previously queued or approved explicit memory requests do not abort model inference. Direct `ProposeMemory` remains strict for manual correction flows.
2. **Conversation-based contextual learning.** Added schema **6** to create external-content `messages_fts`, with triggers and a one-time rebuild to backfill earlier messages. `RecallEarlierConversations` queries *user-authored* statements, requires an existing workspace ID, filters to that workspace and excludes the current chat. Prior chat snippets have small bounds and are labeled **unverified context** in the prompt, never approved memories or elevated instructions.
3. **Watched-folder stale document reconciliation.** A successfully and completely enumerated watched folder can deactivate missing old documents without destroying imported encrypted revisions, chunks or historical records. Renamed files create a new active import and retire the old path. Empty, oversized, unsupported or unsafe symlink replacements are not treated as current. On missing/unsafe folders, interrupted scans, a 5,000-entry truncation or transient read errors, the application avoids mass retirement and reports errors. The retirement DB transaction is serialized with the cooperating application's file lock.
4. **Human oversight for conflicting knowledge.** Approval dialog shows potentially overlapping current memories, clearly not claiming semantic contradiction proof. Supersession still requires explicit selection of an old memory and approval.
5. **Optional embeddings failover.** If the local embedding query fails with supported non-cancellation errors, chat can continue using keyword retrieval. The UI explicitly reports this fallback; no semantic evidence is fabricated.
6. **Additional regression coverage.** Added actual xUnit service test cases for repeated memory commands, workspace-scoped conversation recall, cascade cleanup, deleted and renamed documents, empty-file replacement, unsafe symlink replacement, vanished watched folders and reimport. The updated SQLite source-extraction QA executes schema 6 DDL, older-message backfill, workspace filtering and FTS trigger behavior.

## Tests ACTUALLY executed in this session

| Test | Result | What it proves |
|---|---|---|
| `python scripts/static_qa.py` | **74/74 PASS** | Source/XAML/wiring checks |
| `python scripts/recheck_qa.py` | **79/79 PASS** | Source + plaintext SQLite smoke |
| `python scripts/learning_qa.py` | **32/32 PASS** | SQL extracted from C#, migrations V4/V5/V6, FTS, scoped retrieval, source guards |
| `python scripts/independent_round3_qa.py` | **21/21 PASS** | Independent historical SQLite/structure checks updated for V6 |
| `python -m compileall -q scripts` | **PASS** | Python test-script syntax |
| `dotnet build` for this patch | **NOT EXECUTED** | No .NET SDK available in this environment; external download host cannot resolve |
| xUnit production-service regression tests | **NOT EXECUTED** | C# code and real SQLCipher service tests require .NET and native library |
| Windows 11 WPF/UI, model inference, native runtime, OneDrive, cross-PC recovery | **NOT EXECUTED** | Dedicated authorized test environments/assets unavailable |

These passing tests are **not** C# build, Windows end-to-end, or security certification. Earlier reported 72/72 tests are for a **different previous source revision** and cannot be reused as acceptance evidence for this ZIP.

## Limitations and risk decisions

- The **106 registered requirements remain the acceptance baseline**. The supplied register is preserved without artificially declaring any item 100% complete. Material missing features include model/vision compatibility, full auto-learning and semantic contradiction resolution, workspaces as genuine multiuser security boundaries, OneDrive account authorization and retention, dependable cross-PC recovery, signed supply chain, PDF export, and complete Windows standalone runtime. This source patch does **not** fulfill all 106 requirements.
- Conversation recall is not fine-tuning a neural network. It is model-independent, locally indexed contextual retrieval. Durable memory stays approval-gated to avoid learning from hallucinated assistant output and malicious documents.
- Folder reconciliation covers only **top-level watched files**. It cannot promise detection of every filesystem race, permission problem, recursive directory change or external same-user attacker. Offline folders retain prior indexed text and display an error instead of mass deletion; treat such content as potentially stale.
- A freshly approved or updated memory is preserved in the encrypted vault; prior backups might still contain retired or deleted data. Secure retention/key rotation and per-workspace authorization remain incomplete.
- The embedding fallback is source-implemented only; no live local embedding server was available to exercise its error handling.
- Signing, pinning/running approved Windows native `.exe`/`.dll` assets and generating a vetted all-in-one Windows EXE remain blocked, **not bypassed**.

## Required next acceptance steps

1. On an approved Windows 11 machine with .NET SDK, run `./scripts/Validate-Source.ps1`. Repair **all** compilation/analyzer/xUnit failures, rerun all tests against this exact source revision and attach full logs.
2. On a Linux environment with the test infrastructure authorized/available, run `bash scripts/setup-linux-tests.sh && bash scripts/validate-source.sh` to execute production C# services with real SQLCipher and restic. This package does not contain vetted Linux or Windows native tool binaries.
3. Perform interactive WPF scenario tests: repeated approved memory during Send, conflict warnings, switching workspace after recall, files removed/renamed/temporarily offline, optional embedding crash, cancel and restart.
4. Acquire authorized, SHA-verified Windows natives; test native SQLCipher loading, GPT-OSS/Qwen with real model files, CPU/RAM under load, OCR, restic/rclone and clean-VM behavior. Package only after these gates succeed.
5. Verify locally encrypted backup to authorized OneDrive, remote byte consistency, and restoration on a replacement Windows PC with no lost approved memory or encrypted documents.
6. Reconcile every one of the 106 requirement IDs with real acceptance evidence; don't declare finished if required features are still missing.

## Reproducibility / safety

- Source includes no real user work records, passphrases, SQLCipher vault, model GGUF weights, compiled EXE or native dependency archives.
- Source checksum manifest is regenerated from final contents. Inspect `SOURCE_SHA256SUMS.txt` after extraction.
- Do not run C# tests against an existing work vault. The test module initializer forces its own synthetic temporary data root.

## Official references used in review

- SQLite FTS5 external-content triggers/rebuild: https://www.sqlite.org/fts5.html
- Microsoft custom SQLite providers: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions
- GitHub Mem0 contradictions discussion: https://github.com/mem0ai/mem0/issues/4536
- GitHub llama.cpp engine: https://github.com/ggml-org/llama.cpp
- GitHub restic integrity/repair: https://github.com/restic/restic
