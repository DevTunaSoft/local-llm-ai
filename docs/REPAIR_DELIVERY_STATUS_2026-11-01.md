# Work Intelligence AI — Latest repair source delivery (2026-11-01)

**Status:** SOURCE CODE ENGINEERING CANDIDATE ONLY. NOT COMPILED, NOT A RELEASE, NOT PRODUCTION READY.

This package is derived from the third recheck and adds draft scanned-PDF OCR, workspace/task controls, approved-folder monitoring, memory proposal review, sequential model comparison, basic benchmarking, Word document export, and SQLCipher provider configuration changes.

## Checks actually run in this environment

- `python scripts/static_qa.py`: 71/71 source checks PASS.
- `python scripts/recheck_qa.py`: 76/76 source/SQLite smoke checks PASS.
- `python scripts/independent_round3_qa.py`: 21/21 source/SQLite checks PASS.

These tests are largely structural and are not evidence of successful C# compilation or end-to-end functionality.

## Known release blockers

- No .NET SDK or Windows WPF environment was available to compile, run, or GUI-test the project.
- The required vendor native binaries and signed/hash-reviewed runtime manifest are not included. The Windows publish script intentionally blocks an incomplete release.
- Model compatibility, token streaming, and actual 20B/27B inference on target hardware have not been tested.
- Microsoft OneDrive authentication, unattended scheduling, backup transfer and second-PC recovery have not been exercised.
- SQLCipher native provider linkage and encrypted vault compatibility have not been validated on Windows.
- Full feature-completeness, OCR accuracy on Windows, prompt-injection resilience, and the 99-point end-to-end acceptance audit remain open.

**Do not use real confidential or regulated work data with this candidate.**
