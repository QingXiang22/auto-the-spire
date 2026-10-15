"""从 v0.1 wire schema 派生 v0.2 wire schema（ENV-T03 的协议 0.2 机器资产）。

为什么是派生而不是手写第二份：``protocol.md`` §7 明确规定 0.2 只差四件事——
版本常量、``start.seed`` 形状、``start`` 成功结果多一个实际 seed、以及新增 ``string`` 值类型；
结构（目录/快照/错误/生命周期）两版本相同。手写一份两千行的 0.2 schema 会让两版结构各自漂移，
因此这里逐条应用 §7 的差异，其余结构与 v0.1 逐字节相同。

用法::

    python -X utf8 net-the-spire/tools/emit_wire_schema_0_2.py            # 写入 0.2/wire.schema.json
    python -X utf8 net-the-spire/tools/emit_wire_schema_0_2.py --check    # 只核对已落盘文件是否同步

输出是**自包含**的（所有 ``$ref`` 仍是本地 ``#/$defs/*``）：两端的 schema 校验器
（``Net.TheSpire.Contracts/LiteSchema.cs`` 与 ``tools/contract-check/jsonschema_min.py``）
只解析本地引用，跨文件 ``$ref`` 会让 0.2 资产在验收时根本读不了。
"""

from __future__ import annotations

import argparse
import io
import json
import pathlib
import sys

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = REPO_ROOT / "docs" / "contracts" / "net" / "0.1" / "wire.schema.json"
TARGET = REPO_ROOT / "docs" / "contracts" / "net" / "0.2" / "wire.schema.json"

V01 = "0.1"
V02 = "0.2"


def build(source: dict) -> dict:
    """按 protocol.md §7 把 v0.1 schema 变成 v0.2 schema（结构其余部分不动）。"""
    document = json.loads(json.dumps(source))  # 深拷贝，避免就地改调用方对象

    document["title"] = "net-the-spire 公开协议 v0.2 wire schema"
    document["description"] = (
        "由 docs/contracts/net/0.1/wire.schema.json 派生（见 net-the-spire/tools/emit_wire_schema_0_2.py），"
        "只应用 docs/design/net-the-spire/protocol.md §7 列出的 0.2 差异：协议版本常量、start.seed 为"
        "非空游戏字符串、start 成功结果多一个实际 seed、以及新增 string 值类型。结构其余部分与 0.1 逐字节相同。"
    )
    document["$comment"] = (
        "ENV-T03：0.2 只对正式单人构建开放，0.1 保留旧切片/作者场景；两版本常量各自精确相等，不自动降级。"
    )

    defs = document["$defs"]

    # ① 版本常量：Identity / PublicCatalog / PublicSnapshot 三处都写 0.2（exact identity）。
    for name in ("Identity", "PublicCatalog", "PublicSnapshot"):
        node = defs[name]["properties"]["protocol_version"]
        if node.get("const") != V01:
            raise SystemExit(f"{name}.protocol_version 不是 v0.1 常量：{node!r}")
        node["const"] = V02
        node["description"] = (
            "net 协议版本；0.2 与目录/快照必须一致地写 \"0.2\"，与 0.1 精确区分（protocol.md §7）。"
        )

    # ② 值类型：0.2 多一个 string（不冒充有限类别）。
    value_type = defs["ValueType"]
    if "string" in value_type["enum"]:
        raise SystemExit("ValueType 里已经有 string：源文件可能已被改成 0.2")
    value_type["enum"] = [*value_type["enum"], "string"]
    value_type["description"] = (
        "字段声明 value_type（§1 第 30、40 行；§2 表 第 66-71 行）。0.2 新增 string（§7）："
        "公开字符串原样编码，不是类别词表。"
    )

    # ③ start.seed：0.2 是**非空游戏字符串**，不再走规范 uint64 十进制编码。
    defs["GameSeedString"] = {
        "type": "string",
        "minLength": 1,
        "description": (
            "0.2 的每局游戏 seed 字符串（protocol.md §7）：非空；net 不 trim、不改大小写、"
            "不数值强转，具体校验/标准化由规则提供方执行。旧的规范 uint64 十进制编码（SessionSeed）"
            "只在 0.1 保留。"
        ),
    }
    start_payload_seed = defs["StartPayload"]["properties"]["seed"]
    if start_payload_seed.get("$ref") != "#/$defs/SessionSeed":
        raise SystemExit(f"StartPayload.seed 不是 SessionSeed 引用：{start_payload_seed!r}")
    defs["StartPayload"]["properties"]["seed"] = {"$ref": "#/$defs/GameSeedString"}
    defs["StartPayload"]["description"] = (
        "start payload = {expected_identity, scenario_profile, seed?}（§4 表 第 170 行；0.2 的 seed 形状见 §7）。"
    )

    # ④ start 成功结果：0.2 恰为 {session_id, seed, snapshot}，seed 是本次实际接受的游戏字符串。
    start_result = defs["StartResult"]
    if "seed" in start_result["required"]:
        raise SystemExit("StartResult 里已经有 seed：源文件可能已被改成 0.2")
    start_result["required"] = [*start_result["required"], "seed"]
    start_result["properties"]["seed"] = {"$ref": "#/$defs/GameSeedString"}
    start_result["description"] = (
        "start 成功结果（§4 表 第 170 行；0.2 为 {session_id, seed, snapshot}，seed 是本局实际接受值，见 §7）。"
    )

    # ⑤ string 值可以是空串：0.1 的 value 只允许 NonEmptyString（类别/引用 ID 非空），
    #    而 0.2 的公开 String 字段可以合法为 ""（例如尚未选定的意图名）。加一个 string 分支，
    #    不放松任何既有分支：DecimalString / NonEmptyString / boolean 全部保留。
    field_value = defs["Field"]["properties"]["value"]
    if not any(branch.get("$ref") == "#/$defs/NonEmptyString" for branch in field_value["anyOf"]):
        raise SystemExit(f"Field.value 结构与预期不符：{field_value!r}")
    field_value["anyOf"] = [*field_value["anyOf"], {"type": "string"}]
    field_value["description"] = (
        "known 的 wire 值：number→规范十进制字符串、boolean→JSON true/false、category→类别 ID 字符串、"
        "entity_ref→快照内 instance_id 字符串（§2 表 第 66-71 行）、0.2 的 string→原样字符串（可为空串，§7）。"
    )

    return document


def encode(document: dict) -> str:
    return json.dumps(document, ensure_ascii=False, indent=2) + "\n"


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="派生 docs/contracts/net/0.2/wire.schema.json")
    parser.add_argument("--check", action="store_true", help="只核对已落盘文件与派生结果一致")
    args = parser.parse_args(argv)

    source = json.load(io.open(SOURCE, encoding="utf-8"))
    text = encode(build(source))

    if args.check:
        if not TARGET.exists():
            print(f"缺少 {TARGET}", file=sys.stderr)
            return 1
        current = TARGET.read_text(encoding="utf-8")
        if current != text:
            print(f"{TARGET} 与派生结果不一致：重新运行本脚本", file=sys.stderr)
            return 1
        print(f"0.2 wire schema 与派生结果一致：{TARGET}")
        return 0

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {TARGET} ({len(text)} chars)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
