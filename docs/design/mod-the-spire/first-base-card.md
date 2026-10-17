# 在base能力上写出第一张卡

文档类型：教程（Tutorial）。你将从一个空目录写一张卡，用base真实前端与Engine出牌，然后只改DSL观察伤害变化。本教程使用已实现的攻击和格挡，不需要修改C#。

前置条件：安装.NET 9 SDK和PowerShell；在仓库根目录操作。教程文件写入你自己的`.audit/tutorial-base-card`，不改正式卡池。完整Run的安装与源资格见[内容操作指南](modding-guide.md)。

## 1. 创建学习目录并构建工具

```powershell
$cardLesson = '.audit/tutorial-base-card'
if (Test-Path $cardLesson) { throw '学习目录已存在，请保留旧成果并选择一个新的目录名' }
New-Item -ItemType Directory -Path $cardLesson | Out-Null
dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj --artifacts-path .artifacts/parallel/tutorial-base-card -v quiet
$cardTool = '.artifacts/parallel/tutorial-base-card/bin/BaseSpire.Dev/debug/BaseSpire.Dev.dll'
```

构建应返回0并生成`BaseSpire.Dev.dll`。后面运行这个DLL，修改外部DSL不需要重建。

## 2. 写卡牌定义

将以下完整内容写入新文件：

```powershell
@'
using namespace practice;
dsl_version = "base/0.0.2"

card TrainingStrike {
    common = {
        type = attack
        rarity = common
        cost = 1
        target = { selectOne = { from = context.opponents where = alive } }
        keywords = {}
        play = {
            attack = { damage = 7 }
            gainBlock = { amount = 2 }
        }
        hooks = {}
    }
    normal = {}
    upgraded = {
        play = {
            attack = { damage = 11 }
            gainBlock = { amount = 3 }
        }
    }
}
'@ | Set-Content -Encoding utf8 "$cardLesson/training-cards.spire"
dotnet $cardTool check "$cardLesson/training-cards.spire"
```

结果应包含`practice:training-strike`，`declared / parsed / lowered / registered`均为1。这里的registered说明受控牌组里创建了对应卡实例；还没有证明出牌行为。

## 3. 打出普通卡和升级卡

```powershell
dotnet $cardTool play "$cardLesson/training-cards.spire" practice:training-strike normal
dotnet $cardTool play "$cardLesson/training-cards.spire" practice:training-strike upgraded
```

两次应返回0。观察输出的`play.before`和`play.after`：

| 变体 | 目标生命 | 格挡 | 能量 | 结果 |
| --- | --- | --- | --- | --- |
| normal | 38 → 31 | 0 → 2 | 3 → 2 | 一次目标选择，进入弃牌堆 |
| upgraded | 38 → 27 | 0 → 3 | 3 → 2 | 一次目标选择，进入弃牌堆 |

费用、选目标和弃牌由base共享流程完成。你只写了攻击及格挡。

## 4. 只改DSL再打一次

```powershell
$cardText = Get-Content -Raw "$cardLesson/training-cards.spire"
$cardText.Replace('damage = 7', 'damage = 9') | Set-Content -Encoding utf8 "$cardLesson/training-cards.spire"
dotnet $cardTool play "$cardLesson/training-cards.spire" practice:training-strike normal
dotnet $cardTool play "$cardLesson/training-cards.spire" practice:training-strike upgraded
```

普通卡现在把生命从38打到29，格挡仍为2；升级卡仍打到27、格挡3。`upgraded.play`替换整个效果列表，所以普通效果的修改没有自动叠加进升级效果。

你已经完成一张新卡的定义、注册、普通/升级出牌和外部文件修改。教程只改一个DSL文件；没有进入规则包构建器、Host或原版源码。未知机制和完整Run资格分别在[作者参考](../../dsl/0.0.2/authoring-reference.md)及[base开发指南](base-development-guide.md)中处理。
