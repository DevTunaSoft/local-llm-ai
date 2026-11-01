# Bundled Windows native runtime (release-only)

The source archive intentionally does **not** contain third-party executables.

For a verified release build, obtain the approved **Windows x64 CPU build** of `llama-server.exe` and all its required DLLs from the official [ggml-org/llama.cpp](https://github.com/ggml-org/llama.cpp/releases) release, obtain an approved Windows Tesseract 5 build with `tessdata/eng.traineddata`, and obtain `restic.exe` from the official [restic project](https://github.com/restic/restic/releases). Check distribution licenses and security advisories first.

Place the vetted files in `vendor/runtime/`, then create a `vendor/SHA256SUMS.txt` file with exact SHA-256 for every file, in the form:

```
<sha256>  llama-server.exe
<sha256>  restic.exe
<sha256>  llama.dll
```

These are **placeholders only**; actual digests must be measured against official, independently verified downloads. The release script refuses to package missing or unverified components. Do not ship source-only builds to end users as if they include an inference engine.

The release process embeds a checked archive into `WorkIntelligence.exe`; installation does not require installing llama.cpp, restic, .NET, or Python separately. At runtime, the native bundle is extracted under the user's local application directory and checked against the embedded package.

The license and SBOM for all third-party native binaries must accompany any distribution.


**OCR security:** only the official, vetted Tesseract 5 binary and English `eng.traineddata` are accepted by the release script.
OCR of PNG/JPEG/BMP/TIFF is performed through stdin/stdout, not temporary plaintext image files.
Scanned or image-only PDFs still require a separately reviewed PDF rendering pipeline; this is a release blocker, not a feature claimed complete.
