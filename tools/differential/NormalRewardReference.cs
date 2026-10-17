#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rewards;

namespace Differential;
internal static partial class NormalRewardEntry
{
    public static CaseRecord RunReference(FixtureCase fixture, CaseRecord? actionSource = null)
    {
        List<BoundaryRecord> records = [];
        using var random = new ReferenceRandomProbe();
        string status = "ok"; string? detail = null; long before = 0;
        try
        {
            Validate(fixture); CombatReplayDemo.EnsureModelsRegistered();
            var run = fixture.BaseScope == BaseScopes.DevRun
                ? new RunState(fixture.GameSeed!, new Sts2Sim.Core.Content.ActDefinition[] { new Overgrowth(), new Hive(), new Glory() })
                : new RunState(fixture.GameSeed!, new Overgrowth());
            var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), run);
            run.AddPlayer(player);
            _ = run.CurrentAncientEventType; // Public native lazy initialization, before the first comparable boundary.
            random.Attach(run.Rng, player); random.MarkInitialization(); before = random.Count;
            if (actionSource?.CaseId != fixture.Id || actionSource.Status != "ok" && !IsActRun(fixture)
                || actionSource.SemanticActions is not { Count: > 0 } plan)
                throw new CheckFailed("Normal reference replay requires a successful matching base semantic action plan");
            var side = new ReferenceSide(run, player, random, IsActRun(fixture));
            void Add(string kind, IReadOnlyList<DecisionSnapshot> decisions) => records.Add(new(
                records.Count, kind, kind, side.Actions(), decisions, side.Facts(),
                side.Streams(records.Count, kind), null));
            Add("start", []);
            for (int i = 0; i < plan.Count; i++)
            {
                var step = plan[i];
                RequireSameActionBoundary(side, step);
                var decisions = side.Execute(step.Action, step.Token, step.Decisions);
                Add("step:" + i + ":" + step.Action + (step.Token is null ? "" : ":" + step.Token), decisions);
            }
            if (actionSource.Status == "ok" && (IsActRun(fixture) ? side.Phase is not ("defeat" or "act-completed") : side.Phase != "travel-ready"))
                throw new CheckFailed("Common action replay did not reach its real Run outcome");
            RequirePotionAcquisition(fixture, plan, records);
            RequirePotionDiscard(fixture, plan, records);
            RequireEvent(fixture, plan);
            if (!IsActRun(fixture)) RequireCombatClosure(fixture, records);
            RequirePostEventCarryover(fixture, plan, records);
            RequireCapacity(fixture, plan, records);
        }
        catch (Exception e) { status = "faulted"; detail = e.GetType().Name + ": " + e.Message; }
        return new(fixture.Id, ReferenceBackend.Name, IsActRun(fixture) ? "normal-run@1" : "normal-reward-prefix@1", fixture.RngProfile,status,detail,random.Count != before,
            [$"Independent native sequential RunState/{(fixture.BaseScope == BaseScopes.DevRun ? "Overgrowth-Hive-Glory" : "Overgrowth")}/player; v0.109 seed derivation, no reseed or MockRng.",
             "Normal travel uses MapTravel, AddVisitedMapCoord, RoomFactory, PushRoom, Enter. Victory requires Engine.Won then ResolveOutcomeAsync(true); real SelectOption/Take/Skip consumers.",
             "Public native room pre-generation completes before boundary0; map and player RNG remain native. Settled HP is an observation projection refreshed after victory resolution.",
             IsActRun(fixture) ? "Continuous Run adapter: native rest-site commands; treasure choice boundaries compose source order from native RNG, shared bag and commands (native TreasureRoom.Enter auto-obtains and cannot represent skip)." : "Reward-prefix adapter.",
             actionSource?.Status == "ok" ? "Complete requested plan replayed." : "Only the successful prefix before the base fault was replayed; this is not a completed Run."], records)
        { SessionStatus = status == "ok" ? (records.Last().Facts.Any(f => f.Path == "run.phase" && f.Value == "defeat") ? "Terminal" : "Running") : "Faulted", InitializationRandomCount = before,
          Terminal = records.LastOrDefault()?.Facts.Any(f => f.Path == "run.phase" && f.Value == "defeat") == true ? "base-spire:Defeat" : null,
          ReferenceIdentity = NativeIdentity(fixture.GameSeed!), ReferenceStreams = random.Evidence(),
          SemanticActions = actionSource?.SemanticActions };
    }

    private static ReferenceIdentity NativeIdentity(string seed)
    {
        var rng = new RunRngSet(seed);
        var (head, source) = Repo.GitHead("reference/sts2-sim");
        return new(ReferenceBackend.Name, "sts2-v0.109-native-sequential@1",
            "Native RunState constructor; no MockRng, no reseed, no state injection",
            "Native root/run/player/map/content derivation", seed, rng.Seed,
            [.. Enum.GetValues<Sts2Sim.Core.Entities.Rngs.RunRngType>().Select(type =>
                new ReferenceStreamSeed(type.ToString(), RngSourceCompat.EngineStreamName(type.ToString()),
                    unchecked(rng.Seed + Sts2Sim.Core.Helpers.StringHelper.GetDeterministicHashCode(
                        Sts2Sim.Core.Helpers.StringHelper.SnakeCase(type.ToString())))))],
            [], ["Reference behavior unchanged"], null, head, source,
            [new ReferenceFileDigest("loaded Sts2Sim.Core.dll", "sha256:" +
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(RunState).Assembly.Location))).ToLowerInvariant())]);
    }

    private sealed partial class ReferenceSide(RunState run, Player player, ReferenceRandomProbe random, bool runLifecycle = false) : Side
    {
        private CombatRoom? room;
        private EventRoom? eventRoom;
        private readonly DeckOrigins<CardModel> deckOrigins = new();
        private readonly DeckOrigins<PotionModel> potionOrigins = new();
        private int rooms;
        private string lastRoomType = "Unassigned";
        private int settledHp = player.Creature.CurrentHp;
        private int settledMaxHp = player.Creature.MaxHp;
        public string Phase => runLifecycle && player.Creature.CurrentHp == 0 ? "defeat"
            : VisitPhase ?? (eventRoom is not null ? "event" : room is null ? (rooms == 0 ? "map" : "travel-ready") : room.Engine.IsInProgress ? "combat" : "rewards");
        private string Token(MapPoint p) => $"map:{p.coord.col},{p.coord.row}";
        private IEnumerable<MapPoint> Travel => MapTravel.GetTravelablePointsFrom(run,run.CurrentMapPoint ?? run.Map.StartingMapPoint);
        private string Card(CardModel c) => ReferenceBackend.Project(c.Id) + (c.IsUpgraded ? "+" : "");
        public IReadOnlyList<ActionOption> Actions()
        {
            if (Phase == "defeat") return [];
            var potions = player.CanUseOrRemovePotions
                ? player.PotionSlots.Where(p => p is not null).Select((_, i) => "potion:" + i).ToArray() : [];
            List<ActionOption> inventory = potions.Length == 0 ? [] : [new("base-spire:DiscardPotion", potions, potions.Length)];
            if (VisitActions(inventory) is { } visit) return visit;
            if (eventRoom is not null) return inventory.Concat(eventRoom.Event.CurrentOptions.Where(o => !o.IsLocked)
                .Select(o => new ActionOption(EventOptionAction(o.Key), [], 1))).OrderBy(a => a.Action, StringComparer.Ordinal).ToArray();
            if (room is null) return inventory.Append(new("base-spire:ChooseMapPoint",Travel.Select(Token).Order(StringComparer.Ordinal).ToArray(), Travel.Count())).OrderBy(a => a.Action, StringComparer.Ordinal).ToArray();
            if (room.Engine.IsInProgress)
            {
                // Query native manual-use legality, including potions whose base effect is not implemented.
                // Omitting those candidates would hide a real unsupported-content boundary.
                var targets = room.Engine.State.Allies.Concat(room.Engine.State.HittableEnemies)
                    .Cast<Sts2Sim.Core.Entities.Creatures.Creature?>().Prepend(null).Distinct().ToArray();
                var usable = player.PotionSlots.Where(p => p is not null).Select((p, i) => (Potion: p!, Token: "potion:" + i))
                    .Where(p => targets.Any(target => Sts2Sim.Core.Commands.PotionCmd.CanUseManually(room.Engine.State, player, p.Potion, target)))
                    .Select(p => p.Token).ToArray();
                if (usable.Length > 0) inventory.Add(new("base-spire:UsePotion", usable, usable.Length));
                return ReferenceBackend.LegalActions(room.Engine.State,player).Concat(inventory).OrderBy(a => a.Action, StringComparer.Ordinal).ToArray();
            }
            var rewards = room.Rewards ?? throw new CheckFailed("Real victory produced no rewards");
            List<ActionOption> a = inventory;
            if (!rewards.Gold.IsResolved) a.Add(new("base-spire:TakeRewardGold",[],1));
            if (!rewards.Card.IsResolved)
            {
                a.Add(new("base-spire:TakeRewardCard",rewards.Card.Options.Select((_,i)=>"reward:card:"+i).ToArray(),rewards.Card.Options.Count));
                a.Add(new("base-spire:SkipRewardCard",[],1));
            }
            if (rewards.Potion is { IsResolved:false } potion)
            {
                a.Add(new("base-spire:SkipRewardPotion",[],1));
                if (potion.CanTake && player.PotionSlots.Contains(null)) a.Add(new("base-spire:TakeRewardPotion",[],1));
            }
            if (rewards.Relic is { IsResolved: false })
            {
                a.Add(new("base-spire:TakeRewardRelic", [], 1)); a.Add(new("base-spire:SkipRewardRelic", [], 1));
            }
            return a.OrderBy(x=>x.Action,StringComparer.Ordinal).ToArray();
        }
        public IReadOnlyList<FactSnapshot> Facts()
        {
            List<FactSnapshot> facts = [];
            void F(string path,object value)=> facts.Add(new(path,Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)!.ToLowerInvariant()));
            F("run.phase",Phase); F("run.gold",player.Gold); F("run.hp",settledHp); F("run.max_hp",settledMaxHp); F("run.room",rooms);
            F("run.room_type",lastRoomType);
            F("relic.inventory.order",string.Join(",",player.Relics.Select(r=>ReferenceBackend.Project(r.Id))));
            facts.AddRange(VisitFacts());
            if (Phase == "event") F("run.event",eventRoom!.Event.GetType().Name);
            F("run.map_floor",run.CurrentMapCoord is { } coord ? coord.row+1 : 0);
            F("run.map_coord",run.CurrentMapPoint is { } point ? Token(point) : Token(run.Map.StartingMapPoint));
            F("deck.order",string.Join(",",player.Deck.Cards.Select(Card))); F("deck.instance_count",player.Deck.Cards.Distinct().Count());
            F("deck.origins.order", deckOrigins.Capture(player.Deck.Cards));
            if (eventRoom?.Event is Sts2Sim.Core.Models.Events.SlipperyBridge bridge)
            {
                F("event.slippery.card", Card(bridge.RandomCardToLose));
                F("event.slippery.origin", deckOrigins.Of(bridge.RandomCardToLose));
                F("event.slippery.hp_loss", bridge.CurrentHpLoss);
            }
            for(var i=0;i<player.Deck.Cards.Count;i++) F("deck.instance:"+i,Card(player.Deck.Cards[i]));
            F("potion.capacity",player.MaxPotionCount);
            var rewardPotion = room?.Rewards?.Potion;
            if (rewardPotion is { IsResolved: false, Potion: not null }) potionOrigins.Capture([rewardPotion.Potion]);
            potionOrigins.Capture(player.PotionSlots.OfType<PotionModel>());
            F("potion.inventory.origins",string.Join(",",player.PotionSlots.Select((p,i)=>(p,i)).Where(x=>x.p is not null).Select(x=>x.i+":"+potionOrigins.Of(x.p!))));
            F("potion.inventory",string.Join(",",player.PotionSlots.Select((p,i)=>(p,i)).Where(x=>x.p is not null).Select(x=>x.i+":"+ReferenceBackend.Project(x.p!.Id))));
            foreach(var p in run.Map.GetAllMapPoints().Append(run.Map.StartingMapPoint).Append(run.Map.BossMapPoint))
            {
                F("map."+Token(p)+".type",p.PointType==MapPointType.Monster ? "Monster" : "type:"+(int)p.PointType);
                foreach(var child in p.Children) F("map.edge."+Token(p)+"->"+Token(child),true);
            }
            if(Phase == "combat" || Phase == "defeat" && room is not null)
            {
                facts.AddRange(ReferenceBackend.Facts(room!.Engine.State,player,true));
                foreach (var enemy in room.Engine.State.Enemies)
                    F("visible." + ReferenceBackend.SlotOf(room.Engine.State,player,enemy) + ".definition", ReferenceBackend.Project(enemy.Monster!.Id));
                foreach(var (c,i) in player.PlayerCombatState!.Hand.Cards.Select((c,i)=>(c,i))) F("visible.card:hand:"+i+".definition",ReferenceBackend.Project(c.Id));
                foreach(var (c,i) in player.PlayerCombatState!.Hand.Cards.Select((c,i)=>(c,i))) F("visible.card:hand:"+i+".cost", Math.Max(0, c.EnergyCost));
                foreach(var (c,i) in player.PlayerCombatState!.Hand.Cards.Select((c,i)=>(c,i))) F("visible.card:hand:"+i+".kind", c.Type);
                foreach (var (pile, key) in new[] { (player.PlayerCombatState.Hand, "hand"), (player.PlayerCombatState.DrawPile, "draw"),
                    (player.PlayerCombatState.DiscardPile, "discard"), (player.PlayerCombatState.ExhaustPile, "exhaust"), (player.PlayerCombatState.PlayPile, "play") })
                    F("pile." + key + ".origins", string.Join(",", pile.Cards.Select(card => card.DeckVersion is { } source ? deckOrigins.Of(source) : "temporary")));
            }
            if(Phase=="rewards")
            {
                var r=room!.Rewards!;
                F("reward.gold",r.Gold.Amount); F("reward.gold.resolved",r.Gold.IsResolved);
                F("reward.card.resolved",r.Card.IsResolved); F("reward.cards.order",r.Card.IsResolved?"":string.Join(",",r.Card.Options.Select(Card)));
                if(!r.Card.IsResolved) for(var i=0;i<r.Card.Options.Count;i++)
                {
                    F("reward.card:"+i+".rarity",(int)r.Card.Options[i].Rarity);
                    F("reward.card:"+i+".upgraded",r.Card.Options[i].IsUpgraded);
                }
                F("reward.potion.present",r.Potion is not null); F("reward.potion.resolved",r.Potion is null || r.Potion.IsResolved);
                F("reward.potion.definition",r.Potion is {IsResolved:false,Potion:not null} p?ReferenceBackend.Project(p.Potion.Id):"");
                F("reward.potion.origin",r.Potion is {IsResolved:false,Potion:not null} actualPotion ? potionOrigins.Capture([actualPotion.Potion]) : "");
                F("reward.relic.present", r.Relic is not null); F("reward.relic.resolved", r.Relic is null || r.Relic.IsResolved);
                F("reward.relic.definition", r.Relic?.Relic is { } item ? ReferenceBackend.Project(item.Id) : "");
                F("reward.relic.rarity", r.Relic?.Relic is { } rolledRelic ? rolledRelic.Rarity.ToString() : "");
            }
            return Records.Facts(facts);
        }
        public IReadOnlyList<StreamSnapshot> Streams(int index,string kind)
        {
            random.AtBoundary(index,kind);
            return random.Streams().Where(s => s.Draws > 0).Select(s =>
                s.Stream.EndsWith(":named.act_1_map", StringComparison.Ordinal)
                    ? s with { Stream = "base-spire:run:act_1_map" } : s).ToArray();
        }
        public DiagnosticSnapshot? Diagnostic()=>null;
        public IReadOnlyList<DecisionSnapshot> Execute(string action,string? token, IReadOnlyList<DecisionSnapshot>? expectedDecisions = null)
        {
            List<DecisionSnapshot> decisions=[];
            if (action == "base-spire:DiscardPotion")
            {
                var potion = player.PotionSlots.Where(p => p is not null).ElementAt(int.Parse(token!.Split(':').Last()))!;
                Sts2Sim.Core.Commands.PotionCmd.Discard(potion).GetAwaiter().GetResult();
                return decisions;
            }
            if(action=="base-spire:ChooseMapPoint")
            {
                var point=Travel.Single(p=>Token(p)==token);
                var roomType = RoomFactory.ResolveRoomType(run,point);
                if (!runLifecycle && roomType is not (RoomType.Monster or RoomType.Event or RoomType.Elite)) throw new CheckFailed("Normal prefix requires Monster, Event or actual Elite");
                lastRoomType = roomType.ToString();
                run.AddVisitedMapCoord(point.coord);
                if (runLifecycle && EnterVisit(roomType)) return decisions;
                if (roomType == RoomType.Event)
                {
                    eventRoom = (EventRoom)RoomFactory.CreateRoom(run, roomType);
                    run.PushRoom(eventRoom); eventRoom.Enter(run).GetAwaiter().GetResult();
                    random.ObserveEvent(eventRoom.Event);
                    return decisions;
                }
                if (roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
                    throw new CheckFailed("Normal Run room is not adapted: " + roomType);
                rooms++;
                room=(CombatRoom)RoomFactory.CreateRoom(run,roomType); run.PushRoom(room);
                random.ObserveEncounter(() => room.Enter(run).GetAwaiter().GetResult(),
                    () => BaseSpire.Slice.VanillaRandomLayout.EncounterStreamName(room.Encounter!.IdEntry, run.TotalFloor));
                return decisions;
            }
            if (runLifecycle && ExecuteVisit(action, token)) return decisions;
            if (eventRoom is not null)
            {
                var choice = eventRoom.Event.CurrentOptions.Single(o => EventOptionAction(o.Key) == action);
                var selector = new DeckSelection(expectedDecisions ?? [], decisions, player);
                // Public RunDriver construction installs an external decision source; it does not advance gameplay.
                _ = new RunDriver(run, selector);
                eventRoom.Event.ChooseOption(choice).GetAwaiter().GetResult();
                if (selector.Consumed != (expectedDecisions?.Count ?? 0)) throw new CheckFailed("Unconsumed permanent deck decision");
                settledHp = player.Creature.CurrentHp; settledMaxHp = player.Creature.MaxHp;
                if (!eventRoom.Event.IsFinished)
                {
                    if (!runLifecycle) throw new CheckFailed("Event did not finish");
                    return decisions;
                }
                eventRoom.Exit(run).GetAwaiter().GetResult(); run.PopCurrentRoom(); eventRoom = null;
                return decisions;
            }
            if (action == "base-spire:UsePotion")
            {
                var potion = player.PotionSlots.Where(p => p is not null).ElementAt(int.Parse(token!.Split(':').Last()))!;
                var selection = new OfferSelection(expectedDecisions ?? [], decisions);
                var previous = room!.Engine.State.CardSelectionSource;
                room.Engine.State.CardSelectionSource = selection;
                try
                {
                    // The registered first-use policies currently exercise the self/player card-choice potion.
                    Sts2Sim.Core.Entities.Creatures.Creature? target = potion.TargetType switch
                    {
                        Sts2Sim.Core.Entities.Cards.TargetType.Self or Sts2Sim.Core.Entities.Cards.TargetType.AnyPlayer => player.Creature,
                        _ => throw new CheckFailed("Potion target policy is not registered: " + potion.TargetType),
                    };
                    Sts2Sim.Core.Commands.PotionCmd.Use(potion, player, target).GetAwaiter().GetResult();
                    if (selection.Consumed != (expectedDecisions?.Count ?? 0)) throw new CheckFailed("Unconsumed common potion decisions");
                }
                finally { room.Engine.State.CardSelectionSource = previous; }
            }
            else if(action=="base-spire:PlayCard")
            {
                var i=int.Parse(token!.Split(':').Last()); var card=player.PlayerCombatState!.Hand.Cards[i];
                if(!card.CanPlay(out _))throw new CheckFailed("Reference selected card is not playable");
                var targets=CombatTargetCandidates.ForCard(room!.Engine.State,player,card.TargetType);
                var target=targets.FirstOrDefault();
                if (expectedDecisions is not null)
                {
                    if (targets.Count == 0)
                    {
                        // Non-target card choices, if any, are consumed by the native card selector below.
                    }
                    else
                    {
                        if (expectedDecisions.Count < 1) throw new CheckFailed("Common action requires a target decision");
                        var expected = expectedDecisions[0];
                        var candidates = targets.Select(t => ReferenceBackend.SlotOf(room.Engine.State,player,t)).ToArray();
                        if (expected.Minimum != 1 || expected.Maximum != 1 || !candidates.Order(StringComparer.Ordinal).SequenceEqual(expected.Candidates.Order(StringComparer.Ordinal)))
                            throw new CheckFailed("Common target candidate set differs");
                        target = targets.Single(t => ReferenceBackend.SlotOf(room.Engine.State,player,t) == expected.Chosen);
                    }
                }
                if(target is not null) decisions.Add(new("Target","",targets.Select(t=>ReferenceBackend.SlotOf(room.Engine.State,player,t)).ToArray(),1,1,ReferenceBackend.SlotOf(room.Engine.State,player,target)));
                var choices = expectedDecisions?.Skip(targets.Count == 0 ? 0 : 1).ToArray() ?? [];
                var selection = new PileSelection(choices, decisions, room.Engine.State, player);
                var previous = room.Engine.State.CardSelectionSource;
                room.Engine.State.CardSelectionSource = selection;
                try
                {
                    room.Engine.PlayCardAsync(player,card,target).GetAwaiter().GetResult();
                    if (selection.Consumed != choices.Length) throw new CheckFailed("Unconsumed common card selection");
                }
                finally { room.Engine.State.CardSelectionSource = previous; }
            }
            else if(action=="base-spire:EndTurn") room!.Engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
            else
            {
                var r=room!.Rewards ?? throw new CheckFailed("No real reward set");
                switch(action)
                {
                    case "base-spire:TakeRewardCard": r.Card.SelectOption(r.Card.Options[int.Parse(token!.Split(':').Last())]).GetAwaiter().GetResult();break;
                    case "base-spire:SkipRewardCard":r.Card.Skip().GetAwaiter().GetResult();break;
                    case "base-spire:TakeRewardPotion":r.Potion!.Take().GetAwaiter().GetResult();break;
                    case "base-spire:SkipRewardPotion":r.Potion!.Skip().GetAwaiter().GetResult();break;
                    case "base-spire:TakeRewardGold":r.Gold.Take().GetAwaiter().GetResult();break;
                    case "base-spire:TakeRewardRelic":r.Relic!.Take().GetAwaiter().GetResult();break;
                    case "base-spire:SkipRewardRelic":r.Relic!.Skip().GetAwaiter().GetResult();break;
                    default:throw new CheckFailed("Unsupported semantic action "+action);
                }
                if(r.Card.IsResolved && r.Gold.IsResolved && (r.Potion is null || r.Potion.IsResolved) && (r.Relic is null || r.Relic.IsResolved))
                { room.Exit(run).GetAwaiter().GetResult();run.PopCurrentRoom();room=null; }
                return decisions;
            }
            room!.Engine.CheckWinCondition();
            if(!room.Engine.IsInProgress)
            {
                if(!room.Engine.Won)
                {
                    if (!runLifecycle) throw new CheckFailed("Normal first combat lost; not a reward prefix");
                    settledHp = player.Creature.CurrentHp; settledMaxHp = player.Creature.MaxHp;
                    return decisions;
                }
                room.ResolveOutcomeAsync(true).GetAwaiter().GetResult();
                settledHp = player.Creature.CurrentHp;
                settledMaxHp = player.Creature.MaxHp;
                if(!room.Won || room.GeneratedRewards.Count!=1)throw new CheckFailed("Victory lifecycle did not generate one reward set");
                if (room.Rewards!.ExtraRewards.Any(r => !r.IsResolved)) throw new CheckFailed("Native extra rewards are not yet adapted");
            }
            return decisions;
        }

    }

    private static string EventOptionAction(string key) => key switch
    {
        "LET_GO" => "base-spire:AromaOfChaosLetGo",
        "MAINTAIN_CONTROL" => "base-spire:AromaOfChaosMaintainControl",
        "OVERCOME" => "base-spire:SlipperyBridgeOvercome",
        var option when option.StartsWith("HOLD_ON_", StringComparison.Ordinal) => "base-spire:SlipperyBridgeHoldOn",
        "DIVE_INTO_WATER" => "base-spire:SunkenStatueDiveIntoWater",
        "GRAB_SWORD" => "base-spire:SunkenStatueGrabSword",
        "DECIPHER" => "base-spire:TabletOfTruthDecipher",
        "SMASH" => "base-spire:TabletOfTruthSmash",
        "GIVE_UP" => "base-spire:TabletOfTruthGiveUp",
        _ => throw new CheckFailed("Unregistered event option " + key),
    };

    private sealed class DeckSelection(IReadOnlyList<DecisionSnapshot> expected, List<DecisionSnapshot> actual, Player player) : IRunDecisionSource
    {
        public int Consumed { get; private set; }
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => throw new CheckFailed("Unexpected map policy");
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => throw new CheckFailed("Unexpected combat policy");
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            string Token(CardModel card) => "deck:" + player.Deck.Cards.ToList().IndexOf(card);
            if (Consumed >= expected.Count) throw new CheckFailed("Missing common deck choice");
            var want = expected[Consumed++];
            var candidates = request.Candidates.Select(Token).ToArray();
            if (want.Minimum != request.MinCount || want.Maximum != request.MaxCount || !candidates.SequenceEqual(want.Candidates))
                throw new CheckFailed("Permanent deck candidates/order or bounds differ");
            var selected = request.Candidates.Single(card => Token(card) == want.Chosen);
            actual.Add(new("CardSelection", "", candidates, request.MinCount, request.MaxCount, want.Chosen));
            return Task.FromResult<IReadOnlyList<CardModel>>([selected]);
        }
    }

    private sealed class PileSelection(IReadOnlyList<DecisionSnapshot> expected, List<DecisionSnapshot> actual, CombatState state, Player player) : ICardSelectionDecisionSource
    {
        public int Consumed { get; private set; }
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            if (Consumed >= expected.Count) throw new CheckFailed("Missing common pile choice");
            var want = expected[Consumed++];
            string Token(CardModel card) => ReferenceBackend.SlotOf(state, player, card);
            var candidates = request.Candidates.Select(Token).ToArray();
            if (want.Minimum != request.MinCount || want.Maximum != request.MaxCount || !candidates.SequenceEqual(want.Candidates))
                throw new CheckFailed("Common pile candidates/order or bounds differ");
            IReadOnlyList<CardModel> selected = want.Chosen == "cancel" && request.Cancelable ? []
                : [request.Candidates.Single(card => Token(card) == want.Chosen)];
            actual.Add(new("CardSelection", "", candidates, request.MinCount, request.MaxCount, want.Chosen));
            return Task.FromResult(selected);
        }
    }

    private sealed class OfferSelection(IReadOnlyList<DecisionSnapshot> expected, List<DecisionSnapshot> actual) : ICardSelectionDecisionSource
    {
        public int Consumed { get; private set; }
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            string Token(CardModel card) => "offer:" + ReferenceBackend.Project(card.Id) + (card.IsUpgraded ? "+" : "");
            var candidates = request.Candidates.Select(Token).ToArray();
            if (Consumed >= expected.Count) throw new CheckFailed("Missing common generated-card decision");
            var want = expected[Consumed++];
            if (want.Minimum != request.MinCount || want.Maximum != request.MaxCount || !candidates.SequenceEqual(want.Candidates))
                throw new CheckFailed("Generated-card candidates/order or bounds differ before selection");
            IReadOnlyList<CardModel> selected = want.Chosen == "cancel" && request.Cancelable
                ? [] : [request.Candidates.Single(card => Token(card) == want.Chosen)];
            actual.Add(new("CardSelection", "", candidates, request.MinCount, request.MaxCount, want.Chosen));
            return Task.FromResult(selected);
        }
    }
}
#else
namespace Differential;
internal static partial class NormalRewardEntry
{
    public static CaseRecord RunReference(FixtureCase fixture, CaseRecord? actionSource = null) => throw new CheckFailed("Reference adapter unavailable");
}
#endif
