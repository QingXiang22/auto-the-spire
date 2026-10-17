using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class PowerCompilation
{
    internal static PowerQueryBindings PowerQuery(
        ContentBindings contentBindings,
        PowerBindings powerBindings,
        PilesBindings pilesBindings)
    {

        // -- powers -----------------------------------------------------------------------------------------
        // Power behaviour is looked up by declared id; the slice has a handful, so explicit comparisons are honest
        // and readable. A definition table is B-05/B-06 work.
        var powerStacksOf = contentBindings.Package.Procedure("PowerStacksOf");

        var stacksOwner = powerStacksOf.Param("Owner", FieldTypeSchema.AnyRef());

        var stacksId = powerStacksOf.Param("PowerId", FieldTypeSchema.String(64));

        var stacksFound = powerStacksOf.Local("found", FieldTypeSchema.Binding());

        var stacksValue = powerStacksOf.Local("value", FieldTypeSchema.Int(-99, 99));

        powerStacksOf.Pure().Result(FieldTypeSchema.Int(-99, 99));

        powerStacksOf.Body(
            powerStacksOf.SetLocal(stacksValue, powerStacksOf.Const(0L)),
            powerStacksOf.SetLocal(stacksFound, powerStacksOf.First(powerStacksOf.Where(
                powerStacksOf.Relation(pilesBindings.Powers, powerStacksOf.Param(stacksOwner)),
                power => powerStacksOf.Equal(powerStacksOf.Field(power, powerBindings.PowerDefinition),
                    powerStacksOf.Param(stacksId))))),
            powerStacksOf.If(powerStacksOf.Not(powerStacksOf.IsNone(powerStacksOf.LocalRef(stacksFound))),
                powerStacksOf.SetLocal(stacksValue, powerStacksOf.Field(powerStacksOf.LocalRef(stacksFound), powerBindings.PowerStacks))),
            powerStacksOf.Return(powerStacksOf.LocalRef(stacksValue)));

        return new PowerQueryBindings(powerStacksOf);
    }

    internal static PowerCommandsBindings PowerCommands(
        ContentBindings contentBindings,
        PowerBindings powerBindings,
        PilesBindings pilesBindings)
    {
        var applyPower = contentBindings.Package.Procedure("ApplyPower");

        // ENV-02：`after_power_applied:<能力 id>`（ViciousPower）的派发点�?
        // **早声明符号、晚�?Body**：这条链要调 `draw`，�?`draw` 在文件后面才定义�?
        // 因此这里只取符号，Body �?`draw` 就绪后再填（过程符号�?Body 可以分开给）�?
        var resolvePowerApplied = contentBindings.Package.Procedure("ResolvePowerApplied");

        var appliedPowerIdParam = resolvePowerApplied.Param("PowerId", FieldTypeSchema.String(64));

        var appliedOwnerParam = resolvePowerApplied.Param("Owner", FieldTypeSchema.AnyRef());

        var applyOwner = applyPower.Param("Owner", FieldTypeSchema.AnyRef());

        var applyId = applyPower.Param("PowerId", FieldTypeSchema.String(64));

        var applyStacks = applyPower.Param("Stacks", FieldTypeSchema.Int(-99, 99));

        var applyTicks = applyPower.Param("Ticks", FieldTypeSchema.Bool());

        var applySkip = applyPower.Param("SkipNextTick", FieldTypeSchema.Bool());

        var applyFound = applyPower.Local("found", FieldTypeSchema.Binding());

        var applyCreated = applyPower.Local("created", FieldTypeSchema.Binding());

        // ENV-02：临时力量能力（来源 `TemporaryStrengthPower.BeforeApplied`）：施加这条能力时，
        // 同时给拥有�?`符号 × 层数` 力量。用**自递归调用 ApplyPower** 施加 Strength（临时能力表里没�?
        // Strength，因此不会无限递归）�?
        // 注意：`ProcedureBuilder.If(...)` 只是**返回**一条指令，必须放进 `Body(...)` 的参数里才生效—�?
        // 单独调用会被静默丢弃（本轮踩过一次：钩子"看着写了"但什么都没做）�?
        var temporaryStrengthHooks = (contentBindings.Author?.Powers
                .Where(entry => entry.TemporaryStrengthPerStack != 0) ?? [])
            .Select(temporary => applyPower.If(
                applyPower.Equal(applyPower.Param(applyId), applyPower.Const(temporary.Id)),
                applyPower.Call(applyPower.Symbol, applyPower.Param(applyOwner),
                    applyPower.Const(SliceManifest.StrengthId),
                    applyPower.Multiply(applyPower.Param(applyStacks),
                        applyPower.Const((long)temporary.TemporaryStrengthPerStack)),
                    applyPower.Const(false), applyPower.Const(false))))
            .ToArray();

        applyPower.Body(
        [
            applyPower.SetLocal(applyFound, applyPower.First(applyPower.Where(
                applyPower.Relation(pilesBindings.Powers, applyPower.Param(applyOwner)),
                power => applyPower.Equal(applyPower.Field(power, powerBindings.PowerDefinition),
                    applyPower.Param(applyId))))),
            applyPower.IfElse(
                applyPower.IsNone(applyPower.LocalRef(applyFound)),
                new[]
                {
                    applyPower.CreateEntity(powerBindings.Power, applyPower.EntityScope(applyPower.Param(applyOwner)), applyCreated,
                        new[]
                        {
                            (powerBindings.PowerDefinition, applyPower.Param(applyId)),
                            (powerBindings.PowerStacks, applyPower.Param(applyStacks)),
                            (powerBindings.PowerTicks, applyPower.Param(applyTicks)),
                            (powerBindings.PowerSkip, applyPower.Param(applySkip)),
                        }),
                    applyPower.AddRelation(pilesBindings.Powers, applyPower.Param(applyOwner), applyPower.LocalRef(applyCreated)),
                },
                new[]
                {
                    applyPower.SetField(applyPower.LocalRef(applyFound), powerBindings.PowerStacks,
                        applyPower.Add(applyPower.Field(applyPower.LocalRef(applyFound), powerBindings.PowerStacks),
                            applyPower.Param(applyStacks))),
                    applyPower.SetField(applyPower.LocalRef(applyFound), powerBindings.PowerTicks, applyPower.Param(applyTicks)),
                    // SkipNextTick is deliberately NOT re-armed when a power stacks onto an existing instance.
                    // Authority: sts2/src/Core/Commands/PowerCmd.cs:78-84 sends a stack to ModifyAmount (:215-271),
                    // which never touches SkipNextDurationTick; only the attach path (:144-147 for player debuffs) and
                    // RitualPower's own AfterApplied (:36-43, only reached from :155) set it. The legacy backend
                    // re-arms it on every application (State.cs:553-555) and so keeps Vulnerable/Weak one turn
                    // longer; the slice follows the source. See docs/review/tofix/r-damage-impl-2026-10-01.md.
                }),
            .. temporaryStrengthHooks,
            // 施加完成后派�?`after_power_applied` 链（没有该声明时这条调用是空操作）�?
            applyPower.Call(resolvePowerApplied.Symbol, applyPower.Param(applyId),
                applyPower.Param(applyOwner)),
        ]);

        var gainStrength = contentBindings.Package.Procedure("GainStrength");

        var gainOwner = gainStrength.Param("Owner", FieldTypeSchema.AnyRef());

        var gainStacks = gainStrength.Param("Stacks", FieldTypeSchema.Int(-99, 99));

        gainStrength.Body(
            gainStrength.Call(applyPower.Symbol, gainStrength.Param(gainOwner), gainStrength.Const(SliceManifest.StrengthId),
                gainStrength.Param(gainStacks), gainStrength.Const(false), gainStrength.Const(false)));

        return new PowerCommandsBindings(applyPower, resolvePowerApplied, appliedPowerIdParam, appliedOwnerParam, gainStrength);
    }

    internal static PowerRulesBindings PowerRules(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PowerBindings powerBindings,
        PilesBindings pilesBindings,
        RunCollectionsBindings runCollectionsBindings)
    {

        var powerRules = new PowerRuleRuntime(contentBindings.Package, contentBindings.Author, cardsBindings.CardSchema, cardsBindings.CardDefinitions,
            new(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay), pilesBindings.PlayHistory, battleBindings.CardKind,
            playerBindings.Player, powerBindings.Power, battleBindings.BattleScope, pilesBindings.Powers, powerBindings.PowerDefinition, powerBindings.PowerStacks, playerBindings.PlayerHp, playerBindings.PlayerEnergy,
            pilesBindings.Monsters, runCollectionsBindings.LongTermDeck);

        return new PowerRulesBindings(powerRules);
    }

    internal static Expr EnergyGainBlocked(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PowerQueryBindings powerQueryBindings,
        IrScope scope)
    {
        var blockers = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => power.PreventEnergyGain).ToList();
        if (blockers.Count == 0)
        {
            return scope.Const(false);
        }

        var conditions = blockers.Select(power => scope.Greater(
            scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                scope.Const(power.Id)),
            scope.Const(0L)));
        return conditions.Aggregate((left, right) => scope.Or(left, right));
    }

    internal static Expr BlockClearPrevented(
        ContentBindings contentBindings,
        PowerQueryBindings powerQueryBindings,
        IrScope scope,
        Expr owner)
    {
        var keepers = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => power.PreventsBlockClear).ToList();
        if (keepers.Count == 0)
        {
            return scope.Const(false);
        }

        var conditions = keepers.Select(power => scope.Greater(
            scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)),
            scope.Const(0L)));
        return conditions.Aggregate((left, right) => scope.Or(left, right));
    }

    internal static Expr DrawPrevented(
        ContentBindings contentBindings,
        PowerQueryBindings powerQueryBindings,
        IrScope scope,
        Expr owner)
    {
        var keepers = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => string.Equals(power.Role, "NoDraw", StringComparison.Ordinal))
                .ToList();
        if (keepers.Count == 0)
        {
            return scope.Const(false);
        }

        var conditions = keepers.Select(power => scope.Greater(
            scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)),
            scope.Const(0L)));
        return conditions.Aggregate((left, right) => scope.Or(left, right));
    }

    internal static Expr MaxEnergyBonus(
        ContentBindings contentBindings,
        PowerQueryBindings powerQueryBindings,
        IrScope scope,
        Expr owner)
    {
        var donors = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => power.MaxEnergyPerStack != 0).ToList();
        if (donors.Count == 0)
        {
            return scope.Const(0L);
        }

        var terms = donors.Select(power => scope.Multiply(
            scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)),
            scope.Const((long)power.MaxEnergyPerStack)));
        return terms.Aggregate((left, right) => scope.Add(left, right));
    }

    internal static Inst PotionEffectChain(
        ContentBindings contentBindings,
        PilesBindings pilesBindings,
        PotionsBindings potionsBindings,
        CompileEffectsCallback CompileEffects,
        IrScope scope,
        Expr potionRef,
        Expr owner,
        int targetSlot,
        string targetKind)
    {
        // 只分�?*目标种类一�?*的药水：敌人目标的分支不能拿自己的目标槽去跑自我目标的药水�?
        var declarations = contentBindings.Author!.Potions
            .Where(potion => string.Equals(potion.Target, targetKind, StringComparison.Ordinal)
                || targetKind == PotionTargets.Self && potion.Target == "allEnemies").ToList();
        if (declarations.Count == 0)
        {
            return scope.If(scope.Const(false));
        }

        return Branch(0);

        Inst Branch(int index)
        {
            var declaration = declarations[index];
            var condition = scope.Equal(scope.Field(potionRef, potionsBindings.PotionDefinition!), scope.Const(declaration.Id));
            var body = new List<Inst>();
            var program = LegacyCardAdapter.Potion(declaration);
            if (program.NeedsSelectedTarget)
                body.Add(scope.Decide(scope.Const(Value.OfParticipant(SliceManifest.Player)),
                    DecisionResponseKind.EntityRef, "Choose a target", targetSlot,
                    scope.Collection(pilesBindings.Monsters), scope.Const(1L), scope.Const(1L)));
            // Target selection completes before use; the bottle is consumed before effect decisions.
            body.Add(scope.Destroy(potionRef));
            body.AddRange(CompileEffects(scope, potionRef, owner, scope.LocalRef(targetSlot), targetSlot,
                -1, -1, -1, program, declaration.Unpowered));
            return index == declarations.Count - 1
                ? scope.If(condition, body.ToArray())
                : scope.IfElse(condition, body, new[] { Branch(index + 1) });
        }
    }

    internal static Inst[] TriggerChain(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PowerQueryBindings powerQueryBindings,
        PlayerBlockBindings playerBlockBindings,
        IrScope scope,
        string trigger,
        Expr? playedCard = null)
    {
        var declared = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => string.Equals(power.Trigger, trigger, StringComparison.Ordinal)).ToList();
        if (declared.Count == 0)
        {
            return [scope.If(scope.Const(false))];
        }

        var instances = new List<Inst>();
        var index = 0;
        foreach (var power in declared)
        {
            var stacks = scope.Local($"triggerStacks{index}", FieldTypeSchema.Int(-99, 99));
            var amount = long.Parse(power.TriggerEffect[PowerTriggerEffect.GainBlockPerStackPrefix.Length..]);
            instances.Add(scope.SetLocal(stacks,
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                    scope.Const(power.Id))));
            var pay = scope.Call(playerBlockBindings.GainPlayerBlock.Symbol,
                scope.Multiply(scope.LocalRef(stacks), scope.Const(amount)));
            pay = scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)), pay);
            // `when-card-type`：只有被献出的那张牌属于该种类时才触发（没写就不限）�?
            if (playedCard is not null && power.WhenCardType.Length > 0)
            {
                pay = scope.If(scope.Equal(scope.Field(playedCard, cardsBindings.CardKindField),
                    scope.EnumConst(battleBindings.CardKind, power.WhenCardType)), new[] { pay });
            }
            instances.Add(pay);
            index++;
        }

        return [.. instances];
    }

    internal static Inst[] GrantPowerChain(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PowerBindings powerBindings,
        PilesBindings pilesBindings,
        PowerQueryBindings powerQueryBindings,
        PowerCommandsBindings powerCommandsBindings,
        PlayerBlockBindings playerBlockBindings,
        IrScope scope)
    {
        var declared = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => string.Equals(power.Trigger,
                AuthorPowerTrigger.AfterPlayerTurnStart, StringComparison.Ordinal)).ToList();
        if (declared.Count == 0)
        {
            return [scope.If(scope.Const(false))];
        }

        var instances = new List<Inst>();
        var index = 0;
        foreach (var power in declared)
        {
            var stacks = scope.Local($"grantStacks{index}", FieldTypeSchema.Int(-99, 99));
            instances.Add(scope.SetLocal(stacks,
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                    scope.Const(power.Id))));
            if (power.MinimumStartTriggerTurn > 0)
                instances.Add(scope.If(scope.Less(scope.Field(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                    playerBindings.PlayerTurn), scope.Const((long)power.MinimumStartTriggerTurn)), scope.SetLocal(stacks, scope.Const(0L))));
            // 一次触发可以有**多个效果**（`sequence(A, B)` 降级�?`A|B`）：按声明顺序逐个展开�?
            foreach (var effect in power.TriggerEffect.Split(PowerTriggerEffect.SequenceSeparator))
            {
                if (effect.StartsWith(PowerTriggerEffect.GainPowerPerStackPrefix, StringComparison.Ordinal))
                {
                    var payload = effect[PowerTriggerEffect.GainPowerPerStackPrefix.Length..];
                    var parts = payload.Split(':', 2);
                    var perStack = long.Parse(parts[0]);
                    var grantedId = parts.Length > 1 ? parts[1] : string.Empty;
                    instances.Add(scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)),
                        scope.Call(powerCommandsBindings.ApplyPower.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                            scope.Const(grantedId),
                            scope.Multiply(scope.LocalRef(stacks), scope.Const(perStack)),
                            scope.Const(false), scope.Const(false))));
                }
                else if (effect.StartsWith(PowerTriggerEffect.GainBlockPrefix, StringComparison.Ordinal))
                {
                    // 固定数值的格挡（不按层数）：`gain-block:N`�?
                    var block = long.Parse(effect[PowerTriggerEffect.GainBlockPrefix.Length..]);
                    instances.Add(scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)),
                        scope.Call(playerBlockBindings.GainPlayerBlock.Symbol, scope.Const(block))));
                }
                else if (string.Equals(effect, PowerTriggerEffect.RemoveSelf, StringComparison.Ordinal))
                {
                    // `removePower(自己)`：把这条能力的实例移除（一次性能力）�?
                    instances.Add(scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)),
                        scope.ForEach(scope.Relation(pilesBindings.Powers, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope))),
                            instance => new[]
                            {
                                    scope.If(scope.Equal(scope.Field(instance, powerBindings.PowerDefinition),
                                            scope.Const(power.Id)),
                                        scope.Destroy(instance)),
                            })));
                }
                else if (string.Equals(effect, PowerTriggerEffect.DecrementSelf, StringComparison.Ordinal))
                {
                    // ENV-02 `decrementPower(自己)`：把自身层数�?1，减�?0 就移�?
                    //（PlatingPower：`trigger(event=afterOwnerTurnStart, effect=decrementPower(PlatingPower))`）�?
                    // �?`remove-self` 同一套取实例的写法，只把 Destroy 换成"�?1 再判 0"�?
                    instances.Add(scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)),
                        scope.ForEach(scope.Relation(pilesBindings.Powers, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope))),
                            instance => new[]
                            {
                                    scope.If(scope.Equal(scope.Field(instance, powerBindings.PowerDefinition),
                                            scope.Const(power.Id)),
                                        scope.SetField(instance, powerBindings.PowerStacks,
                                            scope.Subtract(scope.Field(instance, powerBindings.PowerStacks),
                                                scope.Const(1L)))),
                                    scope.If(scope.And(
                                            scope.Equal(scope.Field(instance, powerBindings.PowerDefinition),
                                                scope.Const(power.Id)),
                                            scope.LessOrEqual(scope.Field(instance, powerBindings.PowerStacks),
                                                scope.Const(0L))),
                                        scope.Destroy(instance)),
                            })));
                }
            }

            index++;
        }

        return [.. instances];
    }

    internal static Expr HoldsPower(
        PowerBindings powerBindings,
        PilesBindings pilesBindings,
        IrScope scope,
        Expr owner,
        string id)
    {
        // Legal-action guards must stay read-only: no InvokePure while awaiting a decision.
        var held = scope.First(scope.Where(scope.Relation(pilesBindings.Powers, owner),
            p => scope.Equal(scope.Field(p, powerBindings.PowerDefinition), scope.Const(id))));
        return scope.And(scope.Not(scope.IsNone(held)), scope.Greater(scope.Field(held, powerBindings.PowerStacks), scope.Const(0L)));
    }

    internal static Inst[] RemovePower(
        PowerBindings powerBindings,
        PilesBindings pilesBindings,
        IrScope scope,
        Expr owner,
        string id) => [scope.ForEach(scope.Where(scope.Relation(pilesBindings.Powers, owner),
            p => scope.Equal(scope.Field(p, powerBindings.PowerDefinition), scope.Const(id))), p => [scope.Destroy(p)])];
}
