using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class TurnCompilation
{
    internal static TurnEndBindings TurnEnd(ContentBindings contentBindings)
    {

        // -- turn end: powers run their **declared** behaviour program ---------------------------------------
        var resolveTurnEnd = contentBindings.Package.Procedure("ResolveOwnerTurnEnd");

        var turnEndOwner = resolveTurnEnd.Param("Owner", FieldTypeSchema.AnyRef());

        // 站点事件名：作者路径下，一个能力的"回合末"行为只在**它声明的那个事件**上跑
        // （来源：Vulnerable/Weak/Frail 都在 `AfterSideTurnEnd(side == Enemy)` 递减，
        // 而 NoDraw 一类在拥有者自己的回合末）。调用方必须把站点事件传进来。
        var turnEndEvent = resolveTurnEnd.Param("Event", FieldTypeSchema.String(32));

        // "这个站点是不是**拥有者自己**那一侧的回合末"：没有声明 `trigger` 的能力（例如冻结切片内容里的
        // `owner-turn-end = convert-to-strength`）沿用旧语义——在拥有者自己的回合末处理。
        var turnEndOwnSide = resolveTurnEnd.Param("OwnSide", FieldTypeSchema.Bool());

        return new TurnEndBindings(resolveTurnEnd, turnEndOwner, turnEndEvent, turnEndOwnSide);
    }

    internal static EnemyTurnBindings EnemyTurn(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PilesBindings pilesBindings,
        TurnEndBindings turnEndBindings,
        HardcodedPowerTurnEndCallback HardcodedPowerTurnEnd,
        DeclaredPowerTurnEndCallback DeclaredPowerTurnEnd,
        ExpireAtOpponentTurnEndCallback ExpireAtOpponentTurnEnd,
        Func<IrScope, Inst[]>? enemyDoom)
    {
        turnEndBindings.ResolveTurnEnd.Body(
            turnEndBindings.ResolveTurnEnd.ForEach(turnEndBindings.ResolveTurnEnd.Relation(pilesBindings.Powers, turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndOwner)), power => new[]
            {
                // 作者路径：�?*声明**�?`owner-turn-end` 分派（none / tick / convert-to <power>）；
                // 程序化路径保持原来的"Ritual 转力量，其余�?Ticks 标志�?1"�?
                contentBindings.Author is null
                    ? HardcodedPowerTurnEnd(power)
                    : DeclaredPowerTurnEnd(power),
            }));

        var endEnemyTurn = contentBindings.Package.Procedure("EndEnemyTurn");

        endEnemyTurn.Body([
            .. enemyDoom is null ? Array.Empty<Inst>() : new[] { endEnemyTurn.If(endEnemyTurn.Const(true), enemyDoom(endEnemyTurn)) },
            endEnemyTurn.ForEach(endEnemyTurn.Collection(pilesBindings.Monsters), each => new[]
            {
                // 怪物自己声明的 `afterEnemyTurnEnd` 能力（Hatch / TerritorialPower 等）。
                endEnemyTurn.Call(turnEndBindings.ResolveTurnEnd.Symbol, each, endEnemyTurn.Const("enemy"),
                    endEnemyTurn.Const(true)),
            }),
            // **玩家**身上声明 `trigger = afterEnemyTurnEnd` 的能力（Vulnerable/Weak/Frail）在
            // **对手（敌人）**回合末递减（来源 `VulnerablePower.AfterSideTurnEnd(side == Enemy)` 等）；
            // 放在拥有者自己的回合末会整体提前一个回合。
            contentBindings.Author is null
                ? endEnemyTurn.If(endEnemyTurn.Const(false))
                : endEnemyTurn.Call(turnEndBindings.ResolveTurnEnd.Symbol,
                    endEnemyTurn.First(endEnemyTurn.Type(playerBindings.Player, battleBindings.BattleScope)),
                    endEnemyTurn.Const("enemy"), endEnemyTurn.Const(false)),
            // W-06：`removeAfterOpponentTurnEnd` — **玩家**持有的这类能力在对手（敌人）回合末移除，
            // 因此它覆盖敌人的整个回合（FlameBarrier 的反击窗口正是这一回合）。
            contentBindings.Author is null
                ? endEnemyTurn.If(endEnemyTurn.Const(false))
                : endEnemyTurn.ForEach(endEnemyTurn.Relation(pilesBindings.Powers,
                        endEnemyTurn.First(endEnemyTurn.Type(playerBindings.Player, battleBindings.BattleScope))),
                    power => new[] { ExpireAtOpponentTurnEnd(endEnemyTurn, power) })]);

        // -- enemy turn: first move applies Ritual, every later move strikes ----------------------------------
        var startEnemyTurn = contentBindings.Package.Procedure("StartEnemyTurn");

        return new EnemyTurnBindings(endEnemyTurn, startEnemyTurn);
    }

    internal static void EnemyTurnBody(
        ContentBindings contentBindings,
        EnemiesBindings enemiesBindings,
        PilesBindings pilesBindings,
        EnemyTurnBindings enemyTurnBindings,
        HardcodedEnemyMoveCallback HardcodedEnemyMove,
        DeclaredEnemyMoveCallback DeclaredEnemyMove, Func<IrScope, Inst[]>? enemyPoison)
    {
        // B-05c：作者路径下敌人�?*行动程序**（不只是数据）也来自作者文本：
        //   move incantation { ritual = first-ritual }
        //   move dark-strike { damage = strike }
        //   cycle = [incantation, dark-strike xforever]
        // 降级�?�?MoveIndex 分派 + 最后一个行动永远重�?的通用程序；程序化路径保持原来的两分支程序�?
        enemyTurnBindings.StartEnemyTurn.Body([
            .. enemyPoison is null ? Array.Empty<Inst>() : enemyPoison(enemyTurnBindings.StartEnemyTurn),
            enemyTurnBindings.StartEnemyTurn.ForEach(enemyTurnBindings.StartEnemyTurn.Collection(pilesBindings.Monsters), each =>
            {
                var steps = new List<Inst>
                {
                    contentBindings.Author is null ? HardcodedEnemyMove(each) : DeclaredEnemyMove(each),
                };
                if (contentBindings.Author is null)
                {
                    // 程序化路径逐字保留旧行为：意图在行动后固定�?暗击"�?
                    steps.Add(enemyTurnBindings.StartEnemyTurn.SetField(each, enemiesBindings.MonsterNextMove,
                        enemyTurnBindings.StartEnemyTurn.Const(SliceManifest.DarkStrikeMove)));
                }

                steps.Add(enemyTurnBindings.StartEnemyTurn.SetField(each, enemiesBindings.MonsterMoveIndex,
                    enemyTurnBindings.StartEnemyTurn.Add(enemyTurnBindings.StartEnemyTurn.Field(each, enemiesBindings.MonsterMoveIndex), enemyTurnBindings.StartEnemyTurn.Const(1L))));
                return steps.ToArray();
            }),
            enemyTurnBindings.StartEnemyTurn.Call(enemyTurnBindings.EndEnemyTurn.Symbol)]);

    }

    internal static PlayerTurnBindings PlayerTurn(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PilesBindings pilesBindings,
        EventsBindings eventsBindings,
        PowerRulesBindings powerRulesBindings,
        TurnStartTriggerBindings turnStartTriggerBindings,
        DrawBindings drawBindings,
        EnemySelectionBindings enemySelectionBindings,
        MaxEnergyBonusCallback MaxEnergyBonus,
        BlockClearPreventedCallback BlockClearPrevented,
        InnateCallback Innate, Func<IrScope, Inst[]> orbTurnStart, Func<IrScope, Inst[]>? playerPoison)
    {

        var startPlayerTurn = contentBindings.Package.Procedure("StartPlayerTurn");

        var turnPlayer = startPlayerTurn.First(startPlayerTurn.Type(playerBindings.Player, battleBindings.BattleScope));

        var handDraw = powerRulesBindings.PowerRules.HandDraw(startPlayerTurn, startPlayerTurn.Const((long)SliceManifest.DrawPerTurn));

        startPlayerTurn.Body([
            // BASE-02 �?1：玩家回合开始先让每只怪按状态机选取行动（原�?`PrepareForNextTurn` �?`RollMove`
            // �?`AfterTurnStart`/抽牌之前）。出生时已经选过一次，这里�?当前是行动且还没执行�?的怪是空操作�?
            enemySelectionBindings.SelectEnemyMove is null
                ? startPlayerTurn.If(startPlayerTurn.Const(false))
                : startPlayerTurn.ForEach(startPlayerTurn.Collection(pilesBindings.Monsters),
                    each => [startPlayerTurn.Call(enemySelectionBindings.SelectEnemyMove, each)]),
            startPlayerTurn.SetField(turnPlayer, playerBindings.PlayerTurn,
                startPlayerTurn.Add(startPlayerTurn.Field(turnPlayer, playerBindings.PlayerTurn), startPlayerTurn.Const(1L))),
            // This turn's history is reset before anything is played. Authority for the order:
            // the recovered turn start resets the play history before setting energy
            // (Environment.cs:760-772; the slice declares it as the first step after the turn counter).
            startPlayerTurn.If(startPlayerTurn.Const(true), pilesBindings.Lifecycle.BeginTurn(startPlayerTurn, turnPlayer)),
            powerRulesBindings.PowerRules.Call(startPlayerTurn, "CaptureTurnStart"),
            startPlayerTurn.SetField(turnPlayer, powerRulesBindings.PowerRules.EndingTurn, startPlayerTurn.Const(false)),
            startPlayerTurn.SetField(turnPlayer, playerBindings.PlayerEnergy,
                startPlayerTurn.Add(startPlayerTurn.Field(turnPlayer, playerBindings.PlayerMaxEnergy),
                    MaxEnergyBonus(startPlayerTurn, turnPlayer))),
            powerRulesBindings.PowerRules.Call(startPlayerTurn, "AfterEnergyReset"),
            powerRulesBindings.PowerRules.Call(startPlayerTurn, "BeforeHandDraw"),
            // W-06：格挡在回合开始清�?*除非**持有 `preventsBlockClear` 的能力（Barricade）�?
            startPlayerTurn.If(startPlayerTurn.Not(BlockClearPrevented(startPlayerTurn, turnPlayer)),
                startPlayerTurn.SetField(turnPlayer, playerBindings.PlayerBlock, startPlayerTurn.Const(0L))),
            // W-06：`trigger = afterPlayerTurnStart` 的声明式链（DemonFormPower：每回合开始按层数加力量）�?
            // 位置：回合计�?能量/格挡重置之后、抽牌之前（来源�?turn start 触发时点）�?
            startPlayerTurn.Call(turnStartTriggerBindings.TriggerTurnStart.Symbol),
            startPlayerTurn.If(startPlayerTurn.Const(true), handDraw.Capture),
            startPlayerTurn.IfElse(startPlayerTurn.Equal(startPlayerTurn.Field(turnPlayer, playerBindings.PlayerTurn), startPlayerTurn.Const(1L)),
                [startPlayerTurn.Call(drawBindings.Draw,startPlayerTurn.Maximum(handDraw.Count,
                    startPlayerTurn.Count(startPlayerTurn.Where(startPlayerTurn.Collection(pilesBindings.DrawPile),c=>Innate(startPlayerTurn,c)))))],
                [startPlayerTurn.Call(drawBindings.Draw, handDraw.Count)]),
            powerRulesBindings.PowerRules.Call(startPlayerTurn, "AfterTurnStart"),
            .. playerPoison is null ? Array.Empty<Inst>() : playerPoison(startPlayerTurn),
            startPlayerTurn.If(startPlayerTurn.Const(true), orbTurnStart(startPlayerTurn)),
            startPlayerTurn.Emit(eventsBindings.TurnStarted, (eventsBindings.TurnStartedTurn, startPlayerTurn.Field(turnPlayer, playerBindings.PlayerTurn)))]);

        return new PlayerTurnBindings(startPlayerTurn);
    }

    internal static PlayerTurnEndBindings PlayerTurnEnd(ContentBindings contentBindings)
    {
        var endPlayerTurn = contentBindings.Package.Procedure("EndPlayerTurn");

        return new PlayerTurnEndBindings(endPlayerTurn);
    }

    internal static void PlayerTurnEndBody(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PilesBindings pilesBindings,
        PowerRulesBindings powerRulesBindings,
        TurnEndBindings turnEndBindings,
        EnemyTurnBindings enemyTurnBindings,
        PlayerTurnBindings playerTurnBindings,
        PlayerTurnEndBindings playerTurnEndBindings,
        DispatchCardHookCallback DispatchCardHook,
        EndTurnHandStepsCallback EndTurnHandSteps,
        ClearCostModifierCallback ClearCostModifier, Func<IrScope, Inst[]> orbTurnEnd,
        Func<IrScope, Inst[]>? playerDoom)
    {
        playerTurnEndBindings.EndPlayerTurn.Body([
            .. playerDoom is null ? Array.Empty<Inst>() : new[] { playerTurnEndBindings.EndPlayerTurn.If(playerTurnEndBindings.EndPlayerTurn.Const(true), playerDoom(playerTurnEndBindings.EndPlayerTurn)) },
            playerTurnEndBindings.EndPlayerTurn.If(playerTurnEndBindings.EndPlayerTurn.Const(true), orbTurnEnd(playerTurnEndBindings.EndPlayerTurn)),
            powerRulesBindings.PowerRules.Call(playerTurnEndBindings.EndPlayerTurn, "EndTurnAuto"),
            playerTurnEndBindings.EndPlayerTurn.ForEach(playerTurnEndBindings.EndPlayerTurn.Collection(pilesBindings.ExhaustPile), c => DispatchCardHook(playerTurnEndBindings.EndPlayerTurn, c, "after_auto_post_play")),
            // The loop iterates a snapshot of the hand taken when the loop starts, so moving each card out is safe.
            playerTurnEndBindings.EndPlayerTurn.ForEach(playerTurnEndBindings.EndPlayerTurn.Collection(pilesBindings.Hand), EndTurnHandSteps.Invoke),
            // `expires = { turnEnd }`：临时费用修正按回合过期—�?*四个牌区都扫**（只清手牌会留下
            // "被弃�?消耗掉的牌带着旧折扣以后再出现"的陈旧修正）�?
            playerTurnEndBindings.EndPlayerTurn.ForEach(playerTurnEndBindings.EndPlayerTurn.Collection(pilesBindings.Hand), held => ClearCostModifier(playerTurnEndBindings.EndPlayerTurn, held)),
            playerTurnEndBindings.EndPlayerTurn.ForEach(playerTurnEndBindings.EndPlayerTurn.Collection(pilesBindings.DrawPile), held => ClearCostModifier(playerTurnEndBindings.EndPlayerTurn, held)),
            playerTurnEndBindings.EndPlayerTurn.ForEach(playerTurnEndBindings.EndPlayerTurn.Collection(pilesBindings.DiscardPile), held => ClearCostModifier(playerTurnEndBindings.EndPlayerTurn, held)),
            playerTurnEndBindings.EndPlayerTurn.ForEach(playerTurnEndBindings.EndPlayerTurn.Collection(pilesBindings.ExhaustPile), held => ClearCostModifier(playerTurnEndBindings.EndPlayerTurn, held)),            // B-06a（修潜伏缺口）：**玩家自己�?*能力也要跑回合末行为。此前只有敌人侧调用
                                                                                                                                                                                                                     // `ResolveOwnerTurnEnd`，于是玩家身上的易伤/虚弱/Shrink 时长从不递减，`owner-turn-end = remove`
                                                                                                                                                                                                                     // （RagePower �?本回合有�?）也永远不生效。位置按来源：手牌的端末处置之后、敌人回合开始之前�?
            playerTurnEndBindings.EndPlayerTurn.Call(turnEndBindings.ResolveTurnEnd.Symbol, playerTurnEndBindings.EndPlayerTurn.First(playerTurnEndBindings.EndPlayerTurn.Type(playerBindings.Player, battleBindings.BattleScope)),
                playerTurnEndBindings.EndPlayerTurn.Const("player"), playerTurnEndBindings.EndPlayerTurn.Const(true)),
            powerRulesBindings.PowerRules.Call(playerTurnEndBindings.EndPlayerTurn, "AfterTurnEnd"),
            playerTurnEndBindings.EndPlayerTurn.SetField(playerTurnEndBindings.EndPlayerTurn.First(playerTurnEndBindings.EndPlayerTurn.Type(playerBindings.Player, battleBindings.BattleScope)), powerRulesBindings.PowerRules.EndingTurn, playerTurnEndBindings.EndPlayerTurn.Const(true)),
            playerTurnEndBindings.EndPlayerTurn.Call(enemyTurnBindings.StartEnemyTurn.Symbol),
            playerTurnEndBindings.EndPlayerTurn.Call(playerTurnBindings.StartPlayerTurn.Symbol)]);

    }

    internal static Inst ExpireAtOpponentTurnEnd(
        ContentBindings contentBindings,
        PowerBindings powerBindings,
        IrScope scope,
        Expr power)
    {
        var declared = contentBindings.Author!.Powers
            .Where(entry => string.Equals(entry.OwnerTurnEnd, PowerTurnEnd.RemoveAtOpponentTurnEnd,
                StringComparison.Ordinal))
            .ToList();
        if (declared.Count == 0)
        {
            return scope.If(scope.Const(false));
        }

        return Branch(0);

        Inst Branch(int index)
        {
            var entry = declared[index];
            var otherwise = index == declared.Count - 1
                ? new[] { scope.If(scope.Const(false)) }
                : new[] { Branch(index + 1) };
            var condition = scope.Equal(scope.Field(power, powerBindings.PowerDefinition), scope.Const(entry.Id));
            return scope.IfElse(condition, new[] { scope.Destroy(power) }, otherwise);
        }
    }

    internal static Inst[] ClearCostModifier(CardsBindings cardsBindings, IrScope scope, Expr held) =>
                [scope.SetField(held, cardsBindings.CardCostModifier, scope.Const(0L)),
                scope.SetField(held, cardsBindings.CardSchema.FreeUntilPlayed, scope.Const(false))];

    internal static Inst HardcodedPowerTurnEnd(
        PowerBindings powerBindings,
        PowerCommandsBindings powerCommandsBindings,
        TurnEndBindings turnEndBindings,
        Expr power) => turnEndBindings.ResolveTurnEnd.IfElse(
                turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerDefinition),
                    turnEndBindings.ResolveTurnEnd.Const(SliceManifest.RitualId)),
                // Ritual: gain Strength equal to its stacks, except on the turn it was applied.
                new[]
                {
                turnEndBindings.ResolveTurnEnd.IfElse(
                turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerSkip), turnEndBindings.ResolveTurnEnd.Const(true)),
                new[] { turnEndBindings.ResolveTurnEnd.SetField(power, powerBindings.PowerSkip, turnEndBindings.ResolveTurnEnd.Const(false)) },
                new[]
                {
                    turnEndBindings.ResolveTurnEnd.Call(powerCommandsBindings.GainStrength.Symbol, turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndOwner),
                        turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerStacks)),
                }),
                },
                // 其余能力：按时长衰减（跳过被施加的那一回合）�?
                new[]
                {
                turnEndBindings.ResolveTurnEnd.If(turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerTicks),
                    turnEndBindings.ResolveTurnEnd.IfElse(
                        turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerSkip), turnEndBindings.ResolveTurnEnd.Const(true)),
                        new[] { turnEndBindings.ResolveTurnEnd.SetField(power, powerBindings.PowerSkip, turnEndBindings.ResolveTurnEnd.Const(false)) },
                        new[]
                        {
                            turnEndBindings.ResolveTurnEnd.SetField(power, powerBindings.PowerStacks,
                                turnEndBindings.ResolveTurnEnd.Subtract(turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerStacks),
                                    turnEndBindings.ResolveTurnEnd.Const(1L))),
                        })),
                });

    internal static Inst DeclaredPowerTurnEnd(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        EnemiesBindings enemiesBindings,
        PowerBindings powerBindings,
        PowerCommandsBindings powerCommandsBindings,
        PlayerDamageBindings playerDamageBindings,
        PlayerBlockBindings playerBlockBindings,
        TurnEndBindings turnEndBindings,
        Expr power)
    {
        var declared = contentBindings.Author!.Powers
            .Where(entry => !string.Equals(entry.OwnerTurnEnd, PowerTurnEnd.None, StringComparison.Ordinal))
            .ToList();
        Inst Noop() => turnEndBindings.ResolveTurnEnd.If(turnEndBindings.ResolveTurnEnd.Const(false));
        if (declared.Count == 0)
        {
            return Noop();
        }

        return Branch(0);

        Inst Branch(int index)
        {
            var entry = declared[index];
            var otherwise = index == declared.Count - 1 ? new[] { Noop() } : new[] { Branch(index + 1) };
            // 站点门控：能力的**声明触发点**必须等于本次站点那一侧（"enemy"/"player"）；
            // 没有声明触发点的能力只在"拥有者自己那一侧的回合末"处理（`OwnSide`）。
            var condition = turnEndBindings.ResolveTurnEnd.And(
                turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Field(power, powerBindings.PowerDefinition),
                    turnEndBindings.ResolveTurnEnd.Const(entry.Id)),
                turnEndBindings.ResolveTurnEnd.Or(
                    turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndEvent),
                        turnEndBindings.ResolveTurnEnd.Const(entry.TurnEndSide)),
                    turnEndBindings.ResolveTurnEnd.And(
                        turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Const(entry.TurnEndSide),
                            turnEndBindings.ResolveTurnEnd.Const(string.Empty)),
                        turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndOwnSide))));
            return turnEndBindings.ResolveTurnEnd.IfElse(condition, new[] { BehaviourFor(entry, power) }, otherwise);
        }

        Inst BehaviourFor(AuthorPower entry, Expr subject) => entry.OwnerTurnEnd switch
        {
            PowerTurnEnd.Tick => turnEndBindings.ResolveTurnEnd.If(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerTicks),
                turnEndBindings.ResolveTurnEnd.IfElse(
                    turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerSkip), turnEndBindings.ResolveTurnEnd.Const(true)),
                    new[] { turnEndBindings.ResolveTurnEnd.SetField(subject, powerBindings.PowerSkip, turnEndBindings.ResolveTurnEnd.Const(false)) },
                    new[]
                    {
                            turnEndBindings.ResolveTurnEnd.SetField(subject, powerBindings.PowerStacks,
                                turnEndBindings.ResolveTurnEnd.Subtract(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerStacks),
                                    turnEndBindings.ResolveTurnEnd.Const(1L))),
                    })),
            // `remove`：回合末把整个能力移除（来源 removeAfterPlayerTurnEnd），不做时长递减�?
            // ENV-02：若�?*临时力量**能力，移除的同一刻回�?`−符�?× 层数` 力量
            //（来�?`TemporaryStrengthPower.AfterSideTurnEnd` �?Remove + Apply(−Sign × Amount)）�?
            PowerTurnEnd.Remove => entry.TemporaryStrengthPerStack == 0
                ? turnEndBindings.ResolveTurnEnd.Destroy(subject)
                : turnEndBindings.ResolveTurnEnd.If(turnEndBindings.ResolveTurnEnd.Const(true),
                    turnEndBindings.ResolveTurnEnd.Call(powerCommandsBindings.GainStrength.Symbol, turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndOwner),
                        turnEndBindings.ResolveTurnEnd.Multiply(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerStacks),
                            turnEndBindings.ResolveTurnEnd.Const(-(long)entry.TemporaryStrengthPerStack))),
                    turnEndBindings.ResolveTurnEnd.Destroy(subject)),
            // `remove-at-opponent-turn-end`�?*不在拥有者自己的回合�?*处理（那会提前一整个回合），
            // 它在对手回合末由 ExpireAtOpponentTurnEnd 移除 �?这里保持空操作�?
            PowerTurnEnd.RemoveAtOpponentTurnEnd => Noop(),
            PowerTurnEnd.ConvertToStrength => turnEndBindings.ResolveTurnEnd.IfElse(
                turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerSkip), turnEndBindings.ResolveTurnEnd.Const(true)),
                new[] { turnEndBindings.ResolveTurnEnd.SetField(subject, powerBindings.PowerSkip, turnEndBindings.ResolveTurnEnd.Const(false)) },
                new[]
                {
                        turnEndBindings.ResolveTurnEnd.Call(powerCommandsBindings.GainStrength.Symbol, turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndOwner),
                            turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerStacks)),
                }),
            // ENV-02 `gain-block-per-stack:<n>`：回合末按自身层数给玩家 n×层数 点格挡（PlatingPower）�?
            // �?`convert-to-strength` 同一形状：只读自身层数、不做递减、不引入新状态�?
            _ when entry.OwnerTurnEnd.StartsWith(PowerTurnEnd.GainBlockPerStackPrefix,
                StringComparison.Ordinal) => turnEndBindings.ResolveTurnEnd.IfElse(
                turnEndBindings.ResolveTurnEnd.Greater(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerStacks), turnEndBindings.ResolveTurnEnd.Const(0L)),
                new[]
                {
                        turnEndBindings.ResolveTurnEnd.Call(playerBlockBindings.GainPlayerBlock.Symbol, turnEndBindings.ResolveTurnEnd.Multiply(
                            turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerStacks),
                            turnEndBindings.ResolveTurnEnd.Const(long.Parse(entry.OwnerTurnEnd[
                                PowerTurnEnd.GainBlockPerStackPrefix.Length..])))),
                },
                new[] { Noop() }),
            // ENV-02 `damage-owner-unpowered:base:perStack`：拥有者回合末对自己造成
            // `base + perStack × 层数` 点 **unpowered** 伤害（ConstrictPower 的自伤）。
            // 只在**玩家自己**的回合末展开（内容里该能力只由敌人施加给玩家；敌人持有同款未覆盖，已记账），
            // `Powered = false` 因此不消化 Strength/Weak/Vulnerable，也不触发"powered 且正数"的反击链。
            _ when entry.OwnerTurnEnd.StartsWith(PowerTurnEnd.DamageOwnerUnpoweredPrefix,
                StringComparison.Ordinal) => DamageOwnerAtTurnEnd(entry, subject),
            // TerritorialPower.AfterSideTurnEnd: participants contains Owner. The existing OwnSide
            // gate supplies this condition; application neither skips nor consumes source stacks.
            _ when entry.OwnerTurnEnd.StartsWith(PowerTriggerEffect.GainPowerPerStackPrefix,
                StringComparison.Ordinal) => GrantPowerAtTurnEnd(entry, subject),
            _ => throw Unsupported($"power turn-end '{entry.OwnerTurnEnd}'", "没有对应的通用 IR 降级"),
        };

        Inst GrantPowerAtTurnEnd(AuthorPower entry, Expr subject)
        {
            var payload = entry.OwnerTurnEnd[PowerTriggerEffect.GainPowerPerStackPrefix.Length..];
            var separator = payload.IndexOf(':');
            var perStack = long.Parse(payload[..separator], System.Globalization.CultureInfo.InvariantCulture);
            var grantedId = payload[(separator + 1)..];
            var scope = turnEndBindings.ResolveTurnEnd;
            return scope.If(scope.Greater(scope.Field(subject, powerBindings.PowerStacks), scope.Const(0L)),
                scope.Call(powerCommandsBindings.ApplyPower.Symbol, scope.Param(turnEndBindings.TurnEndOwner),
                    scope.Const(grantedId), scope.Multiply(scope.Field(subject, powerBindings.PowerStacks),
                        scope.Const(perStack)), scope.Const(false), scope.Const(false)));
        }

        Inst DamageOwnerAtTurnEnd(AuthorPower entry, Expr subject)
        {
            var parts = entry.OwnerTurnEnd[PowerTurnEnd.DamageOwnerUnpoweredPrefix.Length..].Split(':');
            var amount = turnEndBindings.ResolveTurnEnd.Add(turnEndBindings.ResolveTurnEnd.Const(long.Parse(parts[0])),
                turnEndBindings.ResolveTurnEnd.Multiply(turnEndBindings.ResolveTurnEnd.Field(subject, powerBindings.PowerStacks),
                    turnEndBindings.ResolveTurnEnd.Const(long.Parse(parts[1]))));
            return turnEndBindings.ResolveTurnEnd.If(
                turnEndBindings.ResolveTurnEnd.And(
                    turnEndBindings.ResolveTurnEnd.Equal(turnEndBindings.ResolveTurnEnd.Param(turnEndBindings.TurnEndEvent), turnEndBindings.ResolveTurnEnd.Const("player")),
                    turnEndBindings.ResolveTurnEnd.Greater(amount, turnEndBindings.ResolveTurnEnd.Const(0L))),
                new[]
                {
                        // 攻击者：本包不暴露自伤的攻击者身份，用当前第一只怪占位；因为 `Powered = false`，
                        // 反击链不会因此触发，数值与格挡吸收都与源一致。
                        turnEndBindings.ResolveTurnEnd.Call(playerDamageBindings.DamagePlayer.Symbol,
                            turnEndBindings.ResolveTurnEnd.First(turnEndBindings.ResolveTurnEnd.Type(enemiesBindings.Monster, battleBindings.BattleScope)),
                            amount, turnEndBindings.ResolveTurnEnd.Const(false)),
                });
        }
    }

    internal static IEnumerable<Inst> EndTurnHandSteps(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PowerRulesBindings powerRulesBindings,
        ExhaustTriggerBindings exhaustTriggerBindings,
        PlayerTurnEndBindings playerTurnEndBindings,
        TurnEndHookChainCallback TurnEndHookChain,
        DispatchCardHookCallback DispatchCardHook,
        KeepOrDiscardCallback KeepOrDiscard,
        Expr cardInHand)
    {
        if (contentBindings.Author is null)
        {
            yield return playerTurnEndBindings.EndPlayerTurn.RemoveFromCollection(pilesBindings.Hand, cardInHand);
            yield return playerTurnEndBindings.EndPlayerTurn.AddToCollection(pilesBindings.DiscardPile, cardInHand);
            yield break;
        }

        yield return TurnEndHookChain(cardInHand);
        // `ethereal`：端末仍在手�?�?�?*消耗区**（B-06a）�?
        yield return playerTurnEndBindings.EndPlayerTurn.IfElse(playerTurnEndBindings.EndPlayerTurn.Field(cardInHand, cardsBindings.CardEthereal),
            new Inst[]
            {
                    playerTurnEndBindings.EndPlayerTurn.RemoveFromCollection(pilesBindings.Hand, cardInHand),
                    playerTurnEndBindings.EndPlayerTurn.AddToCollection(pilesBindings.ExhaustPile, cardInHand),
                    playerTurnEndBindings.EndPlayerTurn.SetField(playerTurnEndBindings.EndPlayerTurn.First(playerTurnEndBindings.EndPlayerTurn.Type(playerBindings.Player,battleBindings.BattleScope)),powerRulesBindings.PowerRules.EtherealExhaust,playerTurnEndBindings.EndPlayerTurn.Const(true)),
                    playerTurnEndBindings.EndPlayerTurn.If(playerTurnEndBindings.EndPlayerTurn.Const(true),DispatchCardHook(playerTurnEndBindings.EndPlayerTurn,cardInHand,AuthorSteps.HookOnExhaust)),
                    playerTurnEndBindings.EndPlayerTurn.Call(exhaustTriggerBindings.TriggerAfterExhaust.Symbol),
                    playerTurnEndBindings.EndPlayerTurn.SetField(playerTurnEndBindings.EndPlayerTurn.First(playerTurnEndBindings.EndPlayerTurn.Type(playerBindings.Player,battleBindings.BattleScope)),powerRulesBindings.PowerRules.EtherealExhaust,playerTurnEndBindings.EndPlayerTurn.Const(false)),
            },
            new[] { KeepOrDiscard(cardInHand) });
    }

    internal static Inst KeepOrDiscard(
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PlayerTurnEndBindings playerTurnEndBindings,
        Expr cardInHand)
    {
        Inst Discard() => playerTurnEndBindings.EndPlayerTurn.If(playerTurnEndBindings.EndPlayerTurn.Const(true),
            playerTurnEndBindings.EndPlayerTurn.RemoveFromCollection(pilesBindings.Hand, cardInHand),
            playerTurnEndBindings.EndPlayerTurn.AddToCollection(pilesBindings.DiscardPile, cardInHand));
        Inst Keep() => playerTurnEndBindings.EndPlayerTurn.If(playerTurnEndBindings.EndPlayerTurn.Const(false));

        var cards = cardsBindings.CardDefinitions.Compiled.ToList();
        return Branch(0);

        Inst Branch(int index)
        {
            var card = cards[index];
            var otherwise = index == cards.Count - 1 ? new[] { Discard() } : new[] { Branch(index + 1) };
            // 两个形态都不留：直接进弃牌堆（少一层无意义的分支）�?
            if (!card.Normal.Retain && !card.Upgraded.Retain)
            {
                return index == cards.Count - 1
                    ? Discard()
                    : playerTurnEndBindings.EndPlayerTurn.IfElse(playerTurnEndBindings.EndPlayerTurn.Equal(
                            playerTurnEndBindings.EndPlayerTurn.Field(cardInHand, cardsBindings.CardDefinition),
                            playerTurnEndBindings.EndPlayerTurn.Const(card.Id)),
                        new[] { Discard() },
                        otherwise);
            }

            return playerTurnEndBindings.EndPlayerTurn.IfElse(
                playerTurnEndBindings.EndPlayerTurn.Equal(playerTurnEndBindings.EndPlayerTurn.Field(cardInHand, cardsBindings.CardDefinition),
                    playerTurnEndBindings.EndPlayerTurn.Const(card.Id)),
                new[]
                {
                        playerTurnEndBindings.EndPlayerTurn.IfElse(playerTurnEndBindings.EndPlayerTurn.Field(cardInHand, cardsBindings.CardUpgraded),
                            new[] { card.Upgraded.Retain ? Keep() : Discard() },
                            new[] { card.Normal.Retain ? Keep() : Discard() }),
                },
                otherwise);
        }
    }

    internal static Inst TurnEndHookChain(
        PlayerTurnEndBindings playerTurnEndBindings,
        DispatchCardHookCallback DispatchCardHook,
        Expr cardInHand) => playerTurnEndBindings.EndPlayerTurn.If(playerTurnEndBindings.EndPlayerTurn.Const(true),
                DispatchCardHook(playerTurnEndBindings.EndPlayerTurn, cardInHand, AuthorSteps.HookTurnEndInHand));
}
