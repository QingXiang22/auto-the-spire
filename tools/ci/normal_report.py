"""Read normal-run reports without expanding unused diagnostic histories."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import zipfile


class _Evidence(dict):
    def __init__(self, source: dict, reader: _Reader):
        super().__init__(source)
        self.reader = reader

    def __getitem__(self, key):
        value = self.reader.resolve(super().__getitem__(key))
        super().__setitem__(key, value)
        return value

    def get(self, key, default=None):
        return self[key] if key in self else default

    def items(self):
        return ((key, self[key]) for key in self)

    def values(self):
        return (self[key] for key in self)

    def close(self):
        self.reader.archive.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


class _Reader:
    def __init__(self, path: Path, report: dict):
        self.archive = zipfile.ZipFile(path)
        try:
            sizes = {}
            for name in self.archive.namelist():
                if name in sizes or not re.fullmatch(r"[0-9a-f]{64}\.json", name):
                    raise ValueError("invalid or duplicate normal evidence entry")
                data = self.archive.read(name)
                if hashlib.sha256(data).hexdigest() + ".json" != name:
                    raise ValueError("normal evidence digest mismatch")
                value = json.loads(data)
                sizes[name] = len(value) if isinstance(value, list) else None

            def validate(value):
                if isinstance(value, dict):
                    if set(value) == {"$artifact"}:
                        if value["$artifact"] not in sizes:
                            raise ValueError("missing normal evidence entry")
                    elif set(value) == {"$chunks", "$count"}:
                        counts = [sizes.get(chunk.get("$artifact")) for chunk in value["$chunks"]]
                        if any(count is None for count in counts) or sum(counts) != value["$count"]:
                            raise ValueError("normal evidence chunk count mismatch")
                    else:
                        for child in value.values():
                            validate(child)
                elif isinstance(value, list):
                    for child in value:
                        validate(child)
            validate(report)
        except Exception:
            self.archive.close()
            raise

    def resolve(self, value):
        if isinstance(value, _Evidence):
            return value
        if isinstance(value, dict):
            if set(value) == {"$artifact"}:
                name = value["$artifact"]
                if not isinstance(name, str) or not re.fullmatch(r"[0-9a-f]{64}\.json", name):
                    raise ValueError("invalid normal evidence reference")
                data = self.archive.read(name)
                if hashlib.sha256(data).hexdigest() + ".json" != name:
                    raise ValueError("normal evidence digest mismatch")
                return self.resolve(json.loads(data))
            if set(value) == {"$chunks", "$count"}:
                result = []
                for reference in value["$chunks"]:
                    chunk = self.resolve(reference)
                    if not isinstance(chunk, list):
                        raise ValueError("normal evidence chunk is not an array")
                    result.extend(chunk)
                if len(result) != value["$count"]:
                    raise ValueError("normal evidence chunk count mismatch")
                return result
            return _Evidence(value, self)
        if isinstance(value, list):
            return [self.resolve(item) for item in value]
        return value

    def __del__(self):
        if hasattr(self, "archive"):
            self.archive.close()


def load_report(path: Path) -> dict:
    report = json.loads(path.read_text(encoding="utf-8"))
    if report.get("schema", "").startswith("normal-run/paired@"):
        if report["schema"] != "normal-run/paired@2" or report.get("evidence_archive") != "normal-evidence.zip":
            raise ValueError("unsupported normal report format")
        return _Reader(path.parent / report["evidence_archive"], report).resolve(report)
    return report
