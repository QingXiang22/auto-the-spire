using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class CardCompilation
{
    internal static KnowledgeCommandsBindings KnowledgeCommands(
        ContentBindings contentBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        KnowledgeBindings knowledgeBindings,
        PilesBindings pilesBindings)
    {

        // -- known-card bookkeeping --------------------------------------------------------------------------
        // Dropping a fact must **destroy** it, not merely take it out of the area: a fact left alive is still a
        // projected entity, so the consumer would keep reading a position the player no longer knows (the H-01
        // check `known_positions �?visible_members` fails on exactly that). Destroying also drops the authorization
        // edge, which is done explicitly first so the intent is visible here.
        var clearKnown = contentBindings.Package.Procedure("ClearKnownCards");

        clearKnown.Body(
            clearKnown.ForEach(clearKnown.Collection(knowledgeBindings.Known), entry => new[]
            {
                clearKnown.ClearRelation(knowledgeBindings.KnownReveal, entry),
                clearKnown.Destroy(entry),
            }));

        var revokeKnown = contentBindings.Package.Procedure("RevokeKnown");

        var revokeCard = revokeKnown.Param("Card", FieldTypeSchema.RefTo(cardsBindings.Card));

        revokeKnown.Body(
            revokeKnown.ForEach(revokeKnown.Collection(knowledgeBindings.Known), entry => new[]
            {
                revokeKnown.If(revokeKnown.Equal(revokeKnown.Field(entry, knowledgeBindings.KnownCardRef),
                        revokeKnown.Param(revokeCard)),
                    revokeKnown.ClearRelation(knowledgeBindings.KnownReveal, entry),
                    revokeKnown.Destroy(entry)),
            }));

        // Records an absolute position the rules just revealed, computed from the pile itself.
        var recordKnownTop = contentBindings.Package.Procedure("RecordKnownTop");

        var knownSource = recordKnownTop.Param("Viewer", FieldTypeSchema.RefTo(playerBindings.Player));

        var knownTarget = recordKnownTop.Param("Card", FieldTypeSchema.RefTo(cardsBindings.Card));

        var knownEntry = recordKnownTop.Local("entry", FieldTypeSchema.Binding());

        recordKnownTop.Body(
            recordKnownTop.Call(clearKnown.Symbol),
            recordKnownTop.CreateEntity(knowledgeBindings.KnownCard, recordKnownTop.EntityScope(recordKnownTop.Param(knownTarget)),
                knownEntry, new[]
                {
                    (knowledgeBindings.KnownViewer, recordKnownTop.Field(recordKnownTop.Param(knownSource), playerBindings.PlayerOwner)),
                    (knowledgeBindings.KnownCardRef, recordKnownTop.Param(knownTarget)),
                    (knowledgeBindings.KnownPile, recordKnownTop.Const(SliceManifest.DrawPileName)),
                    (knowledgeBindings.KnownPosition, recordKnownTop.CollectionIndex(pilesBindings.DrawPile,
                        recordKnownTop.Param(knownTarget))),
                }, knowledgeBindings.Known),
            recordKnownTop.AddRelation(knowledgeBindings.KnownReveal, recordKnownTop.LocalRef(knownEntry),
                recordKnownTop.Param(knownTarget)));

        return new KnowledgeCommandsBindings(clearKnown, revokeKnown, recordKnownTop);
    }

    internal static DrawBindings Draw(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PowerQueryBindings powerQueryBindings,
        PowerCommandsBindings powerCommandsBindings,
        PowerRulesBindings powerRulesBindings,
        KnowledgeCommandsBindings knowledgeCommandsBindings,
        DrawPreventedCallback DrawPrevented)
    {

        // -- drawing, reshuffling and the player turn --------------------------------------------------------
        // 源 `StableShuffle` 预排序的纯计算资产（排序接缝）：声明在这里，执行由包内计算后端按 Entry 分派。
        var sourceOrder = SourceStableShuffle.Declare(contentBindings.Package, cardsBindings.CardSchema.SourceOrderKey);
        var cardDraw = new CardDrawProcedures(new(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay),
            cardsBindings.CardKindField, battleBindings.CardKind, knowledgeCommandsBindings.ClearKnown.Symbol, knowledgeCommandsBindings.RevokeKnown.Symbol,
            scope => scope.Or(DrawPrevented(scope, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope))),
                scope.Not(scope.Exists(scope.Collection(pilesBindings.Monsters)))),
            sourceOrder,
            (scope, drawn) => [powerRulesBindings.PowerRules.Call(scope, "AfterDraw", drawn)]);

        var draw = cardDraw.Declare(contentBindings.Package);

        var autoPlayCommands = new AutoPlayProcedures(contentBindings.Package, cardsBindings.CardSchema, new(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay),
            cardDraw, pilesBindings.ExecuteCardPlay.Symbol,
            scope => scope.Greater(scope.Field(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerHp), scope.Const(0L)),
            scope => scope.And(
                scope.Greater(scope.Field(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerHp), scope.Const(0L)),
                scope.Greater(scope.Count(scope.Collection(pilesBindings.Monsters)), scope.Const(0L))));

        // ENV-02：`after_power_applied:<能力 id>` 链（ViciousPower）。放�?`draw` 之后就绪�?
        // 因为效果�?按自身层数抽�?：`draw-per-stack:n` �?�?`n × 自己的层数` 张�?
        {
            var declared = contentBindings.Author?.Powers
                .Where(power => power.Trigger.StartsWith(AuthorPowerTrigger.AfterPowerAppliedPrefix,
                    StringComparison.Ordinal))
                .ToList() ?? [];
            if (declared.Count == 0)
            {
                powerCommandsBindings.ResolvePowerApplied.Body(powerCommandsBindings.ResolvePowerApplied.If(powerCommandsBindings.ResolvePowerApplied.Const(false)));
            }
            else
            {
                var applied = new List<Inst>();
                var appliedIndex = 0;
                foreach (var watchedPower in declared)
                {
                    var watched = watchedPower.Trigger[AuthorPowerTrigger.AfterPowerAppliedPrefix.Length..];
                    var perStack = long.Parse(
                        watchedPower.TriggerEffect[PowerTriggerEffect.DrawPerStackPrefix.Length..]);
                    var stacks = powerCommandsBindings.ResolvePowerApplied.Local($"appliedStacks{appliedIndex++}",
                        FieldTypeSchema.Int(-99, 99));
                    // `owner=source` 的语义：**能力拥有者自己施�?*的才算。切片的施加点不�?谁施放的"�?
                    // 因此�?施加对象不是玩家"收窄 = 玩家施加给敌人（当前内容里敌人的同类施加一�?`target=player`�?
                    // 已逐条核对 enemies*.spire �?6 �?`applyPower(Vulnerable, �? target=player)`）�?
                    var matches = powerCommandsBindings.ResolvePowerApplied.And(
                        powerCommandsBindings.ResolvePowerApplied.Equal(powerCommandsBindings.ResolvePowerApplied.Param(powerCommandsBindings.AppliedPowerIdParam),
                            powerCommandsBindings.ResolvePowerApplied.Const(watched)),
                        powerCommandsBindings.ResolvePowerApplied.Not(powerCommandsBindings.ResolvePowerApplied.Equal(
                            powerCommandsBindings.ResolvePowerApplied.Param(powerCommandsBindings.AppliedOwnerParam),
                            powerCommandsBindings.ResolvePowerApplied.First(powerCommandsBindings.ResolvePowerApplied.Type(playerBindings.Player, battleBindings.BattleScope)))));
                    applied.Add(powerCommandsBindings.ResolvePowerApplied.If(matches,
                        powerCommandsBindings.ResolvePowerApplied.SetLocal(stacks,
                            powerCommandsBindings.ResolvePowerApplied.CallPure(powerQueryBindings.PowerStacksOf.Symbol,
                                powerCommandsBindings.ResolvePowerApplied.First(powerCommandsBindings.ResolvePowerApplied.Type(playerBindings.Player, battleBindings.BattleScope)),
                                powerCommandsBindings.ResolvePowerApplied.Const(watchedPower.Id)))));
                    applied.Add(powerCommandsBindings.ResolvePowerApplied.If(
                        powerCommandsBindings.ResolvePowerApplied.And(matches,
                            powerCommandsBindings.ResolvePowerApplied.Greater(powerCommandsBindings.ResolvePowerApplied.LocalRef(stacks),
                                powerCommandsBindings.ResolvePowerApplied.Const(0L))),
                        powerCommandsBindings.ResolvePowerApplied.Call(draw,
                            powerCommandsBindings.ResolvePowerApplied.Multiply(powerCommandsBindings.ResolvePowerApplied.LocalRef(stacks),
                                powerCommandsBindings.ResolvePowerApplied.Const(perStack)))));
                }

                powerCommandsBindings.ResolvePowerApplied.Body([.. applied]);
            }
        }

        return new DrawBindings(cardDraw, draw, autoPlayCommands);
    }

    internal static void CardPlay(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        EventsBindings eventsBindings,
        PowerCommandsBindings powerCommandsBindings,
        PowerRulesBindings powerRulesBindings,
        MonsterDamageBindings monsterDamageBindings,
        PlayerHpLossBindings playerHpLossBindings,
        PlayerBlockBindings playerBlockBindings,
        PlayedTriggerBindings playedTriggerBindings,
        DrawBindings drawBindings,
        HoldsPowerCallback HoldsPower,
        CardPilesCallback CardPiles,
        CompileEffectsCallback CompileEffects,
        DispatchCardHookCallback DispatchCardHook,
        ManifestEffectsCallback ManifestEffects,
        RemovePowerCallback RemovePower,
        HasTagCallback HasTag,
        CloneToHandCallback CloneToHand,
        EnergyGainBlockedCallback EnergyGainBlocked,
        InCombatCallback InCombat,
        BattleEndTailCallback BattleEndTail)
    {

        var payment = new CardPaymentRules(cardsBindings.CardSchema, battleBindings.CardKind, playerBindings.PlayerEnergy, contentBindings.Author, HoldsPower.Invoke, cardsBindings.CardDefinitions.Compiled) { Stars = playerBindings.PlayerStars };

        var piles = new CardPileSymbols(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay);

        var resultRules = new CardResultRules(cardsBindings.CardSchema, battleBindings.CardKind, contentBindings.Author, cardsBindings.CardDefinitions.Compiled, CardPiles(), HoldsPower.Invoke);

        var dispatch = contentBindings.Author is null ? null : new CardEffectDispatcher(cardsBindings.CardSchema, cardsBindings.CardDefinitions.Compiled, pilesBindings.Monsters,
            (c, program, unpowered) => CompileEffects(c.Scope, c.Card, c.Owner, c.Scope.LocalRef(c.Target),
                c.Target, c.DiscardChoice, c.DiscardChoices, c.Created, program, unpowered, c.Scope.LocalRef(c.PaidX), c.Scope.LocalRef(c.PaidStarX)));

        new CardPlayPipeline(cardsBindings.CardSchema, piles, pilesBindings.PlayHistory, pilesBindings.PlayedThisTurn, payment, resultRules,
            scope => scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
            c => c.Scope.And(c.Scope.Greater(c.Scope.Field(c.Owner, playerBindings.PlayerHp), c.Scope.Const(0L)),
                c.Scope.Greater(c.Scope.Count(c.Scope.Collection(pilesBindings.Monsters)), c.Scope.Const(0L))),
            c => dispatch?.PrepareTarget(c) ?? [],
            (c, hook) => DispatchCardHook(c.Scope, c.Card, hook),
            c => [],
            c => [.. dispatch?.Body(c) ?? ManifestEffects(c),
                .. new CardEnchantments(cardsBindings.CardDefinitions).AfterBody(c,
                    c.Scope.Greater(c.Scope.Field(c.Owner, playerBindings.PlayerHp), c.Scope.Const(0L)),
                    amount => [c.Scope.If(c.Scope.Not(EnergyGainBlocked(c.Scope)),
                        c.Scope.SetField(c.Owner, playerBindings.PlayerEnergy,
                            c.Scope.Add(c.Scope.Field(c.Owner, playerBindings.PlayerEnergy), amount)))])],
            c => [],
            c => contentBindings.Author is null ? [] : [c.Scope.Call(playedTriggerBindings.TriggerAfterPlayed.Symbol, c.Card)],
            RemovePower.Invoke, eventsBindings.CardPlayed, eventsBindings.CardPlayedCard, powerRulesBindings.PowerRules, pilesBindings.PlayedReveal).Build(pilesBindings.ExecuteCardPlay);

        powerRulesBindings.PowerRules.Build(new(powerCommandsBindings.ApplyPower.Symbol, drawBindings.Draw, playerHpLossBindings.LosePlayerHp.Symbol, monsterDamageBindings.DamageMonster.Symbol, playerBlockBindings.GainPlayerBlock.Symbol,
            pilesBindings.ExecuteCardPlay.Symbol, (s, c) => DispatchCardHook(s, c, AuthorSteps.HookOnEnterCombat), HasTag.Invoke,
            (s, c) => CloneToHand(s, c), (s, c) => DispatchCardHook(s, c, AuthorSteps.HookOnEnterCombat),
            s => s.And(s.Greater(s.Field(s.First(s.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerHp), s.Const(0L)), s.Exists(s.Collection(pilesBindings.Monsters))))
        {
            CanReceivePower = (s, target) => s.Or(s.Equal(target,s.First(s.Type(playerBindings.Player,battleBindings.BattleScope))),
                s.GreaterOrEqual(s.CollectionIndex(pilesBindings.Monsters,target),s.Const(0L))),
            GainEnergy = (s, amount) => [s.If(s.Not(EnergyGainBlocked(s)),
                    s.SetField(s.First(s.Type(playerBindings.Player,battleBindings.BattleScope)), playerBindings.PlayerEnergy,
                        s.Add(s.Field(s.First(s.Type(playerBindings.Player,battleBindings.BattleScope)), playerBindings.PlayerEnergy), amount)))]
        });

        var playCard = contentBindings.Package.Action("PlayCard");

        var playedCard = playCard.Parameter("Card", FieldTypeSchema.RefTo(cardsBindings.Card));

        var playBody = contentBindings.Package.ActionBody(playCard);

        var actingPlayer = playBody.First(playBody.Type(playerBindings.Player, battleBindings.BattleScope));

        var playedRef = playBody.Param(playedCard.DeclarationIndex);

        playBody.Role(SliceManifest.PlayerRole);

        playBody.Candidates("Card", playBody.Collection(pilesBindings.Hand));

        playBody.Requires(playBody.And(InCombat(playBody), playBody.And(playBody.And(payment.CanPay(playBody, playedRef, actingPlayer),
            playBody.Not(playBody.Field(playedRef, cardsBindings.CardUnplayable))),
            playBody.Greater(playBody.Count(playBody.Collection(pilesBindings.Monsters)), playBody.Const(0L)))));

        playBody.Body(playBody.Call(pilesBindings.ExecuteCardPlay.Symbol, playedRef, playBody.Const((long)CardEffectPile.Hand),
            playBody.Const(false), playBody.Const(false), playBody.Const(1L)), BattleEndTail(playBody));

    }

    internal static (FieldSymbol Field, Expr Value)[] CardFields(
        CardsBindings cardsBindings,
        IrScope scope,
        SliceCard definition,
        bool upgraded,
        bool unplayable = false,
        bool ethereal = false) => cardsBindings.CardDefinitions.Fields(scope, definition, upgraded, unplayable, ethereal);

    internal static IReadOnlyList<(FieldSymbol Field, Expr Value)> RewardCardFields(
        CardsBindings cardsBindings,
        IrScope scope,
        CardDefinition definition,
        bool upgraded) => cardsBindings.CardDefinitions.New(scope, definition, upgraded);

    internal static CardEffectStateBindings EffectState(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PowerQueryBindings powerQueryBindings,
        HasTagCallback HasTag,
        IrScope scope) => new CardEffectStateBindings(
                    Hand: pilesBindings.Hand, Draw: pilesBindings.DrawPile, Discard: pilesBindings.DiscardPile, Exhaust: pilesBindings.ExhaustPile,
                    CardKind: battleBindings.CardKind, Kind: cardsBindings.CardKindField,
                    Block: playerBindings.PlayerBlock, LostHpThisTurn: playerBindings.PlayerLostHp, ExhaustedCardThisTurn: playerBindings.PlayerExhaustedCard,
                    EnemyHp: enemiesBindings.MonsterHp, Variables: cardsBindings.VariableFields, PowerStacks: powerQueryBindings.PowerStacksOf.Symbol)
    {
        InPlay = pilesBindings.InPlay,
        Enemies = pilesBindings.Monsters,
        History = pilesBindings.PlayHistory,
        HpLossCount = playerBindings.PlayerHpLossCount,
        HasTag = (tag, instance) => HasTag(scope, instance, tag),

    };

    internal static Inst[] CompileEffects(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PowerCommandsBindings powerCommandsBindings,
        PowerRulesBindings powerRulesBindings,
        MonsterDamageBindings monsterDamageBindings,
        PlayerHpLossBindings playerHpLossBindings,
        DrawBindings drawBindings,
        EffectStateCallback EffectState,
        BlockGainStepsCallback BlockGainSteps,
        EnergyGainBlockedCallback EnergyGainBlocked,
        CardPilesCallback CardPiles,
        PileCollectionCallback PileCollection,
        ExhaustAttackAndGrowCallback ExhaustAttackAndGrow,
        DispatchCardHookCallback DispatchCardHook,
        IrScope scope,
        Expr cardRef,
        Expr owner,
        Expr selectedTarget,
        int targetSlot,
        int discardSlot,
        int discardSlotsSlot,
        int createdSlot,
        CardEffectProgram program,
        bool unpowered = false,
        Expr? paidX = null, Expr? paidStarX = null, BaseSpire.Slice.Domain.Combat.CombatOrbs? orbs = null,
        BaseSpire.Slice.Domain.Combat.CombatDoom? doom = null,
        Func<BaseSpire.Slice.Domain.Combat.CombatOsty>? osty = null,
        Func<IrScope, Expr, Expr, Expr>? damageModifier = null)
    {
        var expressions = new CardEffectExpressions(scope, EffectState(scope), cardRef, owner, selectedTarget, paidX) { PaidStarX = paidStarX };

        // A separate binding prevents a random hit from changing the player's selected target.
        var randomTargetSlot = scope.Local("effectRandomTarget", FieldTypeSchema.Binding());
        return CardEffectCompiler.Compile(scope, program, new CardEffectBindings(
            owner, selectedTarget, scope.Collection(pilesBindings.Monsters), randomTargetSlot,
            playerBindings.PlayerHp, playerBindings.PlayerMaxHp, enemiesBindings.MonsterHp, monsterDamageBindings.DamageMonster.Symbol, powerRulesBindings.PowerRules.ApplyOwnedPower.Symbol, drawBindings.Draw, playerHpLossBindings.LosePlayerHp.Symbol,
            expressions.Amount, expressions.Condition, amount => [.. BlockGainSteps(scope, amount, unpowered)],
            amount => [scope.If(scope.Not(EnergyGainBlocked(scope)), scope.SetField(owner, playerBindings.PlayerEnergy,
                    scope.Add(scope.Field(owner, playerBindings.PlayerEnergy), amount)))],
            (name, delta) => scope.SetField(cardRef, cardsBindings.VariableFields[name],
                scope.Add(scope.Field(cardRef, cardsBindings.VariableFields[name]), scope.Const(delta))),
            pile => scope.Collection(PileCollection(pile)),
            instance => scope.Equal(scope.Field(instance, cardsBindings.CardKindField), scope.EnumConst(battleBindings.CardKind, "Attack")),
            (from, to, card) => CardPiles().Move(scope, from, to, card),
            (from, cardId, upgraded, card) => CardPiles().Transform(scope, card, from, cardId, upgraded),
            unpowered)
        {
            PlayedCard = cardRef,
            ModifyDamage = damageModifier is null ? null : amount => damageModifier(scope, cardRef, amount),
            IsNonSlySkill = instance => scope.And(scope.Equal(scope.Field(instance,cardsBindings.CardKindField),scope.EnumConst(battleBindings.CardKind,"Skill")),
                scope.Not(cardsBindings.CardDefinitions.IsSly(scope,instance))),
            GrantSelectedSly = instance => [scope.SetField(instance,cardsBindings.CardSchema.SingleTurnSly,scope.Const(true))],
            AfterDiscardBatch = () => [powerRulesBindings.PowerRules.Call(scope,"AfterDiscardBatch")],
            Osty = (effect, amount) => [scope.If(scope.And(scope.Not(scope.Field(owner, playerBindings.PlayerBattleSettled)),
                scope.Exists(scope.Collection(pilesBindings.Monsters))), effect.Operation == "ostyAttack"
                ? osty!().Attack(scope, owner, selectedTarget, amount, monsterDamageBindings.DamageMonster.Symbol)
                : osty!().Change(scope, owner, effect.Operation, amount))],
            Orbs = (effect, amount) => orbs?.Emit(scope, effect, amount) ?? throw new InvalidOperationException("Orb runtime binding missing"),
            ResolveDoom = () => doom is null ? throw new InvalidOperationException("resolveDoom requires a registered Doom power") : [doom.Resolve(scope, true)],
            Forge = (effect, amount) =>
            {
                var forge = new BaseSpire.Slice.Domain.Combat.CombatForge(cardsBindings.CardDefinitions,
                    new CardPileSymbols(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay),
                    cardsBindings.VariableFields, (current, instance) => DispatchCardHook(current, instance, AuthorSteps.HookOnEnterCombat));
                return [scope.If(scope.And(scope.Not(scope.Field(owner, playerBindings.PlayerBattleSettled)),
                    scope.Exists(scope.Collection(pilesBindings.Monsters))), forge.Emit(scope, owner, effect, amount))];
            },
            Stars = (operation, amount) => new BaseSpire.Slice.Domain.Combat.CombatStars(playerBindings.PlayerStars,
                current => scope.Not(scope.Field(current, playerBindings.PlayerBattleSettled))).Change(scope, owner, operation, amount),
            CanUpgrade = card => cardsBindings.CardDefinitions.CanUpgrade(scope, card),
            UpgradeCard = card => [cardsBindings.CardDefinitions.Upgrade(scope, card)],
            CloneCard = (card, to) => CardPiles().Clone(scope, card, to),
            MoveChosenCard = (effect, card) => CardPiles().MoveChosen(scope, effect.From, effect.To, effect.Top, card),
            ModifyPlayedCost = delta => [scope.SetField(cardRef, cardsBindings.CardSchema.CostModifier,
                scope.Maximum(scope.Const(-9L), scope.Minimum(scope.Const(9L),
                    scope.Add(scope.Field(cardRef, cardsBindings.CardSchema.CostModifier), scope.Const(delta)))))],
            CreateDefinedCard = id =>
            {
                var created = scope.Local("generatedCard", FieldTypeSchema.RefTo(cardsBindings.Card));
                return [scope.CreateEntity(cardsBindings.Card, scope.EntityScope(owner), created,
                    cardsBindings.CardDefinitions.New(scope, cardsBindings.CardDefinitions.Definition(id), false), pilesBindings.Hand)];
            },
            RewritePlayedCard = id => [.. cardsBindings.CardDefinitions.New(scope,
                cardsBindings.CardDefinitions.Definition(id), false).Select(field => scope.SetField(cardRef, field.Field, field.Value))],
            ModifyPowerVariable = (id, variable, delta) => powerRulesBindings.PowerRules.ModifyVariable(scope, id, variable, delta),
            ExhaustAttackAndGrow = variable => ExhaustAttackAndGrow(scope, cardRef, variable),
            AutoPlaySelf = from => [scope.If(scope.And(scope.Greater(scope.CollectionIndex(PileCollection(from),cardRef),scope.Const(-1L)),
                        scope.And(scope.Greater(scope.Field(owner,playerBindings.PlayerHp),scope.Const(0L)),scope.Exists(scope.Collection(pilesBindings.Monsters)))),
                        scope.Call(pilesBindings.ExecuteCardPlay.Symbol,cardRef,scope.Const((long)from),scope.Const(true),scope.Const(false),scope.Const(1L)))],
            DrawUntilKind = kind => drawBindings.CardDraw.Until(scope, kind),
            AutoPlay = (count, force) => drawBindings.AutoPlayCommands.FromTop(scope, owner, count, force),
            SelectCards = (effect, apply) => [scope.If(scope.Exists(scope.Collection(pilesBindings.Monsters)),
                new CardSelectionProcedures(cardsBindings.CardSchema,
                new(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay),
                (s, instance) => s.Equal(s.Field(instance, cardsBindings.CardKindField), s.EnumConst(battleBindings.CardKind, "Attack")),
                (s, instance) => cardsBindings.CardDefinitions.CanUpgrade(s, instance))
                { IsNonSlySkill = (s,instance) => s.And(s.Equal(s.Field(instance,cardsBindings.CardKindField),s.EnumConst(battleBindings.CardKind,"Skill")),
                    s.Not(cardsBindings.CardDefinitions.IsSly(s,instance))) }.Emit(scope, effect, apply))],
            GenerateCards = (count, effect) => new CardGenerationRules(cardsBindings.CardSchema, cardsBindings.CardDefinitions,
                new(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay),
                (s, instance) => DispatchCardHook(s, instance, AuthorSteps.HookOnEnterCombat)).Emit(scope, owner, count, effect)
        });
    }

    internal static CollectionSymbol PileCollection(
        PilesBindings pilesBindings,
        CardEffectPile pile) => new CardPileSymbols(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay)[pile];

    internal static CardPileProcedures CardPiles(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        ExhaustTriggerBindings exhaustTriggerBindings,
        KnowledgeCommandsBindings knowledgeCommandsBindings,
        PowerRulesBindings powerRulesBindings,
        DispatchCardHookCallback DispatchCardHook) => new(cardsBindings.CardSchema, cardsBindings.CardDefinitions,
                new CardPileSymbols(pilesBindings.Hand, pilesBindings.DrawPile, pilesBindings.DiscardPile, pilesBindings.ExhaustPile, pilesBindings.InPlay),
                (scope, cardRef) => [
                    scope.SetField(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerExhaustedCard, scope.Const(true)),
                .. DispatchCardHook(scope, cardRef, AuthorSteps.HookOnExhaust), scope.Call(exhaustTriggerBindings.TriggerAfterExhaust.Symbol)],
                (scope, cardRef) => DispatchCardHook(scope, cardRef, AuthorSteps.HookOnEnterCombat),
                (scope, cardRef) => [.. powerRulesBindings.PowerRules.QueueSly(scope,cardRef),
                    .. DispatchCardHook(scope, cardRef, AuthorSteps.HookOnDiscard)],
                (scope, cardRef) => [scope.Call(knowledgeCommandsBindings.RecordKnownTop.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), cardRef)]);

    internal static Inst[] DispatchCardHook(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PowerRulesBindings powerRulesBindings,
        CompileEffectsCallback CompileEffects,
        IrScope scope,
        Expr cardRef,
        string hook) =>
                [.. hook == AuthorSteps.HookOnEnterCombat ? new Inst[] { powerRulesBindings.PowerRules.Call(scope,"EnterCard",cardRef) } : [],
                .. new CardEventDispatch(cardsBindings.CardSchema, cardsBindings.CardDefinitions.Compiled, (s, c, p) =>
                    CompileEffects(s, c, s.First(s.Type(playerBindings.Player, battleBindings.BattleScope)), s.First(s.Type(playerBindings.Player, battleBindings.BattleScope)), -1, -1, -1, -1, p)).Dispatch(scope, cardRef, hook)];

    internal static Inst[] ManifestEffects(
        BattleBindings battleBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PowerCommandsBindings powerCommandsBindings,
        MonsterDamageBindings monsterDamageBindings,
        PlayerBlockBindings playerBlockBindings,
        KnowledgeCommandsBindings knowledgeCommandsBindings,
        CardPlayContext c)
    {
        var scope = c.Scope;
        return [scope.IfElse(scope.Equal(scope.Field(c.Card, cardsBindings.CardKindField), scope.EnumConst(battleBindings.CardKind, "Attack")),
                [c.SelectTarget(pilesBindings.Monsters), scope.Call(monsterDamageBindings.DamageMonster.Symbol, c.Owner, scope.LocalRef(c.Target), scope.Field(c.Card, cardsBindings.CardAmount), scope.Const(true)),
                    scope.If(scope.Not(scope.Equal(scope.Field(c.Card, cardsBindings.CardPowerId), scope.Const(string.Empty))),
                        scope.Call(powerCommandsBindings.ApplyPower.Symbol, scope.LocalRef(c.Target), scope.Field(c.Card, cardsBindings.CardPowerId), scope.Field(c.Card, cardsBindings.CardPowerStacks), scope.Const(true), scope.Const(false)))],
                [scope.If(scope.Equal(scope.Field(c.Card, cardsBindings.CardKindField), scope.EnumConst(battleBindings.CardKind, "Skill")),
                    scope.Call(playerBlockBindings.GainPlayerBlock.Symbol, scope.Field(c.Card, cardsBindings.CardAmount)))]),
                scope.If(scope.Equal(scope.Field(c.Card, cardsBindings.CardDefinition), scope.Const(SliceManifest.HeadbuttId)),
                    scope.If(scope.Exists(scope.Collection(pilesBindings.DiscardPile)),
                        scope.Decide(scope.Const(Value.OfParticipant(SliceManifest.Player)), DecisionResponseKind.EntityRef,
                            SliceManifest.HeadbuttPrompt, c.DiscardChoice, scope.Collection(pilesBindings.DiscardPile), scope.Const(1L), scope.Const(1L)),
                        scope.RemoveFromCollection(pilesBindings.DiscardPile, scope.LocalRef(c.DiscardChoice)),
                        scope.AddToCollection(pilesBindings.DrawPile, scope.LocalRef(c.DiscardChoice), InsertPosition.Start),
                        scope.Call(knowledgeCommandsBindings.RecordKnownTop.Symbol, c.Owner, scope.LocalRef(c.DiscardChoice))))];
    }

    internal static Expr Innate(CardsBindings cardsBindings, IrScope s, Expr c)
    {
        Expr match = s.Const(false);
        foreach (var d in cardsBindings.CardDefinitions.Compiled.Where(c => c.Normal.Innate || c.Upgraded.Innate))
            match = s.Or(match, s.And(s.Equal(s.Field(c, cardsBindings.CardDefinition), s.Const(d.Id)),
                d.Normal.Innate == d.Upgraded.Innate ? s.Const(d.Normal.Innate) : d.Upgraded.Innate ? s.Field(c, cardsBindings.CardUpgraded) : s.Not(s.Field(c, cardsBindings.CardUpgraded))));
        return match;
    }

    internal static Inst[] CloneToHand(
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        DispatchCardHookCallback DispatchCardHook,
        IrScope s,
        Expr c)
    {
        var created = s.Local("powerClone", FieldTypeSchema.RefTo(cardsBindings.Card)); return [
            s.CreateEntity(cardsBindings.Card,s.EntityScope(c),created,cardsBindings.CardDefinitions.Copy(s,c,clone:true)),
                s.IfElse(s.Less(s.Count(s.Collection(pilesBindings.Hand)),s.Const((long)SliceManifest.MaxHandSize)),
                    [s.AddToCollection(pilesBindings.Hand,s.LocalRef(created))],[s.AddToCollection(pilesBindings.DiscardPile,s.LocalRef(created))]),
                ..DispatchCardHook(s,s.LocalRef(created),AuthorSteps.HookOnEnterCombat)];
    }

    internal static Inst[] ExhaustAttackAndGrow(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PowerQueryBindings powerQueryBindings,
        EffectStateCallback EffectState,
        CardPilesCallback CardPiles,
        IrScope s,
        Expr c,
        string variable)
    {
        var pick = s.Local("growthVictim", FieldTypeSchema.RefTo(cardsBindings.Card)); var amount = s.Local("growthDamage", FieldTypeSchema.Int(0, 9999));
        var fixedAmount = s.Local("growthFixed", FieldTypeSchema.Fixed()); var powerAmount = s.Local("growthPower", FieldTypeSchema.Int(-99, 99));
        var owner = s.First(s.Type(playerBindings.Player, battleBindings.BattleScope));
        var instructions = new List<Inst> { s.SamplePick(SliceManifest.RandomCardStream, s.Where(s.Collection(pilesBindings.Hand), x => s.Equal(s.Field(x, cardsBindings.CardKindField), s.EnumConst(battleBindings.CardKind, "Attack"))), pick) };
        var body = new List<Inst> { s.SetLocal(amount, s.Const(0L)) };
        var values = new CardEffectExpressions(s, EffectState(s), s.LocalRef(pick), owner, s.Const(Value.None), null);
        foreach (var d in cardsBindings.CardDefinitions.Compiled.Where(c => c.Kind == "Attack")) body.Add(s.If(s.Equal(s.Field(s.LocalRef(pick), cardsBindings.CardDefinition), s.Const(d.Id)),
            s.IfElse(s.Field(s.LocalRef(pick), cardsBindings.CardUpgraded),
                [s.SetLocal(amount, d.Upgraded.DamageValue is null ? s.Const(0L) : values.Amount(d.Upgraded.DamageValue))],
                [s.SetLocal(amount, d.Normal.DamageValue is null ? s.Const(0L) : values.Amount(d.Normal.DamageValue))])));
        body.Add(s.SetLocal(fixedAmount, s.IntToFixed(s.LocalRef(amount))));
        // Native Thrash applies outgoing modifiers with no selected target.
        foreach (var p in contentBindings.Author!.Powers.Where(p => p.DamageModifier == PowerDamage.AddPerStack)) body.Add(s.SetLocal(fixedAmount,
            s.Add(s.LocalRef(fixedAmount), s.IntToFixed(s.Multiply(s.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, s.Const(p.Id)), s.Const(long.Parse(p.DamageModifierValue)))))));
        foreach (var p in contentBindings.Author.Powers.Where(p => p.DamageModifier == PowerDamage.MultiplyOutgoing)) body.Add(s.If(s.Greater(s.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, s.Const(p.Id)), s.Const(0L)),
            s.SetLocal(fixedAmount, s.Multiply(s.LocalRef(fixedAmount), s.Const(decimal.Parse(p.DamageModifierValue, System.Globalization.CultureInfo.InvariantCulture))))));
        body.Add(s.SetField(c, cardsBindings.VariableFields[variable], s.Add(s.Field(c, cardsBindings.VariableFields[variable]), s.FixedToInt(s.Maximum(s.Const(0m), s.LocalRef(fixedAmount)), FixedRounding.Truncate))));
        body.AddRange(CardPiles().Move(s, CardEffectPile.Hand, CardEffectPile.Exhaust, s.LocalRef(pick)));
        instructions.Add(s.If(s.Not(s.IsNone(s.LocalRef(pick))), body.ToArray())); return [.. instructions];
    }

    internal static Expr HasTag(CardsBindings cardsBindings, IrScope scope, Expr instance, string tag)
    {
        if (!cardsBindings.TaggedDefinitions.TryGetValue(tag, out var members) || members.Count == 0)
        {
            return scope.Const(false);
        }

        var match = scope.Equal(scope.Field(instance, cardsBindings.CardDefinition), scope.Const(members[0]));
        for (var index = 1; index < members.Count; index++)
        {
            match = scope.Or(match,
                scope.Equal(scope.Field(instance, cardsBindings.CardDefinition), scope.Const(members[index])));
        }

        return match;
    }

}
