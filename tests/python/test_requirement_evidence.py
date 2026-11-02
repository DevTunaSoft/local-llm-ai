import copy
import importlib.util
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('traceability', ROOT / 'scripts/verify-requirements.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class TraceabilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.registry = json.loads((ROOT / 'docs/handoff/requirements.json').read_text())
        cls.evidence = json.loads((ROOT / 'docs/handoff/requirement-evidence.json').read_text())

    def test_current_evidence_is_consistent(self):
        module.validate(self.registry, self.evidence)

    def test_deleted_requirement_is_rejected(self):
        altered = copy.deepcopy(self.evidence)
        altered['records'].pop()
        with self.assertRaises(ValueError):
            module.validate(self.registry, altered)

    def test_missing_implementation_file_is_rejected(self):
        altered = copy.deepcopy(self.evidence)
        altered['records'][0]['completion_review']['implementation_files'] = ['missing/source.cs']
        with self.assertRaises(ValueError):
            module.validate(self.registry, altered)

    def test_invented_test_case_is_rejected(self):
        altered = copy.deepcopy(self.evidence)
        altered['component_regression_groups']['WIN'] = ['tests/Core/StorageTests.cs::InventedPassingTest']
        with self.assertRaises(ValueError):
            module.validate(self.registry, altered)

    def test_full_acceptance_claim_is_rejected_without_certification_support(self):
        altered = copy.deepcopy(self.evidence)
        altered['records'][0]['completion_review']['observed_full_acceptance_result'] = 'PASS'
        with self.assertRaises(ValueError):
            module.validate(self.registry, altered)

    def test_false_summary_is_rejected(self):
        altered = copy.deepcopy(self.evidence)
        altered['counts']['total'] = 105
        with self.assertRaises(ValueError):
            module.validate(self.registry, altered)


if __name__ == '__main__':
    unittest.main()
