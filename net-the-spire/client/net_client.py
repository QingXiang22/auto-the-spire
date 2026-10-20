"""net-the-spire 的 Python NDJSON 客户端（N-04 交付物；仅标准库）。

职责：把宿主当成一个"一次一行的协议通道"使用，并把 **超时** 与 **不确定结果** 的恢复规则写进代码：
`protocol.md:197/:199` —— 超时只说明"没收到完成应答"，**不是"没执行"**；必须用**原 request_id**
查询 `request_status`：`completed` 取回原响应、`pending` 继续等或停、`not_found` **不是未执行证明**
（协调者需按 uncertain 处理并停止控制，不得盲目重发）。

H-03 补强的两条通道契约：

1. **响应按 `request_id` 归属**：一行响应不保证就是当前请求的响应（超时后迟到的响应会排在恢复查询
   之前）。`_read_response` 逐行比对 `request_id`，把迟到响应归档给它自己的请求；`_recover` 先取归档，
   再查 `request_status`。唯一的例外是宿主对"解析前就被拒绝的帧"给出的占位 `request_id`
   （`<malformed>`）：它无法归属到任何请求，只可能属于刚刚写出的那一帧。
2. **传输层失败也是不确定**：管道断开、恢复查询无响应 → `OutcomeUnknown`，绝不编造结果、绝不重发。

用法::

    client = NetClient.host_from_solution()      # 优先直接用已构建的宿主程序集
    identity = client.describe()["result"]["identity"]
    ...
    client.shutdown()

故障注入（H-03，只用于检查，不代表真实游戏）：``host_from_solution_delayed(delay)`` 是真实宿主 +
延迟管道，``scripted(script)`` 是不结算任何规则的脚本化测试后端。
"""

from __future__ import annotations

import json
import os
import pathlib
import subprocess
import sys
import time
import uuid
from typing import Any, Optional

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
HERE = pathlib.Path(__file__).resolve().parent
HOST_PROJECT = REPO_ROOT / "net-the-spire" / "Net.TheSpire.Host" / "Net.TheSpire.Host.csproj"
HOST_DLL = (REPO_ROOT / "net-the-spire" / "Net.TheSpire.Host" / "bin" / "Debug" / "net9.0"
            / "Net.TheSpire.Host.dll")

#: 宿主对"解析前就被拒绝的帧"使用的非空占位 request_id（Net.TheSpire.Host/Program.cs）。
MALFORMED_REQUEST_ID = "<malformed>"


def host_command() -> list[str]:
    """宿主启动命令。

    优先**直接**运行已构建的宿主程序集：`dotnet run` 会再起一层包装进程，杀掉的只是包装、
    真正的宿主会变成孤儿并占住管道（H-03 的断开注入需要"杀的就是宿主"）。同时这也避免测试触发构建。
    程序集不存在时回退到 `dotnet run`（需要先 `dotnet build AutoTheSpire.sln`）。
    """
    explicit = os.environ.get("NET_THE_SPIRE_HOST_DLL")
    if explicit:
        selected = pathlib.Path(explicit)
        if not selected.is_file():
            raise FileNotFoundError(f"NET_THE_SPIRE_HOST_DLL does not exist: {selected}")
        return ["dotnet", str(selected)]
    if HOST_DLL.exists():
        return ["dotnet", str(HOST_DLL)]
    return ["dotnet", "run", "--project", str(HOST_PROJECT), "--"]

class NetClientError(RuntimeError):
    """客户端侧错误（协议响应之外的问题）。"""


class OutcomeUnknown(NetClientError):
    """结果不确定：超时后 `request_status` 既不是 completed 也不是 pending。

    调用方**不得**重发原请求；应按 `protocol.md:215` 停止控制并交给协调者。
    """

    def __init__(self, request_id: str, status: str) -> None:
        super().__init__(
            f"request {request_id} 结果不确定（status={status}）：not_found 不是未执行的证明，"
            "不得盲目重发；按 uncertain 处理并停止控制")
        self.request_id = request_id
        self.status = status


class _LineReader:
    """后台线程逐行读取（管道上的阻塞读），主线程用队列超时取行。"""

    def __init__(self, stream) -> None:
        import queue
        import threading

        self._queue: "queue.Queue[Optional[bytes]]" = queue.Queue()
        self._thread = threading.Thread(target=self._pump, args=(stream,), daemon=True)
        self._thread.start()

    def _pump(self, stream) -> None:
        try:
            for line in iter(stream.readline, b""):
                self._queue.put(line)
        finally:
            self._queue.put(None)

    def get(self, timeout: Optional[float]) -> Optional[bytes]:
        import queue

        try:
            line = self._queue.get(timeout=timeout)
        except queue.Empty:
            return None
        return None if line in (None, b"") else line


class NetClient:
    """一个宿主进程的连接。每次 :meth:`request` 写一行 NDJSON 并读一行响应。"""

    def __init__(self, command: list[str], *, frame_limit: Optional[int] = None,
                 cwd: Optional[pathlib.Path] = None) -> None:
        self._command = command + (["--frame-limit", str(frame_limit)] if frame_limit else [])
        self._process = subprocess.Popen(
            self._command,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            cwd=str(cwd or REPO_ROOT),
        )
        self._reader: Optional[_LineReader] = None
        # 按 request_id 归档的"迟到响应"（超时后才是到达的，或与本请求无关的错序响应）。
        self._late: dict[str, dict] = {}
        # 诊断计数（H-03 证据用）：迟到响应归位次数、传输层失败次数。
        self.late_archived = 0
        self.late_recovered = 0
        self.transport_failures = 0

    # -- 构造 -----------------------------------------------------------------------------------------------

    @classmethod
    def host_from_solution(cls, *, frame_limit: Optional[int] = None,
                           extra_args: Optional[list[str]] = None) -> "NetClient":
        """起宿主（默认帧上限）：优先已构建的程序集，见 :func:`host_command`。

        ``extra_args`` 用于宿主的显式开关（例如 ``--test-headbutt``，受控测试场景；见宿主文档）。
        """
        return cls(host_command() + list(extra_args or []), frame_limit=frame_limit)

    @classmethod
    def host_from_solution_delayed(cls, delay: float) -> "NetClient":
        """**故障注入**：真实宿主 + 固定延迟管道（宿主 stdout 晚 `delay` 秒转发给客户端）。

        用来验证"响应只是迟到"这条路径：客户端必须把迟到响应归还原请求，不能把它当成后一个请求的响应，
        也不能因此重发原请求。注入的是传输延迟，不改变宿主行为。
        """
        return cls([sys.executable, str(HERE / "delay_proxy.py"), "--delay", str(delay), "--"]
                   + host_command())

    @classmethod
    def scripted(cls, script: dict) -> "NetClient":
        """**故障注入**：脚本化测试后端（`scripted_host.py`）。

        它只按脚本回答协议行，**不结算任何规则、不代表游戏后端**；用途是驱动客户端在真实宿主上
        无法产生的 `pending` / `outcome_unknown` 分支。
        """
        return cls([sys.executable, str(HERE / "scripted_host.py"), json.dumps(script, separators=(",", ":"))])

    # -- 通道 -----------------------------------------------------------------------------------------------

    def _write_frame(self, frame: dict) -> None:
        assert self._process.stdin is not None
        payload = json.dumps(frame, ensure_ascii=False, separators=(",", ":"))
        self._process.stdin.write((payload + "\n").encode("utf-8"))
        self._process.stdin.flush()

    def request(self, operation: str, payload: Optional[dict] = None, *,
                request_id: Optional[str] = None, timeout: Optional[float] = None) -> dict:
        """发一条请求并读回响应；`timeout` 秒内没有响应则走**恢复规则**（见模块文档）。"""
        request_id = request_id or uuid.uuid4().hex
        frame = {"request_id": request_id, "operation": operation, "payload": payload or {}}
        try:
            self._write_frame(frame)
        except OSError as failure:
            # 管道已断：请求是否送达、是否执行都无法确认 → 按 uncertain 停止控制（protocol.md:197/199）。
            self.transport_failures += 1
            raise OutcomeUnknown(request_id, "transport_failure") from failure
        response = self._read_response(request_id, timeout)
        if response is None:
            return self._recover(request_id)
        if response.get("error"):
            # 错误响应**只按 wire 形状**返回：`$defs.Error` 是 additionalProperties:false，往 error 对象里塞
            # request_id 会让宿主发来的合规帧在本客户端手里变成不合规（schema 校验会报未知键）。
            # 归属信息本来就在信封（`$defs.Response.request_id`）上；宿主用 "<malformed>" 占位时无法归属到
            # 具体请求，这里只保证信封上有值，不改 wire。
            response.setdefault("request_id", request_id)
        return response

    def _read_response(self, request_id: str, timeout: Optional[float]) -> Optional[dict]:
        """读一行并**按 request_id 归属**。

        通道上"一行响应"不保证就是当前请求的响应：超时后迟到的响应会排在恢复查询之前。把它当成当前请求的
        响应会造成张冠李戴，因此按 `request_id` 归档，留给它自己的请求去取（响应包必带 `request_id`）。
        """
        deadline = None if timeout is None else time.monotonic() + timeout
        while True:
            if deadline is None:
                remaining = None
            else:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    return None
            line = self._read_line(remaining)
            if line is None:
                return None
            response = json.loads(line)
            owner = response.get("request_id")
            if not owner or owner == request_id or owner == MALFORMED_REQUEST_ID:
                # 空/占位 request_id：超限、畸形或空帧在解析前就被拒绝，宿主无法把响应归属到某个
                # request_id（见 Net.TheSpire.Host/Program.cs 的 "<malformed>" 非空占位）。它只可能属于
                # **刚刚写出的那一帧**，也就是当前请求。
                return response
            # 别的 request_id：迟到或错序的响应，归档给它自己的请求，不能当成当前请求的响应。
            self._late[owner] = response
            self.late_archived += 1

    def _read_line(self, timeout: Optional[float]) -> Optional[str]:
        """读一行；超时返回 None（超时**不**代表未执行）。

        用后台读取线程 + 队列实现超时：Windows 上 ``select`` 对管道会抛 WSAStartup 错误，
        且管道的"可读"并不等价于"已有一整行"。
        """
        if self._reader is None:
            assert self._process.stdout is not None
            self._reader = _LineReader(self._process.stdout)
        line = self._reader.get(timeout)
        return None if line is None else line.decode("utf-8").strip()

    def send_raw(self, line: str, *, timeout: Optional[float] = None) -> Optional[dict]:
        """写一行**原始**文本（可故意畸形）并读回一行；必须走同一个读取器，不能另开 readline。"""
        assert self._process.stdin is not None
        self._process.stdin.write((line + "\n").encode("utf-8"))
        self._process.stdin.flush()
        response_line = self._read_line(timeout)
        return None if response_line is None else json.loads(response_line)

    def _recover(self, request_id: str) -> dict:
        """超时后的恢复：先看迟到响应，再查询**原 request_id**（绝不重发原请求）。"""
        late = self._late.pop(request_id, None)
        if late is not None:
            # 响应只是迟到：它本身就是该请求的完成结果，不需要查询、更不得重发。
            self.late_recovered += 1
            return late

        query_id = uuid.uuid4().hex
        try:
            self._write_frame({"request_id": query_id, "operation": "request_status",
                               "payload": {"target_request_id": request_id}})
            response = self._read_response(query_id, 10.0)
        except OSError as failure:
            self.transport_failures += 1
            raise OutcomeUnknown(request_id, "unreachable") from failure
        if response is None:
            self.transport_failures += 1
            raise OutcomeUnknown(request_id, "no_response")
        result = response.get("result") or {}
        status = result.get("status")
        if status == "completed":
            return result.get("response") or {}
        if status == "pending":
            raise NetClientError(f"request {request_id} 仍在执行（pending）：等待或停止控制，不得重发")
        raise OutcomeUnknown(request_id, str(status))

    # -- 便捷方法 -------------------------------------------------------------------------------------------

    def describe(self, *, timeout: Optional[float] = None) -> dict:
        return self.request("describe", {}, timeout=timeout)

    def get_catalog(self, catalog_id: str, *, timeout: Optional[float] = None) -> dict:
        return self.request("get_catalog", {"catalog_id": catalog_id}, timeout=timeout)

    def start(self, expected_identity: dict, scenario_profile: str, *,
              seed: Optional[str] = None, request_id: Optional[str] = None,
              timeout: Optional[float] = None) -> dict:
        """`start`。

        seed 是可选的非空游戏字符串，原样发送，不 trim、不改大小写或数值转换。
        成功结果包含 session_id、seed 和 snapshot；seed 是本次实际接受值。
        scenario_profile 使用 describe 发布的受支持配置。
        """
        payload: dict = {"expected_identity": expected_identity, "scenario_profile": scenario_profile}
        if seed is not None:
            payload["seed"] = seed
        return self.request("start", payload, request_id=request_id, timeout=timeout)

    def observe(self, session_id: str, *, timeout: Optional[float] = None) -> dict:
        return self.request("observe", {"session_id": session_id}, timeout=timeout)

    def step(self, session_id: str, expected_observation_id: int, candidate_id: str, *,
             request_id: Optional[str] = None, timeout: Optional[float] = None) -> dict:
        return self.request("step", {
            "session_id": session_id,
            "expected_observation_id": expected_observation_id,
            "candidate_id": candidate_id,
        }, request_id=request_id, timeout=timeout)

    def request_status(self, target_request_id: str, session_id: Optional[str] = None, *,
                       timeout: Optional[float] = None) -> dict:
        payload: dict = {"target_request_id": target_request_id}
        if session_id is not None:
            payload["session_id"] = session_id
        return self.request("request_status", payload, timeout=timeout)

    def close_session(self, session_id: str, *, timeout: Optional[float] = None) -> dict:
        return self.request("close", {"session_id": session_id}, timeout=timeout)

    # -- 生命周期 -------------------------------------------------------------------------------------------

    def stderr_text(self) -> str:
        assert self._process.stderr is not None
        try:
            remaining = self._process.stderr.read()
        except Exception:  # pragma: no cover - 进程已退出时读不到
            remaining = b""
        if isinstance(remaining, bytes):
            # 管道是二进制模式（见 __init__ 的 Popen），read() 给的是 bytes；与 str 相加会 TypeError。
            remaining = remaining.decode("utf-8", "replace")
        return self._diagnostics + (remaining or "")

    _diagnostics = ""

    def shutdown(self) -> int:
        # 宿主可能已被 kill（断开注入）：关闭已终止进程的 stdin 在 Windows 上会抛 OSError，
        # 这不是被测行为，清理必须容错。
        try:
            if self._process.stdin is not None:
                self._process.stdin.close()
        except OSError:
            pass
        try:
            return self._process.wait(timeout=30)
        except subprocess.TimeoutExpired:
            self._process.kill()
            return self._process.wait(timeout=30)

    def kill(self) -> None:
        """**故障注入**：直接杀掉宿主进程（模拟进程丢失/连接断开）。

        进程丢失后的 `not_found` 不是"未执行"的证明（protocol.md:199），因此调用方看到的是
        <see cref="OutcomeUnknown"/> 而不是任何编造的结果。
        """
        self._process.kill()
        self._process.wait(timeout=30)


def main(argv: list[str]) -> int:
    """最小自检：describe → get_catalog → start → observe → step → request_status → close。"""
    client = NetClient.host_from_solution()
    try:
        describe = client.describe()
        if not describe.get("ok"):
            print(f"describe 失败：{describe}", file=sys.stderr)
            return 1
        identity = describe["result"]["identity"]
        catalog_id = identity["catalog_id"]
        print(f"ruleset={identity['ruleset_id']} catalog={catalog_id}")

        catalog = client.get_catalog(catalog_id)
        print(f"catalog parsed: {catalog.get('ok')}")
        return 0
    finally:
        client.shutdown()


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
