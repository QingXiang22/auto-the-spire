# content-manifest：base-spire 内容迁移清单（B-05a）

把 **base-spire 当前实际注册结果** 冻结成机器可读的迁移 manifest。产物用于 B-05a（内容与原语清单）
与后续 B-05b/B-05c 的冻结分母。

```powershell
# 仓库根目录
dotnet run --project tools/content-manifest -- tools/content-manifest/manifest-2026-10-02.json
```

不带输出参数时打印到 stdout。

## 它做什么

1. **真实注册路径**：`new BaseSpirePackage()` → `ContentRegistry.Build()` → 遍历 `GameDefinition` 的
   各类别字典（cards/powers/enemies/afflictions/potions/card_pools/relics/encounters/run_events/
   characters/runs/state_variables）。注册数来自这条路径，不来自手抄。
2. **源文件侧**：逐个嵌入的 `.spire` 资源解析并编译，取其声明的 ID；同时记录每个文件的
   sha256 与字节数。C# 声明的内容（遗物/遭遇/角色/流程）登记为独立的 `code_sources`，
   其 ID 在清单里标为 `<csharp-declared>`。
3. **差异**：`declared_but_not_registered`（不可达声明）与 `registered_without_spire_source`
   （来源是 C# 而不是 `.spire`）**分开列出**，避免把"代码声明"误报成"缺来源"或反过来。

## 三条分母（互不替代，见 `totals`）

| 键 | 含义 |
| --- | --- |
| `declared_in_sources` | 逐个 `.spire` 编译后取到的声明数 |
| `registered_total` | 走注册路径后实际注册的声明总数 |
| `ironclad_card_pool_size` | Ironclad 卡池成员数 |

## 纪律

- **不为了让分母变小而删难项**：不可达声明必须显式列在 `differences` 里并处理，源文件不删。
- **类别记账必须一致**：每个类别的 ID 都要进入 `registered` 集合（曾漏过状态变量，导致假"不可达"
  与注册总数少算 4——见 `docs/review/tofix/b-05a-content-manifest-2026-10-02.md` §6）。
- 产物是**清单**，不是规则覆盖证明：某条声明"已注册"只说明它进了注册表，不代表其行为已被验证。

## 冻结产物

- `manifest-2026-10-02.json`：354 条注册声明 / 278 条源文件声明 / Ironclad 池 85 张。
