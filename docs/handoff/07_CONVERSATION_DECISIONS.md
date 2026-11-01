# Work Intelligence AI — decisions reconstructed from this project conversation

This is a **project/product decision record**, not a profile of the user. It deliberately excludes unrelated personal information.

## Evolution and locked decisions
1. User asked if a fully local LLM could learn and be used exclusively for work. We agreed on a dedicated local Windows intelligence assistant with persistent memory + RAG rather than relying on implicit fine-tuning.
2. User required that accumulated knowledge be **extractable, backed up to encrypted OneDrive and transferred to a replacement computer**. Design separates local primary databases from encrypted backup transport, mandates real recovery and independent recovery passwords.
3. Target hardware fixed as **Intel Core i7-1370P, 32GB RAM, Intel integrated graphics, Windows 11 Pro x64**; CPU-only operation must work.
4. User selected a **full work intelligence desktop application**, not a minimal chatbot. Discussed Chat, Knowledge Library, Memory Manager, Documents, Search, Workspaces/Projects, Diagnostics, Backup/Recovery and Security controls.
5. User required that **everything except large LLM weights be packaged inside one Windows EXE**, including native runtime components. The program may extract its own signed/verified helper binaries; mutable encrypted data lives outside EXE. No separate developer/runtime installation on end user's computer.
6. Built-in model manager downloads/imports official compatible models with selectable location, hash verification, resumes and runtime validation. Multiple models can be installed; user willingly accepts **slower larger models**, so there must be no arbitrary model-size limit.
7. The user's latest preferred models are **GPT-OSS 20B default** and **Qwen3.5 27B deep analysis**; both share identical knowledge/memory. Lower and bigger models can be added; models should be compared sequentially.
8. User asked if anything was missing; identified OCR/scanned PDFs, semantic retrieval, learning approval/provenance, uninterrupted backup, OneDrive recovery, complete resource monitoring, output exports, model compatibility and independent QA.
9. User demanded complete clean code, repeated root-cause debugging, comprehensive web/upstream research, 99-angle QA, and no premature release claim. Created original `docs/MASTER_DEVELOPMENT_PROMPT.md` and `docs/ARCHITECTURE_v2.md` plus earlier v1 retained.
10. Earlier answer disclosed source project had not started; development source candidate was then created, static checks run, and rechecks discovered defects. User repeatedly requested code corrections and complete coverage, objecting to omitted features.
11. Third recheck added initial semantic, OCR, scheduled and remote-backup code but Windows end-to-end tests remained impossible. Most recent repair candidate (2026-11-01) added scanned-PDF OCR source, primitive workspaces/tasks, memory proposals, folder scans, model benchmark/compare controls, DOCX exporter and SQLCipher provider configuration changes.
12. User asked whether everything was present; comparison showed **not complete**. User insisted 'Fix it' and sought Codex because they want an actual working Windows EXE, not another unverified source archive.
13. User specifically requested a **Master Codex Project Handoff Package** encompassing code, all functional decisions, defects, acceptance tests, exhaustive traceability and explicit Codex instructions so the assistant cannot omit anything.

## Material limitations clarified in the conversation
- A successful structural/source scan (historically 53/53, 57/57, 59/59, 64/64, 71/71, 76/76 and 21/21 checks) is NOT evidence of working Windows UI, native libs, models, remote OneDrive or cross-PC restore.
- Source packages of about 77–124 KB are **source only**. Single EXE target around 250–600 MB was a rough estimate, not established by a Windows build.
- No real confidential company data may be uploaded to Codex, GitHub or a personal OneDrive without organizational authorization. Approved OneDrive must be used for actual backups.
- Software must distinguish user-authorized approved memory from raw conversations, retrieved text, or unsupported AI conclusions.

## Scope rule
Everything above is an agreed requirement unless explicitly marked optional. The consolidated requirement registry is the actionable checklist. Codex may propose architectural improvements but must preserve user-visible behavior and testability.
