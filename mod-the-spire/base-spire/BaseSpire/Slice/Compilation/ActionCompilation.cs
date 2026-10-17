using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class ActionCompilation
{
    internal static void PotionAction(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        PotionsBindings potionsBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        InCombatCallback InCombat,
        PotionEffectChainCallback PotionEffectChain,
        BattleEndTailCallback BattleEndTail)
    {

        // -- use potion（药水与牌共用步骤降级；用后离开药水栏＝消耗，且不消耗能量） --------------------------
        if (potionsBindings.HasPotions)
        {
            potionsBindings.Potions!.RegisterDiscard(contentBindings.Package, scope => roomServicesBindings.ActMap is null ? scope.Const(true)
                : scope.Not(scope.Field(scope.First(scope.Collection(runCollectionsBindings.RunStates!)), roomServicesBindings.ActMap.RunActCompleted)));
            var usePotion = contentBindings.Package.Action("UsePotion");
            var usedPotionDeclaration = usePotion.Parameter("Potion", FieldTypeSchema.RefTo(potionsBindings.Potion!));
            var useBody = contentBindings.Package.ActionBody(usePotion);
            var usedPotion = useBody.Param(usedPotionDeclaration.DeclarationIndex);
            useBody.Role(SliceManifest.PlayerRole);
            useBody.Candidates("Potion", useBody.Where(useBody.Collection(potionsBindings.PotionBelt!),
                candidate => useBody.Field(candidate, potionsBindings.Potions!.UseSupported)));
            useBody.Requires(useBody.And(InCombat(useBody), useBody.Exists(useBody.Where(
                useBody.Collection(potionsBindings.PotionBelt!), candidate => useBody.Field(candidate, potionsBindings.Potions!.UseSupported)))));
            var useOwner = useBody.First(useBody.Type(playerBindings.Player, battleBindings.BattleScope));
            // 两个分支各自�?*类型正确**的目标槽：敌人目标是 ref<Monster>，自我目标是 ref<Player>�?
            var useEnemy = useBody.Local("potionEnemy", FieldTypeSchema.RefTo(enemiesBindings.Monster));
            var useSelf = useBody.Local("potionSelf", FieldTypeSchema.RefTo(playerBindings.Player));

            // 效果：按药水自己�?target 语义选目标（enemy 提问 / self 直接用玩家），然后跑它声明的步骤�?
            useBody.Body(
                useBody.IfElse(
                    useBody.Equal(useBody.Field(usedPotion, potionsBindings.PotionTarget!), useBody.Const("enemy")),
                    // 敌人目标�?*不在这里提问**——药水自己的 `target` 步骤会提问（与牌的行为完全一致，
                    // 因此"目标确定方式唯一"这条既有校验也继续管着药水）�?
                    new[]
                    {
                        PotionEffectChain(useBody, usedPotion, useOwner, useEnemy, PotionTargets.Enemy),
                    },
                    new[]
                    {
                        useBody.SetLocal(useSelf, useOwner),
                        PotionEffectChain(useBody, usedPotion, useOwner, useSelf, PotionTargets.Self),
                    }),
                // 消耗：离开药水栏（药水是消耗品，且不消耗能量）�?
                // ENV-T02（复核修正）：胜利判定在**动作体末尾**——药水照样能造成伤害并击杀最后一个敌人，
                // 必须在"效果 + 消耗"跑完之后再判，否则这条路径又会截断收尾（与 `PlayCard`/`EndTurn` 同规格）。
                BattleEndTail(useBody));
        }

    }

    internal static void EndTurnAction(
        ContentBindings contentBindings,
        PilesBindings pilesBindings,
        PlayerTurnEndBindings playerTurnEndBindings,
        InCombatCallback InCombat,
        BattleEndTailCallback BattleEndTail)
    {

        var endTurn = contentBindings.Package.Action("EndTurn");

        var endTurnBody = contentBindings.Package.ActionBody(endTurn);

        endTurnBody.Role(SliceManifest.PlayerRole);

        // BASE-01�?*本场战斗还有敌人时才允许结束回合**。没有这条守卫时�?清房之后结束回合"是一�?
        // 合法的空操作（状态版本不变、不推进任何东西），公开候选里会一直挂着一个什么都不做的动作�?
        endTurnBody.Requires(endTurnBody.And(InCombat(endTurnBody), endTurnBody.Greater(endTurnBody.Count(endTurnBody.Collection(pilesBindings.Monsters)),
            endTurnBody.Const(0L))));

        endTurnBody.Body(endTurnBody.Call(playerTurnEndBindings.EndPlayerTurn.Symbol), BattleEndTail(endTurnBody));

    }

    internal static void RunActions(
        ContentBindings contentBindings,
        CardsBindings cardsBindings,
        RunStateBindings runStateBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        RoomTransitionBindings roomTransitionBindings)
    {

        // -- W-05：进入下一间房（作者路径） ------------------------------------------------------------------
        // 只在**本间房已经结�?*（`Run.SettledRoom == Run.CurrentRoom`）时合法：这�?打完一间房再进下一�?
        // 在动作层面可核对，而不是靠调用方自觉。它�?战斗结束"�?整局结束"分开�?
        // 清房后会话仍�?Running，玩家可以执行本动作进入下一间房；最后一间房清完才设整局胜利终局�?
        if (runStateBindings.HasRun && roomServicesBindings.ActMap is null)
        {
            var enterNextRoom = contentBindings.Package.Action("EnterNextRoom");
            var enterBody = contentBindings.Package.ActionBody(enterNextRoom);
            enterBody.Role(SliceManifest.PlayerRole);
            var enterRun = enterBody.First(enterBody.Collection(runCollectionsBindings.RunStates!));
            // 注意：`Requires` �?*设置**单个 guard（不是累积条件），两个前置条件必须显式合取�?
            // 正式构建的房间上限来�?`Run.RoomCount`（遭遇池长度）；受控切片沿用冻结的房间数常量�?
            var roomLimit = runStateBindings.RunRoomCount is null
                ? enterBody.Const((long)SliceManifest.RunRoomCount)
                : (Expr)enterBody.Field(enterRun, runStateBindings.RunRoomCount);
            enterBody.Requires(enterBody.And(
                enterBody.Equal(enterBody.Field(enterRun, runStateBindings.RunSettledRoom!),
                    enterBody.Field(enterRun, runStateBindings.RunCurrentRoom!)),
                enterBody.Less(enterBody.Field(enterRun, runStateBindings.RunCurrentRoom!), roomLimit)));
            enterBody.Body(enterBody.Call(roomTransitionBindings.StartNextRoom!.Symbol));

            // -- W-05：房间奖励——把一�?*长期**牌升级（每间房至多一次） -----------------------------------------
            // �?`EnterNextRoom` 分开：可以清房后**跳过**奖励直接进下一间房�?
            // 升级�?*内容声明**取升级变体的数值（费用/数�?能力层数/实例变量/不可打出/虚无），
            // 由按 DefinitionId 的动态分派完成（长期实例�?DefinitionId 是运行期值）�?
            var upgradeCard = contentBindings.Package.Action("UpgradeLongTermCard");
            var upgradeParam = upgradeCard.Parameter("Card", FieldTypeSchema.RefTo(cardsBindings.Card));
            var upgradeBody = contentBindings.Package.ActionBody(upgradeCard);
            var upgradeRun = upgradeBody.First(upgradeBody.Collection(runCollectionsBindings.RunStates!));
            var upgradedRef = upgradeBody.Param(upgradeParam.DeclarationIndex);
            upgradeBody.Role(SliceManifest.PlayerRole);
            upgradeBody.Candidates("Card", upgradeBody.Collection(runCollectionsBindings.LongTermDeck!));
            upgradeBody.Requires(upgradeBody.And(
                // �?本间房已结算（奖励只在清房之后）
                upgradeBody.Equal(upgradeBody.Field(upgradeRun, runStateBindings.RunSettledRoom!),
                    upgradeBody.Field(upgradeRun, runStateBindings.RunCurrentRoom!)),
                // �?本间房还没拿过奖励（`Requires` 是设置单个守�?�?条件必须显式合取�?
                upgradeBody.Not(upgradeBody.Equal(upgradeBody.Field(upgradeRun, runStateBindings.RunRewardTaken!),
                    upgradeBody.Field(upgradeRun, runStateBindings.RunCurrentRoom!)))));

            // 升级数值：�?DefinitionId 分派；已经是升级形态时是空操作（幂等，且不重复计数）�?
            Inst UpgradeDispatch(Expr cardRef)
            {
                Expr eligible = upgradeBody.Const(false);
                foreach (var definition in cardsBindings.CardDefinitions.Compiled.Where(d => d.Upgradeable))
                    eligible = upgradeBody.Or(eligible, upgradeBody.Equal(
                        upgradeBody.Field(cardRef, cardsBindings.CardDefinition), upgradeBody.Const(definition.Id)));
                return upgradeBody.If(upgradeBody.And(eligible,
                    upgradeBody.Not(upgradeBody.Field(cardRef, cardsBindings.CardUpgraded))),
                    cardsBindings.CardDefinitions.Upgrade(upgradeBody, cardRef),
                    upgradeBody.SetField(upgradeRun, runStateBindings.RunRewardTaken!,
                        upgradeBody.Field(upgradeRun, runStateBindings.RunCurrentRoom!)));
            }

            upgradeBody.Body(UpgradeDispatch(upgradedRef));
        }

    }

    internal static Expr InCombat(
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        IrScope scope) => roomServicesBindings.ActMap is null ? scope.Const(true) :
                scope.Equal(scope.Field(scope.First(scope.Collection(runCollectionsBindings.RunStates!)), roomServicesBindings.ActMap.RunMapPhase), scope.Const(1L));
}
