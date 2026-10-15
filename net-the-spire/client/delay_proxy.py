"""故障注入：在宿主 stdout 与客户端之间插入固定延迟的管道（H-03）。

用法::

    python delay_proxy.py --delay 2.5 -- dotnet run --project <host.csproj> --

stdin/stdout 是协议通道（原样转发，只对宿主 stdout 加延迟），stderr 直接继承给诊断通道。
注入的是**传输延迟**：宿主的进程、规则结算和应答内容都不改变，因此这条路径验证的是客户端
「超时后才到达的响应」处理，而不是某种后端行为。
"""

from __future__ import annotations

import subprocess
import sys
import threading
import time


def main(argv: list[str]) -> int:
    delay = 0.0
    if len(argv) >= 2 and argv[0] == "--delay":
        try:
            delay = float(argv[1])
        except ValueError:
            print(f"delay_proxy: --delay 需要秒数，得到 {argv[1]!r}", file=sys.stderr)
            return 2
        argv = argv[2:]
    if argv and argv[0] == "--":
        argv = argv[1:]
    if not argv:
        print("delay_proxy: 缺少要运行的命令", file=sys.stderr)
        return 2

    child = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=sys.stderr)

    def pump_in() -> None:
        assert child.stdin is not None
        try:
            for line in iter(sys.stdin.buffer.readline, b""):
                child.stdin.write(line)
                child.stdin.flush()
        finally:
            try:
                child.stdin.close()
            except OSError:
                pass

    def pump_out() -> None:
        assert child.stdout is not None
        for line in iter(child.stdout.readline, b""):
            if delay:
                time.sleep(delay)
            sys.stdout.buffer.write(line)
            sys.stdout.buffer.flush()

    threads = [
        threading.Thread(target=pump_in, daemon=True),
        threading.Thread(target=pump_out, daemon=True),
    ]
    for thread in threads:
        thread.start()
    code = child.wait()
    for thread in threads:
        thread.join(timeout=2.0)
    return code


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
