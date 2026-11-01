# QA execution and limits — learning/workspace continuation, 2026-11-01

## Executed checks (platform SQLite 3 and Python; Linux x64)

| Command | Result | Captured log |
|---|---|---|
| `python scripts/static_qa.py` | 74/74 pass | `static.log` |
| `python scripts/recheck_qa.py` | 79/79 pass | `recheck.log` |
| `python scripts/independent_round3_qa.py` | 21/21 pass | `independent.log` |
| `python scripts/learning_qa.py` | 24/24 pass | `learning.log` |
| `python -m compileall -q scripts` | pass | terminal execution |
| XML parsing of solution/project/XAML files | pass | terminal execution |

The new SQL harness extracts schema definition, V4/V5 migrations, lexical/semantic query statements and memory FTS statements **directly from the revised source** and executes those SQL statements on platform SQLite with synthetic fixtures. This verifies SQL grammar and expected result filtering, **not** .NET C# behavior, actual SQLCipher runtime or Windows.

## Not executed / NOT VERIFIED

- .NET 10 compilation and portable C# unit tests on the **modified** code. `dotnet` is not installed in this session, and the .NET download host could not be resolved. Previous 72/72 portable tests and a successful WPF build refer to the **original uploaded source**, not this revised candidate.
- Windows WPF UI, DPAPI, scheduled backups, native DLL load, live local model inference, OCR/PDFium, OneDrive, and replacement-machine recovery.
- Automated semantic conflict resolution, file delete/rename reconciliation, and all other unresolved requirements from the 106-row register.

## Next required commands on authorized Windows test machine

```powershell
.\scripts\Validate-Source.ps1
```

For a full pre-release quality gate, then independently run interactive Windows tests with real native dependencies, encrypted vaults created from synthetic data, and model compatibility tests. Any failure must be fixed and re-tested. Do not package or claim an EXE ready before these gates succeed; source-level checks cannot substitute for them.
