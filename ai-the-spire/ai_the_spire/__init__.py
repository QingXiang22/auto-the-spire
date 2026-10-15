"""ai-the-spire 首批结构化编码契约（A-01）。

本包只包含 net v0.1 公开协议的**纯协议类型**与校验外壳；不含游戏规则、
不含历史/缓存、不含可学习参数、不导入 `core-the-spire`、`mod-the-spire`
或任何活动 client。

包布局（encoder-design §8）：

    ai_the_spire/contracts/   纯协议类型 + 复用共享规范资产的校验入口

首批不安装训练框架。
"""

__all__ = ["contracts"]
