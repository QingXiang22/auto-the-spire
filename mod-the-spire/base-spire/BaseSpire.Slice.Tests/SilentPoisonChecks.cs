using BaseSpire.Slice;
using CoreTheSpire.Engine;
using System.Text.RegularExpressions;

namespace BaseSpire.Slice.Tests;

// Source oracles: sts2/src/Core/Models/Cards/<Name>.cs and Powers/<Name>Power.cs.
// Formal content in controlled battles; does not claim Silent Run or independent differential coverage.
internal static class SilentPoisonChecks
{
    private sealed record Case(string Id, int Cost, int UpCost, string Kind, string Rarity,
        int Amount, int UpAmount, string Power = "poison", bool Self = false, bool All = false);
    private static readonly Case[] Cases = [
        new("accelerant",1,1,"Power","rare",1,2,"accelerant",Self:true),
        new("bouncing-flask",2,2,"Skill","uncommon",9,12),
        new("bubble-bubble",1,1,"Skill","uncommon",9,12),
        new("corrosive-wave",1,1,"Skill","rare",2,3,"corrosive-wave-power",Self:true),
        new("deadly-poison",1,1,"Skill","common",5,7),
        new("envenom",2,2,"Power","rare",1,2,"envenom-power",Self:true),
        new("haze",3,3,"Skill","uncommon",4,6,All:true),
        new("mirage",1,0,"Skill","uncommon",0,0),
        new("noxious-fumes",1,1,"Power","uncommon",2,3,"noxious-fumes-power",Self:true),
        new("outbreak",1,1,"Power","uncommon",11,15,"outbreak-power",Self:true),
        new("poisoned-stab",1,1,"Attack","common",3,4),
        new("snakebite",2,2,"Skill","common",7,10),
    ];
    private static readonly Dictionary<bool,RuleSet> Rules = [];
    public static void RunSourceChecks()
    {
        Check.Run("Silent poison: source pool and directly referenced powers identify the complete family",()=> {
            var root=new DirectoryInfo(Environment.CurrentDirectory);
            while(root is not null && !File.Exists(Path.Combine(root.FullName,"AutoTheSpire.sln"))) root=root.Parent;
            Check.True(root is not null,"run source checks in this repository");
            var models=Path.Combine(root!.FullName,"sts2/src/Core/Models");
            var pool=File.ReadAllText(Path.Combine(models,"CardPools/SilentCardPool.cs"));
            var names=Regex.Matches(pool,@"ModelDb.Card<([^>]+)>").Select(m=>m.Groups[1].Value);
            var family=names.Where(name=> {
                var card=File.ReadAllText(Path.Combine(models,"Cards",name+".cs"));
                return card.Contains("PoisonPower",StringComparison.Ordinal) ||
                    Regex.Matches(card,@"PowerCmd.Apply<([^>]+)>").Any(match=> {
                        var path=Path.Combine(models,"Powers",match.Groups[1].Value+".cs");
                        return File.Exists(path) && File.ReadAllText(path).Contains("PoisonPower",StringComparison.Ordinal);
                    });
            }).Select(SpireCardSource.ToId).ToArray();
            Check.Sequence(family,Cases.Select(c=>c.Id),"independent source-derived family, not DSL-derived denominator");
        });
    }
    public static void Run()
    {
        Check.Run("Silent poison: all 12 source identities, metadata and power links load",()=> {
            var content=FormalSpirePackage.Load();
            foreach(var entry in Cases)
            {
                var card=content.Model.Cards.Single(c=>c.Id==Id(entry.Id));
                Check.Equal(entry.Kind,card.Kind,entry.Id+" type");
                Check.Equal(entry.Rarity,card.Rarity,entry.Id+" rarity");
                Check.Sequence(new[]{"SilentCardPool"},card.Pools,entry.Id+" pool");
                Check.True(card.Upgradeable && !card.MultiplayerOnly,entry.Id+" single player upgradeable");
            }
            Check.True(content.Model.Cards.Single(c=>c.Id==Id("haze")).NormalSly,"Haze has native Sly");
        });
        foreach(var entry in Cases) foreach(var mode in new[]{"normal","created-upgraded","armaments-upgraded"})
            Check.Run($"Silent poison: {entry.Id}/{mode} source effects, payment and result",()=>Verify(entry,mode));
        foreach(var up in new[]{false,true})
        {
            Check.Run($"Silent poison: BubbleBubble requires target poison / upgraded={up}",()=> {
                var s=Start(up); var enemy=Enemies(s)[0]; Play(s,"bubble-bubble",enemy);
                Check.Equal(0L,Stacks(s,enemy),"no poison does not acquire poison");
            });
            Check.Run($"Silent poison: Accelerant real card ticks decreasing amounts / upgraded={up}",()=> {
                var s=Start(up); var enemy=Enemies(s)[0]; Play(s,"accelerant"); Play(s,"deadly-poison",enemy);
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(up ? 982L : 991L,Hp(s,enemy),"7+6+5 or 5+4, never repeated initial amount");
                Check.Equal(up ? 4L : 3L,Stacks(s,enemy),"decrement once per tick");
            });
            Check.Run($"Silent poison: NoxiousFumes starts next turn, stacks and repeats / upgraded={up}",()=> {
                var s=Start(up); var enemy=Enemies(s)[0]; Play(s,"noxious-fumes");
                Check.Equal(0L,Stacks(s,enemy),"not on play");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.True(Enemies(s).All(e=>Stacks(s,e)==(up?3:2)),"all living opponents receive poison");
                Check.Equal(1000L,Hp(s,enemy),"no tick until enemy side");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(up?997L:998L,Hp(s,enemy),"enemy tick before next fumes");
                Check.Equal(up?5L:3L,Stacks(s,enemy),"persistent fumes stacks after decrement");
            });
            Check.Run($"Silent poison: CorrosiveWave counts actual draws and expires before hand draw / upgraded={up}",()=> {
                var s=Start(up); Play(s,"corrosive-wave"); Play(s,"adrenaline");
                Check.True(Enemies(s).All(e=>Stacks(s,e)==(up?6:4)),"two cards drawn, each hits all opponents");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(0L,FormalSliceFixture.PowerStacks(s,Player(s),Id("corrosive-wave-power")),"expires at owner turn end");
                Check.True(Enemies(s).All(e=>Stacks(s,e)==(up?5:3)),"next hand draw adds no poison");
            });
            Check.Run($"Silent poison: Envenom only unblocked powered attacks / upgraded={up}",()=> {
                var s=Start(up); var enemies=Enemies(s); Play(s,"envenom");
                Set(s,enemies[0],"Block",20); Play(s,"dagger-spray");
                Check.Equal(0L,Stacks(s,enemies[0]),"fully blocked hits do not poison");
                Check.Equal(up?4L:2L,Stacks(s,enemies[1]),"each unblocked hit poisons separately");
            });
            Check.Run($"Silent poison: Outbreak counts applications, carries remainder and ignores poison ticks / upgraded={up}",()=> {
                var s=Start(up); var enemies=Enemies(s); Play(s,"outbreak"); Play(s,"deadly-poison",enemies[0]);
                Check.Equal(1000L,Hp(s,enemies[0]),"seven or five stacks is one application");
                Play(s,"haze");
                Check.True(enemies.All(e=>Hp(s,e)==1000-(up?15:11)),"one targeted plus two area applications trigger once");
                Play(s,"bouncing-flask");
                Check.True(enemies.All(e=>Hp(s,e)==1000-2*(up?15:11)),"three more applications trigger once, remainder preserved");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(up?15L:11L,FormalSliceFixture.PowerStacks(s,Player(s),Id("outbreak-power")),"damage magnitude stays as stacks");
            });
            Check.Run($"Silent poison: Haze Sly is free on manual discard, ordinary hand flush does not play / upgraded={up}",()=> {
                var s=Start(up); var haze=FormalSliceFixture.MoveToHand(s,Id("haze"));
                var survivor=FormalSliceFixture.MoveToHand(s,Id("survivor"));
                Energy(s,1); FormalSliceFixture.PlayAt(s,survivor,haze); Healthy(s);
                Check.True(Enemies(s).All(e=>Stacks(s,e)==(up?6:4)),"discarded Haze actually auto plays");
                Check.Equal(0L,Number(s,Player(s),FormalSliceFixture.PlayerTypeId,"Energy"),"Sly does not pay three energy");
                Check.True(Members(s,FormalSliceFixture.DiscardPile).Contains(haze),"normal result returns to discard");
                var flush=Start(up); FormalSliceFixture.MoveToHand(flush,Id("haze"));
                FormalSliceFixture.EndTurn(flush); Healthy(flush);
                Check.True(Enemies(flush).All(e=>Stacks(flush,e)==0),"end-turn flush is not manual discard");
            });
        }
        Check.Run("Silent poison: Artifact blocks one application without advancing Outbreak",()=> {
            var s=Start(false); var enemy=Enemies(s)[0]; Play(s,"outbreak");
            FormalSliceFixture.GivePower(s,enemy,Id("artifact"),1);
            Play(s,"deadly-poison",enemy);
            Check.Equal(0L,Stacks(s,enemy),"blocked poison does not attach");
            Check.Equal(0L,FormalSliceFixture.PowerStacks(s,enemy,Id("artifact")),"one Artifact consumed");
            Play(s,"haze");
            Check.True(Enemies(s).All(e=>Hp(s,e)==1000),"blocked application is not counted");
            Play(s,"deadly-poison",enemy);
            Check.True(Enemies(s).All(e=>Hp(s,e)==989),"third successful application triggers once");
        });
        Check.Run("Silent poison: lethal PoisonedStab does not attach poison to a dead target",()=> {
            var s=Start(false); var enemy=Enemies(s)[0]; Set(s,enemy,"Hp",6);
            Play(s,"poisoned-stab",enemy);
            Check.Equal(0L,Stacks(s,enemy),"dead target cannot receive poison");
            Check.Equal(1,Enemies(s).Count,"other opponent survives");
        });
        Check.Run("Silent poison: Envenom-triggered Outbreak emits each death once",()=> {
            var s=Start(false,trace:true); var enemies=Enemies(s).ToArray();
            Play(s,"outbreak"); Play(s,"envenom"); Play(s,"haze");
            foreach(var enemy in enemies) Set(s,enemy,"Hp",12);
            Set(s,enemies[1],"Hp",11);
            Play(s,"poisoned-stab",enemies[0]);
            Check.Equal(0,Enemies(s).Count,"attack plus triggered blast kills both");
            Check.Equal(2,s.Debug!.Records.Count(r=>r.Kind==SessionDebugKind.EventEmit && r.Subject==Id("MonsterDied")),"exactly one confirmed death per victim");
            Check.Equal(0L,s.Debug.DroppedRecords,"full event evidence captured");
        });
        Check.Run("Silent poison: Haze waits for the full selected discard batch",()=> {
            var s=Start(false,trace:true); var haze=FormalSliceFixture.MoveToHand(s,Id("haze"));
            var prepared=FormalSliceFixture.MoveToHand(s,Id("prepared"));
            var wound=FormalSliceFixture.MoveToHand(s,"probe:discard-witness");
            Play(s,"outbreak");
            foreach(var enemy in Enemies(s)) Set(s,enemy,"Hp",10);
            Energy(s,0);
            var play=s.LegalActions(SliceManifest.Player).Single(a=>a.Definition.QualifiedId==FormalSliceFixture.PlayCardAction);
            s.Submit(play.Definition,SliceManifest.Player,[Value.OfRef(prepared)]);
            Check.Equal(SessionStatus.AwaitingDecision,s.Status,"Prepared+ pauses for two selected cards");
            var pending=s.PendingDecision!; s.Answer(new DecisionAnswer(pending.RequestId,[haze,wound])); Healthy(s);
            Check.True(Members(s,FormalSliceFixture.DiscardPile).Contains(wound),"second chosen card discarded before Sly resolves");
            Check.Equal(0L,Number(s,Player(s),FormalSliceFixture.PlayerTypeId,"Block"),"second discard hook observes no poison before queued Haze");
            Check.True(Enemies(s).All(e=>Stacks(s,e)==4),"Haze auto plays once after both discards");
        });
        Check.Run("Silent poison: random bounces stop sampling after Outbreak clears the living set",()=> {
            var s=Start(false); Play(s,"outbreak"); Play(s,"haze");
            foreach(var enemy in Enemies(s)) Set(s,enemy,"Hp",11);
            Play(s,"bouncing-flask");
            Check.Equal(0,Enemies(s).Count,"first bounce triggers lethal Outbreak");
            Check.Equal(1L,s.Random.DrawCount(SliceManifest.RandomTargetStream),"later bounces have no target and draw no random words");
        });
        Check.Run("Silent poison: Outbreak remainder crosses turns and ignores decrement events",()=> {
            var s=Start(false); var enemies=Enemies(s).ToArray(); Play(s,"outbreak"); Play(s,"deadly-poison",enemies[0]);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(995L,Hp(s,enemies[0]),"only natural poison damage");
            Check.Equal(1000L,Hp(s,enemies[1]),"poison decrement does not trigger the blast");
            Play(s,"haze");
            Check.Equal(984L,Hp(s,enemies[0]),"remainder one plus two area applications triggers");
            Check.Equal(989L,Hp(s,enemies[1]),"one unpowered blast");
        });
        Check.Run("Silent poison: Mirage excludes dead targets and applies ordinary Dexterity and Frail",()=> {
            var s=Start(false); var enemies=Enemies(s).ToArray();
            FormalSliceFixture.GivePower(s,enemies[0],Id("poison"),5);
            FormalSliceFixture.GivePower(s,enemies[1],Id("poison"),20);
            Set(s,enemies[1],"Hp",0);
            FormalSliceFixture.GivePower(s,Player(s),Id("dexterity"),3);
            FormalSliceFixture.GivePower(s,Player(s),Id("frail"),1);
            Play(s,"mirage");
            Check.Equal(6L,Number(s,Player(s),FormalSliceFixture.PlayerTypeId,"Block"),"(5+3)*0.75, dead poison excluded");
        });
        Check.Run("Silent poison: CorrosiveWave does not fire for prevented draws",()=> {
            foreach(var full in new[]{false,true}) {
                var s=Start(false); Play(s,"corrosive-wave");
                var card=FormalSliceFixture.MoveToHand(s,Id("adrenaline"));
                if(full) {
                    foreach(var c in Members(s,FormalSliceFixture.DrawPile).Take(9).ToArray())
                        Move(s,c,FormalSliceFixture.DrawPile,FormalSliceFixture.HandPile);
                } else FormalSliceFixture.GivePower(s,Player(s),Id("no-draw"),1);
                Energy(s,1); FormalSliceFixture.PlayAt(s,card,Enemies(s)[0]); Healthy(s);
                Check.True(Enemies(s).All(e=>Stacks(s,e)==(full?2:0)),"playing frees one slot; only the actual first draw triggers, NoDraw triggers neither");
            }
        });
        Check.Run("Silent poison: lethal CorrosiveWave/Outbreak stops the remaining draw",()=> {
            var s=Start(false); Play(s,"outbreak"); Play(s,"deadly-poison",Enemies(s)[0]); Play(s,"corrosive-wave");
            foreach(var enemy in Enemies(s)) Set(s,enemy,"Hp",11);
            var card=FormalSliceFixture.MoveToHand(s,Id("adrenaline"));
            var before=Members(s,FormalSliceFixture.HandPile).Count;
            Energy(s,1); FormalSliceFixture.PlayAt(s,card,Enemies(s)[0]); Healthy(s);
            Check.Equal(0,Enemies(s).Count,"first actual draw supplies two applications and clears combat");
            Check.Equal(before,Members(s,FormalSliceFixture.HandPile).Count,"played card leaves hand and only one card is drawn");
        });
    }
    private static void Verify(Case entry,string mode)
    {
        var up=mode!="normal"; var s=Start(mode=="created-upgraded"); var enemy=Enemies(s)[0];
        var card=FormalSliceFixture.MoveToHand(s,Id(entry.Id));
        if(mode=="armaments-upgraded") Play(s,"armaments",card);
        if(entry.Id=="bubble-bubble") FormalSliceFixture.GivePower(s,enemy,Id("poison"),1);
        if(entry.Id=="mirage") { FormalSliceFixture.GivePower(s,enemy,Id("poison"),5); FormalSliceFixture.GivePower(s,Enemies(s)[1],Id("poison"),7); }
        SetPlayer(s,"Block",0); Energy(s,10);
        var decisions=FormalSliceFixture.PlayAtCountingDecisions(s,card,enemy); Healthy(s);
        Check.Equal(10L-(up?entry.UpCost:entry.Cost),Number(s,Player(s),FormalSliceFixture.PlayerTypeId,"Energy"),"variant fee");
        if(entry.Id=="bouncing-flask") {
            Check.Equal((long)(up?entry.UpAmount:entry.Amount),Enemies(s).Sum(e=>Stacks(s,e)),"3 stacks per independent bounce");
            Check.Equal(up?4L:3L,s.Random.DrawCount(SliceManifest.RandomTargetStream),"one random target draw per bounce");
        } else if(entry.Id=="mirage") {
            Check.Equal(12L,Number(s,Player(s),FormalSliceFixture.PlayerTypeId,"Block"),"sum both enemies, not a selected target");
            Check.True(Members(s,FormalSliceFixture.ExhaustPile).Contains(card),"Mirage exhausts");
        } else {
            Check.Equal((long)(up?entry.UpAmount:entry.Amount)+(entry.Id=="bubble-bubble"?1:0),
                FormalSliceFixture.PowerStacks(s,entry.Self?Player(s):enemy,Id(entry.Power)),"source amount and owner");
            Check.Equal(entry.All?(long)(up?entry.UpAmount:entry.Amount):0L,Stacks(s,Enemies(s)[1]),"area versus selected target");
        }
        Check.Equal(entry.Id=="poisoned-stab" ? up?992L:994L :1000L,Hp(s,enemy),"attack resolves before poison");
        Check.Equal(entry.Self || entry.All || entry.Id is "mirage" or "bouncing-flask" ? 0:1,decisions,"only targeted cards ask once");
        if(entry.Kind=="Power") Check.True(!Members(s,FormalSliceFixture.DiscardPile).Contains(card),"Power leaves combat piles");
        if(entry.Id=="snakebite") {
            var retained=Start(up); var held=FormalSliceFixture.MoveToHand(retained,Id("snakebite"));
            FormalSliceFixture.EndTurn(retained); Healthy(retained);
            Check.True(Members(retained,FormalSliceFixture.HandPile).Contains(held),"Snakebite retains");
        }
    }
    private static Session Start(bool upgraded,bool trace=false)
    {
        if(!Rules.TryGetValue(upgraded,out var rules)) {
            var model=FormalSpirePackage.Load().Model;
            var witness=SpireCardSource.Parse("""
                using namespace probe;
                dsl_version = "base/0.0.2";
                card DiscardWitness {
                  common = { type = skill rarity = common cost = 0 target = { selectSelf = {} }
                    keywords = {} play = {} hooks = { onDiscard = { gainBlock = { amount = enemyPowerSum(power=base-spire:poison) } } } }
                  normal = {} upgraded = {}
                }
                ""","discard-witness.spire",new Dictionary<string,string>{{"base-spire:poison",Id("poison")}});
            Check.Equal(0,witness.Unsupported.Count,string.Join(";",witness.Unsupported.Select(c=>c.Reason)));
            model=model with { Cards=[..model.Cards,..witness.Cards] };
            var deck=Cases.Select(c=>new AuthorDeckEntry(Id(c.Id),1,upgraded)).Concat(new[]{
                new AuthorDeckEntry(Id("armaments"),1),new AuthorDeckEntry(Id("adrenaline"),1,upgraded),
                new AuthorDeckEntry(Id("dagger-spray"),1,upgraded),new AuthorDeckEntry(Id("survivor"),1),
                new AuthorDeckEntry(Id("prepared"),1,true),
                new AuthorDeckEntry("probe:discard-witness",1),
                new AuthorDeckEntry(Id("wound"),30)}).ToArray();
            var builder=new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel:model with { Deck=deck }).Build());
            Rules[upgraded]=rules=builder.Build();
        }
        var s=new Session(rules,20261017UL,[SliceManifest.PlayerRole],computeBackend:SourceWeightedPickBackend.Instance,
            debug:trace?new SessionDebugOptions(MaxRecords:50000):null);
        s.Start(); Healthy(s);
        foreach(var enemy in Enemies(s)) Set(s,enemy,"Hp",1000);
        // Keep room for actual draw; controlled fixture only changes piles before any card action.
        s.State.RunAtomic(()=> { foreach(var c in Members(s,FormalSliceFixture.HandPile).ToArray()) {
            s.State.RemoveFromCollection(FormalSliceFixture.Collection(s,FormalSliceFixture.HandPile),c);
            s.State.AddToCollection(FormalSliceFixture.Collection(s,FormalSliceFixture.DrawPile),c); } });
        return s;
    }
    private static void Play(Session s,string id,InstanceId target=default) { Energy(s,10);
        FormalSliceFixture.PlayAt(s,FormalSliceFixture.MoveToHand(s,Id(id)),target==default?Enemies(s)[0]:target); Healthy(s); }
    private static string Id(string id)=>"base-spire:"+id;
    private static InstanceId Player(Session s)=>FormalSliceFixture.Player(s);
    private static IReadOnlyList<InstanceId> Enemies(Session s)=>Members(s,"base-spire:Monsters");
    private static IReadOnlyList<InstanceId> Members(Session s,string collection)=>s.State.CollectionMembers(FormalSliceFixture.Collection(s,collection));
    private static long Stacks(Session s,InstanceId e)=>FormalSliceFixture.PowerStacks(s,e,Id("poison"));
    private static long Hp(Session s,InstanceId e)=>Number(s,e,FormalSliceFixture.MonsterTypeId,"Hp");
    private static long Number(Session s,InstanceId e,string type,string field)=>FormalSliceFixture.Number(s,e,type,field);
    private static void Set(Session s,InstanceId e,string field,long value)=>FormalSliceFixture.SetNumber(s,e,FormalSliceFixture.MonsterTypeId,field,value);
    private static void SetPlayer(Session s,string field,long value)=>FormalSliceFixture.SetNumber(s,Player(s),FormalSliceFixture.PlayerTypeId,field,value);
    private static void Energy(Session s,long value)=>SetPlayer(s,"Energy",value);
    private static void Move(Session s,InstanceId card,string from,string to)=>s.State.RunAtomic(()=> {
        s.State.RemoveFromCollection(FormalSliceFixture.Collection(s,from),card);
        s.State.AddToCollection(FormalSliceFixture.Collection(s,to),card); });
    private static void Healthy(Session s)=>Check.True(s.Status!=SessionStatus.Faulted,$"{s.Fault}");
}
