# K-09a：项目依赖图与边界检查（2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **K-09a（依赖抽离，第一步：先画项目依赖）** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（K-09a 行）（另见 §9.2 的领域抽离总纲） |
| 完成判据（本批覆盖的部分） | "先画项目依赖"；"**无 base↔core 环**"；"**Engine 不依赖 STS**"；"新通用路径不引用 Legacy" |
| 负责范围 | `tools/dependency-check/check_dependencies.py`（新增）、`tools/dependency-check/depmap-2026-10-02.json`（机器产物） |
| 状态 | **第一步完成**；文件/引用/测试的**迁移**（K-09a 的后半段）未开始（见 §5） |
| 前置 | B-05b（新通用路径已存在：`Engine/` + `Slice/`） |

## 1. 为什么先做这个

B-05b 的报告里写过一句"作者前端只引用 `CoreTheSpire.Engine`，不经 Legacy"——但那是**叙述**，
没有任何机械核对。K-09a 的第一条就是"先画项目依赖"，而"Engine 不依赖 STS""新路径不偷偷回退 Legacy"
正是可以**机械证伪**的性质。本批把它变成一条可运行的检查，并顺带产出依赖图。

## 2. 检查做了什么

```powershell
python tools/dependency-check/check_dependencies.py --json tools/dependency-check/depmap-2026-10-02.json
```

1. **项目依赖图**：扫描全部 56 个 `.csproj` 的 `ProjectReference`，并对图做检查——
   **无环**；`Core.TheSpire` **不**依赖 `BaseSpire`；`BaseSpire` → `Core.TheSpire`（单向）。
2. **旧后端类型清单**：从 `Core.TheSpire/Legacy/**` 收集 **363** 个声明的类型名。
3. **新通用路径不得引用它们**：扫 `Engine/**` 与 `mod-the-spire/base-spire/BaseSpire/Slice/**`（**14** 个文件），
   在**类型使用位置**上匹配（`new X(`、`X.`、基类列表、`X ident`、`X?`/`X[]`/`List<X>`、`(X ident`）。
4. **检查器自检**：用合成片段验证"真的抓得到 Legacy 用法"且"不误报同名成员"。

## 3. 结果（退出码 0）

| 检查 | 结果 |
| --- | --- |
| `checker/self-test` | **通过**（6 个真用法全部抓到；5 个同名成员的良性片段全部不误报） |
| `graph/non-empty`、`graph/acyclic` | 56 个项目、**0 个环** |
| `graph/core-does-not-reference-base-spire` | `Core.TheSpire` 的引用里没有 `BaseSpire` |
| `graph/base-spire-references-core` | `BaseSpire → Core.TheSpire` ✓（单向） |
| `legacy/types-collected` | 363 个 Legacy 类型名 |
| `engine-and-slice/scan-non-empty` | 14 个新路径文件 |
| **`engine-and-slice/no-legacy-types`** | **0 个违规**：`Engine/**` 与 `Slice/**` 都**没有**在类型位置引用任何 Legacy 类型 |

`TOTAL: 8 passed, 0 failed, 8 executed`。依赖图落盘为 [depmap-2026-10-02.json](../../../../tools/dependency-check/depmap-2026-10-02.json)（2.6 KB）。

## 4. 过程中修掉的三个"检查自己的错误"（都是假阳性/漏报）

1. **`record struct X` 把关键字当类型名**：`Legacy` 里 `record struct Foo` 让名字集合里出现了 `struct`，
   于是 `Engine/Value.cs` 里的 `struct` 关键字被当成引用。→ 加 `(?:struct|class)?` 并过滤 C# 关键字。
2. **注释与字符串里的提及**：先去掉注释/字符串再做匹配（注释里提到旧类型不是依赖）。
3. **同名成员被当成类型依赖**：切片自己有 `SliceCard(string ContentId, …)`，而 `ContentId` 恰好是 Legacy 类型名。
   → 只在**类型使用位置**匹配；并且自检专门用"同名成员"的良性片段守住这条（此后 `record Wrapper(ContentId Id)`
   这种**类型位置**的写法被自检抓出漏报，又把模式从 `[a-z_]` 放宽到 `[A-Za-z_]`）。

**这三条都是"检查本身可能是错的"的具体例子**，因此本检查带自检，而不是只报绿色。

## 5. 未完成（K-09a 的后半段与 K-09b）

- **迁移本身**：把旧 `Card/Power/Enemy/Combat/Run` 等定义/流程的**领域编译与规则**移向 base-spire、
  更新测试与 `TestHookMod` 引用——这是"按通用设计第 9 节逐项"的持续工作，需分批；
- **K-09b**：默认入口转新后端、旧兼容入口显式命名、把旧 STS 实现隔离到游戏侧兼容项目；
- 本批只覆盖"画依赖 + 证明边界"，**不宣称** K-09a 完成。

## 6. 修改文件

- `tools/dependency-check/check_dependencies.py`（新增；含自检）
- `tools/dependency-check/depmap-2026-10-02.json`（机器产物）
- `docs/design/implementation-kickoff.md` §10（登记命令入口）

未提交、未清理共享工作区。

## 7. 下一批建议

**K-09a 第二步：把切片需要的 STS 领域职责显式化**——按清单"先迁切片需要的职责、确保新运行时真实使用，
再分批抽离其余"：先列出 `Legacy/**` 中**被新路径间接依赖的领域知识**（例如伤害次序、hooks 语义）
当前以什么形式存在（文档？旧代码？），并给出"哪些先迁、哪些留作对照"的清单；
随后按该清单分批迁移，每批都带"新运行时真的在用"的证据。移路径不算迁移。
