"""故障注入用的**脚本化测试后端**（H-03）。

它只按脚本回答协议行，**不结算任何规则、不代表游戏后端、不代表真实模拟**。存在的唯一理由是：
同步宿主在一个会话锁内走完 pending → settling → 应答，因此 `request_status` 的 `pending` /
`outcome_unknown` 在真实宿主上不可达（见 `Net.TheSpire.Tests` 的
「H-03 同步后端只报 completed/not_found，不伪造 pending/outcome_unknown」）。这两个状态是协议
为异步提供方和进程丢失后的重新识别预留的，客户端对它们的处理必须单独验证。

用法::

    python scripted_host.py '{"silent":["lost-1"],"request_status":["pending"]}'

脚本键：

- ``silent``：这些 ``request_id`` 完全不回答（模拟响应丢失/超时）。
- ``request_status``：依次用于回答 ``request_status`` 的状态字符串（用完后重复最后一个）。
- ``session_id``：``start`` 返回的会话句柄，默认 ``"session-scripted-1"``。
"""

from __future__ import annotations

import json
import sys
from typing import Any


def _reply(response: dict[str, Any]) -> None:
    sys.stdout.write(json.dumps(response, ensure_ascii=False, separators=(",", ":")) + "\n")
    sys.stdout.flush()


def main(argv: list[str]) -> int:
    script = json.loads(argv[0]) if argv else {}
    silent = set(script.get("silent", []))
    statuses: list[str] = list(script.get("request_status", ["pending"]))
    session_id = script.get("session_id", "session-scripted-1")

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
        except json.JSONDecodeError:
            _reply({"request_id": "", "ok": False,
                    "error": {"code": "invalid_input", "message": "scripted host: 不是 JSON"}})
            continue

        request_id = request.get("request_id", "")
        operation = request.get("operation", "")
        if request_id in silent:
            # 故意不回答：驱动客户端的超时路径。
            continue

        if operation == "request_status":
            status = statuses[0] if len(statuses) == 1 else statuses.pop(0)
            result: dict[str, Any] = {"status": status, "response": None}
        elif operation == "describe":
            profile = script.get("identity", {})
            result = {
                "identity": {
                    "protocol_version": "0.1",
                    "ruleset_id": profile.get("ruleset_id", "scripted:ruleset"),
                    "catalog_id": profile.get("catalog_id", "scripted:catalog"),
                    "backend_profile_id": profile.get("backend_profile_id", "scripted:backend"),
                },
                "capabilities": {"backend_kind": "scripted", "operations": ["describe", "start"]},
            }
        elif operation == "start":
            result = {"session_id": session_id, "snapshot": None}
        else:
            result = {}

        _reply({"request_id": request_id, "ok": True, "result": result})

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
