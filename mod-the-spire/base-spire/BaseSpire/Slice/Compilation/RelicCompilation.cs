using BaseSpire.Slice.Domain.Run;
using BaseSpire.Slice.Domain.Combat;
using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Compilation;

/// <summary>Inventory-ordered relic programs compiled to Engine IR. No runtime state or relic-ID branches.</summary>
internal sealed class RelicCompilation(PackageBuildOptions options, ContentBindings content,
    BattleBindings battle, PlayerBindings player, PilesBindings piles, RelicsBindings relics,
    RunStateBindings runState, RunCollectionsBindings runCollections, RoomServicesBindings services,
    EventsBindings events, PowerCommandsBindings powers, Func<IrScope, Expr, Inst[]> gainBlock,
    Func<IrScope, Expr> energyBlocked, Func<IrScope, string, string, Expr, Inst[]> orbEffect,
    Func<IrScope, Expr, string, Expr> cardKindIs)
{
    private int nextLocal;
    private readonly Dictionary<string, ProcedureSymbol> modifiers = new(StringComparer.Ordinal);

    internal Expr Modify(IrScope s, string kind, Expr amount)
    {
        if (!relics.HasRelics || options.RelicLibrary is null) return amount;
        var entries = options.RelicLibrary.SelectMany(r => r.Effects.Where(e => e.Trigger == RelicTriggers.Modifier && e.Kind == kind)
            .Select(e => (Relic: r, Effect: e))).ToArray();
        if (entries.Length == 0) return amount;
        if (!modifiers.TryGetValue(kind, out var symbol))
        {
            var body = content.Package.Procedure("RelicModifier_" + kind.Replace('-', '_'));
            body.Pure().Result(FieldTypeSchema.Int(-999, 999999));
            var input = body.Param("Amount", FieldTypeSchema.Int(-999, 999999));
            var result = body.Local("result", FieldTypeSchema.Int(-999, 999999));
            body.Body(body.SetLocal(result, body.Param(input)),
                body.ForEach(body.Collection(relics.Schema!.Owned), instance =>
                    entries.Select(entry => body.If(body.And(body.Not(body.Field(instance, relics.Schema.IsMelted)),
                        body.And(body.Equal(body.Field(instance, relics.Schema.Definition), body.Const(entry.Relic.ContentId)), Condition(body, entry.Effect, instance))),
                        body.SetLocal(result, body.Add(body.LocalRef(result), body.Const((long)entry.Effect.Amount)))))),
                body.Return(body.LocalRef(result)));
            modifiers[kind] = symbol = body.Symbol;
        }
        return s.CallPure(symbol, amount);
    }

    internal Expr Has(IrScope s, string kind) => s.Greater(Modify(s, kind, s.Const(0L)), s.Const(0L));

    internal Inst[] Dispatch(IrScope s, string trigger) => Dispatch(s, trigger, false);
    internal Inst[] DispatchRun(IrScope s, string trigger) => Dispatch(s, trigger, true);

    /// <summary>`after-card-played`：牌型过滤需要把刚打出的那张牌传进来（空＝任意牌）。</summary>
    internal Inst[] DispatchCard(IrScope s, string trigger, Expr card) => Dispatch(s, trigger, false, card);

    private Inst[] Dispatch(IrScope s, string trigger, bool outsideCombat, Expr? card = null)
    {
        if (!relics.HasRelics || options.RelicLibrary is null) return [];
        var definitions = options.RelicLibrary.Where(r => r.Effects.Any(e => e.Trigger == trigger)).ToArray();
        if (definitions.Length == 0) return [];
        var schema = relics.Schema!;
        return [s.ForEach(s.Collection(schema.Owned), instance =>
        {
            // Replacement may destroy this instance. Snapshot its definition before running any effect.
            var id = s.Local($"triggerRelic{nextLocal++}", FieldTypeSchema.String(64));
            var body = new List<Inst> { s.SetLocal(id, s.Field(instance, schema.Definition)) };
            foreach (var definition in definitions)
            {
                var effects = new List<Inst>();
                foreach (var effect in definition.Effects.Where(e => e.Trigger == trigger))
                    effects.Add(s.If(Condition(s, effect, instance, outsideCombat, card), Effect(s, instance, effect, outsideCombat)));
                body.Add(s.If(s.Equal(s.LocalRef(id), s.Const(definition.ContentId)), effects.ToArray()));
            }
            // An earlier listener may have removed a later member of the snapshot.
            return [s.If(s.GreaterOrEqual(s.CollectionIndex(schema.Owned, instance), s.Const(0L)),
                s.If(s.Not(s.Field(instance, schema.IsMelted)), body.ToArray()))];
        })];
    }

    private Expr Condition(IrScope s, SliceRelicEffect effect, Expr instance, bool outsideCombat = false, Expr? card = null)
    {
        Expr condition = s.Const(true);
        if (effect.CardKind.Length > 0)
            condition = s.And(condition, card is null ? s.Const(false) : cardKindIs(s, card, effect.CardKind));
        if (effect.CounterEquals >= 0)
            condition = s.And(condition, s.Equal(s.Field(instance, relics.Schema!.Counter), s.Const((long)effect.CounterEquals)));
        if (effect.HpAtMostPercent > 0)
            condition = s.And(condition, s.LessOrEqual(s.Field(Owner(s), player.PlayerHp),
                s.Divide(s.Multiply(s.Field(Owner(s), player.PlayerMaxHp), s.Const((long)effect.HpAtMostPercent)), s.Const(100L))));
        if (effect.MapPointType.Length > 0)
            condition = s.And(condition, services.ActMap is null ? s.Const(false) : s.Equal(
                s.Field(s.Field(Run(s), services.ActMap.RunCurrentNode), services.ActMap.NodeType),
                s.EnumConst(services.ActMap.PointType, effect.MapPointType)));
        if (effect.OnTurn > 0)
            condition = s.And(condition, s.Equal(s.Field(Owner(s), player.PlayerTurn), s.Const((long)effect.OnTurn)));
        if (effect.MinimumTurn > 0)
            condition = s.And(condition, s.GreaterOrEqual(s.Field(Owner(s), player.PlayerTurn), s.Const((long)effect.MinimumTurn)));
        if (effect.MaximumTurn > 0)
            condition = s.And(condition, s.LessOrEqual(s.Field(Owner(s), player.PlayerTurn), s.Const((long)effect.MaximumTurn)));
        if (effect.RoomType.Length > 0)
            condition = s.And(condition, services.ActMap is null ? s.Const(false)
                : s.Equal(s.Field(Run(s), services.ActMap.RunActualRoomType), s.EnumConst(services.ActMap.RoomType, effect.RoomType)));
        return condition;
    }

    private Inst[] Effect(IrScope s, Expr instance, SliceRelicEffect effect, bool outsideCombat)
    {
        var owner = outsideCombat ? Run(s) : Owner(s);
        var hp = outsideCombat ? runState.RunHp! : player.PlayerHp;
        var maximum = outsideCombat ? runState.RunMaxHp! : player.PlayerMaxHp;
        var health = new RunResources(hp, maximum, runState.RunGold!, events.Defeat, events.DefeatTurn);
        Expr amount = s.Const((long)effect.Amount);
        if (effect.AmountSource != "constant")
            amount = s.Multiply(amount, s.Divide(s.Count(s.Collection(effect.AmountSource == "hand" ? piles.Hand : runCollections.LongTermDeck!)), s.Const((long)effect.Divisor)));
        switch (effect.Kind)
        {
            case RelicEffectKinds.Heal:
                return [s.If(s.Greater(s.Field(owner, hp), s.Const(0L)), health.HealLivingPlayer(s, owner, amount))];
            case RelicEffectKinds.GainMaxHp:
                return [s.If(s.Greater(s.Field(owner, player.PlayerHp), s.Const(0L)), health.GainMaxHp(s, owner, s.Const((long)effect.Amount)))];
            case RelicEffectKinds.GainBlock:
                return gainBlock(s, amount);
            case RelicEffectKinds.GainEnergy:
                return [s.If(s.Not(energyBlocked(s)), s.SetField(owner, player.PlayerEnergy,
                    s.Add(s.Field(owner, player.PlayerEnergy), s.Const((long)effect.Amount))))];
            case RelicEffectKinds.LoseEnergy:
                return [s.SetField(owner, player.PlayerEnergy, s.Maximum(s.Const(0L),
                    s.Subtract(s.Field(owner, player.PlayerEnergy), s.Const((long)effect.Amount))))];
            case RelicEffectKinds.GainStars:
                return new CombatStars(player.PlayerStars, _ => s.Not(s.Field(owner, player.PlayerBattleSettled)))
                    .Change(s, owner, CardEffectOperation.GainStars, s.Const((long)effect.Amount));
            case RelicEffectKinds.AddCounter:
                var counter = s.Add(s.Field(instance, relics.Schema!.Counter), s.Const((long)effect.Amount));
                if (effect.CounterModulo > 0) counter = s.Modulo(counter, s.Const((long)effect.CounterModulo));
                return services.RelicCommands!.SetCounter(s, instance, counter);
            case RelicEffectKinds.SetCounter:
                return services.RelicCommands!.SetCounter(s, instance, amount);
            case RelicEffectKinds.ChannelOrb:
                return orbEffect(s, "channelOrb", effect.OrbKind, amount);
            case RelicEffectKinds.AddOrbSlots:
                return orbEffect(s, "addOrbSlots", "", amount);
            case RelicEffectKinds.ApplyPower:
                var definition = content.Author?.Powers.SingleOrDefault(p => p.Id == effect.PowerId)
                    ?? throw new InvalidOperationException($"Relic references unloaded Power: {effect.PowerId}");
                Inst Apply(Expr target) => s.Call(powers.ApplyPower.Symbol, target, s.Const(effect.PowerId), s.Const((long)effect.Amount),
                    s.Const(definition.OwnerTurnEnd == PowerTurnEnd.Tick), s.Const(false));
                return effect.Target == "enemies" ? [s.ForEach(s.Collection(piles.Monsters), enemy => [Apply(enemy)])] : [Apply(owner)];
            case RelicEffectKinds.CountAndReplace:
                var commands = services.RelicCommands ?? throw new InvalidOperationException("Relic replacement commands are not wired");
                return [.. commands.AddCounter(s, instance, s.Const((long)effect.Amount)),
                    s.If(s.GreaterOrEqual(s.Field(instance, relics.Schema!.Counter), s.Const((long)effect.CounterThreshold)),
                        commands.Replace(s, Run(s), instance, s.Const(effect.ReplaceWith)))];
            default: throw new InvalidOperationException($"Unwired relic lifecycle effect: {effect.Kind}");
        }
    }

    private Expr Owner(IrScope s) => s.First(s.Type(player.Player, battle.BattleScope));
    private Expr Run(IrScope s) => s.First(s.Collection(runCollections.RunStates!));
}
