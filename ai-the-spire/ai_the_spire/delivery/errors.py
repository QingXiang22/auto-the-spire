"""D1 交付入口的错误分类与退出码（与 `docs/design/battle-learning-delivery-v1.md` §2 一致）。

四类退出码是**契约**：调用方（用户脚本、验收入口）按码判断"承诺是否完成"。

===================  ====================================================================
退出码               含义
===================  ====================================================================
0                    该命令完成了它承诺的事（含正常的 `--stop-after-round`、未改善、有效决策上限截断）
2                    用法 / 准备 / 配置 / identity 或模型错配
1                    运行故障、未知动作结果、非法概率或数据、数值失败、资源超限
130                  用户中断
===================  ====================================================================

"技术故障必须停止当前命令，保留已确认记录，不自动随机出牌或开启新局掩盖失败"——因此
:class:`FaultError` 只用于**真正的故障/未知/超限**，绝不用于"训练之后没有变好"这类正常结果。
"""

from __future__ import annotations

EXIT_OK = 0
EXIT_FAULT = 1
EXIT_USAGE = 2
EXIT_INTERRUPTED = 130


class DeliveryError(RuntimeError):
    """D1 入口错误的基类：带显式退出码与"退出类别"。"""

    exit_code = EXIT_FAULT
    category = "fault"

    def __init__(self, message: str, *, reason: str = "") -> None:
        super().__init__(message)
        self.reason = reason or message


class UsageError(DeliveryError):
    """用法/准备/配置/identity 或模型错配：**在启动游戏之前**报出并给准备步骤。"""

    exit_code = EXIT_USAGE
    category = "usage"


class FaultError(DeliveryError):
    """运行故障、未知动作结果、非法数据、数值失败或资源超限。"""

    exit_code = EXIT_FAULT
    category = "fault"


class UserInterrupt(DeliveryError):
    """用户中断（Ctrl-C / KeyboardInterrupt）：保留已确认记录，不补造结果。"""

    exit_code = EXIT_INTERRUPTED
    category = "interrupted"
