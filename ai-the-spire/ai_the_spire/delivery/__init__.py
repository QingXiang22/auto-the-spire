"""D1 交付入口（`python ai-the-spire/run.py`）的实现包。

分工与边界（任务包 §6）：

* `run.py` 只做参数解析与退出码；真正的编排在这里；
* 本包**复用**现有 runner / encoding / models / reward / evaluation / data 设施，只补真正的衔接缺口
  （模型身份刷新、逐动作奖励、每局限额与截断后的确认观察、可读渲染、冻结 Profile 解析）；
* 不新增卡牌/Power/敌人/药水，不扩地图/整局，不迁完整正式源，不切换默认后端，不重选模型，
  不加 GUI/网页/服务/分布式/训练框架。
"""

from __future__ import annotations

from .config import FROZEN_PROFILES, Profile, load_profile
from .errors import (
    EXIT_FAULT,
    EXIT_INTERRUPTED,
    EXIT_OK,
    EXIT_USAGE,
    DeliveryError,
    FaultError,
    UsageError,
    UserInterrupt,
)

__all__ = [
    "EXIT_FAULT",
    "EXIT_INTERRUPTED",
    "EXIT_OK",
    "EXIT_USAGE",
    "FROZEN_PROFILES",
    "DeliveryError",
    "FaultError",
    "Profile",
    "UsageError",
    "UserInterrupt",
    "load_profile",
]
