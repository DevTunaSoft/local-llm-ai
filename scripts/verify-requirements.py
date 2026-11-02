#!/usr/bin/env python3
"""Fail-closed traceability validation; never a product acceptance certificate."""
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(registry, evidence, root=ROOT):
    requirements = registry['requirements']
    records = evidence['records']
    ids = [r['id'] for r in requirements]
    eids = [r['id'] for r in records]
    require(len(ids) == 106 and len(set(ids)) == 106, 'Registry must have 106 unique IDs')
    require(len(eids) == 106 and len(set(eids)) == 106 and set(eids) == set(ids), 'Evidence IDs are incomplete or duplicated')
    allowed = {'not_fully_verified', 'partially_verified_subsystem',
               'requires_target_environment_or_authorization', 'superseded_by_plaintext_design'}
    by_id = {r['id']: r for r in requirements}
    groups = evidence['component_regression_groups']
    for group, cases in groups.items():
        require(cases, f'Empty regression group: {group}')
        for case in cases:
            path, method = case.split('::', 1)
            source = root / path
            require(source.is_file() and re.search(r'\b' + re.escape(method) + r'\s*\(', source.read_text()),
                    f'Missing regression case: {case}')
    for record in records:
        rid = record['id']
        require(record['acceptance_state'] in allowed, f'Unknown acceptance state: {rid}')
        require(record['legacy_source_status'] == by_id[rid]['source_status'], f'Legacy status mismatch: {rid}')
        require(record['full_acceptance_required'], f'Missing original criteria: {rid}')
        if record['acceptance_state'] == 'partially_verified_subsystem':
            require(record['verified_scope'] and record['evidence_url'], f'Unsubstantiated partial evidence: {rid}')
        review = record['completion_review']
        require(re.fullmatch(r'[0-9a-f]{40}', review['implementation_commit']), f'Missing source commit: {rid}')
        require(review['observed_full_acceptance_result'] in {'NOT RUN', 'BLOCKED', 'FAIL', 'SUPERSEDED'},
                f'Unsupported full-acceptance claim: {rid}')
        require(review['related_regression_group'] in groups, f'Missing component regression group: {rid}')
        for field in ('acceptance_steps', 'acceptance_command', 'environment', 'evidence_scope', 'blocker_owner', 'remaining_work', 'regression_status'):
            require(review[field], f'Missing {field}: {rid}')
        for path in review['implementation_files']:
            require(not Path(path).is_absolute() and '..' not in Path(path).parts and (root / path).is_file(),
                    f'Invalid implementation reference: {rid}: {path}')
        superseded = record['acceptance_state'] == 'superseded_by_plaintext_design'
        require((review['observed_full_acceptance_result'] == 'SUPERSEDED') == superseded,
                f'Inconsistent supersession: {rid}')
    counts = Counter(r['acceptance_state'] for r in records)
    require(counts['superseded_by_plaintext_design'] >= 2, 'Explicit plaintext supersessions are missing')
    require(evidence['counts'] == dict(counts, total=106), 'Summary counts disagree with records')
    return counts


def main():
    registry = json.loads((ROOT / 'docs/handoff/requirements.json').read_text(encoding='utf-8'))
    evidence = json.loads((ROOT / 'docs/handoff/requirement-evidence.json').read_text(encoding='utf-8'))
    counts = validate(registry, evidence)
    print('106 distinct requirements have reviewed implementation, regression, acceptance and blocker records.')
    for status, count in sorted(counts.items()):
        print(f'{status}: {count}')
    print('No full-production acceptance is claimed.')


if __name__ == '__main__':
    main()
