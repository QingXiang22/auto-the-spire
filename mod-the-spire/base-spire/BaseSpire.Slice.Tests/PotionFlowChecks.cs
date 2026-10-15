using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using System.Text.RegularExpressions;

namespace BaseSpire.Slice.Tests;

internal static class PotionFlowChecks
{
    private const string PotionType = "base-spire:Potion";
    private static readonly SinglePlayerContent Content = SinglePlayerContentSource.Shared;

    public static void Run(bool includeLegacy = false)
    {
        Check.Run("药水：完整48项生成元数据逐项核对独立源码及池顺序", () =>
        {
            var root = new DirectoryInfo(Environment.CurrentDirectory);
            while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "sts2"))) root = root.Parent;
            Check.True(root is not null, "源码检查从仓库执行");
            var sourceRoot = Path.Combine(root!.FullName, "sts2/src/Core");
            var shared = File.ReadAllText(Path.Combine(sourceRoot, "Models/PotionPools/SharedPotionPool.cs"));
            var epoch = File.ReadAllText(Path.Combine(sourceRoot, "Timeline/Epochs/Ironclad4Epoch.cs"));
            var names = Regex.Matches(epoch, @"ModelDb\.Potion<([^>]+)>").Select(m => m.Groups[1].Value).Concat(
                Regex.Matches(shared, @"ModelDb\.Potion<([^>]+)>").Select(m => m.Groups[1].Value));
            var pool = Content.Model.PotionPool;
            Check.Equal(48, pool.Count, "完整铁甲全解锁池");
            Check.Sequence(names, pool.Select(p => p.Name), "角色优先、共享源顺序");
            foreach (var p in pool)
            {
                var text = File.ReadAllText(Path.Combine(root.FullName, p.Source));
                string ValueOf(string type) => Regex.Match(text,
                    @"override " + type + @" \w+ => " + type + @"\.(\w+)").Groups[1].Value;
                Check.Equal(ValueOf("PotionRarity").ToLowerInvariant(), p.Rarity, p.Name + " rarity");
                Check.Equal(ValueOf("PotionUsage") switch { "CombatOnly" => "combatOnly", "AnyTime" => "anyTime", _ => "automatic" }, p.Usage, p.Name + " usage");
                Check.Equal(ValueOf("TargetType") switch { "AnyEnemy" => "enemy", "AllEnemies" => "allEnemies", _ => "self" }, p.Target, p.Name + " target");
            }
            Check.Equal(13, Content.Model.Potions.Count, "十三项真实程序，不能把元数据算成效果实现");
            Check.Equal(35, pool.Count(p => p.UnsupportedUseReason.Length > 0), "未实现显式记账");
        });

        var rules = InventoryRules();
        Check.Run("药水：稀有度阈值使用源float32含边界比较", () =>
        {
            var a = rules.ComputeAssets.Single(a => a.Entry == "PotionRarity");
            foreach (var (roll, rarity) in new[] { (0d, 4L), (0.1d, 4L), (0.100001d, 3L), (0.35d, 3L), (0.350001d, 2L), (0.999d, 2L) })
            {
                var output = new Value[1];
                BaseSpireComputeBackend.Instance.Execute(a, [Value.Of((long)(roll * RewardCompute.Span))], output);
                Check.Equal(rarity, output[0].AsInt(), "rarity " + roll);
            }
        });

        Check.Run("药水：生成实物恰好两抽，完整池稀有度和ID匹配，未支持瓶可持有不可空用", () =>
        {
            var session = New(rules);
            for (var i = 0; i < 30; i++)
            {
                var before = session.Random.TotalDraws;
                Submit(session, "Generate");
                Check.Equal(before + 2, session.Random.TotalDraws, "真实生成两抽");
                var potion = Members(session, "RewardPotions").Single();
                var metadata = Content.Model.PotionPool.Single(p => p.Id == Text(session, potion, "DefinitionId"));
                Check.Equal(metadata.Rarity, Text(session, potion, "Rarity"), "生成后稀有度");
                Check.Equal(-1L, Number(session, potion, "SlotIndex"), "奖励尚无栏位");
                Check.Equal(metadata.UnsupportedUseReason.Length == 0, Field(session, potion, "UseSupported").AsBool(), "元数据不会伪装使用能力");
            }
        });

        Check.Run("药水：满栏保留奖励，取消零变更，显式丢弃后复用第一空槽，不挤压其他瓶", () =>
        {
            var session = New(rules);
            for (var i = 0; i < 3; i++) { Submit(session, "Generate"); Submit(session, "Receive"); }
            var original = Members(session, "PotionBelt").ToArray();
            Check.Sequence([0L, 1L, 2L], original.Select(p => Number(session, p, "SlotIndex")), "初始三个槽");
            Submit(session, "Generate");
            var offered = Members(session, "RewardPotions").Single();
            Check.True(Legal(session, "Receive") is null, "满栏不能领取");
            var before = session.State.StateHash();
            var receive = session.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:Receive");
            Check.Fault(EngineFaultCode.IllegalAction, () => session.Submit(receive, SliceManifest.Player, []), "满栏提交拒绝");
            Check.Equal(before, session.State.StateHash(), "满栏拒绝不改变奖励和栏位");
            Submit(session, "DiscardPotion", original[1]);
            Check.Equal(2L, Number(session, original[2], "SlotIndex"), "第三瓶仍在槽2");
            var draws = session.Random.TotalDraws;
            Submit(session, "Receive");
            Check.True(Members(session, "PotionBelt").Contains(offered), "同一奖励实例被领取");
            Check.Equal(1L, Number(session, offered, "SlotIndex"), "回填第一空槽1");
            Check.Sequence([0L, 1L, 2L], Members(session, "PotionBelt").Select(p => Number(session, p, "SlotIndex")),
                "公开成员保持槽号顺序");
            Check.Equal(draws, session.Random.TotalDraws, "领取和丢弃不补抽");
            Check.True(Legal(session, "Receive") is null, "不能重复领取");
        });

        Check.Run("药水：任意来源获得保留实例，满栏失败不移动，hook只在入栏后触发", () =>
        {
            var session = New(InventoryRules());
            var witness = Members(session, "RunStates").Single();
            for (var index = 0; index < SliceManifest.PotionSlotCount; index++) Submit(session, "ReceiveExternal");
            var offered = Members(session, "ExternalPotions").Single();
            var draws = session.Random.TotalDraws;
            Submit(session, "ReceiveExternal");
            Check.Equal(offered, Members(session, "ExternalPotions").Single(), "满栏保留源实例");
            Check.Equal(-1L, Number(session, offered, "SlotIndex"), "失败不写槽位");
            Check.Equal(3L, session.State.GetField(witness, session.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex("PotionHooks")).AsInt(), "失败不发hook");
            Submit(session, "DiscardPotion", Members(session, "PotionBelt")[1]);
            Submit(session, "ReceiveExternal");
            Check.True(Members(session, "PotionBelt").Contains(offered), "相同实例回填空槽");
            Check.Equal(1L, Number(session, offered, "SlotIndex"), "首个空槽");
            Submit(session, "ReceiveOwned");
            Check.Equal(4L, session.State.GetField(witness, session.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex("PotionHooks")).AsInt(), "重复获得不发hook");
            Check.Equal(3, Members(session, "PotionBelt").Count, "重复不新增成员");
            Check.Equal(draws, session.Random.TotalDraws, "获得无随机消费");
        });
        RewardChecks();
        CombatChecks();
        if (includeLegacy) SliceFrontendPotionChecks.Run();
    }

    private static RuleSet InventoryRules(bool withRewards = false)
    {
        var p = new EnginePackageBuilder("base-spire", "potion-check");
        var scope = p.ScopeType("RunScope"); var run = p.EntityType("Run");
        var runs = p.Collection("RunStates", run);
        var hooks = run.AddField("PotionHooks", FieldTypeSchema.Int(0, 99));
        CollectionSymbol? belt = null;
        var inventory = new PotionInventoryRules(p, Content.Model)
        {
            AfterAcquired = (body, potion) => [body.If(body.GreaterOrEqual(body.CollectionIndex(belt!, potion), body.Const(0L)),
                body.SetField(body.First(body.Collection(runs)), hooks, body.Add(body.Field(body.First(body.Collection(runs)), hooks), body.Const(1L))))],
        };
        belt = inventory.Belt;
        var external = p.Collection("ExternalPotions", inventory.Entity);
        var setup = p.Procedure("Setup"); var localScope = setup.Local("scope", FieldTypeSchema.Binding());
        var entity = setup.Local("run", FieldTypeSchema.Binding());
        var bottle = setup.Local("externalPotion", FieldTypeSchema.Binding());
        setup.Body(setup.CreateScope(scope, localScope), setup.CreateEntity(run, setup.LocalRef(localScope), entity, [(hooks, setup.Const(0L))], runs),
            setup.Repeat(setup.Const(4L), setup.CreateEntity(inventory.Entity, setup.LocalRef(localScope), bottle,
                inventory.Fields(setup, Content.Model.PotionPool[0].Id, setup.Const(-1L)), external)));
        p.OnSessionStart(setup.Symbol);
        if (withRewards)
        {
            var kinds = p.Enum("CardKind");
            foreach (var name in new[] { "Attack", "Skill", "Power", "Status", "Curse" }) kinds.Member(name);
            var schema = BaseSpire.Slice.Orchestration.CardSchema.Declare(p, kinds);
            var card = schema.Type; var definition = schema.Definition; var upgraded = schema.Upgraded;
            var deck = p.Collection("LongTermDeck", card);
            var recipes = new BaseSpire.Slice.Orchestration.CardDefinitions(schema, kinds, Content.Model, new Dictionary<string, FieldSymbol>());
            var deckCommands = new BaseSpire.Slice.Domain.Run.RunDeck(recipes, deck);
            var gold = run.AddField("Gold", FieldTypeSchema.Int(0, 99999));
            var hp = run.AddField("Hp", FieldTypeSchema.Int(0, 100));
            var maxHp = run.AddField("MaxHp", FieldTypeSchema.Int(0, 100));
            var defeat = p.TerminalResult("Defeat");
            var defeatTurn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 999));
            var resources = new BaseSpire.Slice.Domain.Run.RunResources(hp, maxHp, gold, defeat, defeatTurn);
            var room = run.AddField("CurrentRoom", FieldTypeSchema.Int(0, 999));
            var taken = run.AddField("RewardTaken", FieldTypeSchema.Int(0, 999));
            var phase = run.AddField("MapPhase", FieldTypeSchema.Int(0, 6));
            var rewards = RewardWiring.Declare(p, run, card, runs, deckCommands, resources, room, taken, phase, Content.Model.Cards.Select(BaseSpire.Slice.Effects.CardDefinition.FromSource).ToArray(),
                (s, c, up) => [(definition, s.Const(c.Id)), (upgraded, s.Const(up))], potions: inventory);
            rewards.RegisterActions(p);
            var roll = p.Action("RollRewards"); var b = p.ActionBody(roll); b.Role(SliceManifest.PlayerRole);
            var current = b.First(b.Collection(runs));
            b.Body(b.SetField(current, room, b.Add(b.Field(current, room), b.Const(1L))),
                b.Call(rewards.OpenRewards.Symbol));
        }
        foreach (var name in new[] { "Generate", "Receive" })
        {
            var action = p.Action(name); var b = p.ActionBody(action); b.Role(SliceManifest.PlayerRole);
            b.Requires(name == "Generate" ? b.Const(true) : b.And(inventory.HasSpace(b), b.Exists(b.Collection(inventory.Rewards))));
            b.Body((name == "Generate" ? inventory.Generate(b, b.First(b.Collection(runs))) : inventory.Acquire(b)).ToArray());
        }
        inventory.RegisterDiscard(p);
        var receiveExternal = p.ActionBody(p.Action("ReceiveExternal")); receiveExternal.Role(SliceManifest.PlayerRole);
        receiveExternal.Body(inventory.Acquire(receiveExternal, receiveExternal.First(receiveExternal.Collection(external)), external).ToArray());
        var receiveOwned = p.ActionBody(p.Action("ReceiveOwned")); receiveOwned.Role(SliceManifest.PlayerRole);
        receiveOwned.Body(inventory.Acquire(receiveOwned, receiveOwned.First(receiveOwned.Collection(inventory.Belt))).ToArray());
        var builder = new RuleSetBuilder(); builder.Add(p.Build()); return builder.Build();
    }

    private static void RewardChecks()
    {
        var rules = InventoryRules(withRewards: true);
        foreach (var drop in new[] { false, true })
            Check.Run($"药水：正规奖励drop={drop}实际11/13抽，领取/跳过才关闭，重复提交拒绝", () =>
            {
                var s = New(rules); var run = Members(s, "RunStates").Single();
                FormalSliceFixture.SetNumber(s, run, "base-spire:Run", "RewardPotionOdds", RewardCompute.BitsOf(drop ? 1f : 0f));
                var draws = s.Random.TotalDraws;
                Submit(s, "RollRewards");
                Check.Equal(draws + (drop ? 13 : 11), s.Random.TotalDraws, "实际开奖完整消费");
                Check.Equal(drop ? 1 : 0, Members(s, "RewardPotions").Count, "有掉落才生成");
                var offered = drop ? Members(s, "RewardPotions").Single() : default;
                Submit(s, RewardWiring.TakeGoldActionName); Submit(s, RewardWiring.SkipCardActionName);
                var phase = s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex("MapPhase");
                if (drop)
                {
                    Check.Equal(3L, s.State.GetField(run, phase).AsInt(), "未领取瓶前奖励不能关闭");
                    var action = Legal(s, RewardWiring.TakePotionActionName)!;
                    Submit(s, RewardWiring.TakePotionActionName);
                    Check.True(Members(s, "PotionBelt").Contains(offered), "真实奖励进入栏位");
                    var state = s.State.StateHash();
                    Check.Fault(EngineFaultCode.IllegalAction, () => s.Submit(action.Definition, SliceManifest.Player, []), "重复领取被拒绝");
                    Check.Equal(state, s.State.StateHash(), "重复提交零改动");
                }
                Check.Equal(2L, s.State.GetField(run, phase).AsInt(), "所有奖励解决后选图");
                Check.Equal(draws + (drop ? 13 : 11), s.Random.TotalDraws, "领取不抽数");
                Submit(s, "RollRewards");
                if (drop) Check.True(Members(s, "PotionBelt").Contains(offered), "下一房开奖保留已持有瓶");
            });
        Check.Run("药水：满栏正规奖励保留，显式丢弃再领取；跳过清候选无补抽", () =>
        {
            var s = New(rules); var run = Members(s, "RunStates").Single();
            for (var i = 0; i < 3; i++) { Submit(s, "Generate"); Submit(s, "Receive"); }
            FormalSliceFixture.SetNumber(s, run, "base-spire:Run", "RewardPotionOdds", RewardCompute.BitsOf(1f));
            Submit(s, "RollRewards");
            var offered = Members(s, "RewardPotions").Single();
            Check.True(Legal(s, RewardWiring.TakePotionActionName) is null, "满栏守卫");
            var discard = Members(s, "PotionBelt")[1];
            Submit(s, "DiscardPotion", discard); Submit(s, RewardWiring.TakePotionActionName);
            Check.True(Members(s, "PotionBelt").Contains(offered), "仍领取原候选");
            Check.Equal(1L, Number(s, offered, "SlotIndex"), "回填空槽");
            Submit(s, RewardWiring.TakeGoldActionName); Submit(s, RewardWiring.SkipCardActionName);
            FormalSliceFixture.SetNumber(s, run, "base-spire:Run", "RewardPotionOdds", RewardCompute.BitsOf(1f));
            Submit(s, "RollRewards"); var draws = s.Random.TotalDraws;
            Submit(s, RewardWiring.SkipPotionActionName);
            Check.Equal(0, Members(s, "RewardPotions").Count, "跳过销毁候选");
            Check.Equal(3, Members(s, "PotionBelt").Count, "跳过不替换持有瓶");
            Check.Equal(draws, s.Random.TotalDraws, "跳过不抽数");
        });
    }

    private static void CombatChecks()
    {
        var model = SliceAuthorFrontend.Load() with { Potions = Content.Model.Potions,
            PotionPool = Content.Model.PotionPool, Powers = [.. Content.Model.Powers,
                .. SpirePowerSource.Parse("package base-spire { power PotionCheckDexterity { role = Dexterity } }", "potion-check").Powers],
            Belt = [new("base-spire:block-potion", 1), new("base-spire:strength-potion", 1), new("base-spire:fire-potion", 1)] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var rules = builder.Build();
        Check.Run("药水：作者全体敌人程序不提问、不空用，复用同一效果编译器", () =>
        {
            var source = SpirePotionSource.Parse("""
                package base-spire { potion FirePotion {
                  rarity = common usage = combatOnly target = allEnemies source = "controlled-author-check"
                  unpowered = true use = { attack = { damage = 20 target = allEnemies } }
                } }
                """, "all-enemies-potion-check", new Dictionary<string, string>());
            var allModel = model with { Potions = source.Potions, PotionPool = source.Pool,
                Belt = [new("base-spire:fire-potion", 1)] };
            var allBuilder = new RuleSetBuilder();
            allBuilder.Add(CombatSlicePackage.Build(authorModel: allModel).Build());
            var s = New(allBuilder.Build());
            var enemies = Members(s, "Monsters").ToArray();
            Check.True(enemies.Length > 1, "使用多敌人区分单体与群体效果");
            foreach (var enemy in enemies)
                FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 21);
            Submit(s, "UsePotion", Members(s, "PotionBelt").Single());
            Check.Equal(SessionStatus.Running, s.Status, "无需敌人选择");
            foreach (var enemy in enemies)
                Check.Equal(1L, FormalSliceFixture.Number(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "每个敌人受到20伤害");
            Check.Equal(0, Members(s, "PotionBelt").Count, "群体效果完成后消耗");
        });
        Check.Run("药水：正式Block不受Dexterity/Frail修正，使用保留空槽且不付能量", () =>
        {
            var s = New(rules); var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.GivePower(s, player, "base-spire:potion-check-dexterity", 8);
            FormalSliceFixture.GivePower(s, player, "base-spire:frail", 2);
            var bottle = Members(s, "PotionBelt").Single(p => Text(s, p, "DefinitionId") == "base-spire:block-potion");
            var energy = FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, "Energy");
            Submit(s, "UsePotion", bottle);
            Check.Equal(12L, FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, "Block"), "unpowered block12");
            Check.Equal(energy, FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, "Energy"), "免费");
            Check.Sequence([1L, 2L], Members(s, "PotionBelt").Select(p => Number(s, p, "SlotIndex")), "槽位不压缩");
        });
        Check.Run("药水：正式Strength/Fire的目标、unpowered与消耗，共用动作尾", () =>
        {
            var s = New(rules); var player = FormalSliceFixture.Player(s);
            var strength = Members(s, "PotionBelt").Single(p => Text(s, p, "DefinitionId") == "base-spire:strength-potion");
            Submit(s, "UsePotion", strength);
            Check.Equal(2L, FormalSliceFixture.PowerStacks(s, player, "base-spire:strength"), "力量+2");
            var enemy = Members(s, "Monsters").First();
            FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 21);
            FormalSliceFixture.GivePower(s, enemy, "base-spire:vulnerable", 3);
            var fire = Members(s, "PotionBelt").Single(p => Text(s, p, "DefinitionId") == "base-spire:fire-potion");
            Submit(s, "UsePotion", fire);
            Check.Equal(SessionStatus.AwaitingDecision, s.Status, "敌人目标先提问");
            Check.True(Members(s, "PotionBelt").Contains(fire), "尚未决定不消耗");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [enemy]));
            Check.Equal(1L, FormalSliceFixture.Number(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "20不受力量/易伤修正");
            Check.False(Members(s, "PotionBelt").Contains(fire), "完成效果后消耗");
        });
        foreach (var (id, power, stacks) in new[] { ("weak-potion", "weak", 3L),
            ("vulnerable-potion", "vulnerable", 3L), ("beetle-juice", "shrink", 4L) })
            Check.Run($"药水：正式{id}独立效果与目标", () =>
            {
                var s = New(rules); ClearBelt(s); var potion = ArrangePotion(s, id);
                var enemy = Members(s, "Monsters").First(); Submit(s, "UsePotion", potion);
                s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [enemy]));
                Check.Equal(stacks, FormalSliceFixture.PowerStacks(s, enemy, "base-spire:" + power), "正式能力层数");
                Check.Equal(0, Members(s, "PotionBelt").Count, "只消耗所用药水");
            });
        foreach (var id in new[] { "energy-potion", "swift-potion", "blessing-of-the-forge", "flex-potion", "dexterity-potion" })
            Check.Run($"药水：正式{id}独立效果与生命周期", () =>
            {
                var s = New(rules); ClearBelt(s); var potion = ArrangePotion(s, id);
                var player = FormalSliceFixture.Player(s);
                var energy = FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, "Energy");
                var hand = Members(s, "Hand").Count;
                Submit(s, "UsePotion", potion);
                if (id == "energy-potion") Check.Equal(energy + 2, FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, "Energy"), "能量+2");
                if (id == "swift-potion") Check.Equal(hand + 3, Members(s, "Hand").Count, "抽3张");
                if (id == "blessing-of-the-forge")
                    foreach(var card in Members(s, "Hand"))
                        Check.True(s.State.GetField(card, s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded")).AsBool(), "升级整手牌");
                if (id == "flex-potion")
                {
                    Check.Equal(5L, FormalSliceFixture.PowerStacks(s, player, "base-spire:strength"), "临时力量+5");
                    Submit(s, "EndTurn");
                    Check.Equal(0L, FormalSliceFixture.PowerStacks(s, player, "base-spire:strength"), "回合末收回临时力量");
                }
                if (id == "dexterity-potion") Check.Equal(2L,
                    FormalSliceFixture.PowerStacks(s, player, "base-spire:dexterity-power"), "敏捷+2");
                Check.Equal(0, Members(s, "PotionBelt").Count, "完成后销毁");
            });
        Check.Run("药水：尚未实现的瓶明确不可用，丢弃保持可操作", () =>
        {
            var s = New(rules); ClearBelt(s); var bottle = ArrangePotion(s, "fairy-in-a-bottle", supported: false);
            Check.True(Legal(s, "UsePotion") is null, "只有未支持瓶不会发布空UsePotion");
            Check.True(Members(s, "PotionBelt").Contains(bottle), "不能使用不等于丢失");
            Submit(s, "DiscardPotion", bottle);
            Check.Equal(0, Members(s, "PotionBelt").Count, "仍可丢弃");
        });
        Check.Run("药水：正式Fire末次击杀先消耗再结算，进入下一房保留其他瓶实例/槽号", () =>
        {
            var s = New(rules); var fire = Members(s, "PotionBelt").Single(p => Text(s, p, "DefinitionId") == "base-spire:fire-potion");
            var retained = Members(s, "PotionBelt").Where(p => p != fire).ToArray();
            foreach(var enemy in Members(s, "Monsters").ToArray())
            {
                FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 1);
                Submit(s, "UsePotion", fire);
                s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [enemy]));
                if (Members(s, "Monsters").Count > 0) fire = ArrangePotion(s, "fire-potion", slot: 2);
            }
            Check.Equal(2, Members(s, "PotionBelt").Count, "末次击杀前先消耗瓶");
            var run = Members(s, "RunStates").Single();
            Check.Equal(FormalSliceFixture.Number(s, run, "base-spire:Run", "CurrentRoom"),
                FormalSliceFixture.Number(s, run, "base-spire:Run", "SettledRoom"), "药水末次击杀正常结算");
            Submit(s, "EnterNextRoom");
            Check.Sequence(retained, Members(s, "PotionBelt"), "跨房间保留相同实例");
            Check.Sequence([0L, 1L], retained.Select(p => Number(s, p, "SlotIndex")), "跨房槽号不变");
        });
    }

    private static void ClearBelt(Session s) { foreach(var bottle in Members(s, "PotionBelt").ToArray()) Submit(s, "DiscardPotion", bottle); }
    // Controlled initial inventory for effect-only checks, not a normal acquisition trajectory.
    private static InstanceId ArrangePotion(Session s, string shortId, bool supported = true, long slot = 0)
    {
        var metadata = Content.Model.PotionPool.Single(p => p.Id == "base-spire:" + shortId);
        var type = s.RuleSet.TypeByQualifiedId(PotionType); var player = FormalSliceFixture.Player(s);
        return s.State.RunAtomic(() =>
        {
            var bottle = s.State.CreateInstance(type.Id, s.State.ScopeOf(player));
            foreach(var (field, value) in new (string, Value)[] { ("DefinitionId", Value.Of(metadata.Id)),
                ("Rarity", Value.Of(metadata.Rarity)), ("Target", Value.Of(metadata.Target)), ("Usage", Value.Of(metadata.Usage)),
                ("SlotIndex", Value.Of(slot)), ("UseSupported", Value.Of(supported)), ("UnsupportedUseReason", Value.Of(metadata.UnsupportedUseReason)) })
                s.State.SetField(bottle, type.FieldIndex(field), value);
            s.State.AddToCollection(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:PotionBelt").Id, bottle);
            return bottle;
        });
    }

    private static Session New(RuleSet rules)
    {
        var s = new Session(rules, 20261011UL, [SliceManifest.PlayerRole], computeBackend: BaseSpireComputeBackend.Instance);
        s.Start(); return s;
    }
    private static IReadOnlyList<InstanceId> Members(Session s, string name) =>
        s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Field(Session s, InstanceId p, string name) =>
        s.State.GetField(p, s.RuleSet.TypeByQualifiedId(PotionType).FieldIndex(name));
    private static long Number(Session s, InstanceId p, string name) => Field(s, p, name).AsInt();
    private static string Text(Session s, InstanceId p, string name) => Field(s, p, name).AsString();
    private static LegalAction? Legal(Session s, string name) => s.LegalActions(SliceManifest.Player)
        .SingleOrDefault(a => a.Definition.QualifiedId == "base-spire:" + name);
    private static void Submit(Session s, string name, InstanceId? target = null)
    {
        var action = Legal(s, name) ?? throw new CheckFailedException("missing action " + name);
        s.Submit(action.Definition, SliceManifest.Player, target is { } p ? [Value.OfRef(p)] : []);
    }
}
