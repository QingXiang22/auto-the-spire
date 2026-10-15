"""D1 命令的运行时支撑：仓库/Host 定位、准备检查、有界客户端、资源限额与机器报告。

边界（任务包 §2/§4/§6）：

* **一个地方**适配 `net-the-spire/client/net_client.py` 的导入位置（不复制客户端，不把测试 helper 引入生产）；
* 输出路径相对**调用者工作目录**解析并记录绝对路径；配置/Host 相对**仓库位置**解析；
* 缺依赖或 Host 程序集时在**启动游戏之前**给准确准备步骤并非零退出（不静默安装、不后台构建）；
* 资源计数与报告全部来自**实际运行**；没测到的指标标记未测，不冒充进程组 RSS。
"""

from __future__ import annotations

import datetime
import json
import pathlib
import platform
import subprocess
import sys
import time
from dataclasses import dataclass, field
from typing import Any, Mapping, Optional

from .errors import FaultError, UsageError

#: 仓库根：`<repo>/ai-the-spire/ai_the_spire/delivery/runtime.py`。
REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]

#: 已构建宿主程序集（`dotnet build AutoTheSpire.sln` 的产物）。
HOST_DLL = (REPO_ROOT / "net-the-spire" / "Net.TheSpire.Host" / "bin" / "Debug" / "net9.0"
            / "Net.TheSpire.Host.dll")

#: 最小依赖声明（不新增训练框架；只 numpy）。
REQUIREMENTS_FILE = REPO_ROOT / "ai-the-spire" / "requirements.txt"

_PREPARATION_STEPS = (
    "准备步骤（在仓库根目录执行，任一步失败就不要继续）：\n"
    "  1) dotnet build AutoTheSpire.sln\n"
    "  2) python -m pip install -r ai-the-spire/requirements.txt\n"
    f"  3) 确认宿主程序集存在：{HOST_DLL}"
)


# --------------------------------------------------------------------------- #
# 准备检查
# --------------------------------------------------------------------------- #
def ensure_numpy() -> Any:
    """NumPy 是当前已有的学习依赖（记录实际版本）；缺失时给准备步骤并退出 2。"""
    try:
        import numpy  # noqa: PLC0415 - 延迟导入，`--help` 不需要 NumPy
    except ImportError as failure:  # pragma: no cover - 环境相关
        raise UsageError(
            f"缺少 Python 依赖 numpy（{failure}）。\n{_PREPARATION_STEPS}"
        ) from failure
    return numpy


def ensure_host() -> pathlib.Path:
    """宿主程序集必须已构建：不在命令里调用 `dotnet build`（每局构建是明确禁止的）。"""
    if not HOST_DLL.is_file():
        raise UsageError(f"没有已构建的宿主程序集：{HOST_DLL}\n{_PREPARATION_STEPS}")
    return HOST_DLL


def load_net_client():
    """唯一的客户端适配点：把 `net-the-spire/client` 挂到 `sys.path` 再导入。"""
    client_dir = REPO_ROOT / "net-the-spire" / "client"
    if not (client_dir / "net_client.py").is_file():
        raise UsageError(f"找不到客户端 net_client.py：{client_dir}\n{_PREPARATION_STEPS}")
    location = str(client_dir)
    if location not in sys.path:
        sys.path.insert(0, location)
    import net_client  # noqa: PLC0415

    return net_client


# --------------------------------------------------------------------------- #
# 输出目录
# --------------------------------------------------------------------------- #
def resolve_output_dir(raw: str, *, command: str) -> pathlib.Path:
    """相对调用者工作目录解析；已非空即拒绝（不覆盖旧结果、不删除已有产物）。"""
    target = pathlib.Path(raw)
    resolved = (pathlib.Path.cwd() / target).resolve() if not target.is_absolute() else target.resolve()
    if resolved.exists():
        if not resolved.is_dir():
            raise UsageError(f"{command}: 输出路径已存在且不是目录：{resolved}")
        entries = sorted(item.name for item in resolved.iterdir())
        if entries:
            raise UsageError(
                f"{command}: 输出目录已非空，拒绝覆盖旧结果：{resolved}\n"
                f"  已有条目（前 10 个）：{entries[:10]}\n"
                "  请换一个 --output 路径；本命令不删除任何已有产物"
            )
    else:
        resolved.mkdir(parents=True, exist_ok=True)
    return resolved


def directory_size_bytes(root: pathlib.Path) -> int:
    total = 0
    for path in root.rglob("*"):
        if path.is_file():
            total += path.stat().st_size
    return total


def check_storage(root: pathlib.Path, limit_bytes: int, *, where: str) -> int:
    """运行存储限额：超限即非零退出（保留已有完整产物，不删旧文件）。"""
    size = directory_size_bytes(root)
    if size > limit_bytes:
        raise FaultError(
            f"{where}: 输出 run 累计 {size} 字节超过存储限额 {limit_bytes} 字节；"
            "已保存完整产物并停止，不删除旧产物"
        )
    return size


# --------------------------------------------------------------------------- #
# 命令时钟与统计
# --------------------------------------------------------------------------- #
@dataclass
class CommandBudget:
    """命令总限额（墙钟）。到点后调用方必须停止采样/更新并非零退出。"""

    limit_s: float
    command: str
    started: float = field(default_factory=time.perf_counter)

    @property
    def elapsed_s(self) -> float:
        return time.perf_counter() - self.started

    @property
    def remaining_s(self) -> float:
        return max(self.limit_s - self.elapsed_s, 0.0)

    def check(self, *, where: str) -> None:
        if self.elapsed_s >= self.limit_s:
            raise FaultError(
                f"{self.command}: 达到命令墙钟限额 {self.limit_s:.1f}s（{where}）；"
                "已保存完整产物并停止，不再开启新局掩盖超限"
            )


# --------------------------------------------------------------------------- #
# 有界客户端
# --------------------------------------------------------------------------- #
@dataclass
class BoundedClient:
    """给 `NetClient` 的每个请求加**显式 10s 截止**，并把"结果不确定"归到中断类。

    超时**不是**"没执行"：客户端会按原 `request_id` 查询 `request_status`，查不到即抛
    `OutcomeUnknown`——这一整类异常必须被 runner 当成"未确认"，不得补造后状态。
    """

    client: Any
    request_timeout_s: float
    interruption_errors: tuple

    def _request(self, operation: str, payload: Optional[dict] = None, **kwargs) -> dict:
        return self.client.request(operation, payload, timeout=self.request_timeout_s, **kwargs)

    def describe(self) -> dict:
        return self._request("describe", {})

    def get_catalog(self, catalog_id: str) -> dict:
        return self._request("get_catalog", {"catalog_id": catalog_id})

    def start(self, expected_identity: dict, scenario_profile: str, *, seed: Optional[int] = None) -> dict:
        payload: dict = {"expected_identity": expected_identity, "scenario_profile": scenario_profile}
        if seed is not None:
            payload["seed"] = str(seed)
        return self._request("start", payload)

    def observe(self, session_id: str) -> dict:
        return self._request("observe", {"session_id": session_id})

    def step(self, session_id: str, expected_observation_id: int, candidate_id: str, *,
             request_id: Optional[str] = None) -> dict:
        return self._request("step", {
            "session_id": session_id,
            "expected_observation_id": expected_observation_id,
            "candidate_id": candidate_id,
        }, request_id=request_id)

    def request_status(self, target_request_id: str, session_id: Optional[str] = None) -> dict:
        payload: dict = {"target_request_id": target_request_id}
        if session_id is not None:
            payload["session_id"] = session_id
        return self._request("request_status", payload)

    def close_session(self, session_id: str) -> dict:
        return self._request("close", {"session_id": session_id})

    def stderr_text(self) -> str:
        return self.client.stderr_text()

    def shutdown(self) -> int:
        return self.client.shutdown()

    def kill(self) -> None:
        self.client.kill()


def open_bounded_client(profile) -> BoundedClient:
    """按冻结 Profile 起一个真实宿主连接（不传任何新场景开关，extra_args 为空即默认 Gym）。"""
    net_client = load_net_client()
    ensure_host()
    inner = net_client.NetClient.host_from_solution(extra_args=list(profile.host_extra_args))
    interruption = (OSError, net_client.NetClientError)
    return BoundedClient(client=inner, request_timeout_s=float(profile.budget["request_timeout_s"]),
                         interruption_errors=interruption)


# --------------------------------------------------------------------------- #
# 机器与软件报告
# --------------------------------------------------------------------------- #
def _working_set_bytes() -> Optional[int]:
    """本进程工作集（Windows）。**只测本 Python 进程**，不冒充它启动的 Host 子进程组。"""
    if sys.platform != "win32":  # pragma: no cover - 本机是 Windows
        return None
    try:  # pragma: no cover - 平台相关
        import ctypes
        from ctypes import wintypes

        class _ProcessMemoryCounters(ctypes.Structure):
            _fields_ = [
                ("cb", wintypes.DWORD),
                ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t),
                ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t),
            ]

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.GetCurrentProcess.restype = wintypes.HANDLE
        handle = kernel32.GetCurrentProcess()
        query = getattr(kernel32, "K32GetProcessMemoryInfo", None)
        if query is None:  # pragma: no cover - 老系统上在 psapi.dll
            query = ctypes.WinDLL("psapi", use_last_error=True).GetProcessMemoryInfo
        query.argtypes = [wintypes.HANDLE, ctypes.POINTER(_ProcessMemoryCounters), wintypes.DWORD]
        query.restype = wintypes.BOOL
        counters = _ProcessMemoryCounters()
        counters.cb = ctypes.sizeof(counters)
        if not query(handle, ctypes.byref(counters), counters.cb):
            return None
        return int(counters.WorkingSetSize)
    except Exception:  # noqa: BLE001 - 测不到就标未测，不引入新平台
        return None


def machine_report(*, peak_python_bytes: Optional[int] = None) -> dict:
    """报告实际测到的机器/软件事实；**未测到的指标显式标记未测**。"""
    numpy_version = "not_imported"
    try:
        import numpy  # noqa: PLC0415

        numpy_version = numpy.__version__
    except ImportError:  # pragma: no cover
        pass
    dotnet = "unavailable"
    try:
        completed = subprocess.run(["dotnet", "--version"], capture_output=True, text=True,
                                   check=False, timeout=30)
        if completed.returncode == 0:
            dotnet = completed.stdout.strip()
    except (OSError, subprocess.SubprocessError):  # pragma: no cover
        pass
    working_set = _working_set_bytes()
    return {
        "python_version": sys.version.split()[0],
        "python_executable": sys.executable,
        "numpy_version": numpy_version,
        "dotnet_sdk_version": dotnet,
        "platform": platform.platform(),
        "processor_count": __import__("os").cpu_count(),
        "python_process_working_set_bytes": working_set,
        "python_process_working_set_scope": (
            "本 Python 进程的工作集（含 NumPy 原生缓冲）；**不含**它启动的 Host 子进程"
            if working_set is not None else "未测"
        ),
        "peak_python_allocation_bytes": peak_python_bytes,
        "peak_python_allocation_scope": (
            "tracemalloc 的 Python 分配峰值：**不含** Host/NumPy 原生缓冲"
            if peak_python_bytes is not None else "未测"
        ),
        "host_process_group_rss_bytes": None,
        "host_process_group_rss_note": (
            "未测：D1 不引入新的监控平台，也不把 Python 进程量冒充整个 Host/NumPy 进程组 RSS"
        ),
        "reported_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    }


def weight_bytes(model) -> int:
    """模型包的**参数字节**（按 dtype 实际占用，不含容器/元数据开销）。"""
    return int(sum(array.nbytes for _, array in model.scorer.params.items()))



# --------------------------------------------------------------------------- #
# 落盘助手
# --------------------------------------------------------------------------- #
def write_json(path: pathlib.Path, document: Mapping[str, Any]) -> pathlib.Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n",
                    encoding="utf-8")
    return path


def write_markdown(path: pathlib.Path, lines: list[str]) -> pathlib.Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines).rstrip("\n") + "\n", encoding="utf-8")
    return path


def console_print(text: str = "") -> None:
    """把可读对局写到调用者终端；Windows 控制台编码差异不应让命令失败。"""
    try:
        print(text)
    except UnicodeEncodeError:  # pragma: no cover - 终端编码过窄时退化为可读替换
        encoding = sys.stdout.encoding or "utf-8"
        sys.stdout.write(text.encode(encoding, errors="replace").decode(encoding) + "\n")
