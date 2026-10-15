using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class TreasureRoomChecks
{
    public static void Run()
    {
        Check.Run("T05 宝箱：完整源池袋序、独立稀有度流、开奖移除、开箱金币与跳过互斥 [受控游标]", () =>
        {
            const string seed = "ACT1-TREASURE";
            var runtime = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.DevMap)).CreateSession(seed);
            runtime.Start(); var s = runtime.Session; var run = Members(s, "RunStates").Single();
            var rng = new RandomService(VanillaRandomLayout.RunSeed(seed), VanillaRandomLayout.AlgorithmId,
                VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
            VanillaRandomLayout.SeedAllStreams(rng, seed);
            var expected = new Dictionary<string, List<string>>();
            foreach (var group in SharedRelicPoolManifest.Entries.GroupBy(item => item.Rarity))
            {
                var ids = group.Select(TreasureRoomWiring.ContentId).ToList();
                // Independently translate native ListExtensions.UnstableShuffle, not the wiring's instructions.
                for (var n = ids.Count - 1; n > 0; n--)
                { var j = rng.DrawInt(VanillaRandomLayout.EngineStreamName("UpFront"), 0, n + 1); (ids[n], ids[j]) = (ids[j], ids[n]); }
                expected[group.Key] = ids;
                Check.Sequence(ids, Members(s, "SharedRelicBag" + group.Key).Select(id => Get(s,id,"SharedRelicBagEntry","RelicId").AsString()), "source bag order " + group.Key);
            }
            Check.Equal(118, expected.Values.Sum(ids => ids.Count), "all unlocked shared identities, not 3 representatives");
            var goldStream = VanillaRandomLayout.EnginePlayerStreamName("Rewards");
            var relicStream = VanillaRandomLayout.EngineStreamName("TreasureRoomRelics");
            var drawCount = s.Random.DrawCount(goldStream);
            var unit = (float)rng.DrawDouble(relicStream);
            var rarity = unit < 0.5f ? "Common" : unit < 0.83f ? "Uncommon" : "Rare";
            EnterTreasure(s,run);
            Check.Equal(rarity, Get(s,run,"Run","TreasureRelicRarity").AsString(), "source float32 rarity");
            Check.Equal(expected[rarity][0], Get(s,run,"Run","TreasureRelicId").AsString(), "actual shuffled bag head");
            Check.Equal(expected[rarity].Count-1, Members(s,"SharedRelicBag"+rarity).Count, "offer removes head before choice");
            Check.Equal(1L, s.Random.DrawCount(relicStream), "one rarity draw on entry");
            Check.Equal(drawCount,s.Random.DrawCount(goldStream), "enter does not reuse gold payload");
            Check.True(Legal(s,"SkipTreasureRelic") is null, "cannot skip before opening chest");
            Check.True(Legal(s,"ChooseMapPoint") is null && Legal(s,"PlayCard") is null && Legal(s,"EndTurn") is null, "room phases exclusive");
            var goldBefore=Get(s,run,"Run","Gold").AsInt();
            Submit(s,"OpenTreasureChest");
            Check.Equal(goldBefore+rng.DrawInt(goldStream,42,53), Get(s,run,"Run","Gold").AsInt(), "actual source gold draw");
            Check.True(Legal(s,"OpenTreasureChest") is null, "no second opening");
            Submit(s,"SkipTreasureRelic");
            Check.Equal(2L,Get(s,run,"Run","MapPhase").AsInt(),"skip returns to map");
            Check.True(Legal(s,"TakeTreasureRelic") is null && Legal(s,"SkipTreasureRelic") is null, "resolution cannot repeat");
            Check.Equal(1,Members(s,"Relics").Count,"skipped relic never becomes owned");
        });
        Check.Run("T05 遗物获得：持有实例、获得效果、重复拒绝 [受控候选/HP，非正常通关]", () =>
        {
            foreach(var (id, hpGain,goldGain) in new[]{("base-spire:pear",10L,0L),("base-spire:mango",14L,0L),("base-spire:old-coin",0L,300L)})
            {
                var runtime=new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0,BaseSpireScope.DevMap)).CreateSession("ACT1-PICKUP");
                runtime.Start();var s=runtime.Session;var run=Members(s,"RunStates").Single();EnterTreasure(s,run);Submit(s,"OpenTreasureChest");
                s.State.RunAtomic(()=>{Set(s,run,"Hp",Value.Of(10L));Set(s,run,"TreasureRelicId",Value.Of(id));});
                var beforeGold=Get(s,run,"Run","Gold").AsInt();var beforeMax=Get(s,run,"Run","MaxHp").AsInt();
                var action=Legal(s,"TakeTreasureRelic")!;s.Submit(action.Definition,SliceManifest.Player,action.Bindings[0]);
                Check.Equal(2,Members(s,"Relics").Count,"owned collection contains real instance");
                Check.Equal(id,Get(s,Members(s,"Relics").Last(),"Relic","DefinitionId").AsString(),"owned identity");
                Check.Equal(10L+hpGain,Get(s,run,"Run","Hp").AsInt(),"gain max HP heals same amount");
                Check.Equal(beforeMax+hpGain,Get(s,run,"Run","MaxHp").AsInt(),"max HP persists");
                Check.Equal(beforeGold+goldGain,Get(s,run,"Run","Gold").AsInt(),"gold persists");
                Check.Fault(EngineFaultCode.IllegalAction,()=>s.Submit(action.Definition,SliceManifest.Player,action.Bindings[0]),"no second acquisition");
            }
        });
        Check.Run("T05 稀有度：float32阈值舍入 [纯源边界]",()=>
        {
            var payload=(long)((double)0.83f*TreasureRelicRules.RandomSpan)-1;
            Check.Equal("Rare",TreasureRelicRules.RarityForPayload(payload),"double below threshold rounds to float threshold, so Rare");
        });
    }

    private static void EnterTreasure(Session s,InstanceId run)
    {
        var node=Members(s,"MapNodes").First(n=>Get(s,n,"MapNode","Type").AsEnumOrdinal()==3);
        var relation=s.RuleSet.Relations.Single(r=>r.QualifiedId=="base-spire:MapChildren").Id;
        var parent=Members(s,"MapNodes").First(n=>s.State.RelationTargets(relation,n).Contains(node));
        s.State.RunAtomic(()=>{Set(s,run,"CurrentNode",Value.OfRef(parent));Set(s,run,"MapPhase",Value.Of(2L));Set(s,run,"MapFloor",Value.Of(8L));});
        var action=Legal(s,"ChooseMapPoint")!;s.Submit(action.Definition,SliceManifest.Player,[Value.OfRef(node)]);
    }
    private static LegalAction? Legal(Session s,string name)=>s.LegalActions(SliceManifest.Player).FirstOrDefault(a=>a.Definition.QualifiedId=="base-spire:"+name);
    private static void Submit(Session s,string name){var a=Legal(s,name)!;s.Submit(a.Definition,SliceManifest.Player,a.Bindings[0]);}
    private static IReadOnlyList<InstanceId> Members(Session s,string name)=>s.State.CollectionMembers(s.RuleSet.Collections.Single(c=>c.QualifiedId=="base-spire:"+name).Id);
    private static Value Get(Session s,InstanceId id,string type,string field)=>s.State.GetField(id,s.RuleSet.TypeByQualifiedId("base-spire:"+type).FieldIndex(field));
    private static void Set(Session s,InstanceId run,string field,Value value)=>s.State.SetField(run,s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex(field),value);
}
