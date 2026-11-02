#!/usr/bin/env python3
"""Auditable release *blocker* report; never substitutes for actual runtime QA.

This is a developer-side diagnostic, not a required installed-app dependency.
The script deliberately fails closed on unverified runtime requirements.
"""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_NATIVE = [
    "llama-server.exe", "rclone.exe",
    "pdfium.dll", "tesseract.exe", "tessdata/eng.traineddata",
]

def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()

def run() -> dict:
    requirements = json.loads((ROOT / "docs/handoff/requirements.json").read_text(encoding="utf-8"))["requirements"]
    if len(requirements) != 106 or len({x["id"] for x in requirements}) != len(requirements):
        raise ValueError("Requirement manifest is inconsistent; cannot validate release")
    native_root = ROOT / "vendor/runtime"
    missing_native = [name for name in REQUIRED_NATIVE if not (native_root / name).is_file()]
    vendor_manifest = ROOT / "vendor/SHA256SUMS.txt"
    vendor_issues = []
    if not vendor_manifest.is_file():
        vendor_issues.append("Pinned native file hash manifest absent")
    else:
        entries = {}
        for line in vendor_manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            parts = line.split(maxsplit=1)
            if len(parts) != 2 or len(parts[0]) != 64 or not all(x in "0123456789abcdefABCDEF" for x in parts[0]):
                vendor_issues.append("Malformed native file manifest entry")
                continue
            name = parts[1].replace("\\", "/")
            if name in entries:
                vendor_issues.append(f"Duplicate manifest filename: {name}")
            entries[name] = parts[0].upper()
        files = {f.relative_to(native_root).as_posix(): f for f in native_root.rglob("*") if f.is_file()} if native_root.exists() else {}
        for name in sorted(set(files) ^ set(entries)):
            vendor_issues.append(f"Native manifest/file mismatch: {name}")
        for name, file in sorted(files.items()):
            if name in entries and sha256(file) != entries[name]:
                vendor_issues.append(f"Native hash mismatch: {name}")
    counts = Counter(x["source_status"] for x in requirements)
    verified = [x["id"] for x in requirements if x.get("verified_runtime") is True]
    blockers = []
    if len(verified) != len(requirements):
        blockers.append(f"Runtime acceptance unverified for {len(requirements)-len(verified)} of {len(requirements)} registered features")
    if missing_native:
        blockers.append("Required Windows native distribution absent: " + ", ".join(missing_native))
    blockers.extend(vendor_issues)
    if os.name != "nt":
        blockers.append("Production Windows runtime acceptance requires Windows: this host is not Windows")
    if not shutil.which("dotnet"):
        blockers.append(".NET SDK unavailable in this test environment: no updated C# compilation/test evidence")
    if not (ROOT / "docs/qa/production-windows-acceptance.json").exists():
        blockers.append("No evidenced Windows clean-machine/model/OneDrive second-device acceptance artifact")
    if not (ROOT / "docs/qa/release-signature-acceptance.json").exists():
        blockers.append("No trusted publisher signature verification artifact")
    return {
        "ready": not blockers,
        "requirements": len(requirements),
        "baseline_status_counts": dict(sorted(counts.items())),
        "runtime_verified_requirements": len(verified),
        "missing_native_components": missing_native,
        "blockers": blockers,
        "note": "Source checks, manifest presence and a build do not establish production acceptance. Evidence files must correspond to the exact artifact under test.",
    }

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", help="JSON report path (optional)")
    args = parser.parse_args()
    result = run()
    encoded = json.dumps(result, indent=2)
    print(encoded)
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(encoded + "\n", encoding="utf-8")
    sys.exit(0 if result["ready"] else 2)
