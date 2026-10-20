"""AI 侧契约错误面。

刻意**不**发明新的错误码：校验结论以共享参考实现 ``wire_layers.Finding`` 的
``rule_id``（``I-xx``，见 ``docs/contracts/net/0.2/invariants.md``）为准，原样携带
层（L0–L4）、输入路径与消息。AI 侧面向编码的阶段错误码
（encoder-design §5.2：``unknown_type``/``value_type_mismatch``/…）属于 A-03 的
校验实现，本批不映射、不复制，避免出现投影错误码与共享规则不一致的第二套词汇。
"""

from __future__ import annotations

from .shared import spec_assets

#: 复用共享 Finding 类型（不重新定义）。
Finding = spec_assets().wire_layers.Finding

#: 把连续多条 finding 的渲染上限，避免错误消息失控。
_MAX_RENDERED = 5


class ContractViolation(ValueError):
    """输入不满足共享规范；不返回可供评分的部分结果。"""

    def __init__(self, role: str, findings) -> None:
        self.role = role
        self.findings = tuple(findings)
        detail = "; ".join(finding.render() for finding in self.findings[:_MAX_RENDERED])
        more = "" if len(self.findings) <= _MAX_RENDERED else f"（另有 {len(self.findings) - _MAX_RENDERED} 条）"
        super().__init__(f"{role}: {len(self.findings)} 条契约违规: {detail}{more}")

    @property
    def rule_ids(self) -> tuple[str, ...]:
        return tuple(sorted({finding.rule_id for finding in self.findings}))

    def render(self) -> list[str]:
        return [finding.render() for finding in self.findings]
