# Canonical Work Intelligence AI specification — conversation-based handoff

**Date:** 2026-11-01. **Status:** requirements consolidation, NOT claim of completed source.

## 1. What user wants
A completely local, work-only intelligence assistant on the user's current Windows laptop. It is a dedicated **Work Intelligence AI** desktop application, not merely Ollama or generic chat. User expects the entire application in a **single self-contained `WorkIntelligence.exe`**, with only **large LLM model weights external**; permitted native DLL/helper extraction must be secure and automatic. Mutable encrypted data and knowledge must also be external (data cannot live inside a read-only EXE). No manual installation of Python, Node, Docker, Ollama or a .NET runtime. First run uses GUI to choose paths, models, vault password and backup destination.

## 2. Locked hardware and model decisions
- Windows 11 Pro x64; Intel Core i7-1370P (14-core/20-thread), 32 GB RAM, integrated Intel graphics. Reliable CPU path required; evaluate Intel acceleration only after measurement. Do not require a dedicated GPU.
- **GPT-OSS 20B** = desired default/main model; validate correct format, Harmony template, response/reasoning separation and throughput.
- **Qwen3.5 27B** = desired deep-analysis model; verify Q4 GGUF and relevant chat/vision configuration.
- The user allows **larger, slower models** and wants no arbitrary 8B/14B or model-count cap. Offer user-controlled import/download, model catalog, GGUF compatibility, SHA-256 verification, and selectable custom folders.
- CPU resource safety is non-negotiable: one large model loaded at a time, RAM checks, cancel/start/stop, model benchmarks; protect Windows responsiveness instead of falsely promising every huge model fits.
- Model weights are portable but can be downloaded again; backup policy should optionally include weights, with explicit storage costs. Embedding weights and OCR language data are support assets: bundle or transparently manage these without a separately installed runtime.

## 3. Local privacy and work-only boundary
- Inference, tokenization, document parsing, OCR, vector embeddings, databases, stored conversations, local retrieval and analysis must stay on user's PC.
- Only explicitly approved model downloads, encrypted OneDrive backup/recovery and optional future update checks may use networking. Never permit cloud LLM or stealth telemetry. `llama-server` should be loopback-only, authenticated and correctly cleaned up. No broad filesystem read or external account access by default.
- Employer approval is REQUIRED to ingest internal/proprietary, employee, patient, financial or other restricted data, and approval to sync encrypted archives to a particular OneDrive account/tenant. Use synthetic fixtures in development. Do not copy real data to GitHub or cloud Codex.
- There is no promise of absolute confidentiality from the mere fact that a model is local; verify threat surfaces, logs, caches, temp files, data/backup encryption and local service exposure.

## 4. Persistent, verifiable learning
- Long-term learning should initially use RAG + encrypted stored factual memories, **not continuous unsupervised neural model retraining**.
- Ingest approved PDF, Word, Excel, PowerPoint, CSV, logs, text, JSON, and screenshots; OCR scanned PDFs and images using local tools. Detect file updates/deletes in authorized folders and reindex automatically or via reliable scheduled scans.
- Hybrid semantic/vector and full-text search, source/provenance references, version-aware citation and no unsupported claims presented as company policy.
- AI proposes memories/corrections; user approves; remember across conversations and **across model switches**. Distinguish proposed/unverified notes from approved policies, and detect conflicts, effective dates and superseded procedures.
- Users must be able to review, edit, retract, delete, export and transfer memories, conversations and data. Deletion from active vault must be meaningful and backup-retention implications clear.

## 5. Functional application areas
AI Chat; Knowledge Library; Memory Manager; Model Manager; Workspaces & Projects; Document Studio; Search; Security Center; Diagnostics; Backup/Recovery. Complete UI with actual working actions, not inert controls. Conversation search, rename, exports, source attachments and status/progress; technical error analysis; project and decision tracking; compare models sequentially; benchmark models; customize settings; copy and export results to Word/PDF and portable JSON/Markdown/SQLite as appropriate.

## 6. Portability and encrypted OneDrive
- Local primary vault, not active OneDrive DB. Consistent snapshots, encrypted **before** OneDrive uploads, using restic, rclone (or approved equivalent), real remote verification, independent recovery password, periodic test restore, configurable retention, failure alerts, catch-up backups and safe migration.
- Backups must be possible with UI closed. If truly unattended when Windows user logged out is infeasible under allowed policy/credential model, implement a supported secure mechanism or clearly distinguish guaranteed conditions; do not call `/IT` logged-in tasks fully unattended.
- Fresh replacement computer: install/copy self-contained EXE, import model weights or download again, select remote/encrypted archive, unlock without original Windows identity, verify recovered memories/source content/embedding compatibility, and answer offline.
- Allow user to export underlying knowledge in documented non-proprietary formats even if application no longer works; no vendor lock-in. Do not force cloud live synchronization or overwrite newer local data.

## 7. Quality and release contract
- User repeatedly demanded proactive clean code, not repetitive user-driven bug hunts. Search authoritative documentation, upstream issue trackers, GitHub/GitLab/Codeberg/community sources for nontrivial defects. Fix root cause, rebuild, run focused test, regression, fault injection, independent recheck and full suite as appropriate.
- Master prompt includes **99 independent audit checks**, but a numeric passing structural scanner isn't sufficient. Need actual Windows compilation, model behavior, encryption, backups, clean PC, second PC recovery and UI tests.
- Source + self-contained Windows release + tests + security documentation + release evidence + user guide + defect register + traceability. No fake file links or claims of completion.

## 8. Hard requirements versus estimates
- Early EXE estimate 250–600 MB and combined storage ~32–35GB for two models were preliminary, NOT build gates. Let verified native dependencies and embedding/OCR assets determine final size.
- Local CPU performance and inference/model formats are hardware-and-release dependent: benchmark rather than assume throughput.
- V1 design explored Qwen3 8B/14B, but latest requirement is GPT-OSS 20B and Qwen3.5 27B as priorities, with smaller and bigger models supported.
- GPU is optional, not required. Online research is a **development capability**, not permission for deployed app to send work data to the internet.

## 9. Explicit non-goals unless user requests later
- Do not fine-tune/train a new foundation model as a prerequisite to local learning; persistent retrieval/memory is required first.
- Do not build external live synchronization between two computers as a substitute for encrypted portability and backups.
- Do not assume permission to integrate company systems, email, or services; no background surveillance.
- Do not ship a multi-user server or autonomous system modification until separately approved.

See `requirements.json` for component-level acceptance behavior, and the original long-form `docs/MASTER_DEVELOPMENT_PROMPT.md` for the expanded 99-check audit.
