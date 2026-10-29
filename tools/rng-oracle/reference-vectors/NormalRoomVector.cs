using System.Text.Json;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Rooms;

internal static class NormalRoomVector {
 internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string,string> hashes) {
RunReplayDemo.EnsureModelsRegistered();
string Id(AbstractModel m)=>"base-spire:"+m.Id.Entry.ToLowerInvariant().Replace('_','-');
var vectors=new List<object>();
foreach(var kind in new[]{"shop","treasure"}) {
var run=new RunState("777",[new Overgrowth(),new Hive(),new Glory()]);
var player=Player.CreateForNewRun(ModelDb.Character<Ironclad>(),run);run.AddPlayer(player);
run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
var neow=(EventRoom)RoomFactory.CreateAncientEventRoom(run);run.PushRoom(neow);await neow.Enter(run);
await neow.Event.ChooseOption(neow.Event.CurrentOptions.Single(o=>o.Key=="BoomingConch"));await neow.Exit(run);run.PopCurrentRoom();
AbstractRoom room=kind=="shop"?new MerchantRoom():new TreasureRoom();run.PushRoom(room);await room.Enter(run);
if(room is MerchantRoom shop) {
 var stock=shop.Inventory;
 vectors.Add(new {kind, cards=stock.Cards.Select(e=>new{id=Id(e.Card),price=e.Price}),relics=stock.Relics.Select(e=>new{id=Id(e.Relic),price=e.Price}),potions=stock.Potions.Select(e=>new{id=Id(e.Potion),price=e.Price}),rewards=player.PlayerRng.Rewards.Counter,shops=player.PlayerRng.Shops.Counter});
} else vectors.Add(new {kind,gold=player.Gold,relics=player.Relics.Select(Id),rewards=player.PlayerRng.Rewards.Counter,treasure=run.Rng.TreasureRoomRelics.Counter});
}
return new { reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes, vectors };
 }
}
