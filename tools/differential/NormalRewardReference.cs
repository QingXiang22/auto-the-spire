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
    public static CaseRecord RunReference(FixtureCase fixture)
    {
        List<BoundaryRecord> records = [];
        using var random = new ReferenceRandomProbe();
        string status = "ok"; string? detail = null; long before = 0;
        IReadOnlyList<(string Type, string Stream, uint Seed)> applied = [];
        try
        {
            Validate(fixture); CombatReplayDemo.EnsureModelsRegistered();
            var run = new RunState(fixture.GameSeed!, new Overgrowth());
            applied = RngSourceCompat.Apply(run.Rng, fixture.GameSeed!);
            var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), run);
            run.AddPlayer(player); random.Attach(run.Rng, player); random.MarkInitialization(); before = random.Count;
            Drive(new ReferenceSide(run, player, random), records, fixture.Script.Single().Action == "normal_reward_skip");
        }
        catch (Exception e) { status = "faulted"; detail = e.GetType().Name + ": " + e.Message; }
        return new(fixture.Id, ReferenceBackend.Name, "normal-reward-prefix@1", fixture.RngProfile,status,detail,random.Count != before,
            ["Independent normal RunState/Overgrowth/player. Native map retained; source-compatible run streams applied before player/room draws.",
             "Normal travel uses MapTravel, AddVisitedMapCoord, RoomFactory, PushRoom, Enter. Victory requires Engine.Won then ResolveOutcomeAsync(true); real SelectOption/Take/Skip consumers.",
             "Known source boundary: RunState constructor generates its map before Apply; no transplant or private map replacement. Native map RNG remains observed evidence, not a synthetic comparable stream."], records)
        { SessionStatus = status == "ok" ? "Running" : "Faulted", InitializationRandomCount = before,
          ReferenceIdentity = ReferenceRunEntry.Identity(fixture.GameSeed!, applied), ReferenceStreams = random.Evidence() };
    }

    private sealed class ReferenceSide(RunState run, Player player, ReferenceRandomProbe random) : Side
    {
        private CombatRoom? room;
        private int rooms;
        public string Phase => room is null ? (rooms == 0 ? "map" : "travel-ready") : room.Engine.IsInProgress ? "combat" : "rewards";
        private string Token(MapPoint p) => $"map:{p.coord.col},{p.coord.row}";
        private IEnumerable<MapPoint> Travel => MapTravel.GetTravelablePointsFrom(run,run.CurrentMapPoint ?? run.Map.StartingMapPoint);
        private string Card(CardModel c) => ReferenceBackend.Project(c.Id) + (c.IsUpgraded ? "+" : "");
        public IReadOnlyList<ActionOption> Actions()
        {
            if (room is null) return [new("base-spire:ChooseMapPoint",Travel.Select(Token).Order(StringComparer.Ordinal).ToArray(), Travel.Count())];
            if (room.Engine.IsInProgress) return ReferenceBackend.LegalActions(room.Engine.State,player);
            var rewards = room.Rewards ?? throw new CheckFailed("Real victory produced no rewards");
            List<ActionOption> a = [];
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
            return a.OrderBy(x=>x.Action,StringComparer.Ordinal).ToArray();
        }
        public IReadOnlyList<FactSnapshot> Facts()
        {
            List<FactSnapshot> facts = [];
            void F(string path,object value)=> facts.Add(new(path,Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)!.ToLowerInvariant()));
            F("run.phase",Phase); F("run.gold",player.Gold); F("run.hp",player.Creature.CurrentHp); F("run.max_hp",player.Creature.MaxHp); F("run.room",rooms);
            F("run.map_floor",run.CurrentMapCoord is { } coord ? coord.row+1 : 0);
            F("run.map_coord",run.CurrentMapPoint is { } point ? Token(point) : Token(run.Map.StartingMapPoint));
            F("deck.order",string.Join(",",player.Deck.Cards.Select(Card))); F("deck.instance_count",player.Deck.Cards.Distinct().Count());
            for(var i=0;i<player.Deck.Cards.Count;i++) F("deck.instance:"+i,Card(player.Deck.Cards[i]));
            F("potion.capacity",player.MaxPotionCount);
            F("potion.inventory",string.Join(",",player.PotionSlots.Select((p,i)=>(p,i)).Where(x=>x.p is not null).Select(x=>x.i+":"+ReferenceBackend.Project(x.p!.Id))));
            foreach(var p in run.Map.GetAllMapPoints().Append(run.Map.StartingMapPoint).Append(run.Map.BossMapPoint))
            {
                F("map."+Token(p)+".type",p.PointType==MapPointType.Monster ? "Monster" : "type:"+(int)p.PointType);
                foreach(var child in p.Children) F("map.edge."+Token(p)+"->"+Token(child),true);
            }
            if(Phase=="combat")
            {
                facts.AddRange(ReferenceBackend.Facts(room!.Engine.State,player,true));
                foreach(var (c,i) in player.PlayerCombatState!.Hand.Cards.Select((c,i)=>(c,i))) F("visible.card:hand:"+i+".definition",ReferenceBackend.Project(c.Id));
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
            }
            return Records.Facts(facts);
        }
        public IReadOnlyList<StreamSnapshot> Streams(int index,string kind)
        {
            random.AtBoundary(index,kind);
            return random.Streams();
        }
        public DiagnosticSnapshot? Diagnostic()=>null;
        public IReadOnlyList<DecisionSnapshot> Execute(string action,string? token)
        {
            List<DecisionSnapshot> decisions=[];
            if(action=="base-spire:ChooseMapPoint")
            {
                var point=Travel.Single(p=>Token(p)==token);
                if(RoomFactory.ResolveRoomType(run,point)!=RoomType.Monster) throw new CheckFailed("First room must be Monster");
                run.AddVisitedMapCoord(point.coord); rooms++;
                room=(CombatRoom)RoomFactory.CreateRoom(run,RoomType.Monster); run.PushRoom(room); room.Enter(run).GetAwaiter().GetResult();
                return decisions;
            }
            if(action=="base-spire:PlayCard")
            {
                var i=int.Parse(token!.Split(':').Last()); var card=player.PlayerCombatState!.Hand.Cards[i];
                if(!card.CanPlay(out _))throw new CheckFailed("Reference selected card is not playable");
                var targets=CombatTargetCandidates.ForCard(room!.Engine.State,player,card.TargetType);
                var target=targets.FirstOrDefault();
                if(target is not null) decisions.Add(new("Target","",targets.Select(t=>ReferenceBackend.SlotOf(room.Engine.State,player,t)).ToArray(),1,1,ReferenceBackend.SlotOf(room.Engine.State,player,target)));
                room.Engine.PlayCardAsync(player,card,target).GetAwaiter().GetResult();
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
                    default:throw new CheckFailed("Unsupported semantic action "+action);
                }
                if(r.Card.IsResolved && r.Gold.IsResolved && (r.Potion is null || r.Potion.IsResolved))
                { room.Exit(run).GetAwaiter().GetResult();run.PopCurrentRoom();room=null; }
                return decisions;
            }
            if(!room!.Engine.IsInProgress)
            {
                if(!room.Engine.Won)throw new CheckFailed("Normal first combat lost; not a reward prefix");
                room.ResolveOutcomeAsync(true).GetAwaiter().GetResult();
                if(!room.Won || room.GeneratedRewards.Count!=1)throw new CheckFailed("Victory lifecycle did not generate one reward set");
            }
            return decisions;
        }
    }
}
#else
namespace Differential;
internal static partial class NormalRewardEntry
{
    public static CaseRecord RunReference(FixtureCase fixture) => throw new CheckFailed("Reference adapter unavailable");
}
#endif
