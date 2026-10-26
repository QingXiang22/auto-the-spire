# core/0.3 文本包与文件组合参考

文档类型：参考（Reference）。本文定义 `DslPackage.Load(manifestPath, scope)` 的文件边界和数据格式，以及 `EngineDslCompiler.LoadFiles` 的同包组合。类型／具名调用与模板语义继续见[作者参考](authoring-reference.md)；base 的领域归属由[目录设计](../../../understand/base-architecture.md)定义。

## 同包 includes 与跨包 imports

入口及片段均声明同一 `using namespace` 和 `dsl_version = "core/0.3"`。`includes = ["relative/file.spire"]` 在绑定前组合声明，允许前向引用，按声明列表展开；规范化后的重复路径仅组合一次，循环引用仍失败。片段可以继续 includes，但不能拥有 `version`、`imports`、`start` 等包级属性。根入口持有启动顺序；重复符号仍报错，错误定位保留到真实片段。

跨包 `imports` 沿用规则库加载，不把其他 namespace 合进本包。`LoadFiles` 解析文件引用；`Compile` 和 `BindInto` 不隐式读取 includes，出现 includes 即拒绝。纯包加载进一步禁止包外路径与文件链接，直接通用 `LoadFiles` 本身没有这一包分发边界。

当前真实入口见 [base.spire](../../../../mod-the-spire/base-spire/base.spire)，子索引见 [relics/index.spire](../../../../mod-the-spire/base-spire/relics/index.spire)。目录存在不代表自动包含，未引用 DSL 文件会使包加载失败。

## Manifest

格式为 `engine-dsl/package@1`，所有路径相对包根目录。只有下列字段：

| 字段 | 类型与约束 |
| --- | --- |
| format | 固定 `engine-dsl/package@1` |
| id、version | 非空字符串，必须匹配实际编译的包标识 |
| language | 当前固定 `core/0.3` |
| entry | `.spire` 入口相对路径 |
| public_metadata | 静态 `.json`；具体 schema 由 transport 校验 |
| default_scope | 必须包含在 supported_scopes 中 |
| supported_scopes | 非空标识字符串列表，不能重复 |
| unsupported_scopes | scope 到非空原因的对象，不能与支持列表重叠 |
| catalogs | 已加载内容目录 `.json` 的相对路径列表 |
| instruction_budget | 可选正 Int64，指定每条宿主命令的指令上限；省略时使用 SessionBudget 默认值 100000，超过上限显式失败 |
| random_profile | `engine-default` 或 `vanilla-uniform@1`；后者选 xoshiro256**／SplitMix64 与 double 截断有界映射，游戏流种子由 DSL 明确设置；其他值未支持 |

包只允许 `.spire` 与 `.json`。所有 JSON 必须由 manifest 角色声明，所有 DSL 必须被入口引用；未知字段、重复 JSON 键、路径逃逸和链接均拒绝。任何层级出现 effects、conditions、hooks、script、code、body、dsl 字段均作为执行型 JSON 拒绝。

包摘要是按规范相对路径排序的全部包文件文本 SHA256：每项为相对路径、换行、文件文本，项间换行；路径分隔符为 `/`，文本 CRLF 规范为 LF。摘要包含静态元数据而不包含每局 seed。规则标识另由 Engine 计算，不与包摘要混用。

## 已加载内容目录

`engine-dsl/content-catalog@1` 只允许 `format`、`kind`、`entries`；kind 为非空内容类别。entries 是数组，每行只允许下列字段：

| 字段 | 含义 |
| --- | --- |
| id | 非空内容标识；同目录 id 唯一 |
| definition | 非空相对路径，必须指向实际加载的 `.spire` 内容模块；不允许 null 或未引用文件 |

definition 指向至少含一个声明的完整 DSL 内容模块，可定义实体、过程、订阅、模板和动作；以模板作用域隔离内部名称。模块顶层属性只允许dsl_version；start、imports、includes、version等包属性仍属于入口／索引。简单use文件同样有效。显式引用、根路径限制、静态JSON、未引用文件和重复符号检查保持。`use` 的参数形状、字面量、范围和未知参数由现有 Engine 模板绑定器检查，无第二种执行语义。

目录只描述当前包的有效定义，不存储来源范围、迁移状态或验证结论。完整来源与哈希保存在包外工具固定数据；来源改动不参与生产包摘要。旧 source-catalog／source-data 格式明确拒绝；调用方应迁移到当前格式，不使用兼容入口。公开协议 catalog 的 schema 和 ID 仍由 transport 维护。

## 加载结果与失败

`DslPackage` 提供包 Id／Version／Scope、入口／公开元数据路径、CatalogPaths、RandomProfile、InstructionBudget、Digest 与编译好的 Rules。`CreateSession(seed, participantRoles, gameSeed=null)` 返回按包 random profile 和指令预算配置、尚未 Start 的 Session；应由宿主传入原始 gameSeed 文本，再由 DSL 确定流种子，不在 Host 重算游戏布局。省略文本时 gameSeed() 返回数值 seed 的 invariant 十进制文本。未支持 scope 或 random profile 抛 `NotSupportedException`；非法 schema／内容子集抛 `FormatException`，DSL 绑定错误保留 `DslSourceException` 及文件位置，缺失文件为 IO 错误。

当前开发工具和 Host 将未支持映射为退出码 3，参数／加载错误映射为 2；Host 在建立会话前执行范围检查。Engine 包加载不解释 net 公开元数据的字段，Host 与工具通过同一公开 metadata schema 继续校验。
