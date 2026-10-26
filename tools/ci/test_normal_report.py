"""Lossless normal-report reading and rejection of incomplete or corrupt evidence."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from normal_report import load_report


class NormalReportTests(unittest.TestCase):
    def test_shared_history_preserves_prefixes_and_exact_strings(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            records = [{"sequence": i, "candidate": ["甲", str(i)]} for i in range(128)]
            exact = '{"value":"\\u7532","number":1.2500}'
            with zipfile.ZipFile(root / "normal-evidence.zip", "w", zipfile.ZIP_DEFLATED) as archive:
                def store(value):
                    data = json.dumps(value, ensure_ascii=False).encode()
                    name = hashlib.sha256(data).hexdigest() + ".json"
                    archive.writestr(name, data)
                    return {"$artifact": name}
                chunks = [store(records[:64]), store(records[64:])]
                report = {"schema": "normal-run/paired@2", "evidence_archive": "normal-evidence.zip",
                          "early": {"$chunks": chunks[:1], "$count": 64},
                          "late": {"$chunks": chunks, "$count": 128}, "exact": store(exact)}
            path = root / "result.json"
            path.write_text(json.dumps(report), encoding="utf-8")
            with load_report(path) as actual:
                self.assertEqual(actual["early"], records[:64])
                self.assertEqual(actual["late"], records)
                self.assertEqual(actual["exact"], exact)

    def test_bad_count_missing_entry_and_corruption_fail(self):
        for failure in ("count", "missing", "corrupt"):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                data = b'[1,2]'
                name = hashlib.sha256(data).hexdigest() + ".json"
                with zipfile.ZipFile(root / "normal-evidence.zip", "w") as archive:
                    if failure != "missing":
                        archive.writestr(name, b'[3,4]' if failure == "corrupt" else data)
                path = root / "result.json"
                path.write_text(json.dumps({"schema": "normal-run/paired@2", "evidence_archive": "normal-evidence.zip",
                                           "history": {"$chunks": [{"$artifact": name}], "$count": 3 if failure == "count" else 2}}), encoding="utf-8")
                with self.assertRaises((ValueError, KeyError)):
                    load_report(path)


if __name__ == "__main__":
    unittest.main()
