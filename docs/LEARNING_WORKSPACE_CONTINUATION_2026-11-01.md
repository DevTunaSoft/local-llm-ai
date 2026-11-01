# Work Intelligence AI — learning and workspace continuation

**Date:** 2026-11-01. **Phase:** repaired source candidate; **NOT** a Windows-validated release. No EXE has been built or shipped in this continuation.

## What changed in this continuation

1. **Schema 5, transactional and additive.** Existing schema 3/4 vaults upgrade to schema 5 with workspace references for chats, active memories and memory proposals. Old entries default to the General workspace. Documents with no existing workspace association are linked to General; explicitly assigned documents retain their membership. The original encrypted database is never replaced with a fresh database.
2. **Workspace-scoped retrieval.** New lexical and semantic search filters accept workspace ID. In Chat and Search UI, retrieval is limited to the currently selected workspace; unassigned imports are assigned to the chosen workspace. General and project workspaces are intentionally separate. Workspace changes now refresh visible chats and memories; an in-flight chat locks the workspace picker and persists responses to the same chat/workspace ID.
3. **Learning is persistent and model-agnostic.** Memory data lives in the encrypted SQLCipher vault, not in a model-specific context or a GGUF file. Only approved memories are retrievable for chat. Previously approved memories survive model changes; review, correction proposals, historical supersession and deletion remain available. A correction requires a target from the Memory Manager, rather than silently creating a second active fact from `correction:` in chat. Durable user-authored phrases also enter a **pending** approval queue; they are never silently promoted to policy.
4. **Conflicts and duplicate safeguards.** Corrections require an active memory in the *same* workspace; approval inserts a new memory and marks the old memory superseded in a single transaction. The code refuses approval of a duplicate active fact and refuses creating orphan chat/memory records for nonexistent workspaces. This does **not** implement automated semantic contradiction detection: different wording of conflicting policies still needs review.
5. **Safer RAG context.** Retrieved evidence is explicitly bounded, labeled untrusted, and followed by an instruction not to follow embedded commands. This is defense-in-depth, not a complete prompt-injection defense. References still require manual verification; no fabricated source guarantees are asserted.
6. **Tests and Windows gates.** Added portable .NET regression tests for memory approval/correction, multiple workspace isolation, lexical/semantic workspace restrictions, chat scoping, and explicit learning intent. Added a direct SQLite test harness which executes the **actual SQL extracted from the C# source**, exercising migrations, FTS, semantic query predicates and supersession. Windows validation scripts and CI now include the portable service suite; this may uncover missing native SQLCipher packaging, and a CI job must not be called successful until it actually runs.

## Learning use case

Select a workspace; import a file or approve a watched folder. When the document changes, scheduled scanning can create a new indexed revision. Ask a question in chat and relevant indexed excerpts are retrieved within that workspace. To save durable knowledge, say `remember that ...` or use the Approved Memories tab. The instruction enters a proposal queue, except explicitly manually added memory. Review the proposal before it is used in answers. To change an old policy, select that approved memory, propose a replacement and approve it. The superseded fact is then excluded from active memory retrieval, but the history remains auditable. All of this is kept in the encrypted vault across model switches and backed up through the existing snapshot process.

## Tests actually run in this environment

- Python source structures, database/FTS/SQL smoke, independent historical checks and the new learning SQL/source harness: consult `docs/qa/learning-continuation-2026-11-01/` for captured outputs.
- The source migration DDL through SQLCipher was previously exercised on Linux for schema 4. **This continuation's schema 5 migration was tested with platform SQLite, not with actual SQLCipher in C#.** The original previous review records 72/72 service tests on the earlier snapshot; those results are **not** evidence that the modified source passed the .NET test suite.
- No `dotnet` SDK is installed in this session and this container could not resolve the external .NET download host. Consequently **the updated .NET source has NOT been recompiled**; no WPF/Windows startup, Windows native SQLCipher, GPT-OSS/Qwen models, OCR, OneDrive or multi-computer recovery was executed. The source QA counts are NOT substitutes.

## Remaining blockers before the user can call this 100% working

- Obtain a Windows 11 test environment with .NET 10 SDK, pinned NuGet packages and vetted native SQLCipher, llama.cpp, OCR/PDFium, restic and rclone assets.
- Run `scripts/Validate-Source.ps1` plus user-interface interaction, shutdown/cancellation, and native model/embedding tests; repair every compile/runtime failure and repeat all regressions.
- Validate model weights/templates, actual work-prompt quality/relevance/citations, automated conflict suggestions and changing-document tombstones. Current auto-learning is proposal-based and still lacks comprehensive semantic conflict detection, effective dates and document-file delete/rename reconciliation.
- Prove Windows encrypted backup scheduling, OneDrive authorization, remote verification, restore on an independent replacement computer, retention and fail-safe key management.
- Workspace scopes are single-user logical filters, **not multi-user access control or a security authorization boundary**. A user of the vault with application-level access can still see the data from another workspace.
- Validate all 106 registered requirements independently before a production claim. Source code present != fully functional product; the original `docs/handoff/02_REQUIREMENTS_TRACEABILITY.md` remains the full register.

## Public technical sources reviewed

- SQLite FTS5 search, triggers and ranking: https://www.sqlite.org/fts5.html
- Microsoft.Data.Sqlite transactions: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions
- Mem0 persistent memory patterns: https://github.com/mem0ai/mem0
- Letta memory patterns: https://github.com/letta-ai/letta-code
- OWASP RAG safety guidance: https://cheatsheetseries.owasp.org/cheatsheets/RAG_Security_Cheat_Sheet.html

No third-party library code was copied. Network search was **development-only**; the deployed application has not gained a cloud inference dependency.
