using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class EffectProgramChecks
{
    public static void Run()
    {
        Check.Run("Effect program: distinct literal amounts and upgraded variants survive lowering", () =>
        {
            foreach (var (normal, upgraded, expected) in new[] { ("3,7", false, 10L), ("3,3", false, 6L), ("10", false, 10L), ("3,7", true, 13L) })
            {
                var parts = normal.Split(',');
                var session = NewRun(string.Join(" ", parts.Select(n => $"attack = {{ damage = {n} }}")),
                    "attack = { damage = 4 } attack = { damage = 9 }");
                var target = Monster(session);
                var before = Hp(session, target);
                var card = ProbeCard(session);
                if (upgraded)
                    session.State.RunAtomic(() => session.State.SetField(card,
                        session.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded"), Value.Of(true)));
                Check.Equal(1, Play(session, card, target), "one target decision per play");
                Check.Equal(expected, before - Hp(session, target), "each damage uses its own amount");
            }
        });

        Check.Run("Effect program: fixed and conditional powers have independent ids, stacks and targets", () =>
        {
            var session = NewRun("applyPower = { power = Strength stacks = 2 target = context.owner } "
                + "if = { condition = { targetAlive = {} } then = { applyPower = { power = Vulnerable stacks = 3 target = context.target } } }");
            var target = Monster(session);
            Play(session, ProbeCard(session), target);
            Check.Equal(2L, FormalSliceFixture.PowerStacks(session, FormalSliceFixture.Player(session), "base-spire:strength"), "self strength");
            Check.Equal(3L, FormalSliceFixture.PowerStacks(session, target, "base-spire:vulnerable"), "target vulnerable");
        });

        Check.Run("Effect program: display metadata cannot replace two explicit damage arguments", () =>
        {
            var session = NewRun("attack = { damage = 3 } attack = { damage = 7 }", variables: "damageValue = 99 upgradedDamageValue = 98");
            var target = Monster(session);
            var before = Hp(session, target);
            Play(session, ProbeCard(session), target);
            Check.Equal(10L, before - Hp(session, target), "explicit effect values survive card-level display metadata");
        });

        Check.Run("Effect program: a conditional in the middle and nested branch preserve sequence", () =>
        {
            var session = NewRun("attack = { damage = 3 } "
                + "if = { condition = { targetAlive = {} } then = { if = { condition = { targetAlive = {} } then = { attack = { damage = 2 } } } } } "
                + "attack = { damage = 7 }");
            var target = Monster(session);
            var before = Hp(session, target);
            Check.Equal(1, Play(session, ProbeCard(session), target), "nested branches reuse target");
            Check.Equal(12L, before - Hp(session, target), "3 then 2 then 7");
        });

        Check.Run("Effect program: repeat executes the entire block and reads live state at each effect", () =>
        {
            var session = NewRun("repeat = { count = 2 effect = { gainBlock = { amount = 1 } gainBlockFromCurrentBlock = {} } }");
            Play(session, ProbeCard(session), Monster(session));
            var player = FormalSliceFixture.Player(session);
            Check.Equal(6L, session.State.GetField(player, session.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Block")).AsInt(), "(0+1)*2 then (2+1)*2");
        });

        Check.Run("Effect program: zero repeat is empty and the following effect still executes", () =>
        {
            var session = NewRun("repeat = { count = 0 effect = { attack = { damage = 99 } } } attack = { damage = 7 }");
            var target = Monster(session);
            var before = Hp(session, target);
            Play(session, ProbeCard(session), target);
            Check.Equal(7L, before - Hp(session, target), "zero repeats consume no effects");
        });

        Check.Run("Effect program: a variable write changes only subsequent dynamic effects", () =>
        {
            var read = "scaledAttack = { base = 0 perCount = 1 count = { cardVariable = { variable = Charge } } }";
            var session = NewRun(read + " modifyCardVariable = { variable = Charge amount = 3 } " + read,
                variables: "variables = { variable = { name = Charge default = 1 upgraded = 2 } }");
            var target = Monster(session);
            var before = Hp(session, target);
            Play(session, ProbeCard(session), target);
            Check.Equal(5L, before - Hp(session, target), "reads 1 then 4 at their own positions");
        });

        Check.Run("Effect program: target power reads observe earlier effects without replacing literal amounts", () =>
        {
            var session = NewRun("applyPower = { power = Vulnerable stacks = 1 target = context.target } "
                + "scaledAttack = { base = 1 perCount = 2 count = { targetPowerAmount = { power = Vulnerable } } } "
                + "attack = { damage = 2 }");
            var target = Monster(session);
            var before = Hp(session, target);
            Play(session, ProbeCard(session), target);
            Check.Equal(7L, before - Hp(session, target), "floor((1+2*1)*1.5) + floor(2*1.5)");
        });

        Check.Run("Effect program: X effects read captured payment after subsequent energy changes", () =>
        {
            // ENV-T05：X 费不再隐式改写数值叶（Whirlwind 的每次伤害是字面 5/8、X 只作用于次数）。
            // 本探针改为作者显式写出的 `count = { energyX = {} }`（＝已付的 X）；断言与判别意图不变：
            // 付款时捕获的 5 必须在随后 modifyEnergy 把能量改成 1 之后仍然读到 5。
            var session = NewRun("modifyEnergy = { amount = 1 } scaledAttack = { base = 0 perCount = 1 count = { energyX = {} } }", cost: "X");
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.SetNumber(session, player, "base-spire:Player", "Energy", 5L);
            var target = Monster(session);
            var before = Hp(session, target);
            Play(session, ProbeCard(session), target);
            Check.Equal(5L, before - Hp(session, target), "uses paid X=5, not current energy=1 or display amount=0");
            Check.Equal(1L, FormalSliceFixture.Number(session, player, "base-spire:Player", "Energy"), "energy modifier retains its own literal");
        });

        Check.Run("Effect program: random effects do not overwrite the selected target", () =>
        {
            foreach (var selectedIndex in new[] { 0, 1 })
            {
                var session = NewRun("randomAttack = { damage = 3 } attack = { damage = 7 }");
                var enemies = session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters")).ToArray();
                var before = enemies.Select(enemy => Hp(session, enemy)).ToArray();
                Check.Equal(1, Play(session, ProbeCard(session), enemies[selectedIndex]), "one selected target decision");
                var damage = enemies.Select((enemy, i) => before[i] - Hp(session, enemy)).ToArray();
                Check.Equal(10L, damage.Sum(), "one random hit then one selected hit");
                Check.True(damage[selectedIndex] >= 7, "the selected hit retains the user's target");
            }
        });

        Check.Run("Effect program: normal and upgraded hooks use independent ordered programs", () =>
        {
            foreach (var upgraded in new[] { false, true })
            {
                var session = NewRun("gainBlockFromCurrentBlock = {}",
                    normalHooks: "beforeCardPlayed = { gainBlock = { amount = 3 } } afterCardPlayed = { gainBlock = { amount = 2 } gainBlock = { amount = 7 } }",
                    upgradedHooks: "beforeCardPlayed = { gainBlock = { amount = 5 } }");
                var card = ProbeCard(session);
                if (upgraded) session.State.RunAtomic(() => session.State.SetField(card,
                    session.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded"), Value.Of(true)));
                Play(session, card, Monster(session));
                Check.Equal(upgraded ? 10L : 15L, FormalSliceFixture.Number(session, FormalSliceFixture.Player(session),
                    "base-spire:Player", "Block"), "before hook, live play effect, own after hook");
            }
        });

        Check.Run("Effect program: a conditional-only hook remains executable and a nested hook choice is rejected", () =>
        {
            var session = NewRun("loseHp = { amount = 1 }", normalHooks:
                "afterCardPlayed = { if = { condition = { playerLostHpThisTurn = {} } then = { gainBlock = { amount = 7 } } else = { gainBlock = { amount = 2 } } } }");
            Play(session, ProbeCard(session), Monster(session));
            Check.Equal(7L, FormalSliceFixture.Number(session, FormalSliceFixture.Player(session), "base-spire:Player", "Block"), "hook dispatch uses the program, not a flat summary count");
            var invalid = ParseProbe("", normalHooks:
                "afterCardPlayed = { repeat = { count = 2 effect = { chooseCard = { from = discard to = draw destinationPosition = top } } } }");
            Check.Equal(0, invalid.Cards.Count, "a hook cannot pause for a nested card choice");
            Check.True(invalid.Unsupported.Single().Reason.Contains("hook", StringComparison.OrdinalIgnoreCase), "specific hook boundary error");
        });

        Check.Run("Effect program: entering another combat uses the copied card's upgraded hook", () =>
        {
            var session = NewRun("attack = { damage = 99 target = allEnemies }",
                normalHooks: "onEnterCombat = { loseHp = { amount = 3 } }",
                upgradedHooks: "onEnterCombat = { loseHp = { amount = 5 } }");
            Check.Equal(77L, FormalSliceFixture.Number(session, FormalSliceFixture.Player(session), "base-spire:Player", "Hp"), "normal entering hook");
            Play(session, ProbeCard(session), Monster(session));
            var longTerm = session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:LongTermDeck")).Single();
            session.State.RunAtomic(() => session.State.SetField(longTerm,
                session.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded"), Value.Of(true)));
            var enter = session.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "base-spire:EnterNextRoom");
            session.Submit(enter.Definition, SliceManifest.Player, []);
            Check.Equal(72L, FormalSliceFixture.Number(session, FormalSliceFixture.Player(session), "base-spire:Player", "Hp"), "upgraded copy uses its own entering hook");
        });

        Check.Run("Effect program: final kill completes later effects, hooks and result area before permanent commit", () =>
        {
            var session = NewRun("attack = { damage = 99 target = allEnemies } heal = { amount = 3 }",
                normalHooks: "afterCardPlayed = { heal = { amount = 7 } }");
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.SetNumber(session, player, "base-spire:Player", "Hp", 45L);
            var card = ProbeCard(session);
            Play(session, card, Monster(session));
            Check.Equal(55L, FormalSliceFixture.Number(session, player, "base-spire:Player", "Hp"), "3 then 7 after kill");
            var run = session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:RunStates")).Single();
            Check.Equal(55L, FormalSliceFixture.Number(session, run, "base-spire:Run", "Hp"), "permanent HP includes all later healing");
            Check.True(session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:DiscardPile")).Contains(card), "card reached result area");
            Check.False(session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:InPlay")).Contains(card), "card left in-play before settlement");
        });

        Check.Run("Effect program: missing nested references fail with source location instead of becoming empty effects", () =>
        {
            foreach (var body in new[] {
                "if = { condition = { targetAlive = {} } then = { applyPower = { power = Missing stacks = 2 } } }",
                "repeat = { count = 2 effect = { modifyCardVariable = { variable = Missing amount = 1 } } }",
                "repeat = { count = 2 effect = { scaledAttack = { base = 0 perCount = 1 count = { cardVariable = { variable = Missing } } } } }" })
            {
                var source = ParseProbe(body);
                Check.Equal(0, source.Cards.Count, "no card with an invalid nested reference is registered");
                Check.True(source.Unsupported.Single().Reason.Contains("effect-probe.spire", StringComparison.Ordinal), "diagnostic carries original source location");
                Check.True(source.Unsupported.Single().Reason.Contains("missing", StringComparison.OrdinalIgnoreCase), "diagnostic names the reference");
            }
        });
    }

    internal static Session NewRun(string normal, string? upgraded = null, string normalHooks = "", string? upgradedHooks = null, string variables = "", string cost = "1")
    {
        var source = ParseProbe(normal, upgraded, normalHooks, upgradedHooks, variables, cost);
        Check.Equal(1, source.Cards.Count, string.Join("; ", source.Unsupported.Select(x => x.Reason)));
        var original = FormalSpirePackage.Load().Model;
        var model = original with { Cards = source.Cards, Deck = [new(source.Cards[0].Id, 1)] };
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    private static SpireCardSource.Source ParseProbe(string normal, string? upgraded = null, string normalHooks = "", string? upgradedHooks = null, string variables = "", string cost = "1")
    {
        string Variant(string body, string hooks) => "type = attack rarity = common cost = " + cost + " "
            + "target = { selectOne = { from = context.opponents where = alive } } keywords = {} play = { " + body + " } hooks = { " + hooks + " }";
        return SpireCardSource.Parse("using namespace base-spire; card EffectProbe { " + variables + " normal = { "
            + Variant(normal, normalHooks) + " } upgraded = { " + Variant(upgraded ?? normal, upgradedHooks ?? normalHooks) + " } }", "effect-probe.spire",
            new Dictionary<string, string> { ["Strength"] = "base-spire:strength", ["Vulnerable"] = "base-spire:vulnerable" });
    }

    internal static InstanceId Monster(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters")).First();
    internal static InstanceId ProbeCard(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Hand")).Single();
    internal static long Hp(Session session, InstanceId monster) => session.State.GetField(monster, session.RuleSet.TypeByQualifiedId("base-spire:Monster").FieldIndex("Hp")).AsInt();

    internal static int Play(Session session, InstanceId card, InstanceId target)
    {
        var action = session.LegalActions(SliceManifest.Player).Single(x => x.Definition.QualifiedId == "base-spire:PlayCard");
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        var decisions = 0;
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            decisions++;
            session.Answer(new(session.PendingDecision!.RequestId, [target]));
        }
        return decisions;
    }
}
