# Third-review mandatory release gates

| Gate | State | Evidence needed |
|---|---|---|
| Source structural and SQLite checks | PASS (limited) | See docs/qa/*.log |
| Real Windows build | NOT VERIFIED | `dotnet build` on Windows .NET 10 |
| WPF UI load/close, no deadlock | NOT VERIFIED | GUI testing under model startup |
| SQLCipher maintained provider / encrypted vault | BLOCKED | Maintained provider + migration/cryptography tests |
| GPT-OSS 20B and Qwen3.5 27B | NOT VERIFIED | Real load/inference/latency/RAM tests |
| Embedded GGUF local semantic search | NOT VERIFIED | Model hash + source-grounded retrieval tests |
| Standalone image OCR | NOT VERIFIED on Windows | Embedded Tesseract, input/output tests |
| Scanned PDF OCR | NOT IMPLEMENTED | PDF page rasterization and image OCR |
| Background daily backup | NOT VERIFIED | Task Scheduler, DPAPI and missed-task tests |
| OneDrive remote verification | NOT VERIFIED | Config/auth, encrypted sync + downloaded byte test |
| Cross-PC recovery | NOT VERIFIED | Fresh computer, separate passphrases, offline readback |
| Native dependencies/EXE packaging | BLOCKED | Vetted llama/Tesseract/restic/rclone assets + signing |
| Full 99-angle QA / product scope | INCOMPLETE | Complete feature matrix and all runtime evidence |
