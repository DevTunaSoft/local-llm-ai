# Bundled Windows native runtime (release-only)

The source does not include third-party executable binaries. Obtain vetted Windows x64 CPU `llama-server.exe` with its required libraries, reviewed PDFium runtime (`pdfium.dll`), Tesseract OCR (`tesseract.exe` and `tessdata/eng.traineddata`), and rclone for optional authorized OneDrive copies. Verify distribution licenses and security advisories.

Place all files under `vendor/runtime/` and supply `vendor/SHA256SUMS.txt` listing the SHA-256 of every file, for example:

```
<verified hash>  llama-server.exe
<verified hash>  rclone.exe
<verified hash>  tesseract.exe
```

These values are placeholders, not hashes. The Windows publishing script refuses any missing, modified or unlisted native file. The complete bundle is embedded into the self-contained EXE for controlled local extraction. The native SQLite library for ordinary non-encrypted SQLite comes from the Microsoft.Data.Sqlite dependency and does not require separate `sqlcipher.dll`. No restic executable is used for plaintext backup snapshots.

Do not release based solely on a successful build; actual Windows app startup, model inference, OCR and OneDrive validation are still required.
