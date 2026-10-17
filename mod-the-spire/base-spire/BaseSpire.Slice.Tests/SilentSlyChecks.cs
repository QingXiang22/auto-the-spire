using BaseSpire.Slice;
using CoreTheSpire.Engine;
using System.Text.RegularExpressions;

namespace BaseSpire.Slice.Tests;

// Oracles: frozen native Cards/<Name>.cs, CardCmd.Discard and ThornsPower.BeforeDamageReceived.
// Controlled formal content, not a claim of a normal Silent Run.
internal static class SilentSlyChecks
{
    private sealed record Case(string Id, int Cost, int UpCost, int Amount, int UpAmount);
    private static readonly Case[] Cases = [
        new("abrasive",3,3,4,6), new("flick-flack",1,1,6,8),
        new("reflex",3,3,2,3), new("ricochet",2,2,4,5),
        new("tactician",3,3,1,2), new("untouchable",2,2,6,9),
        new("hand-trick",1,1,7,10), new("master-planner",2,1,1,1),
    ];
    private static readonly Dictionary<bool,RuleSet> Rules = [];
    public static void RunSourceChecks()
    {
        Check.Run("Sly: independent native single-player keyword family and grant consumers",()=> {
            var root=new DirectoryInfo(Environment.CurrentDirectory);
            while(root is not null && !File.Exists(Path.Combine(root.FullName,"AutoTheSpire.sln"))) root=root.Parent;
            Check.True(root is not null,"source checks require this repository");
            var models=Path.Combine(root!.FullName,"sts2/src/Core/Models");
            var pool=File.ReadAllText(Path.Combine(models,"CardPools/SilentCardPool.cs"));
            var names=Regex.Matches(pool,@"ModelDb.Card<([^>]+)>").Select(m=>m.Groups[1].Value);
            var actual=names.Where(n=> {
                var source=File.ReadAllText(Path.Combine(models,"Cards",n+".cs"));
                return source.Contains("CanonicalKeywords =>") && source.Contains("CardKeyword.Sly")
                    && !source.Contains("MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly");
            }).Select(SpireCardSource.ToId).Order(StringComparer.Ordinal);
            Check.Sequence(new[]{"abrasive","flick-flack","haze","reflex","ricochet","tactician","untouchable"},actual,"complete solo native Sly set");
            Check.True(File.ReadAllText(Path.Combine(models,"Cards/HandTrick.cs")).Contains("ApplySingleTurnSly"),"HandTrick grants single-turn state");
            Check.True(File.ReadAllText(Path.Combine(models,"Powers/MasterPlannerPower.cs")).Contains("CardCmd.ApplyKeyword"),"Planner adds a local keyword");
        });
    }
    public static void Run()
    {
        foreach(var entry in Cases) foreach(var mode in new[]{"normal","created-upgraded","armaments-upgraded"})
            Check.Run($"Sly: {entry.Id}/{mode} actual formal effects and cost",()=>Verify(entry,mode));
        foreach(var entry in Cases.Take(6)) foreach(var up in new[]{false,true})
            Check.Run($"Sly: discarded {entry.Id}/upgraded={up} plays free once",()=> {
                var s=Start(up); var card=Hand(s,entry.Id); var survivor=Hand(s,"survivor");
                Energy(s,1); FormalSliceFixture.PlayAt(s,survivor,card); Healthy(s);
                Check.Equal(entry.Id=="tactician"?(long)(up?2:1):0L,N(s,Player(s),"Energy"),"only Survivor pays energy");
                Check.Equal(1,Finished(s,card),"one automatic execution");
                Check.True(entry.Id=="abrasive" ? !Members(s,"DiscardPile").Contains(card) : Members(s,"DiscardPile").Contains(card),"native result area");
            });
        Check.Run("Sly: HandTrick selects exactly non-Sly Skills and preserves unchosen instances",()=> {
            var s=Start(); var trick=Hand(s,"hand-trick"); var defend=Hand(s,"defend-silent"); var survivor=Hand(s,"survivor");
            Hand(s,"wound"); Hand(s,"flick-flack"); Hand(s,"master-planner"); Hand(s,"reflex");
            Energy(s,10); Submit(s,trick);
            Check.Equal(SessionStatus.AwaitingDecision,s.Status,"two eligible Skills ask");
            Check.Sequence(new[]{defend,survivor}.OrderBy(x=>x.Value),s.PendingDecision!.Candidates.OrderBy(x=>x.Value),"Status, Power, Attack and native Sly excluded");
            s.Answer(new DecisionAnswer(s.PendingDecision.RequestId,[survivor])); Healthy(s);
            Check.True(B(s,survivor,"SingleTurnSly"),"chosen instance gains temporary Sly");
            Check.False(B(s,defend,"SingleTurnSly"),"other instance untouched");
            Check.Equal(7L,N(s,Player(s),"Block"),"block before choice");
        });
        Check.Run("Sly: HandTrick empty domain and singleton never fabricate a choice",()=> {
            foreach(var eligible in new[]{false,true}) {
                var s=Start(); var trick=Hand(s,"hand-trick"); Hand(s,"wound");
                var card=eligible?Hand(s,"defend-silent"):default;
                Energy(s,1); var choices=FormalSliceFixture.PlayAtCountingDecisions(s,trick,Enemies(s)[0]); Healthy(s);
                Check.Equal(0,choices,"empty and singleton process without pause");
                if(eligible) Check.True(B(s,card,"SingleTurnSly"),"singleton receives Sly");
            }
        });
        Check.Run("Sly: temporary grant survives replay but expires in every combat pile",()=> {
            var s=Start(); var target=Hand(s,"defend-silent"); Play(s,"hand-trick",target);
            var chooser=Hand(s,"survivor"); Energy(s,1); FormalSliceFixture.PlayAt(s,chooser,target); Healthy(s);
            Check.True(B(s,target,"SingleTurnSly"),"Sly not consumed by auto play");
            Check.Equal(1,Finished(s,target),"real free skill execution");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.False(B(s,target,"SingleTurnSly"),"discard pile state expires");
            Hand(s,"defend-silent"); chooser=Hand(s,"survivor"); Energy(s,1); FormalSliceFixture.PlayAt(s,chooser,target); Healthy(s);
            Check.Equal(0,Finished(s,target),"next-turn ordinary discard does not play");
        });
        Check.Run("Sly: MasterPlanner grants Skill-only battle state and never rewrites permanent cards",()=> {
            var s=Start(); Play(s,"master-planner"); var target=Hand(s,"defend-silent"); Play(s,"defend-silent");
            Check.True(B(s,target,"AddedSly"),"played Skill gains persistent keyword");
            Play(s,"flick-flack"); var attack=Find(s,"flick-flack"); Check.False(B(s,attack,"AddedSly"),"Attack excluded");
            FormalSliceFixture.EndTurn(s); Healthy(s); Check.True(B(s,target,"AddedSly"),"keyword survives next turn");
            Check.True(Members(s,"LongTermDeck").All(c=>!B(s,c,"AddedSly") && !B(s,c,"SingleTurnSly")),"long-term deck never gets combat mutation");
            target=Hand(s,"defend-silent"); var chooser=Hand(s,"survivor"); Energy(s,1); FormalSliceFixture.PlayAt(s,chooser,target); Healthy(s);
            Check.Equal(1,Finished(s,target),"persistent Sly enables next-turn discard auto play");
        });
        Check.Run("Sly: clone copies keyword states independently, upgrade preserves and next combat resets",()=> {
            var s=Start(); Play(s,"master-planner"); var original=Hand(s,"probe-cloner",raw:true); PlayRaw(s,original);
            var clone=Members(s,"DiscardPile").Single(c=>c!=original && Definition(s,c)=="probe:probe-cloner");
            Check.True(B(s,original,"AddedSly"),"original acquires keyword after play");
            Check.False(B(s,clone,"AddedSly"),"clone made before after-play grant has pre-grant state");
            Move(s,original,"DiscardPile","Hand"); PlayRaw(s,original);
            var later=Members(s,"DiscardPile").Where(c=>c!=original && c!=clone && Definition(s,c)=="probe:probe-cloner").Single();
            Check.True(B(s,later,"AddedSly"),"later clone copies battle keyword");
            var target=Hand(s,"defend-silent"); Play(s,"hand-trick",target); Play(s,"armaments",target);
            Check.True(B(s,target,"SingleTurnSly"),"upgrade preserves temporary keyword");
            foreach(var enemy in Enemies(s).ToArray()) Set(s,enemy,"Hp",1); Play(s,"flick-flack");
            var enter=s.LegalActions(SliceManifest.Player).Single(a=>a.Definition.QualifiedId=="base-spire:EnterNextRoom"); s.Submit(enter.Definition,SliceManifest.Player,[]); Healthy(s);
            Check.True(AllCards(s).All(c=>!B(s,c,"AddedSly") && !B(s,c,"SingleTurnSly")),"new combat from permanent deck clears both mutations");
        });
        Check.Run("Sly: nested discard cannot consume or replay the outer batch entries",()=> {
            var s=Start(); var nested=Hand(s,"probe-nested",raw:true); var tactician=Hand(s,"tactician"); var defend=Hand(s,"defend-silent");
            var batch=Hand(s,"prepared"); Energy(s,0); Submit(s,batch);
            Check.Equal(SessionStatus.AwaitingDecision,s.Status,"outer two-card choice");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId,[nested,tactician]));
            while(s.Status==SessionStatus.AwaitingDecision) s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId,[defend]));
            Healthy(s); Check.Equal(1,Finished(s,tactician),"outer queued card not stolen by nested flush");
            Check.Equal(1,Finished(s,nested),"nested Sly runs once"); Check.Equal(1L,N(s,Player(s),"Energy"),"Tactician pays no cost");
            Check.Equal(0,Members(s,"SlyDiscards").Count,"all internal batch entries destroyed");
        });
        Check.Run("Sly: temporary clone keeps independent state and expires outside the hand",()=> {
            var s=Start(); var original=Hand(s,"probe-cloner",raw:true); Play(s,"hand-trick",original);
            PlayRaw(s,original);
            var clone=Members(s,"DiscardPile").Single(c=>c!=original && Definition(s,c)=="probe:probe-cloner");
            Check.True(B(s,clone,"SingleTurnSly"),"clone copies temporary keyword at creation");
            Move(s,clone,"DiscardPile","ExhaustPile");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.False(B(s,clone,"SingleTurnSly"),"exhausted clone also expires");
            Check.False(B(s,original,"SingleTurnSly"),"source clears independently");
        });
        Check.Run("Sly: transformation creates a fresh instance without the granted keyword",()=> {
            var s=Start(); var original=Hand(s,"defend-silent"); Play(s,"hand-trick",original);
            var transform=Hand(s,"probe-transformer",raw:true); Energy(s,10);
            FormalSliceFixture.PlayAt(s,transform,original); Healthy(s);
            var replacement=Members(s,"Hand").Single(c=>Definition(s,c)=="probe:probe-replacement");
            Check.True(replacement!=original,"transformation replaces the instance");
            Check.False(B(s,replacement,"SingleTurnSly") || B(s,replacement,"AddedSly"),"new definition does not inherit combat keyword mutation");
        });
        Check.Run("Sly: repeated MasterPlanner grants one keyword, not extra executions",()=> {
            var s=Start(); Play(s,"master-planner"); Play(s,"master-planner");
            var target=Hand(s,"defend-silent"); Play(s,"defend-silent");
            Move(s,target,"DiscardPile","Hand"); var chooser=Hand(s,"survivor"); Energy(s,1);
            FormalSliceFixture.PlayAt(s,chooser,target); Healthy(s);
            Check.Equal(2,Finished(s,target),"one manual and one automatic execution despite repeated power");
        });
        foreach(var amount in new[]{0,5}) foreach(var blocked in new[]{false,true})
            Check.Run($"Thorns: before powered hit amount={amount}, blocked={blocked}",()=> {
                var s=Start(); var enemy=Enemies(s)[0]; var victim=Player(s);
                Play(s,"abrasive"); var hp=N(s,victim,"Hp"); Set(s,victim,"Block",blocked?99:0);
                InvokeDamage(s,enemy,amount,true); Healthy(s);
                Check.Equal(996L,N(s,enemy,"Hp"),"even zero or fully blocked hit retaliates once");
                Check.Equal(hp-(blocked?0:amount),N(s,victim,"Hp"),"current hit resolves normally");
            });
        Check.Run("Thorns: lethal retaliation completes current hit, later hits from dead dealer skip",()=> {
            var s=Start(trace:true); var enemy=Enemies(s)[0]; Play(s,"abrasive"); Set(s,enemy,"Hp",4);
            var hp=N(s,Player(s),"Hp"); InvokeDamage(s,enemy,7,true); InvokeDamage(s,enemy,7,true); Healthy(s);
            Check.Equal(hp-7,N(s,Player(s),"Hp"),"one current hit, no second attack from dead source");
            var events=s.Debug!.Records.Where(r=>r.Kind==SessionDebugKind.EventEmit).Select(r=>r.Subject).ToArray();
            Check.True(Array.IndexOf(events,"base-spire:MonsterDied")<Array.IndexOf(events,"base-spire:PlayerDamaged"),"retaliation death before incoming damage event");
        });
        Check.Run("Thorns: unpowered damage does not retaliate; attacker block absorbs unpowered counter",()=> {
            var s=Start(); var enemy=Enemies(s)[0]; Play(s,"abrasive");
            InvokeDamage(s,enemy,5,false); Check.Equal(1000L,N(s,enemy,"Hp"),"unpowered excludes thorns");
            FormalSliceFixture.GivePower(s,Player(s),"base-spire:strength",20); FormalSliceFixture.GivePower(s,enemy,"base-spire:vulnerable",3); Set(s,enemy,"Block",2);
            InvokeDamage(s,enemy,1,true); Healthy(s); Check.Equal(998L,N(s,enemy,"Hp"),"4 minus two block, no Strength/Vulnerable");
        });
        Check.Run("Sly: unsupported grant and selector shapes fail at their source",()=> {
            foreach(var body in new[]{"grantSelectedSly = {}","chooseCard = { from = hand filter = nonSlySkill onSelected = { grantSelectedSly = { duration = combat } } }"}) {
                var text = "using namespace probe; card Invalid { normal = { type = skill rarity = common cost = 0 target = { selectSelf = {} } play = { "+body+" } } upgraded = { type = skill rarity = common cost = 0 target = { selectSelf = {} } play = { "+body+" } } }";
                var source=SpireCardSource.Parse(text,"invalid-sly.spire");
                Check.True(source.Unsupported.Count>0,"grant requires selected card and exact empty block");
            }
        });
    }
    private static void Verify(Case entry,string mode)
    {
        var up=mode!="normal"; var s=Start(mode=="created-upgraded"); var card=Hand(s,entry.Id);
        if(mode=="armaments-upgraded") Play(s,"armaments",card);
        Set(s,Player(s),"Block",0); Energy(s,10); var before=Members(s,"Hand").Count;
        var choices=FormalSliceFixture.PlayAtCountingDecisions(s,card,Enemies(s)[0]); Healthy(s);
        var amount=up?entry.UpAmount:entry.Amount;
        Check.Equal(10L-(up?entry.UpCost:entry.Cost)+(entry.Id=="tactician"?amount:0),N(s,Player(s),"Energy"),"native cost and energy");
        Check.Equal(0,choices,"no fabricated target or empty HandTrick choice");
        if(entry.Id=="abrasive") { Check.Equal(1L,FormalSliceFixture.PowerStacks(s,Player(s),"base-spire:dexterity"),"one Dexterity"); Check.Equal((long)amount,FormalSliceFixture.PowerStacks(s,Player(s),"base-spire:thorns-power"),"native Thorns"); }
        if(entry.Id=="flick-flack") Check.True(Enemies(s).All(e=>N(s,e,"Hp")==1000-amount),"all enemy damage");
        if(entry.Id=="ricochet") { Check.Equal((long)(amount*3),Enemies(s).Sum(e=>1000-N(s,e,"Hp")),"all hits damage"); Check.Equal((long)amount,s.Random.DrawCount(SliceManifest.RandomTargetStream),"independent target per hit"); }
        if(entry.Id=="reflex") Check.Equal(before-1+amount,Members(s,"Hand").Count,"actual cards drawn");
        if(entry.Id is "untouchable" or "hand-trick") Check.Equal((long)amount,N(s,Player(s),"Block"),"native block");
        if(entry.Id=="master-planner") Check.Equal(1L,FormalSliceFixture.PowerStacks(s,Player(s),"base-spire:master-planner-power"),"single Planner effect");
    }
    private static Session Start(bool up=false,bool trace=false)
    {
        if(!Rules.TryGetValue(up,out var rules)) {
            var model=FormalSpirePackage.Load().Model;
            var links=model.Powers.ToDictionary(p=>p.Name,p=>p.Id,StringComparer.Ordinal);
            var declarations=SpireCardSource.LoadResource("BaseSpire.ContentSource.silent-sly-cards.spire",links);
            Check.Equal(0,declarations.Unsupported.Count,string.Join(";",declarations.Unsupported.Select(c=>c.Name+": "+c.Reason)));
            var extra=SpireCardSource.Parse("""
                using namespace probe; dsl_version = "base/0.0.2";
                card ProbeCloner { common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } play = { cloneCard = { pile = discard } } } normal = {} upgraded = {} }
                card ProbeNested { common = { type = skill rarity = common cost = 1 target = { selectSelf = {} } keywords = { sly } play = { chooseCard = { from = hand to = discard autoSelect = true } } } normal = {} upgraded = {} }
                card ProbeReplacement { common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } play = { gainBlock = { amount = 1 } } } normal = {} upgraded = {} }
                card ProbeTransformer { common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } play = { chooseCard = { from = hand onSelected = { transformSelectedCard = { card = ProbeReplacement from = hand } } } } } normal = {} upgraded = {} }
                ""","sly-probes.spire");
            Check.Equal(0,extra.Unsupported.Count,string.Join(";",extra.Unsupported.Select(c=>c.Reason)));
            model=model with { Cards=[..model.Cards,..extra.Cards], Deck=[..Cases.Select(c=>new AuthorDeckEntry("base-spire:"+c.Id,1,up)),
                new("base-spire:haze",1,up),new("base-spire:armaments",1),new("base-spire:defend-silent",1),
                new("base-spire:master-planner",1,up),
                new("base-spire:survivor",1),new("base-spire:prepared",1,true),new("base-spire:wound",30),
                new("probe:probe-cloner",1),new("probe:probe-nested",1),new("probe:probe-transformer",1)] };
            var b=new RuleSetBuilder(); var package=CombatSlicePackage.Build(authorModel:model);
            var damage=package.Declarations.OfType<ProcedureSymbol>().Single(p=>p.Name=="DamagePlayer");
            var action=package.Action("ProbeDamage"); action.Parameter("Target",FieldTypeSchema.AnyRef()); action.Parameter("Amount",FieldTypeSchema.Int(0,999)); action.Parameter("Powered",FieldTypeSchema.Bool());
            var body=package.ActionBody(action); body.Role(SliceManifest.PlayerRole); body.Body(body.Call(damage,body.Param(0),body.Param(1),body.Param(2)));
            b.Add(package.Build()); Rules[up]=rules=b.Build();
        }
        var s=new Session(rules,20261017UL,[SliceManifest.PlayerRole],computeBackend:SourceWeightedPickBackend.Instance,debug:trace?new SessionDebugOptions(MaxRecords:50000):null);
        s.Start(); Healthy(s); foreach(var e in Enemies(s)) Set(s,e,"Hp",1000);
        foreach(var c in Members(s,"Hand").ToArray()) Move(s,c,"Hand","DrawPile"); return s;
    }
    private static void InvokeDamage(Session s,InstanceId enemy,int amount,bool powered)=>s.Submit(s.RuleSet.Actions.Single(a=>a.QualifiedId=="base-spire:ProbeDamage"),SliceManifest.Player,[Value.OfRef(enemy),Value.Of((long)amount),Value.Of(powered)]);
    private static InstanceId Hand(Session s,string id,bool raw=false)=>FormalSliceFixture.MoveToHand(s,raw?"probe:"+id:"base-spire:"+id);
    private static InstanceId Find(Session s,string id)=>AllCards(s).First(c=>Definition(s,c)=="base-spire:"+id && !Members(s,"LongTermDeck").Contains(c));
    private static IEnumerable<InstanceId> AllCards(Session s)=>s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id);
    private static string Definition(Session s,InstanceId c)=>FormalSliceFixture.Definition(s,c);
    private static InstanceId Player(Session s)=>FormalSliceFixture.Player(s);
    private static IReadOnlyList<InstanceId> Enemies(Session s)=>Members(s,"Monsters");
    private static IReadOnlyList<InstanceId> Members(Session s,string name)=>s.State.CollectionMembers(FormalSliceFixture.Collection(s,"base-spire:"+name));
    private static long N(Session s,InstanceId c,string field)=>s.State.GetField(c,s.RuleSet.Type(s.State.TypeIdOf(c)).FieldIndex(field)).AsInt();
    private static bool B(Session s,InstanceId c,string field)=>s.State.GetField(c,s.RuleSet.Type(s.State.TypeIdOf(c)).FieldIndex(field)).AsBool();
    private static void Set(Session s,InstanceId c,string field,long value)=>s.State.RunAtomic(()=>s.State.SetField(c,s.RuleSet.Type(s.State.TypeIdOf(c)).FieldIndex(field),Value.Of(value)));
    private static void Energy(Session s,long value)=>Set(s,Player(s),"Energy",value);
    private static void Submit(Session s,InstanceId c)=>s.Submit(s.RuleSet.Actions.Single(a=>a.QualifiedId==FormalSliceFixture.PlayCardAction),SliceManifest.Player,[Value.OfRef(c)]);
    private static void Play(Session s,string id,InstanceId target=default) { Energy(s,10); FormalSliceFixture.PlayAt(s,Hand(s,id),target==default?Enemies(s)[0]:target); Healthy(s); }
    private static void PlayRaw(Session s,InstanceId c) { Energy(s,10); FormalSliceFixture.PlayAt(s,c,Enemies(s)[0]); Healthy(s); }
    private static void Move(Session s,InstanceId c,string from,string to)=>s.State.RunAtomic(()=> { s.State.RemoveFromCollection(FormalSliceFixture.Collection(s,"base-spire:"+from),c); s.State.AddToCollection(FormalSliceFixture.Collection(s,"base-spire:"+to),c); });
    private static int Finished(Session s,InstanceId c) { var type=s.RuleSet.TypeByQualifiedId("base-spire:CardPlayEntry"); return Members(s,"CardPlayHistory").Count(e=>s.State.GetField(e,type.FieldIndex("Card")).AsRef()==c && s.State.GetField(e,type.FieldIndex("Finished")).AsBool()); }
    private static void Healthy(Session s)=>Check.True(s.Status!=SessionStatus.Faulted,$"{s.Fault}");
}
