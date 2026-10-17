using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class RunSchema
{
    internal static RunStateBindings RunState(PackageBuildOptions options, ContentBindings contentBindings)
    {

        // -- B-07a：整局级状态（只有作者文本声明了 `run { �?}` 时才创建，默认路径形状不变） ----------------
        var hasRun = contentBindings.Author is not null;

        var runEntity = hasRun ? contentBindings.Package.EntityType("Run") : null;

        var runHp = hasRun ? runEntity!.AddField("Hp", FieldTypeSchema.Int(0, 999)) : null;

        var runMaxHp = hasRun ? runEntity!.AddField("MaxHp", FieldTypeSchema.Int(1, 999)) : null;

        var runGold = hasRun ? runEntity!.AddField("Gold", FieldTypeSchema.Int(0, 99999)) : null;

        // W-05�?*按房�?*的结算幂等键。一个不重置的全局布尔不能代表所有房间（§19.2 的分歧点）：
        // `CurrentRoom` �?`StartNextRoom` 里递增，`SettledRoom` 记录"哪一间房已经结算�?�?
        // 因此重复结算同一间房是空操作，而进入下一间房�?*可以**再结算一次�?
        var runCurrentRoom = hasRun ? runEntity!.AddField("CurrentRoom", FieldTypeSchema.Int(0, 999)) : null;

        var runSettledRoom = hasRun ? runEntity!.AddField("SettledRoom", FieldTypeSchema.Int(0, 999)) : null;

        // W-05�?*每间房至多一�?*的战利品标记（奖励不与结算共用一个键：可以结算但不拿奖励）�?
        var runRewardTaken = hasRun ? runEntity!.AddField("RewardTaken", FieldTypeSchema.Int(0, 999)) : null;

        // BASE-01�?*本间房是哪一个遭�?*（正式构建才有的公开事实）。它只在会话内由原版流写一次，
        // 战斗房间的敌人由它分派；规则集里不含任何"第几间遇到谁"的常量�?
        var runEncounter = hasRun && options.EncounterPool is not null
            ? runEntity!.AddField("EncounterId", FieldTypeSchema.String(64))
            : null;

        // BASE-01：本局�?*战斗房间�?*�? 遭遇池长度）。原版的房间总数来自地图，这里只�?
        // 正式 dev 构建里表�?当前范围覆盖到第几间�?，接入地图后由地图取代�?
        var runRoomCount = hasRun && options.EncounterPool is not null
            ? runEntity!.AddField("RoomCount", FieldTypeSchema.Int(0, 999))
            : null;

        return new RunStateBindings(hasRun, runEntity, runHp, runMaxHp, runGold, runCurrentRoom, runSettledRoom, runRewardTaken, runEncounter, runRoomCount);
    }

    internal static RelicsBindings Relics(
        PackageBuildOptions options,
        ContentBindings contentBindings,
        EventsBindings eventsBindings,
        RunStateBindings runStateBindings)
    {
        // BASE-02�?*遗物**（属整局状态，来源 ENV §6"金币/遗物/药水槽与容量 �?Run 状�?）�?
        // 只声明定义与效果；具体某件遗物是否在本局持有由开局实例决定�?
        var hasRelics = runStateBindings.HasRun && options.RelicLibrary is { Count: > 0 };

        var schema = hasRelics ? Domain.Run.RelicSchema.Declare(contentBindings.Package) : null;
        var relic = schema?.Type;
        var relicDefinition = schema?.Definition;
        var relicRarity = schema?.Rarity;
        var relics = schema?.Owned;

        if (hasRelics)
        {
            // 遗物与整局状态一样必�?*可观�?*（否则公开侧看不到玩家持有什么）�?
            eventsBindings.Projection.Type(relic!);
        }

        return new RelicsBindings(hasRelics, relic, relicDefinition, relicRarity, relics, schema);
    }

    internal static RunCollectionsBindings RunCollections(
        ContentBindings contentBindings,
        CardsBindings cardsBindings,
        EventsBindings eventsBindings,
        RunStateBindings runStateBindings)
    {
        // �?*集合**承载这个单例（与敌人�?monsters 集合同一套模式）：结算过程按集合取，
        // 而不是依�?从另一个过程里按类型查作用域成�?（那条路径在实验里取不到实例）�?
        var runStates = runStateBindings.HasRun ? contentBindings.Package.Collection("RunStates", runStateBindings.RunEntity!) : null;

        // W-05�?*整局的长期牌�?*。它与战斗里那些实例�?*两批独立实例**：战斗结束不回写这里�?
        // 战斗里生�?克隆的牌也不会进这里（D5②：同定义实例必须能分别升级/移除）�?
        var longTermDeck = runStateBindings.HasRun ? contentBindings.Package.Collection("LongTermDeck", cardsBindings.Card) : null;

        if (runStateBindings.HasRun)
        {
            // W-01：整局状态与长期牌组也要**可观�?*�?
            //   * Run 单例进入观看者投�?�?它的 Hp/MaxHp/Gold 成为公开字段�?
            //   * LongTermDeck 成员是真实实�?�?公开侧用 group（owner = Run）表�?哪些牌属于整局"�?
            //     从而能区分长期实例与战斗实例（不借用卡牌 pile 语义）�?
            eventsBindings.Projection.Type(runStateBindings.RunEntity!);
        }

        return new RunCollectionsBindings(runStates, longTermDeck);
    }
}
