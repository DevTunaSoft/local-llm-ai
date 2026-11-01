# Non-negotiable implementation and release acceptance plan

## Evidence taxonomy
`PASS`: command/test actually executed on identified environment and observed expected outcome, with artifact/log. `FAIL`: executed but failed. `NOT VERIFIED`: no suitable environment/credentials/permissions, or simply untested. `NOT APPLICABLE`: justified with explanation. `SOURCE CHECK`: static structure, *never* substitute for PASS runtime.

## Phase A — reproducible Windows build
- Use Windows 11 x64 with .NET 10 SDK available **on developer machine only**; log exact SDK, NuGet cache, MSBuild, OS, third-party versions.
- Commands: `dotnet restore WorkIntelligence.slnx`; `dotnet build WorkIntelligence.slnx -c Release --no-restore`; `dotnet test tests/WorkIntelligence.Tests.csproj -c Release`; `python scripts/static_qa.py`, `python scripts/recheck_qa.py`, `python scripts/independent_round3_qa.py` (source QA supplementary, not proof of runtime).
- Compile warnings as errors; run analyzers/security scanning; audit dependencies/licenses; record actual errors and roots; do not skip tests with `--no-build` if tests were not already built.
- Verify GitHub Actions runner image, referenced action major versions and availability; retain independent Windows CI.

## Phase B — bundled native tools and standalone EXE
- Obtain official/verifiable pinned Windows x64 CPU llama.cpp runtime, SQLCipher compatible native library, PDFium, Tesseract (and traineddata), restic, rclone, embedding assets, all dependencies. Confirm license + export/compliance restrictions and DLL closure.
- Record build source/tag, SHA-256 per item, licenses and SBOM; package into `vendor/runtime` with authenticated manifest; run `scripts/Build-Windows.ps1`.
- Inspect publish output for **only WorkIntelligence.exe** (external model weights and data directories are expected). If native extraction occurs, audit ACLs, hashes, race conditions, cleanup and service lifetime.
- On a clean Win11 VM without SDK, Ollama, Python, Node, Docker or separate runtimes, open EXE, initialize and exit without admin/UAC escalation unless specifically justified.

## Phase C — end-to-end local AI and model safety
- Test GPT-OSS 20B and Qwen3.5 27B with **actual approved GGUF weight files**. Validate chat template, tokenizer, reasoning output, UTF-8 SSE streaming, cancellation, stop/restart, dynamic path, local-only network and model version compatibility.
- Benchmark initial response, tokens/sec, load time, peak committed memory, Windows memory pressure and context at 4K and at safe larger sizes. User accepts slow performance, not OS instability.
- Import corrupt GGUF, invalid download checksum, interrupted/resumed download, unsupported architecture, missing files and malicious redirects. Verify no unauthorized endpoints called.
- Compare models sequentially against identical question/grounding, verify retrieval unaffected. Optional model auto-choice and custom per-model settings must be tested if advertised.

## Phase D — encrypted storage / knowledge / documents
- Vault encryption: create, lock, reopen, wrong passphrase, authenticate data, zero plaintext disk leakage, alternate Windows account; test SQLCipher provider and expected DB plaintext header absence. Test schema migrations, backups, crashes and version guard.
- Test approved memory proposals, conflict resolution, policy supersession and source confidence. Verify model-independent retention.
- Test document/version extraction for PDF, scanned PDF, image, DOCX, XLSX, PPTX, CSV, JSON and logs with known synthetic expected text. Include multilingual/rotation/poor contrast or explicitly supported OCR bounds, corrupted files, zip bombs and 1000-page safety limit. Test source citation correctness, page mapping, spreadsheet sheet/cell references.
- Test semantically similar non-keyword queries and irrelevant retrieval; record recall/citation validity against fixed gold fixtures; embeddings must remain local.
- Test authorized-folder watcher: create/modify/rename/delete, locked file, network drive disconnect (if supported), permission denied, no duplicate, cancellation. Index and memory deletion do not return from search.
- Test document export as real DOCX/PDF opened by standard viewers and as portable data independently inspected.

## Phase E — backup, OneDrive and second-PC disaster recovery
- Configure approved OneDrive test account/tenant and rclone remote via GUI. Record remote directory and encrypted format; never upload workplace data in tests. Verify token storage and expiration handling.
- Back up an actively used encrypted vault; read/check restored contents; verify hashes and no partial plaintext file in cloud.
- Shut GUI, lock workstation, test scheduled jobs; sleep, reboot, logoff, network failure, missed trigger, retry and catch-up. State the supported unattended power/session model unambiguously. Windows policies may constrain some modes.
- Verify remote files and repository contents, force failed upload and prove prior snapshots survive. Exercise retention, restore older snapshot, wrong passwords, tamper, quota/full disk and interrupted restore.
- On a **different authorized Win11 test PC / different Windows account**, use only the EXE + model weights + encrypted remote repository + independently held password. Restore, verify files/memories/chats/hashes, rebuild index where necessary, disable internet, answer grounded work questions. Document exact evidence; do not claim cross-PC verified until then.

## Phase F — security, UI, release audit
- Simulate malicious imported instructions attempting to exfiltrate files/keys, unsafe model URL redirects/localhost probing, tampered native bundle, arbitrary archive path traversal, wrong app permissions, transcript/temporary plaintext leaks, insecure debug logs and token exposure.
- Exercise every WPF control (actual clicks), responsive progress, first-run help, accessibility including keyboard-only workflow, cancellation, errors, update/migrate/uninstall and clean rollback.
- Fault inject process kill, power disruption, full disk, insufficient RAM, denied folder, broken OneDrive OAuth, database corruption and simultaneous indexing/backup; verify no user data loss.
- Execute the **full 99 item matrix** in `docs/99_POINT_AUDIT.md`, with evidence. Log unresolved critical/high severity defects. Audit independently after fixing bugs.
- Release artifacts: EXE hash/signature, source ZIP, full tests, model guide, backup/recovery user guide, license/SBOM, checksum manifest, status report, known limitations. No declaration of production readiness without passing all mandatory tests.

## Error repair loop (automatic, not user-triggered)
`Capture error → reproduce → inspect source/config/dependencies → research official documentation and upstream issues (GitHub/GitLab/Codeberg, supplemental forums) → establish root cause → correct → rebuild → focused test → affected integration/regression → independent second review → repeat`. Record exact defect ID and tests; don't suppress exceptions or remove features to make checkmarks green.

## Release stop rules
Any unverified encrypted SQLCipher linkage, failed first-run, broken model stream, unencrypted workplace disk material, failing backup integrity, missing second-device remote restore, critical vulnerability, or missing mandatory feature means **NOT READY**.
