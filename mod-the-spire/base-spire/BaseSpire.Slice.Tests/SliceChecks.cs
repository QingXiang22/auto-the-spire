using CoreTheSpire.Engine;
using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// Executable checks for <c>base-spire:combat-slice-v1</c>.
///
/// B-01: the slice is declared through the generic content interface, links without STS knowledge in the engine,
/// freezes a versioned package, and builds the controlled initial state from the frozen source manifest.
/// B-02: cost and energy, damage with the recovered additive-then-multiplicative order, block absorption, health and
/// death, powers (apply, stack, Ritual, turn-end tick), the two-cultist enemy turn, discard reshuffling, and victory
/// or defeat.
/// </summary>
internal static class SliceChecks
{
    private const string PlayerType = "base-spire:Player";
    private const string MonsterType = "base-spire:Monster";
    private const string CardType = "base-spire:Card";
    private const string PlayCardId = "base-spire:PlayCard";
    private const string EndTurnId = "base-spire:EndTurn";

    public static int RunAll(bool includeMap = true)
    {
        EffectProgramChecks.Run();
        OrchestrationChecks.Run();
        AuthorContractChecks.Run();
        DeclarationChecks();
        InitialStateChecks();
        CombatChecks();
        SliceFrontendChecks.Run();
        SliceFrontendDomainChecks.Run();
        SliceFrontendHookChecks.Run();
        SliceFrontendPlayHookChecks.Run();
        SliceFrontendXCostChecks.Run();
        SliceFrontendRandomChecks.Run();
        SliceFrontendBatchChecks.Run();
        SliceFrontendConditionalChecks.Run();
        SliceFrontendMultiSelectChecks.Run();
        SliceFrontendEnemyProgramChecks.Run();
        SliceEnemyStateMachineChecks.Run();
        SliceEncounterQueueChecks.Run();
        SliceEnemyAddCardsChecks.Run();
        SliceFrailChecks.Run();
        SliceEncounterFormationChecks.Run();
        SliceConstrictChecks.Run();
        SliceByrdonisChecks.Run();
        SliceFrontendRetainChecks.Run();
        SliceFrontendPowerProgramChecks.Run();
        SliceFrontendDamageModifierChecks.Run();
        // Legacy interoperability checks were retired; the old implementation is available only in Git history.
        SliceFrontendAmountSourceChecks.Run();
        SliceFrontendInstanceVariableChecks.Run();
        SliceFrontendGenerationChecks.Run();
        SliceFrontendHookStepChecks.Run();
        SliceFrontendDamageCapChecks.Run();
        SliceFrontendPotionChecks.Run();
        SliceFrontendUnpoweredChecks.Run();
        SliceFrontendShrinkChecks.Run();
        SliceFrontendBlessingChecks.Run();
        SliceFrontendStatusCardChecks.Run();
        SliceFrontendBeckonChecks.Run();
        SliceFrontendBasicCardChecks.Run();
        SliceFrontendDrawChecks.Run();
        SliceFrontendApplyPowerAllChecks.Run();
        SliceFrontendRampageChecks.Run();
        SliceFrontendEntrenchChecks.Run();
        SliceFrontendInflameChecks.Run();
        SliceFrontendPowerTriggerChecks.Run();
        SliceFrontendPowerPlayedTriggerChecks.Run();
        SliceFrontendMultiPowerChecks.Run();
        SliceFrontendCrossEntityChecks.Run();
        SliceFrontendEnergyChecks.Run();
        SliceFrontendPlayConditionalChecks.Run();
        SliceFrontendScaledAmountChecks.Run();
        SliceFrontendCostModifierChecks.Run();
        SliceFrontendRunStateChecks.Run();
        SliceFormalSourceChecks.Run();
        SliceVanillaSeedLayoutChecks.Run();
        SliceFrontendNoDrawChecks.Run();
        SliceFrontendColossusChecks.Run();
        SliceFrontendPyreChecks.Run();
        SliceFrontendCrueltyChecks.Run();
        SliceFrontendPlatingChecks.Run();
        SliceFrontendTemporaryStrengthChecks.Run();
        SliceFrontendManglePowerChecks.Run();
        SliceFrontendMoltenFistChecks.Run();
        SliceFrontendUnmovableChecks.Run();
        SliceFrontendRampageVariableChecks.Run();
        SliceFrontendViciousChecks.Run();
        SliceFrontendObservationPurityChecks.Run();
        SliceFrontendRunWiringChecks.Run();
        SliceFrontendRunSettlementChecks.Run();
        SliceFrontendRoomAdvanceChecks.Run();
        SliceFrontendSettlementChecks.Run();
        SinglePlayerStartChecks.Run();
        // ENV-T05：Ironclad 补齐族的卡牌行为判别。必须排在身份 gate（下一行）之前，
        // 否则 `--combat-only` 会在身份断言上抛出、拿不到这些判别的反馈。
        SliceFrontendIroncladCompletionChecks.Run();
        SliceMapRoomChecks.DefaultIdentity();
        if (includeMap)
        {
            SliceMapRoomChecks.Run();
            TreasureRoomChecks.Run();
        }
        PublicSemanticsChecks.Run();
        ConsecutiveAttackChecks.Run();
        CloneCardChecks.Run();
        NativeAttackSequenceChecks.Run();
        // ENV-T06-source-stable-shuffle-20261012：源 `StableShuffle` 预排序（重洗）的判别面。
        SourceStableShuffleChecks.Run();
        return Check.Passed;
    }

    private static RuleSet Link(params EnginePackageBuilder[] builders)
    {
        var builder = new RuleSetBuilder();
        foreach (var package in builders) builder.Add(package.Build());
        return builder.Build();
    }

    private static Session NewBattle(bool includeHeadbutt = false)
    {
        var rules = Link(CombatSlicePackage.Build(includeHeadbutt));
        var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    // -- B-01 ---------------------------------------------------------------------------------------------------

    private static void DeclarationChecks()
    {
        Check.Run("B-01 the slice declares and links a frozen package version", () =>
        {
            var builder = CombatSlicePackage.Build();
            var package = builder.Build();
            Check.Equal(SliceManifest.PackageId, package.PackageId, "package id");
            Check.Equal(SliceManifest.Version, package.Version, "frozen slice version");
            Check.Equal(RuleSetBuilder.EngineVersion, package.EngineCompatibility, "engine compatibility");

            var rules = Link(CombatSlicePackage.Build());
            Check.Equal("base-spire@combat-slice-v1", rules.Packages[0].PackageId + "@" + rules.Packages[0].Version,
                "linked package identity");
            Check.True(rules.Identity.Length == 16, "identity is a stable hash");
            Check.Contains(rules.Actions, action => action.QualifiedId == PlayCardId, "PlayCard action");
            Check.Contains(rules.Actions, action => action.QualifiedId == EndTurnId, "EndTurn action");
            Check.Contains(rules.Events, e => e.QualifiedId == "base-spire:DamageDealt", "DamageDealt event");
            Check.Contains(rules.Events, e => e.QualifiedId == "base-spire:PlayerDamaged", "PlayerDamaged event");
            Check.Contains(rules.TerminalResults, t => t.QualifiedId == "base-spire:Victory", "Victory terminal");
            Check.Contains(rules.TerminalResults, t => t.QualifiedId == "base-spire:Defeat", "Defeat terminal");
            Check.Contains(rules.Projections, p => p.QualifiedId == "base-spire:PlayerView", "player projection");
        });

        Check.Run("B-01 core declares no STS concept and does not reference the content package", () =>
        {
            var core = typeof(RuleSetBuilder).Assembly;
            Check.DoesNotContain(core.GetReferencedAssemblies(),
                reference => reference.Name is not null &&
                             reference.Name.Contains("BaseSpire", StringComparison.OrdinalIgnoreCase),
                "the engine assembly must not reference the content package");
            Check.True(core.GetName().Name == "Core.TheSpire", "engine assembly");
        });

        Check.Run("B-01 the source manifest covers every slice item with a source, checked against sts2", () =>
        {
            Check.Equal(4, SliceManifest.Cards.Count, "slice cards");
            Check.Equal(3, SliceManifest.Powers.Count, "slice powers");
            Check.Equal(2, SliceManifest.Enemies.Count, "slice enemies");

            foreach (var item in SliceManifest.Cards.Cast<object>()
                         .Concat(SliceManifest.Powers).Concat(SliceManifest.Enemies))
            {
                var path = item switch
                {
                    SliceCard card => card.SourcePath,
                    SlicePower powerEntry => powerEntry.SourcePath,
                    SliceEnemy enemy => enemy.SourcePath,
                    _ => string.Empty,
                };
                var line = item switch
                {
                    SliceCard card => card.SourceLine,
                    SlicePower powerEntry => powerEntry.SourceLine,
                    SliceEnemy enemy => enemy.SourceLine,
                    _ => 0,
                };
                Check.True(path.EndsWith(".spire", StringComparison.Ordinal), $"source path for {item}");
                Check.True(line > 0, $"source line for {item}");
            }

            var strike = SliceManifest.Card(SliceManifest.StrikeId);
            Check.Equal(1, strike.Cost, "Strike cost");
            Check.Equal(6, strike.NormalAmount, "Strike damage");
            Check.Equal(9, strike.UpgradedAmount, "Strike upgraded damage");
            var defend = SliceManifest.Card(SliceManifest.DefendId);
            Check.Equal(5, defend.NormalAmount, "Defend block");
            Check.Equal(8, defend.UpgradedAmount, "Defend upgraded block");
            var bash = SliceManifest.Card(SliceManifest.BashId);
            Check.Equal(2, bash.Cost, "Bash cost");
            Check.Equal(8, bash.NormalAmount, "Bash damage");
            Check.Equal(10, bash.UpgradedAmount, "Bash upgraded damage");
            Check.Equal(2, bash.NormalPowerStacks, "Bash Vulnerable");
            Check.Equal(SliceManifest.VulnerableId, bash.AppliedPowerId, "Bash applies Vulnerable");
            var headbutt = SliceManifest.Card(SliceManifest.HeadbuttId);
            Check.Equal(9, headbutt.NormalAmount, "Headbutt damage");
            Check.Equal(12, headbutt.UpgradedAmount, "Headbutt upgraded damage");

            // The manifest follows the recovered source, not the legacy DSL, for the cultist health ranges.
            Check.Equal(51, SliceManifest.DampCultistNormalMinHp, "DampCultist normal minimum health (source)");
            Check.Equal(53, SliceManifest.DampCultistNormalMaxHp, "DampCultist normal maximum health (source)");
            Check.Equal(54, SliceManifest.DampCultistToughMaxHp, "DampCultist tough maximum health (source)");
            Check.Equal(41, SliceManifest.Enemy(SliceManifest.CalcifiedCultistId).NormalMaxHp,
                "CalcifiedCultist normal maximum health");
            Check.Equal(5, SliceManifest.RitualStacksNormal, "DampCultist Ritual stacks");
            Check.Equal(2, SliceManifest.Enemy(SliceManifest.CalcifiedCultistId).FirstMoveRitual,
                "CalcifiedCultist Ritual stacks");
            Check.Equal(9, SliceManifest.Enemy(SliceManifest.CalcifiedCultistId).StrikeDamageNormal,
                "CalcifiedCultist strike damage");
            Check.Equal(1.5m, SliceManifest.VulnerableDamageMultiplier, "Vulnerable is a flat x1.5");
            Check.Equal(0.75m, SliceManifest.WeakDamageMultiplier, "Weak is a flat x0.75");
            Check.Contains(SliceManifest.Powers, entry => entry.ContentId == SliceManifest.WeakId, "Weak is declared");
        });

        Check.Run("B-01 an incompatible engine version and a missing dependency are rejected", () =>
        {
            var incompatible = CombatSlicePackage.Build();
            incompatible.EngineCompatibility = "engine/ir@0.0";
            Check.Fault(EngineFaultCode.SchemaInvalid, () => Link(incompatible), "engine compatibility");

            var orphan = CombatSlicePackage.Build();
            orphan.Dependencies.Add("missing-package@1.0.0");
            Check.Fault(EngineFaultCode.MissingDependency, () => Link(orphan), "missing dependency");
        });

        Check.Run("B-01 another package can extend slice declarations by qualified id", () =>
        {
            var mod = new EnginePackageBuilder("test-mod", "1.0.0");
            mod.Dependencies.Add("base-spire@combat-slice-v1");
            mod.Extends.Add(PlayerType);
            mod.EntityType(PlayerType).AddField("Focus", FieldTypeSchema.Int(0, 99));

            var rules = Link(CombatSlicePackage.Build(), mod);
            var player = rules.TypeByQualifiedId(PlayerType);
            Check.Equal(rules.TypeByQualifiedId(PlayerType).Fields.Count - 1, player.FieldIndex("Focus"), "extension appended after all base player fields");
            Check.Equal("Focus", player.Fields[^1].Name, "appended field");
            Check.Equal(2, rules.Packages.Count, "both packages linked");
        });
    }

    private static void InitialStateChecks()
    {
        Check.Run("B-01 the controlled battle starts with the frozen player, cultists and deck", () =>
        {
            var session = NewBattle();
            var rules = session.RuleSet;
            var state = session.State;
            var playerType = rules.TypeByQualifiedId(PlayerType);
            var monsterType = rules.TypeByQualifiedId(MonsterType);
            var player = state.AllInstances(playerType.Id).Single();
            var cultists = Monsters(session);
            Check.Equal(2, cultists.Count, "two cultists");
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);
            var damp = Cultist(session, SliceManifest.DampCultistId);

            Check.Equal(SliceManifest.IroncladMaxHp, (int)Number(state, player, playerType, "Hp"), "player health");
            Check.Equal(SliceManifest.IroncladMaxHp, (int)Number(state, player, playerType, "MaxHp"),
                "player max health");
            Check.Equal(SliceManifest.EnergyPerTurn, (int)Number(state, player, playerType, "Energy"), "turn energy");
            Check.Equal(1L, Number(state, player, playerType, "Turn"), "turn counter");
            Check.Equal(ValueKind.Participant, state.GetField(player, playerType.FieldIndex("Owner")).Kind,
                "player owner participant");

            Check.Equal(38L, Number(state, front, monsterType, "Hp"), "front cultist health (range minimum)");
            Check.Equal(SliceManifest.DampCultistSliceHp, (int)Number(state, damp, monsterType, "Hp"),
                "damp cultist health (range minimum)");
            Check.Equal(SliceManifest.DampCultistNormalMaxHp, (int)Number(state, damp, monsterType, "MaxHp"),
                "damp cultist max health");
            Check.Equal(0L, Number(state, damp, monsterType, "Block"), "cultist block");
            Check.Equal(0L, Number(state, damp, monsterType, "MoveIndex"), "cultist move index");
            Check.Equal(2L, Number(state, front, monsterType, "FirstMoveRitual"), "front cultist Ritual stacks");
            Check.Equal(9L, Number(state, front, monsterType, "StrikeDamage"), "front cultist strike damage");

            var hand = Collection(rules, "base-spire:Hand");
            var drawPile = Collection(rules, "base-spire:DrawPile");
            Check.Equal(SliceManifest.DrawPerTurn, state.CollectionMembers(hand.Id).Count, "cards drawn");
            Check.Equal(SliceManifest.StarterDeck().Count - SliceManifest.DrawPerTurn,
                state.CollectionMembers(drawPile.Id).Count, "cards left in the draw pile");

            var deck = state.CollectionMembers(hand.Id).Concat(state.CollectionMembers(drawPile.Id)).ToArray();
            Check.Sequence(SliceManifest.StarterDeck(),
                deck.Select(cardInstance => Definition(state, rules, cardInstance)).ToArray(),
                "the frozen starter deck, in source order");
            Check.Equal(0, state.CollectionMembers(Collection(rules, "base-spire:DiscardPile").Id).Count,
                "empty discard pile");
        });

        Check.Run("B-01 cards with the same definition are still distinct instances", () =>
        {
            var session = NewBattle();
            var state = session.State;
            var cardType = session.RuleSet.TypeByQualifiedId(CardType);
            var strikes = state.CollectionMembers(Collection(session.RuleSet, "base-spire:Hand").Id)
                .Where(cardInstance => Definition(state, session.RuleSet, cardInstance) == SliceManifest.StrikeId)
                .ToArray();

            Check.Equal(5, strikes.Length, "five Strikes in the opening hand");
            Check.Equal(5, strikes.Distinct().Count(), "five distinguishable instances");
            foreach (var strike in strikes)
            {
                Check.Equal((long)SliceManifest.Card(SliceManifest.StrikeId).NormalAmount,
                    Number(state, strike, cardType, "Amount"), "identical definition values");
            }
        });

        Check.Run("B-01 the slice is playable through the declared actions", () =>
        {
            var session = NewBattle();
            var rules = session.RuleSet;
            var state = session.State;
            var playerType = rules.TypeByQualifiedId(PlayerType);
            var player = state.AllInstances(playerType.Id).Single();

            var playCard = Legal(session, PlayCardId);
            Check.Equal(SliceManifest.DrawPerTurn, playCard.Candidates.Count, "every hand card is playable");

            var strikeCard = playCard.Candidates.First(cardInstance =>
                Definition(state, rules, cardInstance) == SliceManifest.StrikeId);
            Submit(session, playCard, [Value.OfRef(strikeCard)], Cultist(session, SliceManifest.DampCultistId));

            Check.Equal((long)(SliceManifest.EnergyPerTurn - SliceManifest.Card(SliceManifest.StrikeId).Cost),
                Number(state, player, playerType, "Energy"), "the card spent one energy");
            Check.Equal(SliceManifest.DrawPerTurn - 1,
                state.CollectionMembers(Collection(rules, "base-spire:Hand").Id).Count, "the card left the hand");
            Check.True(state.IsMember(Collection(rules, "base-spire:DiscardPile").Id, strikeCard),
                "the card reached the discard pile");
            Check.True(state.IsMember(Collection(rules, "base-spire:PlayedThisTurn").Id, strikeCard),
                "the card is recorded in this turn's history");
        });

        Check.Run("B-01 ending the turn runs the declared flow in order", () =>
        {
            var session = NewBattle();
            var rules = session.RuleSet;
            var state = session.State;
            var playerType = rules.TypeByQualifiedId(PlayerType);
            var monsterType = rules.TypeByQualifiedId(MonsterType);
            var player = state.AllInstances(playerType.Id).Single();

            Submit(session, EndTurn(session), []);

            Check.Equal(2L, Number(state, player, playerType, "Turn"), "the next player turn started");
            Check.Equal(SliceManifest.DrawPerTurn,
                state.CollectionMembers(Collection(rules, "base-spire:Hand").Id).Count, "the hand was redrawn");
            Check.Equal(SliceManifest.DrawPerTurn,
                state.CollectionMembers(Collection(rules, "base-spire:DiscardPile").Id).Count,
                "the previous hand was discarded");
            foreach (var cultist in Monsters(session))
                Check.Equal(1L, Number(state, cultist, monsterType, "MoveIndex"), "the enemy turn acted once");
        });
    }

    // -- B-02 ---------------------------------------------------------------------------------------------------

    private static void CombatChecks()
    {
        Check.Run("B-02 an attack deals its source damage and block absorbs first", () =>
        {
            var session = NewBattle();
            var monsterType = session.RuleSet.TypeByQualifiedId(MonsterType);
            var target = Cultist(session, SliceManifest.CalcifiedCultistId);

            Play(session, SliceManifest.StrikeId, target);
            Check.Equal(38L - SliceManifest.Card(SliceManifest.StrikeId).NormalAmount,
                Number(session.State, target, monsterType, "Hp"), "Strike dealt six");

            // Give the cultist block and hit it again: block absorbs, health does not move.
            session.State.SetField(target, monsterType.FieldIndex("Block"), Value.Of(4L));
            Play(session, SliceManifest.StrikeId, target);
            Check.Equal(0L, Number(session.State, target, monsterType, "Block"), "block was consumed");
            Check.Equal(38L - SliceManifest.Card(SliceManifest.StrikeId).NormalAmount - 2,
                Number(session.State, target, monsterType, "Hp"), "only the unabsorbed two points reached health");
        });

        Check.Run("B-02 Bash applies Vulnerable and the next attack is multiplied by 1.5", () =>
        {
            var session = NewBattle();
            var monsterType = session.RuleSet.TypeByQualifiedId(MonsterType);
            var target = Cultist(session, SliceManifest.CalcifiedCultistId);
            var bash = SliceManifest.Card(SliceManifest.BashId);

            // The opening hand is five Strikes, so the Bash is brought in through a controlled draw.
            DrawSpecific(session, SliceManifest.BashId, target);

            var afterBash = 38L - bash.NormalAmount;
            Check.Equal(afterBash, Number(session.State, target, monsterType, "Hp"), "Bash dealt eight");
            Check.Equal((long)bash.NormalPowerStacks, PowerStacks(session, target, SliceManifest.VulnerableId),
                "Bash applied two Vulnerable");

            Play(session, SliceManifest.StrikeId, target);
            // 6 * 150 / 100 = 9: the multiplicative stage, truncated once.
            Check.Equal(afterBash - 9, Number(session.State, target, monsterType, "Hp"),
                "the vulnerable target took nine");
        });

        Check.Run("B-02 both cultists open with Ritual and their next turn uses the Strength", () =>
        {
            var session = NewBattle();
            var playerType = session.RuleSet.TypeByQualifiedId(PlayerType);
            var player = session.State.AllInstances(playerType.Id).Single();
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);
            var damp = Cultist(session, SliceManifest.DampCultistId);

            Submit(session, EndTurn(session), []);
            Check.Equal(2L, PowerStacks(session, front, SliceManifest.RitualId), "front cultist gained Ritual 2");
            Check.Equal(5L, PowerStacks(session, damp, SliceManifest.RitualId), "damp cultist gained Ritual 5");
            Check.Equal((long)SliceManifest.IroncladMaxHp, Number(session.State, player, playerType, "Hp"),
                "the Incantation turn deals no damage");
            Check.Equal(0L, PowerStacks(session, front, SliceManifest.StrengthId), "Ritual skipped its own turn");
            Check.Equal(0L, PowerStacks(session, damp, SliceManifest.StrengthId), "Ritual skipped its own turn");

            // Enemy turn two strikes with no Strength yet, then Ritual grants it at that turn's end.
            Submit(session, EndTurn(session), []);
            var plain = SliceManifest.Enemy(SliceManifest.CalcifiedCultistId).StrikeDamageNormal
                        + SliceManifest.Enemy(SliceManifest.DampCultistId).StrikeDamageNormal;
            Check.Equal((long)(SliceManifest.IroncladMaxHp - plain),
                Number(session.State, player, playerType, "Hp"), "9 + 1 points of unboosted damage");
            Check.Equal(2L, PowerStacks(session, front, SliceManifest.StrengthId), "front cultist has Strength 2");
            Check.Equal(5L, PowerStacks(session, damp, SliceManifest.StrengthId), "damp cultist has Strength 5");

            // Enemy turn three adds the gained Strength to every strike.
            Submit(session, EndTurn(session), []);
            var boosted = (SliceManifest.Enemy(SliceManifest.CalcifiedCultistId).StrikeDamageNormal + 2)
                          + (SliceManifest.Enemy(SliceManifest.DampCultistId).StrikeDamageNormal
                             + SliceManifest.RitualStacksNormal);
            Check.Equal((long)(SliceManifest.IroncladMaxHp - plain - boosted),
                Number(session.State, player, playerType, "Hp"), "11 + 6 points of strength-boosted damage");
        });

        Check.Run("B-02 a slain cultist leaves the living set and the last one ends the battle", () =>
        {
            var session = NewBattle();
            var playCard = Legal(session, PlayCardId);
            Check.Equal(SliceManifest.DrawPerTurn, playCard.Candidates.Count, "card candidates are the hand");
            Check.Equal(1, playCard.Definition.Program.Parameters.Count, "PlayCard takes only the card");

            // The target choice is a decision whose candidates are exactly the living cultists.
            session.Submit(playCard.Definition, SliceManifest.Player,
                [Value.OfRef(playCard.Candidates[0])]);
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "an attack asks for a target");
            var targetChoice = session.PendingDecision!;
            Check.Equal(2, targetChoice.Candidates.Count, "both cultists are targetable");
            session.Answer(new DecisionAnswer(targetChoice.RequestId, [Cultist(session, SliceManifest.CalcifiedCultistId)]));
            Check.Equal(SessionStatus.Running, session.Status, "the attack resolved after the target was chosen");

            var target = Cultist(session, SliceManifest.CalcifiedCultistId);
            var turns = 0;
            var slain = false;
            while (session.Status == SessionStatus.Running && turns < 12)
            {
                var living = Monsters(session);
                if (living.Count == 0) break;
                var current = living[0];
                AttackUntilOutOfAttacks(session, current);
                if (session.Status != SessionStatus.Running) break;
                if (!Monsters(session).Contains(current)) slain = true;
                if (Monsters(session).Count == 0) break;
                Submit(session, EndTurn(session), []);
                turns++;
            }

            Check.True(slain, "a slain cultist left the living set");
            Check.Equal(SessionStatus.Terminal, session.Status, "the battle ended");
            Check.Equal("base-spire:Victory", session.Terminal?.Definition.QualifiedId, "victory");
            Check.Equal(0, Monsters(session).Count, "no cultist is left alive");
            Check.False(Monsters(session).Contains(target), "the front cultist is gone");
        });

        Check.Run("B-02 relentless cultists end the battle in defeat", () =>
        {
            var session = NewBattle();
            var turns = 0;
            while (session.Status == SessionStatus.Running && turns < 12)
            {
                Submit(session, EndTurn(session), []);
                turns++;
            }

            Check.Equal(SessionStatus.Terminal, session.Status, "the battle ended");
            Check.Equal("base-spire:Defeat", session.Terminal?.Definition.QualifiedId, "defeat");
            Check.True(turns >= 4, $"the Ironclad survives several turns (ended after {turns})");
        });

        Check.Run("B-03 Headbutt damages, then puts a chosen discarded card on top of the draw pile", () =>
        {
            var session = NewBattle(includeHeadbutt: true);
            var rules = session.RuleSet;
            var state = session.State;
            var monsterType = rules.TypeByQualifiedId(MonsterType);
            var playerType = rules.TypeByQualifiedId(PlayerType);
            var player = state.AllInstances(playerType.Id).Single();
            var hand = Collection(rules, "base-spire:Hand").Id;
            var drawPile = Collection(rules, "base-spire:DrawPile").Id;
            var discard = Collection(rules, "base-spire:DiscardPile").Id;
            var inPlay = Collection(rules, "base-spire:InPlay").Id;

            // Controlled scenario: Headbutt in hand and exactly one known card in the discard pile.
            var headbutt = Relocate(session, SliceManifest.HeadbuttId, drawPile, hand);
            var discarded = Relocate(session, SliceManifest.StrikeId, hand, discard);
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);

            session.Submit(Legal(session, PlayCardId).Definition, SliceManifest.Player, [Value.OfRef(headbutt)]);
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "the attack asks for a target first");
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));

            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "then it asks which card to put back");
            var choice = session.PendingDecision!;
            Check.Sequence([discarded], choice.Candidates, "only the discarded card is offered");
            Check.Equal(SliceManifest.HeadbuttPrompt, choice.Prompt, "the recovered selection prompt");
            Check.True(state.IsMember(inPlay, headbutt), "the played card waits in play");
            Check.False(state.IsMember(discard, headbutt), "the played card cannot select itself");
            var energyAfterDamage = Number(state, player, playerType, "Energy");

            session.Answer(new DecisionAnswer(choice.RequestId, [discarded]));

            Check.Equal(SessionStatus.Running, session.Status, "the play finished");
            Check.Equal(38L - SliceManifest.Card(SliceManifest.HeadbuttId).NormalAmount,
                Number(state, front, monsterType, "Hp"), "the damage resolved exactly once");
            Check.Equal(energyAfterDamage, Number(state, player, playerType, "Energy"),
                "resuming did not spend energy again");
            Check.Equal(discarded.Value, state.CollectionMembers(drawPile)[0].Value,
                "the chosen card is on top of the draw pile");
            Check.False(state.IsMember(discard, discarded), "the chosen card left the discard pile");
            Check.True(state.IsMember(discard, headbutt), "Headbutt landed in its result area after resolving");
            Check.False(state.IsMember(inPlay, headbutt), "nothing is left in play");
        });

        Check.Run("B-03 an empty discard pile asks for no choice", () =>
        {
            var session = NewBattle(includeHeadbutt: true);
            var rules = session.RuleSet;
            var monsterType = rules.TypeByQualifiedId(MonsterType);
            var hand = Collection(rules, "base-spire:Hand").Id;
            var headbutt = Relocate(session, SliceManifest.HeadbuttId,
                Collection(rules, "base-spire:DrawPile").Id, hand);
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);

            session.Submit(Legal(session, PlayCardId).Definition, SliceManifest.Player, [Value.OfRef(headbutt)]);
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));

            Check.Equal(SessionStatus.Running, session.Status, "no choice is requested");
            Check.Equal(38L - SliceManifest.Card(SliceManifest.HeadbuttId).NormalAmount,
                Number(session.State, front, monsterType, "Hp"), "the damage still applied");
            Check.True(session.State.IsMember(Collection(rules, "base-spire:DiscardPile").Id, headbutt),
                "Headbutt is discarded as usual");
        });

        Check.Run("B-04 the first snapshot already states a known absolute position", () =>
        {
            var session = NewBattle(includeHeadbutt: true);
            var rules = session.RuleSet;
            var drawPile = Collection(rules, "base-spire:DrawPile").Id;
            var hand = Collection(rules, "base-spire:Hand").Id;
            var headbutt = Relocate(session, SliceManifest.HeadbuttId, drawPile, hand);
            var chosen = Relocate(session, SliceManifest.StrikeId, hand,
                Collection(rules, "base-spire:DiscardPile").Id);
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);

            session.Submit(Legal(session, PlayCardId).Definition, SliceManifest.Player, [Value.OfRef(headbutt)]);
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [chosen]));

            // A fresh observation of the resulting state, with no history available, must already carry the fact.
            var view = session.Observe(SliceManifest.Player, "base-spire:PlayerView");
            var knownType = rules.TypeByQualifiedId("base-spire:KnownCard");
            var fact = view.Instances.Single(instance => instance.TypeId == knownType.QualifiedId);
            Check.Equal(chosen, fact.Fields.Single(field => field.Name == "Card").Value.AsRef(),
                "the fact references the real card instance");
            Check.Equal(SliceManifest.DrawPileName,
                fact.Fields.Single(field => field.Name == "Pile").Value.AsString(), "the known pile");
            Check.Equal(0L, fact.Fields.Single(field => field.Name == "Position").Value.AsInt(),
                "position zero, without any history");

            // The protocol's closure holds without any history: the stated card is an authorized member of the hidden
            // pile and an entity of the snapshot, and naming it did not add a member.
            var draw = view.Collections.Single(collection => collection.QualifiedId == "base-spire:DrawPile");
            Check.Sequence([chosen], draw.RevealedMembers, "the known card is the authorized pile member");
            Check.Equal(0, draw.Members.Count, "the rest of the hidden pile stays unlisted");
            Check.True(draw.Size == session.State.CollectionMembers(drawPile).Count,
                "the position record did not change the pile's true size");
            Check.Contains(view.Instances, instance => instance.Id == chosen,
                "the referenced instance is published as an entity");
            Check.Contains(view.Relations, relation => relation.QualifiedId == "base-spire:KnownReveal" &&
                    relation.Targets.Contains(chosen), "the reveal edge names the authorized instance");
        });

        Check.Run("B-04 a hidden pile leaks neither its members nor its order", () =>
        {
            var session = NewBattle();
            var rules = session.RuleSet;
            var view = session.Observe(SliceManifest.Player, "base-spire:PlayerView");
            var drawPile = view.Collections.Single(collection => collection.QualifiedId == "base-spire:DrawPile");
            Check.True(drawPile.Size == SliceManifest.DrawPerTurn, "the draw pile size is public");
            Check.Equal(0, drawPile.Members.Count, "its members are not listed");

            var cardType = rules.TypeByQualifiedId(CardType);
            var drawPileId = Collection(rules, "base-spire:DrawPile").Id;
            foreach (var instance in view.Instances.Where(instance => instance.TypeId == cardType.QualifiedId))
            {
                Check.False(session.State.IsMember(drawPileId, instance.Id),
                    "no card that only sits in the hidden draw pile is projected");
            }

            Check.Equal(SliceManifest.DrawPerTurn,
                view.Instances.Count(instance => instance.TypeId == cardType.QualifiedId),
                "the whole hand is visible");

            // Resources, powers and intents are public facts too.
            var playerType = rules.TypeByQualifiedId(PlayerType);
            var player = view.Instances.Single(instance => instance.TypeId == playerType.QualifiedId);
            Check.Contains(player.Fields, field => field.Name == "Hp", "health is observable");
            Check.Contains(player.Fields, field => field.Name == "Energy", "energy is observable");
            var monsterType = rules.TypeByQualifiedId(MonsterType);
            foreach (var creature in view.Instances.Where(instance => instance.TypeId == monsterType.QualifiedId))
            {
                Check.Equal(SliceManifest.IncantationMove,
                    creature.Fields.Single(field => field.Name == "NextMove").Value.AsString(),
                    "the opening intent is observable");
            }
        });

        Check.Run("B-04 drawing a known card revokes its position", () =>
        {
            var session = NewBattle(includeHeadbutt: true);
            var rules = session.RuleSet;
            var drawPile = Collection(rules, "base-spire:DrawPile").Id;
            var hand = Collection(rules, "base-spire:Hand").Id;
            var headbutt = Relocate(session, SliceManifest.HeadbuttId, drawPile, hand);
            var chosen = Relocate(session, SliceManifest.StrikeId, hand,
                Collection(rules, "base-spire:DiscardPile").Id);
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);

            session.Submit(Legal(session, PlayCardId).Definition, SliceManifest.Player, [Value.OfRef(headbutt)]);
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [chosen]));
            Check.Equal(1, KnownFacts(session).Count, "one fact is known");
            var fact = KnownFacts(session).Single();
            var knownType = rules.TypeByQualifiedId("base-spire:KnownCard");

            Submit(session, EndTurn(session), []);
            Submit(session, EndTurn(session), []);
            Check.True(session.State.IsMember(hand, chosen), "the known card came back to the hand");
            Check.Equal(0, KnownFacts(session).Count, "its position is no longer known");
            // "Forget" must **destroy** the fact, not just take it out of the area: a fact left alive is still a
            // projected entity, so a consumer would keep reading a position the player no longer knows.
            Check.DoesNotContain(session.State.AllInstances(knownType.Id), instance => instance == fact,
                "a revoked fact must not survive as a live instance");
        });

        Check.Run("B-04 a reshuffle voids every known position", () =>
        {
            var session = NewBattle(includeHeadbutt: true);
            var rules = session.RuleSet;
            var drawPile = Collection(rules, "base-spire:DrawPile").Id;
            var hand = Collection(rules, "base-spire:Hand").Id;
            var headbutt = Relocate(session, SliceManifest.HeadbuttId, drawPile, hand);
            var chosen = Relocate(session, SliceManifest.StrikeId, hand,
                Collection(rules, "base-spire:DiscardPile").Id);
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);

            session.Submit(Legal(session, PlayCardId).Definition, SliceManifest.Player, [Value.OfRef(headbutt)]);
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [chosen]));
            Check.Equal(1, KnownFacts(session).Count, "one fact is known");

            Submit(session, EndTurn(session), []);
            Submit(session, EndTurn(session), []);
            Check.True(session.Random.DrawCount(SliceManifest.DrawStream) > 0, "a reshuffle happened");
            Check.Equal(0, KnownFacts(session).Count, "a shuffle voids every known position");
        });

        // -- R-DAMAGE: fixed-point damage, one truncation, Weak and Vulnerable ---------------------------------

        Check.Run("R-DAMAGE base 5 with Weak and Vulnerable truncates once at the end, not in the middle", () =>
        {
            // Authority: docs/review/tofix/r-damage-source-evidence-2026-10-01.md §3.1. The source computes
            // 5 x 0.75 x 1.5 = 5.625 in decimal and truncates once at Creature.cs:449, so the target loses 5. The
            // legacy backend floors inside the multiplicative stage (State.cs:598 then :609) and loses 4. A value of
            // 4 here is the failure this check exists for; base 9 pins the other direction (10.125 -> 10, mid = 9).
            var shortCase = DamageScenario(attackerWeak: 3, targetVulnerable: 2);
            // The chain needs a fixed-point scale fine enough not to round 5 x 0.75 x 1.5 = 5.625 onto an integer
            // before the one conversion. The engine's declared profile is scale 2 with midpoint-to-even, which the
            // implementation report shows is sufficient for the slice's multipliers; asserting it here keeps that a
            // checked assumption instead of an implicit one, because a coarser scale or another midpoint rule can
            // round an intermediate across an integer.
            Check.Equal(2, shortCase.Session.RuleSet.NumericProfile.FixedScale, "the slice's fixed-point scale");
            Check.Equal(MidpointRounding.ToEven, shortCase.Session.RuleSet.NumericProfile.Rounding,
                "the fixed-point midpoint rule");
            PlayStrikeFor(shortCase.Session, 5, shortCase.Target);
            Check.Equal(38L - 5, TargetHp(shortCase), "5 x 0.75 x 1.5 gives 5, not an intermediate 4");

            var longCase = DamageScenario(attackerWeak: 3, targetVulnerable: 2);
            PlayStrikeFor(longCase.Session, 9, longCase.Target);
            Check.Equal(38L - 10, TargetHp(longCase), "9 x 0.75 x 1.5 gives 10, not an intermediate 9");
        });

        Check.Run("R-DAMAGE Vulnerable and Weak stacks are a duration, not a multiplier", () =>
        {
            // Authority: VulnerablePower.cs:22 (DamageIncrease is a flat 1.5m), WeakPower.cs:22 (DamageDecrease is a
            // flat 0.75m) and :59-65/:48-54 (the stacks tick down). A present instance with no stacks is not
            // "present" for damage purposes: the source cannot even attach one (PowerCmd.cs:103 returns on
            // amount == 0), so testing stacks > 0 is the conservative equivalent.
            var two = DamageScenario(attackerWeak: 0, targetVulnerable: 2);
            PlayStrikeFor(two.Session, 6, two.Target);
            Check.Equal(2L, PowerStacks(two.Session, two.Target, SliceManifest.VulnerableId),
                "the target really carries two Vulnerable");
            Check.Equal(38L - 9, TargetHp(two), "6 x 1.5 = 9 with two stacks");

            var three = DamageScenario(attackerWeak: 0, targetVulnerable: 3);
            PlayStrikeFor(three.Session, 6, three.Target);
            Check.Equal(3L, PowerStacks(three.Session, three.Target, SliceManifest.VulnerableId),
                "the target really carries three Vulnerable");
            Check.Equal(TargetHp(two), TargetHp(three), "three stacks deal exactly what two stacks deal");

            var empty = DamageScenario(attackerWeak: 0, targetVulnerable: 0);
            PlayStrikeFor(empty.Session, 6, empty.Target);
            Check.Equal(0L, PowerStacks(empty.Session, empty.Target, SliceManifest.VulnerableId),
                "the target carries an empty Vulnerable instance");
            Check.Equal(38L - 6, TargetHp(empty), "an empty Vulnerable instance does not multiply");

            // The same presence rule on the dealer side: a Weak instance with no stacks must not shrink the attack.
            var weakTwo = DamageScenario(attackerWeak: 2, targetVulnerable: 0);
            PlayStrikeFor(weakTwo.Session, 8, weakTwo.Target);
            Check.Equal(38L - 6, TargetHp(weakTwo), "8 x 0.75 = 6 with two Weak stacks");
            var weakEmpty = DamageScenario(attackerWeak: 0, targetVulnerable: 0);
            PlayStrikeFor(weakEmpty.Session, 8, weakEmpty.Target);
            Check.Equal(38L - 8, TargetHp(weakEmpty), "an empty Weak instance does not multiply");
        });

        Check.Run("R-DAMAGE the player takes the same chain: attacking Weak and player Vulnerable", () =>
        {
            // Player side of the multiplicative stage, which the slice did not cover before R-DAMAGE: the front
            // cultist's 9 damage with Weak x0.75 and the player's Vulnerable x1.5 is 10.125 -> 10, and the damp
            // cultist's 1 damage is 1.5 -> 1. Truncating inside the stage instead gives 9 + 1 = 10 lost, not 11.
            var session = NewBattle();
            var playerType = session.RuleSet.TypeByQualifiedId(PlayerType);
            var player = session.State.AllInstances(playerType.Id).Single();
            var monsterType = session.RuleSet.TypeByQualifiedId(MonsterType);
            var front = Cultist(session, SliceManifest.CalcifiedCultistId);
            GivePower(session, front, SliceManifest.WeakId, 1);
            GivePower(session, player, SliceManifest.VulnerableId, 2);

            // The first enemy turn is both cultists' Incantation, so no damage is dealt yet and neither gains
            // Strength from the Ritual it just applied.
            Submit(session, EndTurn(session), []);
            Check.Equal((long)SliceManifest.IroncladMaxHp, Number(session.State, player, playerType, "Hp"),
                "the Incantation turn deals no damage");
            Check.Equal(0L, PowerStacks(session, front, SliceManifest.StrengthId), "the Ritual skipped its own turn");

            Submit(session, EndTurn(session), []);
            Check.Equal((long)(SliceManifest.IroncladMaxHp - 10 - 1), Number(session.State, player, playerType, "Hp"),
                "9 x 0.75 x 1.5 = 10 and 1 x 1.5 = 1, each truncated once");
            Check.Equal(2L, PowerStacks(session, player, SliceManifest.VulnerableId),
                "the player's Vulnerable is still there: player-side powers do not tick in the slice yet");
        });

        Check.Run("R-DAMAGE stacking a power does not re-arm its skipped first turn end", () =>
        {
            // Adjudication of the only visible difference the source evidence found between the backends
            // (r-damage-source-evidence §5.3): sts2 re-arms the skip only when it attaches a new instance
            // (PowerCmd.cs:78-84 and :144-147); stacking goes through ModifyAmount (:215-271), which never touches
            // it. The legacy backend re-arms on every application (State.cs:553-555) and so keeps the debuff one
            // turn longer. The slice follows the source.
            //
            // Ritual is the slice's only skipped turn-end behaviour, so the scenario replays the cultist's first
            // move by putting its move index back to zero: the second Incantation stacks onto the existing Ritual.
            var session = NewBattle();
            var monsterType = session.RuleSet.TypeByQualifiedId(MonsterType);
            var damp = Cultist(session, SliceManifest.DampCultistId);

            Submit(session, EndTurn(session), []);
            Check.Equal((long)SliceManifest.RitualStacksNormal, PowerStacks(session, damp, SliceManifest.RitualId),
                "the first Incantation applied its Ritual");
            Check.Equal(0L, PowerStacks(session, damp, SliceManifest.StrengthId),
                "and the turn it was applied skipped its grant");

            session.State.RunAtomic(() =>
                session.State.SetField(damp, monsterType.FieldIndex("MoveIndex"), Value.Of(0L)));
            Submit(session, EndTurn(session), []);

            var doubled = SliceManifest.RitualStacksNormal * 2;
            Check.Equal((long)doubled, PowerStacks(session, damp, SliceManifest.RitualId),
                "the second Incantation stacked onto the existing Ritual");
            Check.Equal((long)doubled, PowerStacks(session, damp, SliceManifest.StrengthId),
                "stacking did not re-arm the skip, so the doubled Ritual granted its Strength that turn");
        });

        Check.Run("B-02 the draw pile reshuffles from the discard pile with the package's named stream", () =>
        {
            var session = NewBattle();
            var rules = session.RuleSet;
            var hand = Collection(rules, "base-spire:Hand");
            var discard = Collection(rules, "base-spire:DiscardPile");
            Check.Equal(0L, session.Random.DrawCount(SliceManifest.DrawStream), "no reshuffle yet");

            // Turn one and two consume the whole ten-card deck, so turn three must reshuffle.
            Submit(session, EndTurn(session), []);
            Submit(session, EndTurn(session), []);

            Check.Equal(SliceManifest.DrawPerTurn, session.State.CollectionMembers(hand.Id).Count,
                "the hand was refilled from the reshuffled pile");
            Check.True(session.Random.DrawCount(SliceManifest.DrawStream) > 0,
                "the package's named stream shuffled the discard pile");
            var everywhere = session.State.CollectionMembers(hand.Id)
                .Concat(session.State.CollectionMembers(discard.Id))
                .Concat(session.State.CollectionMembers(Collection(rules, "base-spire:DrawPile").Id))
                .Select(cardInstance => Definition(session.State, rules, cardInstance))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Check.Equal(SliceManifest.StarterDeck().Count, everywhere.Length, "every card is still accounted for");
            Check.Sequence(
                SliceManifest.StarterDeck().OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                everywhere,
                "no card was lost or duplicated by the reshuffle");
        });
    }

    // -- helpers ------------------------------------------------------------------------------------------------

    /// <summary>One controlled R-DAMAGE scenario: a living player with the given Weak and a Vulnerable cultist.</summary>
    private sealed record DamageCase(Session Session, InstanceId Target, EntityTypeDefinition MonsterType);

    private static long TargetHp(DamageCase scenario) =>
        Number(scenario.Session.State, scenario.Target, scenario.MonsterType, "Hp");

    /// <summary>
    /// Sets up a battle where only the declared dealers have powers: the player (the attacker) carries Weak and the
    /// front cultist (the target) carries Vulnerable. The powers are declared directly so a scenario can use stack
    /// counts, including zero, that no slice card applies; nothing else contributes to the damage.
    /// </summary>
    private static DamageCase DamageScenario(long attackerWeak, long targetVulnerable)
    {
        var session = NewBattle();
        var playerType = session.RuleSet.TypeByQualifiedId(PlayerType);
        var attacker = session.State.AllInstances(playerType.Id).Single();
        var target = Cultist(session, SliceManifest.CalcifiedCultistId);
        GivePower(session, attacker, SliceManifest.WeakId, attackerWeak);
        GivePower(session, target, SliceManifest.VulnerableId, targetVulnerable);
        return new DamageCase(session, target, session.RuleSet.TypeByQualifiedId(MonsterType));
    }

    /// <summary>Declares a power instance on an owner, the same shape the package's ApplyPower produces.</summary>
    private static void GivePower(Session session, InstanceId owner, string powerId, long stacks,
        bool ticks = false, bool skipNextTick = false)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        session.State.RunAtomic(() =>
        {
            var instance = session.State.CreateInstance(powerType.Id, session.State.ScopeOf(owner));
            session.State.SetField(instance, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            session.State.SetField(instance, powerType.FieldIndex("Stacks"), Value.Of(stacks));
            session.State.SetField(instance, powerType.FieldIndex("Ticks"), Value.Of(ticks));
            session.State.SetField(instance, powerType.FieldIndex("SkipNextTick"), Value.Of(skipNextTick));
            session.State.AddRelation(relation.Id, owner, instance);
        });
    }

    /// <summary>Plays a Strike whose base damage is set explicitly, so a counterexample can use an exact amount.</summary>
    private static void PlayStrikeFor(Session session, long amount, InstanceId target)
    {
        var rules = session.RuleSet;
        var cardType = rules.TypeByQualifiedId(CardType);
        var hand = Collection(rules, "base-spire:Hand").Id;
        var drawPile = Collection(rules, "base-spire:DrawPile").Id;
        var card = session.State.CollectionMembers(hand).Concat(session.State.CollectionMembers(drawPile))
            .FirstOrDefault(candidate => Definition(session.State, rules, candidate) == SliceManifest.StrikeId);
        if (card == default)
            throw new CheckFailedException("the slice has no Strike in hand or draw pile");

        session.State.RunAtomic(() =>
        {
            if (!session.State.IsMember(hand, card))
            {
                session.State.RemoveFromCollection(drawPile, card);
                session.State.AddToCollection(hand, card);
            }

            session.State.SetField(card, cardType.FieldIndex("Amount"), Value.Of(amount));
        });
        Submit(session, Legal(session, PlayCardId), [Value.OfRef(card)], target);
    }

    /// <summary>The pack-declared known-card facts currently in a session (a public area, so directly readable).</summary>
    private static IReadOnlyList<InstanceId> KnownFacts(Session session) =>
        session.State.CollectionMembers(Collection(session.RuleSet, "base-spire:Known").Id);

    /// <summary>Controlled scenario helper: moves the first card of the named definition between two areas.</summary>
    private static InstanceId Relocate(Session session, string cardId, int from, int to)
    {
        var card = session.State.CollectionMembers(from)
            .FirstOrDefault(candidate => Definition(session.State, session.RuleSet, candidate) == cardId);
        if (card == default)
            throw new CheckFailedException($"no '{cardId}' card in the source area");
        session.State.RunAtomic(() =>
        {
            session.State.RemoveFromCollection(from, card);
            session.State.AddToCollection(to, card);
        });
        return card;
    }

    private static void Play(Session session, string cardId, InstanceId target) =>
        DrawSpecific(session, cardId, target);

    /// <summary>Brings the named card into the hand if it is still in the draw pile, then plays it at the target.</summary>
    private static void DrawSpecific(Session session, string cardId, InstanceId target)
    {
        var rules = session.RuleSet;
        var hand = Collection(rules, "base-spire:Hand").Id;
        var drawPile = Collection(rules, "base-spire:DrawPile").Id;
        var card = session.State.CollectionMembers(hand).Concat(session.State.CollectionMembers(drawPile))
            .FirstOrDefault(candidate => Definition(session.State, rules, candidate) == cardId);
        if (card == default)
            throw new CheckFailedException($"the slice has no '{cardId}' card in hand or draw pile");

        if (!session.State.IsMember(hand, card))
        {
            session.State.RunAtomic(() =>
            {
                session.State.RemoveFromCollection(drawPile, card);
                session.State.AddToCollection(hand, card);
            });
        }

        Submit(session, Legal(session, PlayCardId), [Value.OfRef(card)], target);
    }

    private static void AttackUntilOutOfAttacks(Session session, InstanceId target)
    {
        var cardType = session.RuleSet.TypeByQualifiedId(CardType);
        while (session.Status == SessionStatus.Running)
        {
            var playCard = session.LegalActions(SliceManifest.Player)
                .FirstOrDefault(action => action.Definition.QualifiedId == PlayCardId);
            if (playCard is null) return;
            // 只从**合法绑定**（guard 已通过）里挑攻击牌：`Candidates` 是全部手牌，按它贪心会在
            // "最前面的攻击牌付不起、但同手牌里有付得起的牌"时递交引擎守卫不接受的牌。
            // 源 `StableShuffle` 预排序把 BASH 稳定排到 DEFEND/STRIKE 之前后，这种手牌会真实出现。
            var attack = playCard.Bindings
                .Select(binding => binding[0].AsRef())
                .FirstOrDefault(cardInstance =>
                    Number(session.State, cardInstance, cardType, "Amount") > 0 &&
                    session.State.GetField(cardInstance, cardType.FieldIndex("Kind")).AsEnumOrdinal() == 0);
            if (attack == default) return;
            Submit(session, playCard, [Value.OfRef(attack)], target);
        }
    }

    private static void Submit(Session session, LegalAction action, Value[] arguments, InstanceId? target = null)
    {
        session.Submit(action.Definition, SliceManifest.Player, arguments);
        if (session.Status != SessionStatus.AwaitingDecision) return;
        var pending = session.PendingDecision!;
        var chosen = target is null || !pending.Candidates.Contains(target.Value)
            ? pending.Candidates[0]
            : target.Value;
        session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
    }

    private static LegalAction Legal(Session session, string actionId) =>
        session.LegalActions(SliceManifest.Player).Single(action => action.Definition.QualifiedId == actionId);

    private static LegalAction EndTurn(Session session) => Legal(session, EndTurnId);

    private static IReadOnlyList<InstanceId> Monsters(Session session) =>
        session.State.CollectionMembers(Collection(session.RuleSet, "base-spire:Monsters").Id);

    private static InstanceId Cultist(Session session, string definitionId)
    {
        var rules = session.RuleSet;
        var monsterType = rules.TypeByQualifiedId(MonsterType);
        return Monsters(session).Single(cultist =>
            session.State.GetField(cultist, monsterType.FieldIndex("DefinitionId")).AsString() == definitionId);
    }

    private static long PowerStacks(Session session, InstanceId owner, string powerId)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var powerInstance in session.State.RelationTargets(relation.Id, owner))
        {
            if (session.State.GetField(powerInstance, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
                return session.State.GetField(powerInstance, powerType.FieldIndex("Stacks")).AsInt();
        }

        return 0L;
    }

    private static CollectionDefinition Collection(RuleSet rules, string qualifiedId) =>
        rules.Collections.Single(collection => collection.QualifiedId == qualifiedId);

    private static string Definition(SessionState state, RuleSet rules, InstanceId cardInstance) =>
        state.GetField(cardInstance, rules.TypeByQualifiedId(CardType).FieldIndex("DefinitionId")).AsString();

    private static long Number(SessionState state, InstanceId instance, EntityTypeDefinition type, string field) =>
        state.GetField(instance, type.FieldIndex(field)).AsInt();
}
