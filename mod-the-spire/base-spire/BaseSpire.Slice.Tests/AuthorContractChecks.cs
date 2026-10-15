using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class AuthorContractChecks
{
    public static void Run()
    {
        Check.Run("Power lifecycle: Stampede with no eligible hand attacks skips sampling", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            foreach (var withAttack in new[] { false, true })
            {
                var s = Start(model with { Deck = withAttack ? [new("base-spire:strike-ironclad", 1), new("base-spire:wound", 12)] : [new("base-spire:wound", 12)] });
                FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:stampede-power", 2);
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(withAttack ? 1 : 0, Members(s, "base-spire:DiscardPile").Count(c => FormalSliceFixture.Definition(s, c) == "base-spire:strike-ironclad"), "finite hand candidates, no duplicate play");
            }
        });
        foreach (var upgraded in new[] { false, true })
        {
            foreach (var (id, retained, ethereal) in new[] { ("sow", true, false), ("luminesce", true, false), ("wisp", upgraded, false), ("parse", false, true), ("defy", false, true), ("defile", false, true) })
                Check.Run($"New card lifecycle: {id}/upgraded={upgraded} retain and ethereal at real turn end", () =>
                {
                    var model = FormalSpirePackage.Load().Model;
                    var s = Start(model with { Deck = [new("base-spire:" + id, 1, upgraded), new("base-spire:wound", 12)] });
                    var card = FormalSliceFixture.MoveToHand(s, "base-spire:" + id);
                    FormalSliceFixture.EndTurn(s); Healthy(s);
                    Check.Equal(retained, Members(s, "base-spire:Hand").Contains(card), "retained only when the variant declares retain");
                    Check.Equal(ethereal, Members(s, "base-spire:ExhaustPile").Contains(card), "unplayed ethereal exhausted");
                    Check.Equal(!retained && !ethereal, Members(s, "base-spire:DiscardPile").Contains(card), "ordinary end-of-turn discard");
                });
            foreach (var id in new[] { "backstab", "boot-sequence", "dramatic-entrance", "assassinate" })
                Check.Run($"New card lifecycle: {id}/upgraded={upgraded} innate from end of large starting deck", () =>
                {
                    var model = FormalSpirePackage.Load().Model;
                    var s = Start(model with { Run = new AuthorRun(80, 80, 99), Deck = [new("base-spire:wound", 12), new("base-spire:" + id, 1, upgraded)] });
                    Check.True(Members(s, "base-spire:Hand").Any(c => FormalSliceFixture.Definition(s, c) == "base-spire:" + id), "innate card actually in opening hand");
                });
        }
        Check.Run("Author contract: native Beckon empty play avoids its six HP hand-end penalty", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            foreach (var played in new[] { false, true })
            {
                var s = Start(model with { Deck = [new("base-spire:beckon", 1), new("base-spire:wound", 12)] });
                var player = FormalSliceFixture.Player(s); var hp = Number(s, player, "Hp");
                if (played) Play(s, "base-spire:beckon", player);
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(hp - (played ? 0 : 6), Number(s, player, "Hp"), "native penalty only while held");
            }
        });
        Check.Run("Author contract: Power play leaves combat piles before exhaust rules without exhausting", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var source = Card("Aura", "applyPower = { power = Strength stacks = 2 target = context.owner }", "onExhaust = { modifyEnergy = { amount = 8 } }")
                .Replace("type = skill", "type = power").Replace("keywords = {}", "keywords = { exhaust }");
            var cards = SpireCardSource.Parse("using namespace mymod; " + source, "aura.spire",
                new Dictionary<string, string> { ["Strength"] = "base-spire:strength" });
            Check.Equal(0, cards.Unsupported.Count, "registered aura");
            var s = Start(model with { Cards = cards.Cards, Deck = [new("mymod:aura", 1)] });
            var card = FormalSliceFixture.MoveToHand(s, "mymod:aura");
            var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.PlayAtCountingDecisions(s, card, player); Healthy(s);
            Check.Equal(2L, FormalSliceFixture.PowerStacks(s, player, "base-spire:strength"), "power applied");
            Check.Equal(3L, Number(s, player, "Energy"), "no synthetic exhaust hook");
            foreach (var pile in new[] { "base-spire:Hand", "base-spire:DrawPile", "base-spire:DiscardPile", "base-spire:ExhaustPile", "base-spire:InPlay" })
                Check.True(!Members(s, pile).Contains(card), "Power leaves " + pile);
            Check.Equal("mymod:aura", FormalSliceFixture.Definition(s, card), "history can still refer to card");
        });
        Check.Run("Author contract: Havoc forceExhaust does not exhaust a successfully played Power", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var s = Start(model with { Deck = [new("base-spire:havoc", 1), new("base-spire:inflame", 1)] });
            var aura = FormalSliceFixture.MoveToHand(s, "base-spire:inflame");
            s.State.RunAtomic(() => { s.State.RemoveFromCollection(FormalSliceFixture.Collection(s, "base-spire:Hand"), aura); s.State.AddToCollection(FormalSliceFixture.Collection(s, "base-spire:DrawPile"), aura); });
            var player = FormalSliceFixture.Player(s);
            Play(s, "base-spire:havoc", player);
            Check.Equal(2L, FormalSliceFixture.PowerStacks(s, player, "base-spire:strength"), "automatic Power applied");
            Check.True(!Members(s, "base-spire:ExhaustPile").Contains(aura), "native Power priority over forceExhaust");
            Check.True(!Members(s, "base-spire:DiscardPile").Contains(aura), "Power not discarded");
        });
        Check.Run("Author contract: selectNone permits a real empty play with payment, history and exhaust", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var source = Card("Blank", "").Replace("selectSelf", "selectNone").Replace("cost = 0", "cost = 1").Replace("keywords = {}", "keywords = { exhaust }");
            var cards = SpireCardSource.Parse("using namespace mymod; " + source, "blank.spire");
            Check.Equal(0, cards.Unsupported.Count, string.Join("; ", cards.Unsupported.Select(p => p.Reason)));
            var s = Start(model with { Cards = cards.Cards, Deck = [new("mymod:blank", 1)] });
            Play(s, "mymod:blank", FormalSliceFixture.Player(s));
            Check.Equal(2L, Number(s, FormalSliceFixture.Player(s), "Energy"), "real cost paid");
            Check.Equal(1, Members(s, "base-spire:ExhaustPile").Count, "empty play still commits result pile");
            Check.Equal(1, Members(s, "base-spire:PlayedThisTurn").Count, "empty play counted");
        });
        Check.Run("Author contract: common replacements clear play, hooks and keywords independently", () =>
        {
            var cards = SpireCardSource.Parse("""
                using namespace mymod;
                dsl_version = "base/0.0.2"
                card Clear {
                    common = {
                        type = skill rarity = common cost = 0 target = { selectSelf = {} }
                        keywords = { exhaust }
                        play = { modifyEnergy = { amount = 2 } }
                        hooks = { onExhaust = { modifyEnergy = { amount = 4 } } }
                    }
                    normal = {}
                    upgraded = { play = {} hooks = {} keywords = {} }
                }
                """, "clear.spire");
            Check.Equal(0, cards.Unsupported.Count, string.Join("; ", cards.Unsupported.Select(p => p.Reason)));
            var model = FormalSpirePackage.Load().Model;
            foreach (var upgraded in new[] { false, true })
            {
                var s = Start(model with { Cards = cards.Cards, Deck = [new("mymod:clear", 1, upgraded)] });
                Play(s, "mymod:clear", FormalSliceFixture.Player(s));
                Check.Equal(upgraded ? 3L : 9L, Number(s, FormalSliceFixture.Player(s), "Energy"), "whole play and hook replacement");
                Check.Equal(upgraded ? 0 : 1, Members(s, "base-spire:ExhaustPile").Count, "empty keywords clear exhaust");
                Check.Equal(upgraded ? 1 : 0, Members(s, "base-spire:DiscardPile").Count, "cleared variant uses ordinary result");
            }
        });
        Check.Run("Author contract: selectNone rejects selected-target effects including nested effects", () =>
        {
            foreach (var effect in new[] { "attack = { damage = 3 }", "repeat = { count = 2 effect = { attack = { damage = 3 } } }" })
            {
                var source = Card("Bad", effect).Replace("selectSelf", "selectNone");
                var result = SpireCardSource.Parse("using namespace mymod; " + source, "bad.spire");
                Check.True(result.Unsupported.Any(x => x.Reason.Contains("no-target card", StringComparison.Ordinal)), "selected target is explicitly rejected");
            }
        });
        Check.Run("Author contract: diagnostics load the same Power registry as SinglePlayer, including Dexterity", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            Check.True(model.Powers.Any(p => p.Id == "base-spire:dexterity"), "colocated Dexterity registered");
            Check.True(model.Powers.Select(p => p.Id).Order(StringComparer.Ordinal).SequenceEqual(
                BaseSpire.SinglePlayer.SinglePlayerContentSource.Shared.PowerIds.Order(StringComparer.Ordinal)), "diagnostics and production registry are identical");
            var source = Card("Agility", "applyPower = { power = Dexterity stacks = 2 target = context.owner }")
                + Card("Guard", "gainBlock = { amount = 5 }");
            var links = model.Powers.ToDictionary(p => p.Name, p => p.Id, StringComparer.Ordinal);
            var cards = SpireCardSource.Parse("using namespace mymod; " + source, "cross-file.spire", links);
            Check.Equal(0, cards.Unsupported.Count, "actual registry links cross-file Dexterity");
            var s = Start(model with { Cards = cards.Cards, Deck = [new("mymod:agility", 1), new("mymod:guard", 1)] });
            var player = FormalSliceFixture.Player(s);
            Play(s, "mymod:agility", player); Play(s, "mymod:guard", player);
            Check.Equal(7L, Number(s, player, "Block"), "registered Dexterity changes subsequent block");
        });
        Check.Run("Author contract: powers register by supported behavior and preserve mod namespace", () =>
        {
            var result = SpirePowerSource.Parse("package mymod { power FeelNoPain { trigger = afterCardExhausted effect = gainBlock(1, perPowerStack=FeelNoPain) } }", "powers.spire");
            Check.Equal("mymod:feel-no-pain", result.Powers.Single().Id, "base canonical IDs apply only to base namespace");
            Check.Equal(0, SpirePowerSource.Parse("package mymod { power Broken { role = Unknown } }", "bad.spire").LoweredCount, "unknown behavior rejected");
        });
        Check.Run("Author contract: external exhaust invokes card hook and a separately registered custom power", () =>
        {
            var power = SpirePowerSource.Parse("package mymod { power QuietArmor { trigger = afterCardExhausted effect = gainBlock(1, perPowerStack=QuietArmor) } }", "powers.spire").Powers.Single();
            var source = "using namespace mymod; "
                + Card("Setup", "applyPower = { power = QuietArmor stacks = 3 target = context.owner }")
                + Card("Victim", "gainBlock = { amount = 1 }", "onExhaust = { modifyEnergy = { amount = 2 } }")
                + Card("Consumer", "forEachCard = { from = hand effect = { moveSelectedCard = { from = hand to = exhaust } } }");
            var model = FormalSpirePackage.Load().Model;
            var cards = SpireCardSource.Parse(source, "cards.spire", new Dictionary<string, string> { [power.Name] = power.Id });
            Check.Equal(0, cards.Unsupported.Count, string.Join("; ", cards.Unsupported.Select(p => p.Reason)));
            var s = Start(model with { Cards = cards.Cards, Powers = model.Powers.Append(power).ToArray(), Deck = [.. cards.Cards.Select(c => new AuthorDeckEntry(c.Id, 1))] });
            var player = FormalSliceFixture.Player(s);
            Play(s, "mymod:setup", player);
            var energy = Number(s, player, "Energy");
            Play(s, "mymod:consumer", player);
            Check.Equal(energy + 2, Number(s, player, "Energy"), "external exhaustion grants energy");
            Check.Equal(3L, Number(s, player, "Block"), "custom power triggers from common exhaust command");
            Check.Equal(1, Members(s, "base-spire:ExhaustPile").Count, "victim exhausted exactly once");
        });
        Check.Run("Author contract: an exhaust hook alone does not make manual play exhaust", () =>
        {
            var cards = SpireCardSource.Parse("using namespace mymod; " + Card("Victim", "gainBlock = { amount = 1 }", "onExhaust = { modifyEnergy = { amount = 2 } }"), "card.spire");
            var model = FormalSpirePackage.Load().Model;
            var s = Start(model with { Cards = cards.Cards, Deck = [new(cards.Cards.Single().Id, 1)] });
            Play(s, "mymod:victim", FormalSliceFixture.Player(s));
            Check.Equal(3L, Number(s, FormalSliceFixture.Player(s), "Energy"), "no exhaust hook on ordinary discard");
            Check.Equal(1, Members(s, "base-spire:DiscardPile").Count, "normal result pile");
        });
        Check.Run("Author contract: real Armaments upgrade preserves growth and cost modifiers, then upgrades once", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var source = SpireCardSource.Parse(VariableCard, "variables.spire");
            Check.Equal(0, source.Unsupported.Count, string.Join("; ", source.Unsupported.Select(p => p.Reason)));
            var s = Start(model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:variable", 1), new("base-spire:armaments", 1), new("base-spire:armaments", 1, true)] });
            var card = Members(s, "base-spire:Hand").Single(c => FormalSliceFixture.Definition(s, c) == "mymod:variable");
            var type = s.RuleSet.TypeByQualifiedId("base-spire:Card");
            // Fixture represents an instance that has already grown and received a temporary cost reduction.
            s.State.RunAtomic(() => { s.State.SetField(card, type.FieldIndex("Variable_charge"), Value.Of(5L)); s.State.SetField(card, type.FieldIndex("CostModifier"), Value.Of(-1L)); });
            var armaments = Members(s, "base-spire:Hand").First(c => FormalSliceFixture.Definition(s, c) == "base-spire:armaments" && !s.State.GetField(c, type.FieldIndex("Upgraded")).AsBool());
            FormalSliceFixture.PlayAtCountingDecisions(s, armaments, card);
            Healthy(s);
            long Field(string name) => s.State.GetField(card, type.FieldIndex(name)).AsInt();
            Check.Equal(12L, Field("Variable_charge"), "5 existing + (9 upgraded - 2 normal)");
            Check.Equal(1L, Field("Cost"), "base cost receives upgrade delta");
            Check.Equal(-1L, Field("CostModifier"), "temporary modifier preserved");
            Check.True(s.State.GetField(card, type.FieldIndex("Ethereal")).AsBool(), "upgraded keywords applied");
            Play(s, "base-spire:armaments", card);
            Check.Equal(12L, Field("Variable_charge"), "repeat upgrade does not add the delta twice");
        });
        Check.Run("Author contract: Thrash consumes live BodySlam damage with outgoing modifiers", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            foreach (var upgraded in new[] { false, true })
            {
                var s = Start(model with { Deck = [new("base-spire:thrash", 1, upgraded), new("base-spire:body-slam", 1), new("base-spire:wound", 3)] });
                var player = FormalSliceFixture.Player(s);
                var victim = FormalSliceFixture.MoveToHand(s, "base-spire:body-slam");
                var thrash = FormalSliceFixture.MoveToHand(s, "base-spire:thrash");
                var playerType = s.RuleSet.TypeByQualifiedId("base-spire:Player");
                s.State.RunAtomic(() => s.State.SetField(player, playerType.FieldIndex("Block"), Value.Of(7L)));
                FormalSliceFixture.GivePower(s, player, "base-spire:strength", 2);
                var target = Members(s, "base-spire:Monsters").First();
                FormalSliceFixture.PlayAtCountingDecisions(s, thrash, target); Healthy(s);
                var cardType = s.RuleSet.TypeByQualifiedId("base-spire:Card");
                Check.Equal(upgraded ? 15L : 13L, s.State.GetField(thrash, cardType.FieldIndex("Variable_damage")).AsInt(), "initial damage + live block 7 + Strength 2");
                Check.True(Members(s, "base-spire:ExhaustPile").Contains(victim), "sampled attack exhausted after growth");
            }
        });
        Check.Run("Author contract: direct upgraded creation initializes variables, fee and upgraded entry hook", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var source = SpireCardSource.Parse(VariableCard, "variables.spire");
            var s = Start(model with { Cards = source.Cards, Deck = [new("mymod:variable", 1, true)] });
            var type = s.RuleSet.TypeByQualifiedId("base-spire:Card");
            var card = Members(s, "base-spire:Hand").Single();
            Check.Equal(10L, s.State.GetField(card, type.FieldIndex("Variable_charge")).AsInt(), "upgraded initial variable plus upgraded entry hook");
            Check.Equal(1L, s.State.GetField(card, type.FieldIndex("Cost")).AsInt(), "upgraded initial cost");
        });
    }

    private const string VariableCard = """
        using namespace mymod;
        dsl_version = "base/0.0.2"
        card Variable {
            variables = { variable = { name = Charge default = 2 upgraded = 9 } }
            values = { fee = { normal = 2 upgraded = 1 } }
            common = {
                type = attack rarity = common cost = values.fee target = { selectOne = { from = context.opponents where = alive } }
                keywords = {} play = { attack = { damage = variables.Charge } } hooks = {}
            }
            normal = {}
            upgraded = {
                keywords = { ethereal }
                hooks = { onEnterCombat = { modifyCardVariable = { variable = Charge amount = 1 } } }
            }
        }
        """;
    private static string Card(string name, string play, string hooks = "")
    {
        var variant = $"type = skill rarity = common cost = 0 target = {{ selectSelf = {{}} }} keywords = {{}} play = {{ {play} }} hooks = {{ {hooks} }}";
        return $"card {name} {{ normal = {{ {variant} }} upgraded = {{ {variant} }} }}";
    }
    private static Session Start(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance); s.Start(); Healthy(s); return s;
    }
    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static long Number(Session s, InstanceId p, string f) => FormalSliceFixture.Number(s, p, "base-spire:Player", f);
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
    private static void Play(Session s, string id, InstanceId target) { FormalSliceFixture.PlayAtCountingDecisions(s, Members(s, "base-spire:Hand").First(c => FormalSliceFixture.Definition(s, c) == id), target); Healthy(s); }
}
