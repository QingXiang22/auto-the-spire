#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ai-the-spire 检查套件的最小可运行入口（A-01 fixtures + A-02 Manifest/L0 + A-03 校验层）。

用法（工作目录不限，路径按脚本自身定位）::

    python ai-the-spire/tests/run_checks.py            # 全部检查
    python ai-the-spire/tests/run_checks.py --only negative
    python ai-the-spire/tests/run_checks.py --only manifest
    python ai-the-spire/tests/run_checks.py --only validate
    python ai-the-spire/tests/run_checks.py --list

语义：

* 逐组打印 ``通过/失败`` 计数与用途；
* 每个失败项打印 ``suite/case_id`` 与期望-实际差异；
* 最后一行固定为 ``TOTAL: x passed, y failed, z executed``；
* 有失败（或套件本身抛异常）时以非零退出码结束：
  ``0`` 全通过、``1`` 有用例失败、``2`` 套件在跑完前崩溃（TOTAL 行仍打印，计数不完全）。

本入口只做编排与呈现：检查逻辑、期望与夹具分别在 ``fixture_suite.py``（A-01）、
``encoding_suite.py``（A-02）与 ``validate_suite.py``（A-03）里，不在这里重新实现规则，
也不放宽任何共享规则。
"""

from __future__ import annotations

import argparse
import sys
import traceback
from collections import Counter, OrderedDict
from pathlib import Path

#: 让 ``python tests/run_checks.py`` 与 ``python -m`` 都能导入 ai_the_spire 与检查模块。
_PACKAGE_ROOT = Path(__file__).resolve().parents[1]
if str(_PACKAGE_ROOT) not in sys.path:
    sys.path.insert(0, str(_PACKAGE_ROOT))

from ai_the_spire.contracts import ContractViolation, SpecAssetError  # noqa: E402

sys.path.insert(0, str(Path(__file__).resolve().parent))
import encoding_suite  # noqa: E402
import evaluation_suite  # noqa: E402
import fixture_suite  # noqa: E402
import acceptance_suite  # noqa: E402
import model_learning_suite  # noqa: E402
import model_suite  # noqa: E402
import reward_suite  # noqa: E402
import scenario_suite  # noqa: E402
import tensor_suite  # noqa: E402
import validate_suite  # noqa: E402

#: 组名 → 一句话职责（打印顺序即协议分层与批次顺序）。
SUITE_ORDER: "OrderedDict[str, str]" = OrderedDict(
    [
        ("shared-layers", "共享 L0–L3 分层检查：四类输入 + 覆盖表零违规"),
        ("expectations", "手写期望 vs contracts 视图事实，逐项比对"),
        ("roundtrip", "视图 ↔ 合法 wire 逐键往返（不丢键、不虚构键）"),
        ("negative", "负例：声明式改动必须命中独立写出的共享规则 ID"),
        ("manifest", "A-02：字典索引/变换/exact identity/旧文件与版本混用"),
        ("l0-text", "A-02：L0 严格文本解析（坏字节向量拒绝 + 合法文件接受）"),
        (
            "validate",
            "A-03：字段/内容状态、适用域、引用、牌堆 V-06 四条不变量、候选与容量上限（错误码+路径）",
        ),
        (
            "tensorize",
            "A-04：确定性数组、类型化通道、引用映射、局部行索引、掩码与派生行",
        ),
        (
            "collate",
            "A-04：B 轴拼批、逐样本补齐与掩码、跨样本不误连、混批拒绝",
        ),
        (
            "acceptance",
            "A-05：encoder-design §9 的 13 条验收场景（样例侧）",
        ),
        (
            "model-shape",
            "W-04：显式维度/参数分项/结构身份（不训练、不声称学习效果）",
        ),
        (
            "model-mask",
            "W-04：仅有效候选获得概率、掩码候选恒 0、掩码实体不参与聚合",
        ),
        (
            "model-invariance",
            "W-04：候选重排/句柄换名/行序变化不改变同一语义键的概率与 V",
        ),
        (
            "model-policy",
            "W-04：终止无候选不伪造概率、与 runner 的概率覆盖一致、身份随结果记录",
        ),
        (
            "model-bundle",
            "W-04：模型包往返一致 + 身份/权重/元数据不一致时显式失败",
        ),
        (
            "learning-grad",
            "W-04：有限差分对照自动微分的解析梯度（逐算子 + 整模型选定参数）",
        ),
        (
            "learning-update",
            "W-04：有限合成任务上验证损失下降/正确率上升/参数更新/同种子复现",
        ),
        (
            "learning-restore",
            "W-04：优化器与权重检查点往返；恢复后继续训练与不中断训练一致",
        ),
        (
            "reward-profile",
            "W-04：奖励 Profile 分量/单位/来源/权重覆盖/版本与摘要校验",
        ),
        (
            "reward-derive",
            "W-04：从公开快照序列派生向量与标量（单位保留、只读公开事实、确定性）",
        ),
        (
            "reward-missing",
            "W-04：缺必需公开事实显式失败；optional 分量记 unavailable 且不参与标量化",
        ),
        (
            "eval-plan",
            "W-04：评估计划身份（策略/基线/保留种子/指标单位与方向/自助配置）与摘要",
        ),
        (
            "eval-stats",
            "W-04：描述统计与自助区间（已知答案、可复现、随方差变宽、配对只用共享种子）",
        ),
        (
            "eval-failures",
            "W-04：失败局不混入均值、无可用局不给指标、未训练策略带声明、报告摘要确定",
        ),
        (
            "scenario-manifest",
            "W-03：场景簇必须绑定真实宿主开关与决策差异轴（清单身份/往返/摘要）",
        ),
        (
            "scenario-facts",
            "W-03：区分事实谓词（group 有/无、候选引用内容）在公开快照上的判定",
        ),
        (
            "scenario-splits",
            "W-03：划分按簇隔离、三个 split 非空、退化与跨 split 泄漏可检出",
        ),
        (
            "scenario-cost",
            "W-03：采样成本报告（限额/计数/墙钟/字节/停止原因）与「轮转非吞吐」「随机非近期PPO」声明",
        ),
    ]
)

#: 各模块负责的组（``run_all(only=...)`` 的过滤在模块内完成）。
SUITE_MODULES = (
    fixture_suite,
    encoding_suite,
    validate_suite,
    tensor_suite,
    acceptance_suite,
    model_suite,
    model_learning_suite,
    reward_suite,
    evaluation_suite,
    scenario_suite,
)

MAX_DETAIL = 600


def _print_outcomes(outcomes, only: str | None = None) -> None:
    grouped: "OrderedDict[str, list]" = OrderedDict((name, []) for name in SUITE_ORDER)
    for item in outcomes:
        grouped.setdefault(item.suite, []).append(item)

    for name, title in SUITE_ORDER.items():
        if only is not None and name != only:
            continue  # --only 模式不打印未运行的组，避免把 0 executed 误读成通过
        items = grouped.get(name) or []
        passed = sum(1 for item in items if item.ok)
        failed = len(items) - passed
        print(f"[{'PASS' if failed == 0 else 'FAIL'}] {name}: {passed} passed, {failed} failed, {len(items)} executed")
        print(f"       {title}")
        for item in items:
            if item.ok:
                continue
            detail = " ".join(str(item.detail).split())
            if len(detail) > MAX_DETAIL:
                detail = detail[:MAX_DETAIL] + " …"
            print(f"       - {item.case_id}: {detail or '（无差异说明）'}")

    extras = [name for name in grouped if name not in SUITE_ORDER and grouped[name]]
    for name in extras:  # 套件新增组时的兜底，避免计数被悄悄吞掉
        items = grouped[name]
        passed = sum(1 for item in items if item.ok)
        print(f"[{'PASS' if passed == len(items) else 'FAIL'}] {name}: {passed} passed, {len(items) - passed} failed, {len(items)} executed")
        for item in items:
            if not item.ok:
                print(f"       - {item.case_id}: {' '.join(str(item.detail).split())[:MAX_DETAIL]}")


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):  # 中文差异说明在 cp936 控制台会乱码
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except (ValueError, OSError):  # pragma: no cover - 仅非标准流
            pass

    parser = argparse.ArgumentParser(description="运行 ai-the-spire 检查套件的全部检查")
    parser.add_argument("--only", metavar="SUITE", help="只跑某一组：" + " / ".join(SUITE_ORDER))
    parser.add_argument("--list", action="store_true", help="只列出组名与夹具根目录，不跑检查")
    args = parser.parse_args(argv)

    if args.list:
        for name, title in SUITE_ORDER.items():
            print(f"{name}: {title}")
        print(f"fixtures: {fixture_suite.FIXTURE_ROOT}")
        print(f"manifest fixtures: {encoding_suite.MANIFEST_ROOT}")
        print(f"l0-text fixtures: {encoding_suite.TEXT_ROOT}")
        print(f"validate fixtures: {validate_suite.VALIDATE_ROOT}")
        print(f"tensor fixtures: {tensor_suite.TENSOR_ROOT}")
        return 0

    if args.only and args.only not in SUITE_ORDER:
        print(f"未知组 {args.only!r}；可用: {', '.join(SUITE_ORDER)}", file=sys.stderr)
        return 2

    print(f"ai-the-spire 检查套件  fixtures={fixture_suite.FIXTURE_ROOT}")
    try:
        outcomes = []
        for module in SUITE_MODULES:
            outcomes.extend(module.run_all(only=args.only))
    except (ContractViolation, SpecAssetError) as exc:
        print(f"[ERROR] 套件在完成前失败：{type(exc).__name__}: {exc}", file=sys.stderr)
        traceback.print_exc()
        print("TOTAL: 0 passed, 1 failed, 1 executed (套件提前中止，计数不完整)")
        return 2
    except Exception as exc:  # noqa: BLE001 - 入口必须给出退出码与 TOTAL 行
        print(f"[ERROR] 套件抛出未预期异常：{type(exc).__name__}: {exc}", file=sys.stderr)
        traceback.print_exc()
        print("TOTAL: 0 passed, 1 failed, 1 executed (套件提前中止，计数不完整)")
        return 2

    _print_outcomes(outcomes, only=args.only)

    counts = Counter(item.ok for item in outcomes)
    passed = counts.get(True, 0)
    failed = counts.get(False, 0)
    print(f"TOTAL: {passed} passed, {failed} failed, {len(outcomes)} executed")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
