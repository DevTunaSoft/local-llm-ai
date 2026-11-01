# Work Intelligence AI — Release Gates (2026-10-31)

## Evidence levels

- **PASS (SOURCE)**: explicit source-structure condition checked with `scripts/static_qa.py`; does NOT imply correct build or runtime.
- **NOT VERIFIED**: cannot be established here due to absent .NET/Windows environment, model assets, authorized OneDrive, or an additional machine.
- **NOT IMPLEMENTED**: source lacks the required capability.

## Verified this session

- XML parse of all WPF XAML, project files and solution.
- All declared button and selection events have code-behind method names.
- Release configuration requests win-x64, .NET 10 WPF and self-contained single-file packaging.
- Pinned SQLCipher package references are present without default plaintext Microsoft.Data.Sqlite package.
- Source checks for encrypted data, local llama loopback isolation, model checksums, secure redirects, restic encryption, native package integrity, and Windows CI workflow.
- All checks passed after correction of model runtime API key name, health endpoint, download integrity error cleanup, native component tamper check, backup restore rollback and sensitive-log handling.

**These checks do not prove the code compiles.** No real C# compiler, Windows desktop runtime, llama.cpp binary, model weights or restic binary was accessible in the execution environment.

## Hard blockers before final release

1. **Build**: restore pinned NuGet packages on Windows and compile all projects with warnings as errors. Record full logs.
2. **Unit tests**: execute xUnit suite; correct every failure and rerun all tests.
3. **Native bundle**: pin official releases and verify SHA-256 and distribution licenses for `llama-server.exe`, `restic.exe`, and every required DLL. Ensure one-EXE packaging actually succeeds.
4. **User interface**: launch on a clean Windows 11 PC without .NET SDK, Python, Node, Ollama, Docker or separately installed restic/llama; exercise every control, cancellation, and error scenario.
5. **Model tests**: import verified GPT-OSS 20B and Qwen3.5 27B GGUF files; test chat templates, reasoning output, UTF-8 SSE, startup/stop, memory guard and throughput with 32 GB RAM/integrated Intel graphics.
6. **Encrypted database**: create vault, inspect file header for plaintext leakage, test wrong passwords, native SQLCipher interoperability, migrations, indexes, integrity, restart and deletion.
7. **Documents**: compare PDF/Office extraction with known verified expected output; test corrupted, malicious, image-only and oversized files; implement and validate OCR and semantic retrieval.
8. **Security**: threat-model localhost API, prompt injection, credentials in subprocess environment, imported file handling, temp paths, ACLs, logging, backup password handling and supply chain. Run dependency vulnerability/license audits.
9. **Backup**: implement scheduled backups; verify backup across application closed, interrupted transfers, restic repository damage, wrong password, OneDrive sync and remote copy, snapshots, retention policies, full restoration to a second PC and migration rollback.
10. **Feature completeness**: deliver remaining workspaces, semantic search, OCR, project/decision tracking, document export, model comparison, update/rollback, provenance and citation validation.
11. **Failure injection**: simulate full disk, shutdown, sleep/resume, low RAM, concurrent operations, incomplete native extraction, corrupted encrypted vault and incomplete model download.
12. **Independent final review**: test again from clean Windows installation and separate restore computer, finalize SBOM and signed release, then complete 99-point matrix with evidence.

## Known gaps and potential vulnerabilities

- No final Windows EXE has been created.
- Full-text FTS5 is implemented; semantic embeddings/hybrid retrieval are **not**.
- Plaintext user-authorized exports are available; retention controls are not yet present.
- Scheduled background backups and independent OneDrive remote-copy verification are not implemented.
- Local model runtime depends on a vetted, bundled llama.cpp build not yet supplied.
- Native runtime tools have not undergone vulnerability or code-signature inspection.
- PDF extraction exists in source, but no scanned-PDF OCR.
- Model CPU/RAM suitability is estimated, not measured.
- Source-only static checks are not functional acceptance evidence.

## Fixes made during development

| Defect/risk | Correction | Validation |
|---|---|---|
| Wrong llama API key env variable | Changed to documented `LLAMA_API_KEY` | Source static check PASS; runtime not verified |
| Model readiness could be detected prematurely | Switched to llama `/health` | Source check PASS; runtime not verified |
| Checksum mismatch attempted deletion while file open | Move deletion to after stream disposal | Source review only |
| Model download automatic redirects could hit unauthorized hosts | Disable automatic redirects; validate HTTPS host per hop | Source check PASS; runtime not verified |
| Sensitive model output could enter persistent logs | Replace with redacted diagnostic events | Source check PASS; runtime not verified |
| Extracted native tools could be changed after extraction | Compare extracted files with embedded bundle on access | Source review only |
| Partial restore could strand incomplete files | Roll back created files when restore fails | Source review only |
| Release could package unverified native executables | Require vendor per-file SHA256 manifest before publish | Source check PASS; Windows execution not verified |

## Exit criteria

**Not passed.** The package is a source engineering candidate. Do not represent it as a completed user-ready release until all mandatory functional gates are passed.
