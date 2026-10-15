"""公开产物的落盘与独立复核（D-01）。

目录约定（产物不写进源码树）::

    <root>/
      index.jsonl                 # 一行一条索引：episode_id → 路径/摘要/结束分类/步数/身份摘要
      records/<episode_id>.json   # 一条记录一个文件（规范字节）

索引是**派生**的：`rebuild_index()` 从记录重新生成，`verify_all()` 比对磁盘索引与记录的摘要，
因此"索引被手改过"或"记录被截断"都会被发现，而不是被静默接受。
"""

from __future__ import annotations

import pathlib
from typing import Any, Iterator, Mapping, Optional

from .record import (
    RECORD_FORMAT_VERSION,
    EpisodeRecord,
    RecordError,
    canonical_bytes,
    digest_of,
    validate_document,
)

INDEX_FILE = "index.jsonl"
RECORDS_DIR = "records"


class EpisodeStore:
    """一个有界的公开产物目录：追加写、按 id 读、独立复核。"""

    def __init__(self, root: "str | pathlib.Path") -> None:
        self.root = pathlib.Path(root)

    # -- 路径 ----------------------------------------------------------------------------------------------

    @property
    def index_path(self) -> pathlib.Path:
        return self.root / INDEX_FILE

    @property
    def records_dir(self) -> pathlib.Path:
        return self.root / RECORDS_DIR

    def record_path(self, episode_id: str) -> pathlib.Path:
        if not episode_id or "/" in episode_id or "\\" in episode_id:
            raise RecordError(f"非法 episode_id: {episode_id!r}")
        return self.records_dir / f"{episode_id}.json"

    # -- 写 ------------------------------------------------------------------------------------------------

    def write(self, record: EpisodeRecord) -> pathlib.Path:
        """写一条记录并追加索引项。同一 episode_id 已存在即拒绝（**不静默覆盖**）。"""
        document = record.to_wire()
        target = self.record_path(record.episode_id)
        if target.exists():
            raise RecordError(f"同一 episode_id 已存在，拒绝覆盖: {record.episode_id}")
        self.records_dir.mkdir(parents=True, exist_ok=True)
        target.write_bytes(canonical_bytes(document))
        with self.index_path.open("a", encoding="utf-8") as handle:
            handle.write(message(record))
        return target

    # -- 读 ------------------------------------------------------------------------------------------------

    def read(self, episode_id: str) -> dict:
        """按 id 读记录并做结构/语义校验（不信任磁盘内容）。"""
        path = self.record_path(episode_id)
        if not path.exists():
            raise RecordError(f"记录不存在: {episode_id}")
        import json

        document = json.loads(path.read_text(encoding="utf-8"))
        validate_document(document)
        if document["episode_id"] != episode_id:
            raise RecordError(f"文件名与记录内 episode_id 不一致: {episode_id} != {document['episode_id']}")
        if document["record_digest"] != digest_of(_digest_material(document)):
            raise RecordError(f"记录摘要与内容不一致（内容被改动过）: {episode_id}")
        return document

    def read_record(self, episode_id: str) -> EpisodeRecord:
        return EpisodeRecord.from_wire(self.read(episode_id))

    def index(self) -> list[dict]:
        if not self.index_path.exists():
            return []
        import json

        entries = []
        for line in self.index_path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                entries.append(json.loads(line))
        return entries

    def iter_records(self) -> Iterator[dict]:
        for entry in self.index():
            yield self.read(entry["episode_id"])

    # -- 复核 ----------------------------------------------------------------------------------------------

    def rebuild_index(self) -> list[dict]:
        """从记录重建索引（用于对比磁盘索引是否漂移）。"""
        import json

        entries: list[dict] = []
        for path in sorted(self.records_dir.glob("*.json")):
            document = json.loads(path.read_text(encoding="utf-8"))
            validate_document(document)
            entries.append(_index_entry(document))
        return entries

    def verify_all(self) -> list[str]:
        """独立复核：索引与记录一致 + 摘要/引用/身份完整。返回问题列表（空为通过）。"""
        problems: list[str] = []
        disk = self.index()
        rebuilt = self.rebuild_index()
        by_id = {entry["episode_id"]: entry for entry in rebuilt}
        if len(by_id) != len(rebuilt):
            problems.append("记录目录里存在重复 episode_id")
        if {entry["episode_id"] for entry in disk} != set(by_id):
            problems.append("索引与记录目录不一致（有记录没有索引项，或有索引项没有记录）")
        for entry in disk:
            expected = by_id.get(entry["episode_id"])
            if expected is None:
                continue
            for key in ("record_digest", "ending_class", "steps", "identities_digest", "path"):
                if entry.get(key) != expected.get(key):
                    problems.append(f"索引项 {entry['episode_id']} 的 {key} 与记录不一致")
        return problems


def message(record: EpisodeRecord) -> str:
    import json

    return json.dumps(_index_entry(record.to_wire()), ensure_ascii=False, sort_keys=True,
                      separators=(",", ":")) + "\n"


def _index_entry(document: Mapping[str, Any]) -> dict:
    return {
        "format_version": RECORD_FORMAT_VERSION,
        "episode_id": document["episode_id"],
        "path": f"{RECORDS_DIR}/{document['episode_id']}.json",
        "record_digest": document.get("record_digest") or digest_of(_digest_material(document)),
        "ending_class": document["ending"]["class"],
        "steps": len(document["transitions"]),
        "confirmed_transitions": sum(1 for t in document["transitions"] if t["outcome"] == "confirmed"),
        "unknown_transitions": sum(1 for t in document["transitions"] if t["outcome"] == "unknown"),
        "identities_digest": digest_of(document["identities"]),
    }


def _digest_material(document: Mapping[str, Any]) -> dict:
    """与 `EpisodeRecord.digest_material()` 同构：剔除自身摘要，并按控制元数据归一化快照后重算。"""
    from .record import strip_control_metadata

    if "record_format_version" not in document:
        raise RecordError("记录缺少 record_format_version")
    transitions = []
    for transition in document["transitions"]:
        item = dict(transition)
        item["snapshot"] = strip_control_metadata(transition["snapshot"])
        transitions.append(item)
    return {
        "record_format_version": document["record_format_version"],
        "episode_id": document["episode_id"],
        "identities": document["identities"],
        "provenance": document["provenance"],
        "catalog": document["catalog"],
        "ending": document["ending"],
        "transitions": transitions,
    }
