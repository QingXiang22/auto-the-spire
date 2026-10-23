using System.Reflection;
using System.Text.Json;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal static class BattlewornContinuationVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        var rows = new List<object>();
        foreach (var seed in new[] { "777", "dummy-continuation" })
        foreach (var mode in new[] { "kill", "timeout" })
        {
            var run = new RunState(seed, new Overgrowth());
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            player.Creature.SetCurrentHpInternal(50);
            var deck = player.Deck.Cards.ToArray();
            var room = new EventRoom(() => (EventModel)ModelDb.Event<BattlewornDummy>().MutableClone());
            run.PushRoom(room); await room.Enter(run);
            object Snapshot() => new { hp = player.Creature.CurrentHp, max_hp = player.Creature.MaxHp, gold = player.Gold, finished = room.Event.IsFinished, awaiting = room.Event.IsAwaitingForcedCombat,
                upgraded = deck.Select((c, i) => (c, i)).Where(p => p.c.IsUpgraded).Select(p => p.i).ToArray(),
                niche = run.Rng.Niche.ToSerializable(), shuffle = run.Rng.Shuffle.ToSerializable(), rewards = player.PlayerRng.Rewards.ToSerializable(), event_rng = room.Event.Rng.ToSerializable() };
            var entered = Snapshot();
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == "SETTING_2"));
            var child = (CombatRoom)typeof(EventRoom).GetMethod("CreatePendingForcedCombatRoom", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(room, null)!;
            run.PushRoom(child); await child.Enter(run);
            var started = Snapshot();
            var enemy = child.Engine.State.SpawnedEnemies.Single();
            var hp = enemy.MaxHp; var move = enemy.Monster!.NextMove!.Id;
            for (var turn = 1; turn <= 3; turn++)
            {
                if (mode == "kill" && turn == 3)
                {
                    enemy.SetCurrentHpInternal(5);
                    var feed = (CardModel)ModelDb.Card<Feed>().MutableClone(); feed.AssignOwner(player);
                    player.PlayerCombatState!.Hand.AddInternal(feed);
                    await child.Engine.PlayCardAsync(player, feed, enemy); child.Engine.CheckWinCondition();
                }
                else await child.Engine.EndPlayerTurnAsync();
            }
            if (!child.Engine.Won) throw new InvalidOperationException("Controlled Battleworn path must win or time out.");
            var timedOut = child.Engine.State.EscapedCreatures.Contains(enemy);
            await child.ResolveOutcomeAsync(generateRewards: room.Event.GenerateForcedCombatRewards);
            var completed = Snapshot();
            await child.Exit(run); run.PopCurrentRoom();
            room.Event.ResumeAfterForcedCombat(new ForcedCombatOutcome(true, timedOut));
            var resumed = Snapshot();
            await room.Exit(run); run.PopCurrentRoom();
            rows.Add(new { seed, mode, entered, started, hp, move, timed_out = timedOut, completed, resumed,
                deck = deck.Select(c => new { id = "base-spire:" + c.Id.Entry.ToLowerInvariant().Replace('_', '-'), upgraded = c.IsUpgraded }).ToArray() });
        }
        return JsonSerializer.SerializeToElement(new { format = "battleworn-continuation-vector@1", normal_run_verified = false, reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes,
            controlled_setup = "Direct real event with Ironclad starter deck and HP50. Kill sets the real target to HP5 immediately before a generated Feed on turn3; timeout ends three real turns. No normal-map reachability claim.", vectors = rows },
            new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }
}
