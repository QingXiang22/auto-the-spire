#!/usr/bin/env python3
"""D1 可运行战斗学习版本入口（仓库根目录执行）。

```powershell
dotnet build AutoTheSpire.sln

python ai-the-spire/run.py demo     --config ai-the-spire/configs/battle-demo-v1.json --output artifacts/d1/demo-initial
python ai-the-spire/run.py train    --config ai-the-spire/configs/battle-demo-v1.json --output artifacts/d1/train
python ai-the-spire/run.py evaluate --run artifacts/d1/train --split final --output artifacts/d1/evaluate
python ai-the-spire/run.py demo     --config ai-the-spire/configs/battle-demo-v1.json --model artifacts/d1/train/candidate-model --output artifacts/d1/demo-candidate
python ai-the-spire/run.py train    --config ai-the-spire/configs/battle-demo-smoke-v1.json --stop-after-round 1 --output artifacts/d1/smoke-partial
python ai-the-spire/run.py resume   --run artifacts/d1/smoke-partial --output artifacts/d1/smoke-resumed
```

* 输出路径相对**调用者工作目录**解析；配置与 Host 相对**仓库位置**解析；
* `--help` 不需要 NumPy、也不需要已构建的 Host；
* 退出码：0 完成承诺（含正常 stop-after-round / 未改善 / 有效决策上限截断）、
  2 用法/准备/配置/identity 或模型错配、1 运行故障或资源超限、130 用户中断。

本文件顶层**只用标准库**：重型依赖在真正执行命令时才导入。
"""

from __future__ import annotations

import argparse
import pathlib
import sys
import traceback

#: 仓库根：`<repo>/ai-the-spire/run.py`。
_REPO_ROOT = pathlib.Path(__file__).resolve().parents[1]
_PACKAGE_ROOT = pathlib.Path(__file__).resolve().parent


def _ensure_package_importable() -> None:
    location = str(_PACKAGE_ROOT)
    if location not in sys.path:
        sys.path.insert(0, location)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="python ai-the-spire/run.py",
        description=(
            "D1 可运行战斗学习版本：demo（看一局）/ train（有限真实训练）/ evaluate（三策略对照）/ "
            "resume（从已提交轮次恢复训练）；D2 诊断：diagnose（只读诊断已有 run）。"
            "默认使用当前默认战斗 Host，不加任何新场景开关。"
        ),
        epilog=(
            "准备步骤：1) dotnet build AutoTheSpire.sln；"
            "2) python -m pip install -r ai-the-spire/requirements.txt；"
            "3) 确认 net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll 存在。"
        ),
    )
    subparsers = parser.add_subparsers(dest="command",
                                       metavar="{demo,train,evaluate,resume,diagnose}")

    demo = subparsers.add_parser("demo", help="用随机初始化或已载入的模型真实看一局，并落盘可读对局")
    demo.add_argument("--config", required=True,
                      help="冻结 Profile 配置文件（如 ai-the-spire/configs/battle-demo-v1.json）")
    demo.add_argument("--output", required=True, help="输出目录（相对调用者工作目录；非空即拒绝）")
    demo.add_argument("--model", default=None,
                      help="可选：模型包目录（如 artifacts/d1/train/candidate-model）；省略则随机初始化")

    train = subparsers.add_parser("train", help="按冻结 Profile 做有限真实采样与 PPO 更新，并逐轮存检查点")
    train.add_argument("--config", required=True, help="冻结 Profile 配置文件")
    train.add_argument("--output", required=True, help="输出 run 目录（非空即拒绝）")
    train.add_argument("--stop-after-round", type=int, default=None, metavar="N",
                       help="正常跑完第 N 轮后停止（用于 smoke 分段与 resume 验收；不是故障）")

    evaluate = subparsers.add_parser("evaluate", help="在保留种子上比较均匀基线 / 初始模型 / 训练候选")
    evaluate.add_argument("--run", required=True, help="train 产出的 run 目录（读其 manifest 与模型包）")
    evaluate.add_argument("--split", default="final", choices=("final", "validation"),
                          help="使用哪个保留种子划分（默认 final）")
    evaluate.add_argument("--output", required=True, help="输出目录（非空即拒绝）")

    resume = subparsers.add_parser("resume", help="从 run 的最后一个已提交轮次继续训练到总目标轮数")
    resume.add_argument("--run", required=True, help="原始 run 目录（只读，不被覆盖）")
    resume.add_argument("--output", required=True, help="新的输出目录（非空即拒绝）")

    diagnose = subparsers.add_parser(
        "diagnose",
        help="只读诊断已有训练 run：四策略同条件对照 + 同局面概率探针 + 学习信号复核 + 一个实验建议",
    )
    diagnose.add_argument("--run", required=True, help="已有 D1 train 的 run 目录（只读，不被修改）")
    diagnose.add_argument("--output", required=True, help="诊断输出目录（非空即拒绝）")

    return parser


def _summarize(args: argparse.Namespace, *, exit_code: int, category: str, reason: str) -> None:
    command = getattr(args, "command", None) or "<none>"
    print("")
    print(f"SUMMARY command={command} exit={exit_code} category={category} reason={reason}")


def main(argv: list[str]) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.command is None:
        parser.print_help()
        return 2

    _ensure_package_importable()
    try:
        from ai_the_spire.delivery import commands
        from ai_the_spire.delivery.errors import DeliveryError
    except ImportError as failure:  # pragma: no cover - 缺包时给准备步骤
        print(f"无法导入交付实现：{failure}", file=sys.stderr)
        print("准备步骤：在仓库根目录执行 python -m pip install -r ai-the-spire/requirements.txt",
              file=sys.stderr)
        _summarize(args, exit_code=2, category="usage", reason=f"导入交付实现失败：{failure}")
        return 2

    try:
        return commands.dispatch(args)
    except KeyboardInterrupt:
        _summarize(args, exit_code=130, category="interrupted", reason="用户中断：已保存已确认记录，不补造结果")
        return 130
    except DeliveryError as failure:
        print(str(failure), file=sys.stderr)
        _summarize(args, exit_code=failure.exit_code, category=failure.category, reason=failure.reason)
        return failure.exit_code
    except ImportError as failure:
        # 缺 NumPy 之类的依赖属于"准备"问题：在启动游戏前给准备步骤并非零退出（不静默安装）。
        print(f"缺少 Python 依赖或导入失败：{type(failure).__name__}: {failure}", file=sys.stderr)
        print("准备步骤：", file=sys.stderr)
        print("  1) dotnet build AutoTheSpire.sln", file=sys.stderr)
        print("  2) python -m pip install -r ai-the-spire/requirements.txt", file=sys.stderr)
        _summarize(args, exit_code=2, category="usage", reason=f"缺少 Python 依赖：{failure}")
        return 2
    except Exception as failure:  # noqa: BLE001 - 未知技术故障必须非零退出且不掩盖
        traceback.print_exc()
        _summarize(args, exit_code=1, category="fault",
                   reason=f"未预期的技术故障（{type(failure).__name__}）：{failure}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
