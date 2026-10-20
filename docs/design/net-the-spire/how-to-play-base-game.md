# 手动试玩 base-spire

文档类型：操作指南（How-to）。本页用于亲自选择地图、出牌和处理房间。开发者调用方法见[公开接口教程](how-to-run-public-game.md)，当前进度见[行为要求](../environment-completion-and-differential-v1.md#env-t03)。

## 启动终端菜单

在 PowerShell 执行：

```powershell
Set-Location 'F:\Scylier-Project\active\auto-the-spire'
python -X utf8 tools/base-spire-dev/play_game.py --seed ENV-T03-SEED-L
```

默认启动负责人 2026-10-12 已验证的 Host，范围是 `dev-map`，不自动编译当前源码。程序显示玩家生命、金币、战斗中的能量/格挡、敌人意图、手牌和当前合法动作。

这是终端试玩，卡牌和敌人的名字使用英文内容名；卡牌完整描述尚未作为界面文本接入。菜单显示公开费用、数值和能力，可用 `d 编号` 查当前公开字段。

## 手动选择动作

| 输入 | 操作 |
| --- | --- |
| 动作编号，例如 `2` | 执行当前列表的第 2 个动作 |
| `d 2` | 查看第 2 个动作对应实体的公开字段；不执行动作 |
| `p` | 查看公开的弃牌、消耗牌和长期牌组 |
| `r` | 刷新当前状态；不出牌、不结束回合 |
| `q` | 退出试玩并关闭本次本地 Host |

地图中，菜单给出当前可选节点的行、列和房型。战斗中选择“打出卡牌”；需要目标或选牌时，下一份菜单会变成“选择目标或卡牌”，再输入一个编号。结束回合、领取奖励、休息、升级、购买和离店也用相同方式选择。

编号只属于当前菜单。每次结算后重新看列表，不沿用前一份菜单的编号。费用、金币、目标、容量和动作可用性由引擎判断，客户端只提交本次公开候选。

商店会显示货架、价格、折扣、已售状态和不可买原因。删牌需要先选择“商店删牌”，然后选择要删的具体长期卡牌实例。抽牌堆显示公开数量，不显示隐藏顺序。

## 更换 seed 或构建

```powershell
# 换一局
python -X utf8 tools/base-spire-dev/play_game.py --seed MY-SEED

# 只运行 dev 战斗范围
python -X utf8 tools/base-spire-dev/play_game.py --scope dev --seed MY-SEED

# 使用明确指定的已构建 Host
python -X utf8 tools/base-spire-dev/play_game.py --host-dll PATH_TO_HOST.dll --seed MY-SEED
```

`q` 结束当前进程，菜单没有保存/恢复功能。换 seed 需要重新启动命令。

## 遇到开发边界

当前是开发环境。遇到尚未实现的房间、敌人行为或幕转换，Host 会返回具名错误，菜单打印原因并停止。幕完成也不等于完整整局胜利。保存终端里的 seed、所选动作和最后错误，便于复现。

菜单的交互路径是：玩家选编号 → Python 客户端提交当前候选 → net Host → base 规则在 core 中结算 → 返回新状态与菜单。运行过程中不通过控制台修改引擎状态或另行模拟游戏规则。
