#if REFERENCE_SIM
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

internal static partial class NormalRewardEntry
{
    private sealed partial class ReferenceSide
    {
        private RestSiteRoom? rest;
        private TreasureRoom? treasure;
        private RelicModel? treasureRelic;
        private bool chestOpened;
        private int treasureGold;

        private string? VisitPhase => rest is not null ? "rest" : treasure is not null ? "treasure"
            : runLifecycle && room is null && lastRoomType == "Boss" ? "act-completed" : null;

        private bool EnterVisit(RoomType type)
        {
            if (type == RoomType.RestSite)
            {
                rest = (RestSiteRoom)RoomFactory.CreateRoom(run, type);
                run.PushRoom(rest); rest.Enter(run).GetAwaiter().GetResult();
                return true;
            }
            if (type != RoomType.Treasure) return false;
            // Native TreasureRoom.Enter auto-obtains its relic. Compose the recovered game's
            // two presentation boundaries from native RNG/bag/commands, preserving a real skip.
            treasure = (TreasureRoom)RoomFactory.CreateRoom(run, type);
            run.PushRoom(treasure); chestOpened = false; treasureGold = 0;
            var rarity = RelicFactory.RollRarity(run.Rng.TreasureRoomRelics);
            treasureRelic = run.SharedRelicGrabBag!.PullFromFront(rarity, run)
                ?? ModelDb.Relic<Sts2Sim.Core.Models.Relics.Circlet>();
            return true;
        }

        private IReadOnlyList<ActionOption>? VisitActions(IReadOnlyList<ActionOption> inventory)
        {
            if (VisitPhase == "act-completed") return [new("base-spire:ContinueToNextAct", [], 1)];
            List<ActionOption> actions = [.. inventory];
            if (rest is not null)
            {
                var options = rest.GetAvailableDecisions(run, player);
                if (options.Any(o => o is not (RestSiteDecision.Heal or RestSiteDecision.Smith)))
                    throw new CheckFailed("Additional native rest-site choices are not adapted");
                if (options.Any(o => o is RestSiteDecision.Heal)) actions.Add(new("base-spire:RestAtCampfire", [], 1));
                var smith = options.OfType<RestSiteDecision.Smith>().Where(o => o.IsEnabled)
                    .Select(o => "deck:" + player.Deck.Cards.ToList().IndexOf(o.Card)).ToArray();
                if (smith.Length > 0) actions.Add(new("base-spire:SmithAtCampfire", smith, smith.Length));
            }
            else if (treasure is not null)
            {
                if (!chestOpened) actions.Add(new("base-spire:OpenTreasureChest", [], 1));
                else { actions.Add(new("base-spire:SkipTreasureRelic", [], 1)); actions.Add(new("base-spire:TakeTreasureRelic", [], 1)); }
            }
            else return null;
            return actions.OrderBy(a => a.Action, StringComparer.Ordinal).ToArray();
        }

        private IEnumerable<FactSnapshot> VisitFacts()
        {
            if (treasure is null) yield break;
            yield return new("treasure.opened", chestOpened.ToString().ToLowerInvariant());
            yield return new("treasure.gold", treasureGold.ToString(System.Globalization.CultureInfo.InvariantCulture));
            yield return new("treasure.relic.definition", ReferenceBackend.Project(treasureRelic!.Id));
            yield return new("treasure.relic.rarity", treasureRelic.Rarity.ToString().ToLowerInvariant());
        }

        private bool ExecuteVisit(string action, string? token)
        {
            if (rest is not null)
            {
                var options = rest.GetAvailableDecisions(run, player);
                var option = action == "base-spire:RestAtCampfire" ? options.OfType<RestSiteDecision.Heal>().Single()
                    : (RestSiteDecision)options.OfType<RestSiteDecision.Smith>().Single(o =>
                        player.Deck.Cards.ToList().IndexOf(o.Card) == int.Parse(token!.Split(':')[1]));
                rest.ResolveAsync(player, option).GetAwaiter().GetResult();
                if (rest.HasRemainingDecisions(player)) throw new CheckFailed("Additional rest-site actions remain unadapted");
                rest.Exit(run).GetAwaiter().GetResult(); run.PopCurrentRoom(); rest = null;
                settledHp = player.Creature.CurrentHp; settledMaxHp = player.Creature.MaxHp;
                return true;
            }
            if (treasure is null) return false;
            if (action == "base-spire:OpenTreasureChest")
            {
                treasureGold = player.PlayerRng.Rewards.NextInt(42, 53);
                PlayerCmd.GainGold(treasureGold, player).GetAwaiter().GetResult(); chestOpened = true;
            }
            else
            {
                if (!chestOpened || action is not ("base-spire:TakeTreasureRelic" or "base-spire:SkipTreasureRelic"))
                    throw new CheckFailed("Invalid treasure visit action: " + action);
                if (action == "base-spire:TakeTreasureRelic") RelicCmd.Obtain(treasureRelic!, player).GetAwaiter().GetResult();
                treasure.Exit(run).GetAwaiter().GetResult(); run.PopCurrentRoom(); treasure = null;
                settledHp = player.Creature.CurrentHp; settledMaxHp = player.Creature.MaxHp;
            }
            return true;
        }
    }
}
#endif
