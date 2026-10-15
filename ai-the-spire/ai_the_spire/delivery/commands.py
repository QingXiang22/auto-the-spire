"""子命令分发（延迟导入：`--help` 与参数错误不需要 NumPy / 已构建 Host）。"""

from __future__ import annotations


def dispatch(args) -> int:
    command = args.command
    if command == "demo":
        from . import demo  # noqa: PLC0415

        return demo.run(args)
    if command == "train":
        from . import train  # noqa: PLC0415

        return train.run(args)
    if command == "evaluate":
        from . import evaluate  # noqa: PLC0415

        return evaluate.run(args)
    if command == "resume":
        from . import train  # noqa: PLC0415

        return train.run_resume(args)
    if command == "diagnose":
        from . import diagnose  # noqa: PLC0415

        return diagnose.run(args)
    raise ValueError(f"未知子命令 {command!r}（不得静默成功）")
