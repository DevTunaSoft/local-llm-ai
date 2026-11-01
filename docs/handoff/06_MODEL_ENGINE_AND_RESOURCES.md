# Model engine, model catalog, performance and compatibility

**Fixed hardware:** Win11 Pro, Intel i7-1370P, 32GB physical RAM, integrated Intel graphics. CPU inference is required; GPU backend only optional after testing. SSD capacity unspecified; prompt user only if absolutely necessary for installation selection. Do not assert unmeasured tokens/sec or power draw.

## Preferred model order
1. **GPT-OSS 20B**: default; validate official model origin, actual supported llama.cpp format, MXFP4/GGUF and Harmony response/reasoning tokens, no accidentally exposed internal formatting; lower-memory configs.
2. **Qwen3.5 27B**: advanced/deep analysis; verify official GGUF Q4 variant, tokenizer/template, context and multimodal projector/assets as actually supported.
3. Any additional user-approved compatible GGUF: larger/slower allowed subject to RAM, SSD, model/runtime support, safety controls. Include optional smaller model for responsiveness, without replacing the required two.

## Behavior
- Model library in GUI: official source/catalog (validated), external `.gguf` import, choose folder, checksum/version/license metadata, download progress, resume and cancellation, corrupted/incompatible model error, model switch/unload/reload without losing messages or knowledge.
- Save model-specific temperature, threads, context, RAM ceiling, tokens, timeout; show actual load time, RAM usage, tokens/s; allow side-by-side sequential answer comparison; optional task-based auto routing with user override.
- Model files and primary vault separate; knowledge stored at application retrieval layer not modified model weights. Embedding model independent of generation model; changing embedding model should trigger reindexing without losing original knowledge.
- Correctly parse SSE/OpenAI-compatible streaming and application-level reasoning output for these actual model versions; structured citations anchored to real source chunks. Handle incomplete responses and failed model processes.
- Interact with local inference via loopback-only, randomized auth key; audit SSRF and local API exposure. No public listener and no implicit cloud fallback.
- With 32GB RAM, monitor actual available RAM and safeguard against paging; never claim all 27B variants fit. Benchmark 4K/8K context configurations; run realistic multitasking with Windows.

## Important caveat
Existing `ModelRuntime.cs` is generic; no real GPT-OSS 20B/Qwen3.5 27B run was performed. `EmbeddingRuntime` expects a separate local embedding GGUF but no user-ready embedding model installer/catalog is provided. This must be resolved rather than declaring semantic search complete.
