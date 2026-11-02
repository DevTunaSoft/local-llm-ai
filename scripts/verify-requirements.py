#!/usr/bin/env python3
"""Fail-closed traceability audit. This is not a substitute for full acceptance."""
import json
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[1]
registry = json.loads((root / "docs/handoff/requirements.json").read_text(encoding="utf-8"))
evidence = json.loads((root / "docs/handoff/requirement-evidence.json").read_text(encoding="utf-8"))
requirements = registry["requirements"]
records = evidence["records"]
ids = [r["id"] for r in requirements]
eids = [r["id"] for r in records]
assert len(ids) == 106 and len(set(ids)) == 106, "Requirement registry must have 106 unique IDs"
assert len(eids) == 106 and len(set(eids)) == 106 and set(eids) == set(ids), "Evidence coverage is incomplete or duplicated"
allowed = {
    "not_fully_verified",
    "partially_verified_subsystem",
    "requires_target_environment_or_authorization",
    "superseded_by_plaintext_design",
}
by_id = {r["id"]: r for r in requirements}
for record in records:
    assert record["acceptance_state"] in allowed, f"Unknown state: {record['id']}"
    assert record["legacy_source_status"] == by_id[record["id"]]["source_status"], f"Legacy status inconsistent: {record['id']}"
    assert record["full_acceptance_required"], f"Missing acceptance criteria: {record['id']}"
    if record["acceptance_state"] == "partially_verified_subsystem":
        assert record["verified_scope"] and record["evidence_url"], f"Unsubstantiated partial evidence: {record['id']}"
    assert record["acceptance_state"] != "fully_verified", "Do not certify all requirements using this subset of CI evidence"
counts = Counter(r["acceptance_state"] for r in records)
assert counts["superseded_by_plaintext_design"] >= 2, "Plaintext design supersessions must remain explicit"
print("106 distinct requirements matched to 106 acceptance records.")
for status, count in sorted(counts.items()):
    print(f"{status}: {count}")
print("No all-requirements production acceptance is claimed.")
