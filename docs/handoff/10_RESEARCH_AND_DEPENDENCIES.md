# Verification-first research guide and authoritative upstream sources

Official/current references must be rechecked by Codex at implementation time; URLs below are starting points, NOT guarantees that every version pinned in current source is secure or compatible.

- Microsoft single-file deployment: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Microsoft SQLite encryption integration: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption
- SQLCipher project / provider questions: https://www.zetetic.net/sqlcipher/
- llama.cpp main repo / issues / server documentation: https://github.com/ggml-org/llama.cpp ; https://github.com/ggml-org/llama.cpp/tree/master/tools/server
- GPT-OSS official model: https://huggingface.co/openai/gpt-oss-20b
- Qwen3.5 27B model: https://huggingface.co/Qwen/Qwen3.5-27B
- restic backup: https://restic.readthedocs.io/en/stable/
- rclone OneDrive backend: https://rclone.org/onedrive/
- PDFium: https://pdfium.googlesource.com/pdfium/
- Tesseract OCR: https://github.com/tesseract-ocr/tesseract
- PdfPig: https://github.com/UglyToad/PdfPig
- .NET test: https://learn.microsoft.com/en-us/dotnet/core/testing/
- GitHub Windows runners: https://docs.github.com/en/actions/reference/runners/github-hosted-runners
- Codex repository AGENTS.md instructions: https://learn.chatgpt.com/docs/agent-configuration/agents-md

## Research protocol
Search the **exact error text**, identify source version/commit, check upstream fixed issues/PRs and official docs, compare multiple hypotheses, reproduce minimal failures locally, and regression-test source and actual Windows runtime. Forums and Reddit are supplementary leads, not substitutes for compatibility proofs. Record references + date + test outcomes in `EVIDENCE_LEDGER.md` and the defect log. If upstream published breaking changes, update pinned dependencies only with tested migration, checksums and rollback.
