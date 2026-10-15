using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// Formal potion/Power programs, controlled initial inventory and enemy turns.
// These arrangements prove use semantics, not a normal acquisition trajectory.
internal static class PotionReuseChecks
{
    private static readonly SinglePlayerContent Content = SinglePlayerContentSource.Shared;
    private const string PotionType = "base-spire:Potion";
    private const string Plating = "base-spire:plating-power";

    public static void Run()
    {
        Check.Run("药水复用：正式资源加载、普通Power链接及原11瓶保留", () =>
        {
            Check.Equal(48, Content.Model.PotionPool.Count, "完整池不缩减");
            Check.Equal(13, Content.Model.Potions.Count, "只新增两瓶经过后续判别的效果");
            Check.Equal(35, Content.Model.PotionPool.Count(p => p.UnsupportedUseReason.Length > 0), "未支持仍具名");
            foreach (var id in new[] { "beetle-juice", "blessing-of-the-forge", "block-potion", "dexterity-potion",
                "energy-potion", "fire-potion", "flex-potion", "strength-potion", "swift-potion", "vulnerable-potion", "weak-potion" })
                Check.True(Content.Model.Potions.Any(p => p.Id == "base-spire:" + id), "保留原效果 " + id);
            var explosive = Content.Model.Potions.Single(p => p.Name == "ExplosiveAmpoule");
            Check.True(explosive.Unpowered, "正式unpowered门控");
            Check.False(explosive.Program!.NeedsSelectedTarget, "群体无敌人选择");
            var plating = Content.Model.Powers.Single(p => p.Name == "PlatingPower");
            Check.Equal(Plating, plating.Id, "加载链接表的玩家通用ID");
            Check.Equal("gain-block-per-stack:1", plating.OwnerTurnEnd, "回合末层数格挡");
            Check.False(plating.Id == "base-spire:plating", "不使用敌人专用同名ID");
            Console.WriteLine("POWER_LINK " + string.Join(",", Content.Model.Powers.Where(p => p.Name.Contains("Plating", StringComparison.Ordinal) || p.Name.Contains("Thorns", StringComparison.Ordinal)).Select(p => p.Name + "=" + p.Id)));
            Check.True(Content.Model.Potions.Single(p => p.Name == "HeartOfIron").Program is not null, "正式Heart程序已加载");
            Check.False(Content.Model.Potions.Any(p => p.Name == "LiquidBronze"), "公共缺口不能伪报支持");
            foreach (var source in Content.SourceDigests.Where(p => p.Resource == SpirePotionSource.Resource
                || p.Resource.EndsWith("ironclad-powers.spire", StringComparison.Ordinal)
                || p.Resource.EndsWith("enemies.spire", StringComparison.Ordinal)))
                Console.WriteLine($"SOURCE {source.Resource} {source.Digest}");
            Console.WriteLine($"CONTENT digest={Content.ContentDigest}; seed=20261011; controlled inventory/turns; supported=13 unsupported=35");
        });

        foreach (var mode in new[] { "plain", "strength", "weak", "vulnerable", "combined" })
            Check.Run($"药水复用：ExplosiveAmpoule群体10 unpowered/{mode}，免费且只消耗一次", () =>
            {
                var s = New(); var player = FormalSliceFixture.Player(s);
                var enemies = Members(s, "Monsters").ToArray();
                Check.True(enemies.Length >= 2, "至少两个敌人区分单体与全体");
                foreach (var enemy in enemies) Set(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 31);
                if (mode is "strength" or "combined") FormalSliceFixture.GivePower(s, player, "base-spire:strength", 8);
                if (mode is "weak" or "combined") FormalSliceFixture.GivePower(s, player, "base-spire:weak", 3);
                if (mode is "vulnerable" or "combined")
                    foreach (var enemy in enemies) FormalSliceFixture.GivePower(s, enemy, "base-spire:vulnerable", 3);
                var bottle = Arrange(s, "explosive-ampoule");
                var energy = PlayerNumber(s, "Energy"); var draws = s.Random.TotalDraws;
                var use = Legal(s, "UsePotion")!;
                Submit(s, "UsePotion", bottle);
                Check.Equal(SessionStatus.Running, s.Status, "群体不提问");
                foreach (var enemy in enemies) Check.Equal(21L, Number(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "每个敌人独立损失10");
                Check.Equal(energy, PlayerNumber(s, "Energy"), "不付能量");
                Check.Equal(draws, s.Random.TotalDraws, "使用不补随机");
                ConsumedOnce(s, bottle, use);
            });

        Check.Run("药水复用：ExplosiveAmpoule全体末次击杀后仍销毁瓶并完成结算尾", () =>
        {
            var s = New(); var enemies = Members(s, "Monsters").ToArray();
            Check.True(enemies.Length >= 2, "多目标同时死亡");
            foreach (var enemy in enemies) Set(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 10);
            var retained = Arrange(s, "heart-of-iron", slot: 1);
            var bottle = Arrange(s, "explosive-ampoule"); var energy = PlayerNumber(s, "Energy");
            var use = Legal(s, "UsePotion")!;
            Submit(s, "UsePotion", bottle);
            Check.Equal(0, Members(s, "Monsters").Count, "最后一个目标也死亡");
            Check.Sequence(new[] { retained }, Members(s, "PotionBelt"), "动作尾前只消耗该瓶");
            Check.Equal(energy, PlayerNumber(s, "Energy"), "末次击杀仍免费");
            var run = Members(s, "RunStates").Single();
            Check.Equal(Number(s, run, "base-spire:Run", "CurrentRoom"), Number(s, run, "base-spire:Run", "SettledRoom"), "房间长期结算尾完成");
            var state = s.State.StateHash();
            Check.Fault(EngineFaultCode.IllegalAction, () => s.Submit(use.Definition, SliceManifest.Player, [Value.OfRef(bottle)]), "末次击杀后的旧瓶仍拒绝重复提交");
            Check.Equal(state, s.State.StateHash(), "重复提交不改变结算状态");
            Submit(s, "EnterNextRoom");
            Check.Sequence(new[] { retained }, Members(s, "PotionBelt"), "下一房保留其他瓶实例");
        });

        foreach (var modifiers in new[] { false, true })
            Check.Run($"药水复用：HeartOfIron 7层→6→5→4，后续格挡实效与对照/modifiers={modifiers}", () =>
            {
                var s = New(); var control = New(); var player = FormalSliceFixture.Player(s);
                if (modifiers)
                    foreach (var session in new[] { s, control })
                    {
                        FormalSliceFixture.GivePower(session, FormalSliceFixture.Player(session), "base-spire:dexterity-power", 8);
                        FormalSliceFixture.GivePower(session, FormalSliceFixture.Player(session), "base-spire:frail", 4);
                    }
                var bottle = Arrange(s, "heart-of-iron"); var energy = PlayerNumber(s, "Energy");
                var use = Legal(s, "UsePotion")!; Submit(s, "UsePotion", bottle);
                Check.Equal(7L, FormalSliceFixture.PowerStacks(s, player, Plating), "源PlatingPower 7，不是敌人Plating");
                Check.Equal(0L, PlayerNumber(s, "Block"), "使用不立即给格挡");
                Check.Equal(energy, PlayerNumber(s, "Energy"), "免费"); ConsumedOnce(s, bottle, use);
                for (var turn = 1; turn <= 3; turn++)
                {
                    var hp = PlayerNumber(s, "Hp"); var controlHp = PlayerNumber(control, "Hp");
                    FormalSliceFixture.EndTurn(s); FormalSliceFixture.EndTurn(control);
                    Check.Equal(SessionStatus.Running, s.Status, "后续回合正常完成");
                    Check.Equal(7L - turn, FormalSliceFixture.PowerStacks(s, player, Plating), "每次玩家回合开始减1");
                    if (turn >= 2)
                    {
                        var unblocked = controlHp - PlayerNumber(control, "Hp");
                        Check.True(unblocked > 8 - turn, "对照攻击足以测完整层数格挡");
                        Check.Equal(8L - turn, unblocked - (hp - PlayerNumber(s, "Hp")), "回合末按当前层数给unpowered格挡");
                    }
                    Check.Equal(0, Members(s, "PotionBelt").Count, "消耗后不会再次使用");
                }
            });

        Check.Run("药水复用：LiquidBronze保留未支持且不发布空使用", () =>
        {
            var s = New(); var bottle = Arrange(s, "liquid-bronze");
            Check.True(Legal(s, "UsePotion") is null, "未支持元数据不能变成空效果");
            Check.True(Content.Model.PotionPool.Single(p => p.Name == "LiquidBronze").UnsupportedUseReason.Contains("zero-damage", StringComparison.Ordinal), "具名公共语义缺口");
            Submit(s, "DiscardPotion", bottle); Check.Equal(0, Members(s, "PotionBelt").Count, "保留丢弃能力");
        });
        ThornsWitness();
    }

    private static void ThornsWitness()
    {
        Check.Run("药水复用：LiquidBronze公共缺口最小反例（实际链接表，不计支持）", () =>
        {
            var links = Content.Model.Powers.ToDictionary(p => p.Name, p => p.Id, StringComparer.Ordinal);
            Check.False(links.ContainsKey("ThornsPower"), "源玩家ThornsPower没有正式链接");
            Check.False(links.ContainsKey("Thorns"), "敌人Thorns也未实际加载，不能以声明冒充实现");
            var unsupported = Content.Unsupported.Single(p => p.Item == "power:Thorns");
            Console.WriteLine($"THORNS_UNSUPPORTED {unsupported.Item}: {unsupported.Reason}");
            foreach (var power in new[] { "ThornsPower", "Thorns" })
            {
                var witness = SpirePotionSource.Parse($$"""
                    package base-spire {
                      potion LiquidBronze {
                        rarity = uncommon
                        usage = combatOnly
                        target = self
                        source = "sts2/src/Core/Models/Potions/LiquidBronze.cs"
                        use = { applyPower = { power = {{power}} stacks = 3 target = context.owner } }
                      }
                    }
                    """, "potion-reuse/minimal-liquid-bronze.spire", links);
                Check.Equal(0, witness.Potions.Count, "普通链接表拒绝没有加载的能力");
                Check.True(witness.Pool.Single().UnsupportedUseReason.Length > 0, "仍保留具名未支持原因");
                Console.WriteLine($"THORNS_LINK_WITNESS power={power}; loaded_programs={witness.Potions.Count}; reason={witness.Pool.Single().UnsupportedUseReason}");
            }
            Console.WriteLine("THORNS_SOURCE_EXPECTATION stacks=3: powered damage=9 => retaliation=3; powered damage=0 => retaliation=3; unpowered/nonattack => retaliation=0. No loaded player Thorns; these are source expectations, NOT runtime passes.");
        });
    }

    private static Session New()
    {
        var model = SliceAuthorFrontend.Load() with { Powers = Content.Model.Powers,
            Potions = Content.Model.Potions, PotionPool = Content.Model.PotionPool, Belt = [] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var s = new Session(builder.Build(), 20261011UL, [SliceManifest.PlayerRole], computeBackend: BaseSpireComputeBackend.Instance);
        s.Start(); return s;
    }

    private static InstanceId Arrange(Session s, string shortId, long slot = 0)
    {
        var metadata = Content.Model.PotionPool.Single(p => p.Id == "base-spire:" + shortId);
        var type = s.RuleSet.TypeByQualifiedId(PotionType); var player = FormalSliceFixture.Player(s);
        return s.State.RunAtomic(() =>
        {
            var bottle = s.State.CreateInstance(type.Id, s.State.ScopeOf(player));
            foreach (var (field, value) in new (string, Value)[] { ("DefinitionId", Value.Of(metadata.Id)),
                ("Rarity", Value.Of(metadata.Rarity)), ("Target", Value.Of(metadata.Target)), ("Usage", Value.Of(metadata.Usage)),
                ("SlotIndex", Value.Of(slot)), ("UseSupported", Value.Of(metadata.UnsupportedUseReason.Length == 0)),
                ("UnsupportedUseReason", Value.Of(metadata.UnsupportedUseReason)) })
                s.State.SetField(bottle, type.FieldIndex(field), value);
            s.State.AddToCollection(FormalSliceFixture.Collection(s, "base-spire:PotionBelt"), bottle); return bottle;
        });
    }

    private static void ConsumedOnce(Session s, InstanceId bottle, LegalAction use)
    {
        Check.Equal(0, Members(s, "PotionBelt").Count, "只用一次后销毁库存实例");
        var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
        Check.Fault(EngineFaultCode.IllegalAction, () => s.Submit(use.Definition, SliceManifest.Player, [Value.OfRef(bottle)]), "旧实例重复提交被拒绝");
        Check.Equal(state, s.State.StateHash(), "拒绝后状态不变"); Check.Equal(draws, s.Random.TotalDraws, "拒绝后随机不变");
    }
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, "base-spire:" + name));
    private static long Number(Session s, InstanceId id, string type, string field) => FormalSliceFixture.Number(s, id, type, field);
    private static long PlayerNumber(Session s, string field) => Number(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, field);
    private static void Set(Session s, InstanceId id, string type, string field, long amount) => FormalSliceFixture.SetNumber(s, id, type, field, amount);
    private static LegalAction? Legal(Session s, string name) => s.LegalActions(SliceManifest.Player).SingleOrDefault(a => a.Definition.QualifiedId == "base-spire:" + name);
    private static void Submit(Session s, string name, InstanceId? target = null)
    {
        var action = Legal(s, name) ?? throw new CheckFailedException("missing action " + name);
        s.Submit(action.Definition, SliceManifest.Player, target is { } id ? [Value.OfRef(id)] : []);
    }
}
