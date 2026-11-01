# Executed checks — exact repaired candidate, 2026-11-01

| Command | Actual result | Classification |
|---|---|---|
| `python scripts/static_qa.py` | 74/74 passed | Static WPF/source checks only |
| `python scripts/recheck_qa.py` | 79/79 passed | Source / synthetic plaintext SQLite smoke |
| `python scripts/learning_qa.py` | 32/32 passed | Source-extracted SQL migrations and learning checks |
| `python scripts/independent_round3_qa.py` | 21/21 passed | Independent historical source/SQLite checks |
| `python -m compileall -q scripts` | exit 0 | Python script syntax |
| `python scripts/release_readiness.py` | exit 2 | Correctly **BLOCKED**; see `readiness.json` |
| `dotnet build` / Windows C# xUnit | NOT EXECUTED | .NET SDK unavailable in this Linux environment |
| Windows EXE/native/model/OneDrive E2E | NOT EXECUTED | No Windows runner, native binary bundle, weights or authorized account |

**Totals:** 206 source/synthetic checks passed. They overlap and are not 206 independent runtime tests. New C# test cases are authored but **have not run**. Windows 11 production readiness is **not established**.
