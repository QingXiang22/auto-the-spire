using System.Text.Json;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal static class DrawPermissionVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        var run = new RunState("777", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<BattleFriendV2>().MutableClone());
        run.PushRoom(room); await room.Enter(run);
        var state = room.Engine.State;
        var steps = new List<object>();
        void Capture(string phase)
        {
            var before = JsonSerializer.Serialize(run.Rng.Shuffle.ToSerializable());
            var blocked = !Hook.ShouldDraw(state, player, false, out var preventer);
            var handAllowed = Hook.ShouldDraw(state, player, true, out _);
            if (!handAllowed || before != JsonSerializer.Serialize(run.Rng.Shuffle.ToSerializable()))
                throw new InvalidOperationException("Draw permission query must allow turn hand without RNG.");
            steps.Add(new { phase, hand = player.PlayerCombatState!.Hand.Cards.Count, blocked, hand_allowed = handAllowed, preventer = preventer?.Id.Entry });
        }
        Capture("initial");
        await PowerCmd.Apply<NoDrawPower>(state, player.Creature, 1, null, null); Capture("applied");
        var before = JsonSerializer.Serialize(run.Rng.Shuffle.ToSerializable());
        var order = player.PlayerCombatState!.DrawPile.Cards.ToArray();
        if ((await CardPileCmd.Draw(state, 3, player, false)).Count != 0
            || before != JsonSerializer.Serialize(run.Rng.Shuffle.ToSerializable()) || !order.SequenceEqual(player.PlayerCombatState.DrawPile.Cards))
            throw new InvalidOperationException("NoDraw veto must preserve draw pile and shuffle RNG.");
        Capture("effect-blocked");
        await CardPileCmd.Draw(state, 2, player, true); Capture("turn-hand");
        await PowerCmd.Remove(player.Creature.Powers.OfType<NoDrawPower>().Single()); Capture("removed");
        await CardPileCmd.Draw(state, 1, player, false); Capture("effect-resumed");
        await PowerCmd.Apply<NoDrawPower>(state, player.Creature, 1, null, null);
        await room.Engine.EndPlayerTurnAsync(); Capture("expired-next-turn");
        return new { format = "draw-permission-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes, scope = "controlled native NoDraw / real turn-hand and effect draw", seed = "777", steps };
    }
}
