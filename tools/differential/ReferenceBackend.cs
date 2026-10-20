#if REFERENCE_SIM
using System.Globalization;
using System.Security.Cryptography;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class ReferenceBackend
{
    public static BackendRecord Run(StrikeFixture fixture)
    {
#if REFERENCE_SIM
        using var random = new DrawCounter();
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(fixture.Seed.ToString(CultureInfo.InvariantCulture), new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run);
        run.AddPlayer(player);
        var combat = new CombatState(run);
        combat.AddMonster((MonsterModel)ModelDb.Monster<CalcifiedCultist>().MutableClone(), CombatSide.Enemy);
        combat.AddMonster((MonsterModel)ModelDb.Monster<DampCultist>().MutableClone(), CombatSide.Enemy);
        var engine = new CombatEngine(combat);
        engine.StartCombatAsync().GetAwaiter().GetResult();
        var state = player.PlayerCombatState ?? throw new InvalidOperationException("Reference combat did not start.");
        // Same controlled injection as the original adapter, after the reference's own combat initialization.
        foreach (var card in state.Hand.Cards.ToArray()) state.DrawPile.AddInternal(card);
        var strike = state.DrawPile.Cards.First(c => Project(c.Id) == fixture.Card);
        state.Hand.AddInternal(strike);
        player.Creature.SetMaxHpInternal(fixture.PlayerHp); player.Creature.SetCurrentHpInternal(fixture.PlayerHp);
        state.Energy = fixture.Energy;
        for (var index = 0; index < combat.Enemies.Count; index++)
        {
            combat.Enemies[index].SetMaxHpInternal(fixture.EnemyHp[index]);
            combat.Enemies[index].SetCurrentHpInternal(fixture.EnemyHp[index]);
        }
        var beforeRandom = random.Count;
        var start = Snapshot("start", []);
        var legal = CombatTargetCandidates.ForCard(combat, player, strike.TargetType);
        var target = combat.Enemies[int.Parse(fixture.Target.AsSpan("enemy:".Length), CultureInfo.InvariantCulture)];
        if (!legal.Contains(target) || !strike.CanPlay(out _)) throw new InvalidOperationException("Reference rejects the semantic action.");
        var decision = new DecisionSnapshot(legal.Select(c => "enemy:" + combat.Enemies.ToList().IndexOf(c)).ToArray(), 1, 1, fixture.Target);
        engine.PlayCardAsync(player, strike, target).GetAwaiter().GetResult();
        var end = Snapshot("step:0:play_card", [decision]);
        var assembly = typeof(CombatEngine).Assembly.Location;
        return new("reference:sts2-sim", Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly))),
            combat.IsOverOrEnding() ? "ended" : "running", beforeRandom, random.Count - beforeRandom, StrikeFixture.Dimensions, [start, end]);

        Boundary Snapshot(string kind, DecisionSnapshot[] decisions)
        {
            var facts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Number(string path, long value) => facts.Add(path, value.ToString(CultureInfo.InvariantCulture));
            Number("player.hp", player.Creature.CurrentHp); Number("player.max_hp", player.Creature.MaxHp);
            Number("player.block", player.Creature.Block); Number("player.energy", state.Energy);
            foreach (var enemy in combat.Enemies)
            {
                var prefix = "enemy." + Project(enemy.Monster!.Id);
                Number(prefix + ".hp", enemy.CurrentHp); Number(prefix + ".max_hp", enemy.MaxHp); Number(prefix + ".block", enemy.Block);
                facts.Add(prefix + ".alive", enemy.IsAlive ? "true" : "false");
                Powers(enemy, prefix);
            }
            Powers(player.Creature, "player");
            foreach (var (pile, key) in new[] { (state.DrawPile, "draw"), (state.Hand, "hand"), (state.DiscardPile, "discard"), (state.ExhaustPile, "exhaust") })
            {
                var cards = pile.Cards.Select(c => Project(c.Id) + (c.IsUpgraded ? "+" : "")).ToArray();
                Number("pile." + key + ".size", cards.Length);
                facts.Add("pile." + key + ".sorted", string.Join(",", cards.Order(StringComparer.Ordinal)));
                if (key is "hand" or "exhaust") facts.Add("pile." + key + ".order", string.Join(",", cards));
            }
            var actions = new List<ActionOption>();
            if (!combat.IsOverOrEnding())
            {
                var playable = state.Hand.Cards.Select((card, index) => (card, index)).Where(pair => pair.card.CanPlay(out _))
                    .Select(pair => "card:hand:" + pair.index).ToArray();
                if (playable.Length > 0) actions.Add(new("base-spire:PlayCard", playable, playable.Length));
                actions.Add(new("base-spire:EndTurn", [], 1));
            }
            return new(kind, actions.ToArray(), decisions, facts);

            void Powers(Creature holder, string prefix)
            {
                Number("power." + prefix + ".count", holder.Powers.Count);
                for (var index = 0; index < holder.Powers.Count; index++)
                {
                    facts.Add($"power.{prefix}.{index}.id", Project(holder.Powers[index].Id));
                    Number($"power.{prefix}.{index}.stacks", (long)holder.Powers[index].Amount);
                }
            }
        }
#else
        throw new NotSupportedException("unsupported_reference_adapter: build with the locked reference checkout.");
#endif
    }

#if REFERENCE_SIM
    private static string Project(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');

    // Observe the existing upstream hook only inside this serial CLI case and restore it on failure.
    private sealed class DrawCounter : IDisposable
    {
        private readonly RngDrawObserver? previous = RngDiagnostics.DrawObserver;
        public long Count { get; private set; }
        public DrawCounter() => RngDiagnostics.DrawObserver = (rng, op, min, max, arity) =>
        {
            Count++; previous?.Invoke(rng, op, min, max, arity);
        };
        public void Dispose() => RngDiagnostics.DrawObserver = previous;
    }
#endif
}
