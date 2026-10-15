"""微型反向模式自动微分（numpy 实现，不引入训练框架）。

**为什么写这一层**（用户 2026-10-03 裁定：只用 numpy）：训练需要梯度，而"手写每个算子的解析梯度"
既容易出错又会逼出**第二套前向实现**（本项目的既有规矩是"不搞第二套实现"）。
因此这里提供一个极小的 tape：前向表达式只写一遍（`models/encoder.py`、`models/scorer.py` 里的
`ops.*` 调用），推理时 `ops` 直接落到 numpy（无 tape、无额外开销），训练时同一份代码记录反向闭包。

覆盖范围就是模型真正用到的算子：矩阵乘、广播加/乘、tanh、逐行 LayerNorm、按行号取/散射加、
按掩码取/行均值/行最大值、拼接、标量缩放、掩码 softmax 与交叉熵、向量点积、求和。
**没有**实现的东西不假装支持：未知类型直接抛 `AutodiffError`。

数值验收：`tests/model_learning_suite.py` 用**有限差分**对照每个算子的解析梯度与整模型的梯度
（容差显式；不做"看起来对"的判断）。
"""

from __future__ import annotations

from typing import Any, Callable, Iterable, Mapping, Optional, Sequence

import numpy as np


class AutodiffError(RuntimeError):
    """自动微分层的显式失败：参数形状不符、算子不支持、梯度缺失。"""


# --------------------------------------------------------------------------- #
# Var：一个带反向闭包的值
# --------------------------------------------------------------------------- #
class Var:
    """一个可微值：``data`` 是 numpy 数组，``grad`` 与 ``data`` 同形状（惰性分配）。"""

    __slots__ = ("data", "grad", "requires_grad", "_backward", "_parents", "_name")

    def __init__(self, data: np.ndarray, *, requires_grad: bool = True,
                 parents: Sequence["Var"] = (), name: str = "") -> None:
        array = np.asarray(data)
        if array.dtype.kind not in "fiu":
            raise AutodiffError(f"Var 只接受数值数组，得到 dtype {array.dtype}")
        self.data = array
        self.grad: Optional[np.ndarray] = None
        self.requires_grad = bool(requires_grad)
        self._backward: Optional[Callable[[np.ndarray], None]] = None
        self._parents: tuple[Var, ...] = tuple(parents)
        self._name = name

    # -- 形状/名字（报错时说清是谁） -------------------------------------- #
    @property
    def shape(self) -> tuple:
        return tuple(int(dim) for dim in self.data.shape)

    @property
    def name(self) -> str:
        return self._name

    def __repr__(self) -> str:  # pragma: no cover - 调试用
        return f"Var(name={self._name!r}, shape={self.shape})"

    # -- 梯度 ------------------------------------------------------------ #
    def zero_grad(self) -> None:
        self.grad = None

    def accumulate(self, grad: np.ndarray) -> None:
        contribution = np.asarray(grad)
        if contribution.shape != self.data.shape:
            # 广播算子必须先把梯度归约回本值形状（由算子负责）；到这里还不对就是实现缺陷。
            raise AutodiffError(
                f"梯度形状 {contribution.shape} 与 {self.shape} 不符"
                f"{f'（{self._name}）' if self._name else ''}"
            )
        self.grad = contribution if self.grad is None else self.grad + contribution

    def detach(self) -> "Var":
        """断开反向（值保持不变）；用于"冻结"某段计算。"""
        return Var(self.data, requires_grad=False, name=self._name)


def is_var(value: Any) -> bool:
    return isinstance(value, Var)


def value_of(value: Any) -> np.ndarray:
    return value.data if isinstance(value, Var) else np.asarray(value)


def is_differentiable(*values: Any) -> bool:
    return any(isinstance(item, Var) and item.requires_grad for item in values)


def make(name: str, data: np.ndarray, *, requires_grad: bool = True) -> Var:
    return Var(data, requires_grad=requires_grad, name=name)


def backward(loss: Var, *, retain: bool = True) -> None:
    """反向传播：按逆拓扑序把 ``loss`` 的梯度累加到各 ``Var.grad``（**不清零**，便于多样本累加）。

    多次调用之间梯度会累加——这正是"一个 batch 内多份样本累加后再平均"的用法；
    每轮更新前由调用方显式清零。
    """
    if not isinstance(loss, Var):
        raise AutodiffError("backward 的起点必须是 Var")
    if loss.data.size != 1:
        raise AutodiffError(f"反向传播的起点必须是标量，得到 shape {loss.shape}")
    order: list[Var] = []
    seen: set[int] = set()
    stack: list[tuple[Var, bool]] = [(loss, False)]
    while stack:
        node, expanded = stack.pop()
        if expanded:
            order.append(node)
            continue
        if id(node) in seen:
            continue
        seen.add(id(node))
        stack.append((node, True))
        for parent in node._parents:
            if parent.requires_grad:
                stack.append((parent, False))
    loss.accumulate(np.ones(loss.data.shape, dtype=loss.data.dtype))
    for node in reversed(order):  # 后序的最后一个是 loss 本身：逆序即从 loss 往叶子走
        if node.grad is None or not node.requires_grad or node._backward is None:
            continue
        grad = node.grad
        node._backward(grad)
    if not retain:  # pragma: no cover - 当前调用方都保留
        loss.zero_grad()


# --------------------------------------------------------------------------- #
# 算子（数组或 Var 都可；任一为 Var 就建图）
# --------------------------------------------------------------------------- #
def _unbroadcast(grad: np.ndarray, shape: tuple) -> np.ndarray:
    """把广播后的梯度归约回原形状（对广播出去的前导轴求和）。"""
    if grad.shape == shape:
        return grad
    while grad.ndim > len(shape):
        grad = grad.sum(axis=0)
    for axis, dim in enumerate(shape):
        if dim == 1 and grad.shape[axis] != 1:
            grad = grad.sum(axis=axis, keepdims=True)
    if grad.shape != shape:  # pragma: no cover - 形状不兼容时显式失败
        raise AutodiffError(f"无法把梯度 {grad.shape} 归约回 {shape}")
    return grad


def _binary(a: Any, b: Any, forward: Callable[[np.ndarray, np.ndarray], np.ndarray],
            grad_a: Callable[[np.ndarray, np.ndarray, np.ndarray], np.ndarray],
            grad_b: Callable[[np.ndarray, np.ndarray, np.ndarray], np.ndarray],
            *, name: str) -> Any:
    if not is_differentiable(a, b):
        return forward(value_of(a), value_of(b))
    left, right = value_of(a), value_of(b)
    out = Var(forward(left, right),
              parents=tuple(item for item in (a, b) if isinstance(item, Var) and item.requires_grad),
              name=name)
    out._backward = lambda grad: (
        a.accumulate(_unbroadcast(grad_a(grad, left, right), left.shape))
        if isinstance(a, Var) and a.requires_grad else None,
        b.accumulate(_unbroadcast(grad_b(grad, left, right), right.shape))
        if isinstance(b, Var) and b.requires_grad else None,
    )
    return out


def add(a: Any, b: Any) -> Any:
    return _binary(a, b, np.add, lambda g, x, y: g, lambda g, x, y: g, name="add")


def sub(a: Any, b: Any) -> Any:
    return _binary(a, b, np.subtract, lambda g, x, y: g, lambda g, x, y: -g, name="sub")


def mul(a: Any, b: Any) -> Any:
    return _binary(a, b, np.multiply, lambda g, x, y: g * y, lambda g, x, y: g * x, name="mul")


def divide(a: Any, b: Any) -> Any:
    return _binary(a, b, np.divide,
                   lambda g, x, y: g / y, lambda g, x, y: -g * x / (y * y), name="divide")


def maximum(a: Any, b: Any) -> Any:
    """逐元素取较大者（梯度只走选中的那支；相等时走 a，与 numpy 的语义一致）。"""
    return _binary(a, b, np.maximum,
                   lambda g, x, y: np.where(x >= y, g, 0.0),
                   lambda g, x, y: np.where(x >= y, 0.0, g), name="maximum")


def minimum(a: Any, b: Any) -> Any:
    """逐元素取较小者（PPO 代理目标的 ``min``；相等时走 a）。"""
    return _binary(a, b, np.minimum,
                   lambda g, x, y: np.where(x <= y, g, 0.0),
                   lambda g, x, y: np.where(x <= y, 0.0, g), name="minimum")


def exp(a: Any) -> Any:
    if not isinstance(a, Var) or not a.requires_grad:
        return np.exp(value_of(a))
    activated = np.exp(a.data)
    out = Var(activated, parents=(a,), name="exp")
    out._backward = lambda grad: a.accumulate(grad * activated)
    return out


def clip_scalar(a: Any, low: float, high: float) -> Any:
    """按标量区间裁剪（边界处取次梯度 0；区间内直通）。"""
    if low > high:
        raise AutodiffError(f"clip 的区间非法：[{low}, {high}]")
    if not isinstance(a, Var) or not a.requires_grad:
        return np.clip(value_of(a), low, high)
    inside = (a.data > low) & (a.data < high)
    out = Var(np.clip(a.data, low, high), parents=(a,), name="clip")
    out._backward = lambda grad: a.accumulate(np.where(inside, grad, 0.0))
    return out


def scale(a: Any, factor: float) -> Any:
    if not isinstance(a, Var) or not a.requires_grad:
        return value_of(a) * factor
    out = Var(a.data * factor, parents=(a,), name="scale")
    out._backward = lambda grad: a.accumulate(grad * factor)
    return out


def tanh(a: Any) -> Any:
    if not isinstance(a, Var) or not a.requires_grad:
        return np.tanh(value_of(a))
    activated = np.tanh(a.data)
    out = Var(activated, parents=(a,), name="tanh")
    out._backward = lambda grad: a.accumulate(grad * (1.0 - activated * activated))
    return out


def matmul(a: Any, b: Any) -> Any:
    """二维矩阵乘（``[n, din] @ [din, dout]``）；两侧都按标准公式反传。"""
    if not is_differentiable(a, b):
        return value_of(a) @ value_of(b)
    left, right = value_of(a), value_of(b)
    if left.ndim != 2 or right.ndim != 2:
        raise AutodiffError(f"matmul 只接受二维操作数，得到 {left.shape} 与 {right.shape}")
    out = Var(left @ right, parents=tuple(item for item in (a, b)
                                          if isinstance(item, Var) and item.requires_grad),
              name="matmul")
    out._backward = lambda grad: (
        a.accumulate(grad @ right.T) if isinstance(a, Var) and a.requires_grad else None,
        b.accumulate(left.T @ grad) if isinstance(b, Var) and b.requires_grad else None,
    )
    return out


def dot(a: Any, b: Any) -> Any:
    """一维点积 → 标量标量（``shape ()`` 的 0 维数组）。"""
    if not is_differentiable(a, b):
        return np.asarray(value_of(a) @ value_of(b))
    left, right = value_of(a), value_of(b)
    if left.ndim != 1 or right.ndim != 1:
        raise AutodiffError(f"dot 只接受一维操作数，得到 {left.shape} 与 {right.shape}")
    out = Var(np.asarray(left @ right), parents=tuple(item for item in (a, b)
                                                     if isinstance(item, Var) and item.requires_grad),
              name="dot")
    out._backward = lambda grad: (
        a.accumulate(np.asarray(grad) * right) if isinstance(a, Var) and a.requires_grad else None,
        b.accumulate(np.asarray(grad) * left) if isinstance(b, Var) and b.requires_grad else None,
    )
    return out


def concatenate(parts: Sequence[Any], *, axis: int = 1) -> Any:
    arrays = [value_of(part) for part in parts]
    if not is_differentiable(*parts):
        return np.concatenate(arrays, axis=axis)
    sizes = [array.shape[axis] for array in arrays]
    out = Var(np.concatenate(arrays, axis=axis),
              parents=tuple(part for part in parts if isinstance(part, Var) and part.requires_grad),
              name="concat")
    boundaries = np.cumsum([0] + sizes)

    def run(grad: np.ndarray) -> None:
        for index, part in enumerate(parts):
            if not isinstance(part, Var) or not part.requires_grad:
                continue
            slices = [slice(None)] * grad.ndim
            slices[axis] = slice(int(boundaries[index]), int(boundaries[index + 1]))
            part.accumulate(grad[tuple(slices)])
    out._backward = run
    return out


def gather(table: Any, index: np.ndarray) -> Any:
    """按行号取行（``index`` 是常量行号数组；embedding 查表与引用实体都走它）。"""
    rows = np.asarray(index, dtype=np.int64)
    if not isinstance(table, Var) or not table.requires_grad:
        return value_of(table)[rows]
    source = table.data
    out = Var(source[rows], parents=(table,), name="gather")
    out._backward = lambda grad: table.accumulate(_scatter(rows, grad, source.shape[0]))
    return out


def _scatter(index: np.ndarray, values: np.ndarray, rows: int) -> np.ndarray:
    out = np.zeros((rows,) + values.shape[1:], dtype=values.dtype)
    if values.shape[0] > 0:
        np.add.at(out, index, values)
    return out


def scatter_add(values: Any, index: np.ndarray, rows: int) -> Any:
    """按行号散射相加（``np.add.at``）；反向就是按同一行号取回。"""
    rows = int(rows)
    index = np.asarray(index, dtype=np.int64)
    if not isinstance(values, Var) or not values.requires_grad:
        return _scatter(index, value_of(values), rows)
    out = Var(_scatter(index, values.data, rows), parents=(values,), name="scatter_add")
    out._backward = lambda grad: values.accumulate(grad[index])
    return out


def where(mask: np.ndarray, when_true: Any, when_false: Any) -> Any:
    """``np.where(mask, a, b)``：``mask`` 是**常量**布尔数组（掩码不是可学参数）。"""
    condition = np.asarray(mask, dtype=bool)
    parts = (when_true, when_false)
    if not is_differentiable(*parts):
        return np.where(condition, value_of(when_true), value_of(when_false))
    true_value, false_value = value_of(when_true), value_of(when_false)
    out = Var(np.where(condition, true_value, false_value),
              parents=tuple(part for part in parts if isinstance(part, Var) and part.requires_grad),
              name="where")
    out._backward = lambda grad: (
        when_true.accumulate(_unbroadcast(np.where(condition, grad, 0.0), true_value.shape))
        if isinstance(when_true, Var) and when_true.requires_grad else None,
        when_false.accumulate(_unbroadcast(np.where(condition, 0.0, grad), false_value.shape))
        if isinstance(when_false, Var) and when_false.requires_grad else None,
    )
    return out


def total(a: Any, *, axis: int = 0) -> Any:
    """沿一条轴求和（把 ``[n, d]`` 归约成 ``[d]``）；反向把梯度**广播**回输入形状。"""
    if not isinstance(a, Var) or not a.requires_grad:
        return value_of(a).sum(axis=axis)
    out = Var(a.data.sum(axis=axis), parents=(a,), name="total")

    def run(grad: np.ndarray) -> None:
        expanded = np.expand_dims(np.asarray(grad), axis=axis)
        a.accumulate(np.broadcast_to(expanded, a.data.shape))
    out._backward = run
    return out


def scalar_mean(items: Sequence[Any]) -> Any:
    """若干**标量**项的平均（多样本损失聚合用）。"""
    parts = list(items)
    if not parts:
        raise AutodiffError("scalar_mean 需要至少一项")
    if not is_differentiable(*parts):
        return np.asarray(np.mean([value_of(item) for item in parts]))
    out = Var(np.asarray(np.mean([value_of(item) for item in parts])),
              parents=tuple(item for item in parts if isinstance(item, Var) and item.requires_grad),
              name="scalar_mean")
    share = 1.0 / len(parts)

    def run(grad: np.ndarray) -> None:
        for item in parts:
            if isinstance(item, Var) and item.requires_grad:
                item.accumulate(np.asarray(grad) * share)
    out._backward = run
    return out


def layernorm(a: Any, eps: float = 1e-6) -> Any:
    """逐行 LayerNorm（按行归一化）；反向用标准形式
    ``dx = (gy - mean(gy) - ŷ * mean(gy * ŷ)) / σ``（忽略 eps 的导数，常规做法）。"""
    if not isinstance(a, Var) or not a.requires_grad:
        x = value_of(a)
        mean = x.mean(axis=-1, keepdims=True)
        var = x.var(axis=-1, keepdims=True)
        return (x - mean) / np.sqrt(var + eps)
    x = a.data
    mean = x.mean(axis=-1, keepdims=True)
    var = x.var(axis=-1, keepdims=True)
    std = np.sqrt(var + eps)
    normalized = (x - mean) / std
    out = Var(normalized, parents=(a,), name="layernorm")

    def run(grad: np.ndarray) -> None:
        grad_mean = grad.mean(axis=-1, keepdims=True)
        projection = (grad * normalized).mean(axis=-1, keepdims=True)
        a.accumulate((grad - grad_mean - normalized * projection) / std)
    out._backward = run
    return out


def masked_row_max(a: Any, mask: np.ndarray) -> Any:
    """掩码行最大值：掩码行取 ``-inf`` 后按列取最大；并列时梯度在并列行间平分。"""
    array = value_of(a)
    if array.shape[0] == 0:
        raise AutodiffError("masked_row_max 没有行：调用方必须先判空（不得返回 -inf 冒充结果）")
    condition = np.asarray(mask, dtype=bool).reshape(-1, 1)
    if not condition.any():
        raise AutodiffError("masked_row_max 没有有效行：调用方必须先判空")
    masked = np.where(condition, array, -np.inf)
    best = masked.max(axis=0)
    if not isinstance(a, Var) or not a.requires_grad:
        return best
    winners = (masked == best) & condition
    winner_count = np.maximum(winners.sum(axis=0), 1)
    out = Var(best, parents=(a,), name="masked_row_max")
    out._backward = lambda grad: a.accumulate(np.where(winners, grad / winner_count, 0.0))
    return out


def masked_softmax(logits: Any, mask: np.ndarray) -> Any:
    """只在有效行上归一化；无效行的概率恒为 0（分母不含它们）。"""
    array = value_of(logits)
    condition = np.asarray(mask, dtype=bool)
    if not condition.any():
        raise AutodiffError("没有有效候选：不得产出概率向量")
    shifted = np.where(condition, array - array[condition].max(), -np.inf)
    exp = np.where(condition, np.exp(shifted), 0.0)
    total = exp.sum()
    if not np.isfinite(total) or total <= 0.0:
        raise AutodiffError("softmax 归零：数值异常，不静默改成均匀分布")
    if not isinstance(logits, Var) or not logits.requires_grad:
        return exp / total
    out = Var(exp / total, parents=(logits,), name="masked_softmax")
    out._backward = lambda grad: logits.accumulate(out.data * (grad - (grad * out.data).sum()))
    return out


def negative_log_prob(probabilities: Any, index: int) -> Any:
    """``-log π(a)``（用于策略梯度/PPO 的比率项）。"""
    probs = value_of(probabilities)
    selected = float(probs[int(index)])
    if not np.isfinite(selected) or selected <= 0.0:
        raise AutodiffError(f"被选动作的概率为 {selected}：无法取对数（不静默截断到 eps）")
    if not isinstance(probabilities, Var) or not probabilities.requires_grad:
        return np.asarray(-np.log(selected))
    out = Var(np.asarray(-np.log(selected)), parents=(probabilities,), name="nll")

    def run(grad: np.ndarray) -> None:
        one_hot = np.zeros_like(probs)
        one_hot[int(index)] = 1.0
        probabilities.accumulate(np.asarray(grad) * (-one_hot / selected))
    out._backward = run
    return out


def entropy(probabilities: Any) -> Any:
    """掩码分布的熵（可选正则；正数）。"""
    probs = value_of(probabilities)
    safe = np.where(probs > 0.0, probs, 1.0)
    terms = np.where(probs > 0.0, probs * np.log(safe), 0.0)
    total = float(-terms.sum())
    if not isinstance(probabilities, Var) or not probabilities.requires_grad:
        return np.asarray(total)
    out = Var(np.asarray(total), parents=(probabilities,), name="entropy")

    def run(grad: np.ndarray) -> None:
        d_terms = np.where(probs > 0.0, np.log(safe) + 1.0, 0.0)
        probabilities.accumulate(np.asarray(grad) * (-d_terms))
    out._backward = run
    return out


def mean(values: Iterable[Any]) -> Any:
    """若干项的逐元素平均（要求同形状；多样本损失请用 `scalar_mean`）。"""
    items = list(values)
    if not items:
        raise AutodiffError("mean 需要至少一项")
    if not is_differentiable(*items):
        return np.mean([value_of(item) for item in items], axis=0)
    stacked = concatenate([reshape(item, (1,) + value_of(item).shape) for item in items], axis=0)
    return divide(total(stacked, axis=0), float(len(items)))


def reshape(a: Any, shape: tuple) -> Any:
    if not isinstance(a, Var) or not a.requires_grad:
        return value_of(a).reshape(shape)
    out = Var(a.data.reshape(shape), parents=(a,), name="reshape")
    out._backward = lambda grad: a.accumulate(np.asarray(grad).reshape(a.data.shape))
    return out


def max_abs_grad(named: Mapping[str, Var]) -> float:
    """诊断：给定参数的最大绝对梯度（0 表示没有梯度，是缺陷信号）。"""
    worst = 0.0
    for var in named.values():
        if var.grad is not None:
            worst = max(worst, float(np.abs(var.grad).max()))
    return worst
