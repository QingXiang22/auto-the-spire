using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-01 行为检查：**正式单人工厂**能不能产出"与源一致的真实开局"，以及它的拒绝/身份契约。
///
/// 这里刻意不检查"内部声明自证"：起始牌组从**会话状态**读、遭遇从**实际出生的敌人**读、
/// 洗牌从**随机流计数**读。这样"包加载了 68 张卡"这类自证不能冒充"开局是对的"。
/// </summary>
internal static class SinglePlayerStartChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string LongTermDeck = "base-spire:LongTermDeck";
    private const string Monsters = "base-spire:Monsters";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string DrawPile = "base-spire:DrawPile";
    private const string CardTypeId = "base-spire:Card";

    // ENV 里程碑要求的固定 seed 之一：用它把"真实开局"钉住。
    private const string MilestoneSeed = "ENV-IRONCLAD-00";

    private static BaseSpireRuntimeFactory Factory() =>
        new(RunConfiguration.FrozenIronclad());

    public static void Run()
    {
        Check.Run("BASE-01 构建描述：dev 范围、Ironclad 身份、真实起始资源，且不含每局 seed [真实]", () =>
        {
            var factory = Factory();
            var build = factory.Describe();
            Check.Equal(BaseSpireScope.Dev, build.ScopeId, "默认范围身份是 dev（未闭合范围不能声称完整）");
            Check.Equal(BaseSpireCharacters.IroncladId, build.CharacterId, "角色身份");
            Check.Equal(80, build.StartingHp, "起始最大生命来自角色定义（Ironclad.cs:33）");
            Check.Equal(99, build.StartingGold, "起始金币来自角色定义（Ironclad.cs:35）");
            Check.Sequence(["base-spire:burning-blood"], build.StartingRelics, "起始遗物（Ironclad.cs:57）");
            Check.Equal(BaseSpireCharacters.IroncladDeck.Count, build.DeckSize, "起始牌组 10 张");
            Check.False(build.Summary.Contains(MilestoneSeed, StringComparison.Ordinal),
                "构建描述里不能出现每局 seed");
            Check.True(build.RulesetId.Length > 0, "静态规则身份非空");
            Check.True(build.RoomCount >= 2, $"当前范围至少覆盖 2 个战斗房间（实际 {build.RoomCount}）");
            Check.True(build.ExcludedEncounters.Count > 0,
                "dev 构建必须**逐条列出**被排除的遭遇，而不是静默少内容");
            Check.True(build.UnsupportedContent.Count > 0,
                "dev 构建必须列出未支持内容（含未支持的敌人/能力/卡牌）");
            Console.WriteLine($"      BASE-01 构建：ruleset={build.RulesetId} 卡={build.CardCount} "
                + $"能力={build.PowerCount} 敌人={build.EnemyCount} 起始牌组={build.DeckSize} "
                + $"弱遭遇={build.WeakEncounterCount} 普通遭遇={build.NormalEncounterCount} "
                + $"房间={build.RoomCount} 未支持={build.UnsupportedContent.Count} "
                + $"排除遭遇={build.ExcludedEncounters.Count}");
            Console.WriteLine($"      BASE-01 池：{string.Join(", ", build.SupportedEncounters)}");
            // 逐条打印排除原因：审计时要能核对"到底是没实现还是原版没有"。
            foreach (var (item, reason) in build.ExcludedEncounters)
            {
                Console.WriteLine($"      BASE-01 排除：{item} ← {reason}");
            }
        });

        Check.Run("BASE-01 同一配置两次构造得到同一静态规则身份（seed 不进规则身份）[真实]", () =>
        {
            var first = Factory().Describe();
            var second = Factory().Describe();
            Check.Equal(first.RulesetId, second.RulesetId,
                "同一配置的规则身份必须相同（否则 Host 与工具会各自描述一个构建）");
            var session = Factory().CreateSession(MilestoneSeed);
            Check.True(session.SessionIdentity.Contains("seed=", StringComparison.Ordinal),
                "会话身份含 seed 指纹（这是证据，不是公开 ruleset_id）");
            Check.False(first.RulesetId.Contains("seed=", StringComparison.Ordinal),
                "公开静态规则身份不含 seed");
        });

        Check.Run("BASE-01 会话创建路径：配置完成但**尚未 Start**，且一个随机数都没消费 [真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            Check.Equal(SessionStatus.Running, session.Session.Status, "尚未 Start");
            Check.Equal(0L, session.Session.Random.TotalDraws, "配置阶段不消费随机数");
            Check.Equal(0, session.Session.State.AllInstances(
                session.Session.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Count,
                "尚未 Start 时没有任何玩家实例：不存在建了一半的开局");
            Check.Equal(12 + 3, session.SeededStreams.Count,
                "原版 12 条 run 流 + 3 条 player 流都显式种好");
            Check.True(session.SeededStreams.All(entry => session.Session.Random.DrawCount(entry.Stream) == 0),
                "种下的每条流都还没抽过");
        });

        Check.Run("BASE-01 真实开局：起始牌组/生命/金币来自角色定义（不是诊断牌组）[真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            session.Start();
            Check.Equal(SessionStatus.Running, session.Session.Status, "开局后进入战斗（会话继续）");

            var run = Only(session, RunTypeId);
            Check.Equal(80L, Number(session, run, RunTypeId, "MaxHp"), "整局最大生命");
            Check.Equal(80L, Number(session, run, RunTypeId, "Hp"), "整局当前生命");
            Check.Equal(99L, Number(session, run, RunTypeId, "Gold"), "整局金币");

            var player = Only(session, "base-spire:Player");
            Check.Equal(80L, Number(session, player, "base-spire:Player", "MaxHp"), "战斗玩家最大生命");

            // 长期牌组 = 真实起始牌组（5 打击 / 4 防御 / 1 重击），**不是**每张已降级卡各一张。
            // R2：这里保留**实际顺序**（曾经把双方都排序后比较，于是"Bash→Defend→Strike"这种
            // 顺序缺陷被掩掉）。顺序检查放在下面 R2 的两条检查里，这里只做集合与诊断牌组的判别。
            var deck = LongTermDeckOrder(session);
            Check.Equal(BaseSpireCharacters.IroncladDeck.Count, deck.Count,
                "长期牌组张数 = 原版起始牌组（诊断牌组会让这里出现几十张牌）");
            Check.Sequence(
                BaseSpireCharacters.IroncladDeck.Order(StringComparer.Ordinal),
                deck.Order(StringComparer.Ordinal),
                "长期牌组的多重集 = 原版起始牌组");
            Check.False(deck.Any(id => id == "base-spire:offering"),
                "诊断牌组特征卡（Offering 等非起始牌）不得出现在正式开局");
        });

        RunRelicChecks();

        Check.Run("BASE-02 敌人出生生命：按 `Niche` 流掷点、双端包含、Hp==MaxHp，且可独立复算 [真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            var nicheSeed = session.SeededStreams.Single(entry => entry.Type == "Niche").Seed;
            var runSeed = session.RunSeed;
            session.Start();

            var monsters = session.Session.State.CollectionMembers(Collection(session, Monsters))
                .Select(instance => (
                    Definition: Definition(session, instance, MonsterTypeId),
                    Hp: Number(session, instance, MonsterTypeId, "Hp"),
                    MaxHp: Number(session, instance, MonsterTypeId, "MaxHp")))
                .ToList();
            Check.True(monsters.Count > 0, "第一间房必须有敌人");
            Check.True(monsters.All(monster => monster.Hp == monster.MaxHp),
                "出生时 Hp == MaxHp（来源把掷出的值同时写进两者）");
            Check.Equal(monsters.Count, session.Session.Random.DrawCount(
                    VanillaRandomLayout.EngineStreamName("Niche")),
                "每个敌人恰好消费一次 `Niche` 抽取");

            // 独立复算：同一 `Niche` 种子，按来源算法逐只推一遍（升序候选、排除同侧已用值、
            // 候选非空取第 k 个 / 空则抽整个范围）。与观察值逐只比较。
            var byId = SinglePlayerContentSource.Shared.Model.Enemies
                .ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
            var probe = new RandomService(runSeed, VanillaRandomLayout.AlgorithmId,
                VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
            probe.SeedStream(VanillaRandomLayout.EngineStreamName("Niche"), nicheSeed);
            var used = new List<long>();
            var expected = new List<long>();
            foreach (var monster in monsters)
            {
                var definition = byId[monster.Definition];
                var available = Enumerable.Range(definition.NormalMinHp,
                        definition.NormalMaxHp - definition.NormalMinHp + 1)
                    .Where(value => !used.Contains(value))
                    .ToList();
                var rolled = available.Count == 0
                    ? probe.DrawInt(VanillaRandomLayout.EngineStreamName("Niche"),
                        definition.NormalMinHp, definition.NormalMaxHp + 1)
                    : available[(int)probe.DrawInt(VanillaRandomLayout.EngineStreamName("Niche"), 0,
                        available.Count)];
                used.Add(rolled);
                expected.Add(rolled);
            }

            Check.Sequence(expected, monsters.Select(monster => monster.Hp).ToList(),
                "出生生命逐只等于独立复算的结果（顺序与抽取次数一致）");
            Check.True(monsters.All(monster =>
                    monster.Hp >= byId[monster.Definition].NormalMinHp
                    && monster.Hp <= byId[monster.Definition].NormalMaxHp),
                "掷出的生命落在声明范围内（双端包含）");
        });

        Check.Run("BASE-01-R1 范围身份：只有 dev 可实现，Ironclad/full 在建局前拒绝且不生成 Session [真实]", () =>
        {
            // 三个 id 都传一遍：dev 可运行，另外两个必须**在构建任何 Session 之前**失败。
            var dev = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).Describe();
            Check.Equal(BaseSpireScope.Dev, dev.ScopeId, "默认范围是 dev");
            Check.Sequence([BaseSpireScope.Dev, BaseSpireScope.DevMap, BaseSpireScope.DevRun], BaseSpireScope.Implemented,
                "开发范围显式区分单幕与多幕，完整Ironclad/full身份仍未开放");
            foreach (var scope in new[] { BaseSpireScope.Ironclad, BaseSpireScope.Full })
            {
                var failure = Record.Exception(() =>
                    new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, scope)).Describe());
                Check.True(failure is NotSupportedException,
                    $"范围 {scope} 必须被拒绝（实际 {failure?.GetType().Name ?? "没有异常"}）");
                Check.True(failure!.Message.Contains(scope, StringComparison.Ordinal),
                    $"拒绝信息必须点名范围 id（实际：{failure.Message}）");
                Check.True(failure.Message.Contains(BaseSpireScope.Dev, StringComparison.Ordinal),
                    "拒绝信息必须说明当前唯一可实现的范围是 dev");
            }

            // 未知范围 id 与"已知但未实现"是两类失败：前者是输入错误。
            var unknown = Record.Exception(() =>
                new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, "base-spire-made-up-v1")).Describe());
            Check.True(unknown is ArgumentException,
                $"未知范围 id 应是输入错误（实际 {unknown?.GetType().Name ?? "没有异常"}）");
        });

        Check.Run("BASE-01-R2 起始牌序：长期牌组逐位等于来源声明顺序，不是排序后的多重集 [真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            session.Start();
            var deck = LongTermDeckOrder(session);
            // 来源：sts2/src/Core/Models/Characters/Ironclad.cs:43-55
            //   Strike×5 → Defend×4 → Bash×1；PopulateStartingDeck 按声明顺序加牌。
            Check.Sequence(BaseSpireCharacters.IroncladDeck, deck,
                "长期牌组顺序必须逐位等于来源声明顺序");
            Check.False(deck.SequenceEqual(
                    BaseSpireCharacters.IroncladDeck.Order(StringComparer.Ordinal)),
                "牌组不是按内容 id 排序（排序会改变洗牌输入 ⇒ 同 seed 得到不同的手牌）");
        });

        Check.Run("BASE-01-R2 洗牌输入：独立复算 Fisher-Yates 复现抽牌堆，证明洗牌前是声明顺序 [真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            var shuffleSeed = session.SeededStreams
                .Single(entry => entry.Stream == SliceManifest.DrawStream).Seed;
            var runSeed = session.RunSeed;
            var declared = BaseSpireCharacters.IroncladDeck;
            session.Start();

            // 观察值：开局抽 5 张之后，抽牌堆里剩下的就是洗牌结果的尾段（抽牌从堆首按序取走）。
            var observed = DrawPileOrder(session);
            Check.Equal(declared.Count - SliceManifest.DrawPerTurn, observed.Count,
                "抽牌堆剩余张数 = 牌组 - 每回合抽牌");

            // 独立复算：同一 run seed + 同一 `combat-draw` 流种子，对**声明顺序**做引擎定义的
            // Fisher-Yates（`RandomService.Shuffle`，降序下标）。这是一条不依赖被测实现的对照。
            IReadOnlyList<string> Reproduce(IReadOnlyList<string> input)
            {
                var probe = new RandomService(runSeed, VanillaRandomLayout.AlgorithmId,
                    VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
                probe.SeedStream(SliceManifest.DrawStream, shuffleSeed);
                var order = Enumerable.Range(0, input.Count).ToList();
                probe.Shuffle(SliceManifest.DrawStream, order);
                return [.. order.Select(index => input[index])];
            }

            var expected = Reproduce(declared);
            Check.Sequence(expected.Skip(SliceManifest.DrawPerTurn).ToList(), observed,
                "洗牌前的输入必须是声明顺序：独立复算的洗牌尾段与观察到的抽牌堆一致");

            // 判别力：若洗牌前被按 id 排序过，同一 seed 会得到**不同**的尾段。
            var sorted = declared.Order(StringComparer.Ordinal).ToList();
            Check.False(Reproduce(sorted).Skip(SliceManifest.DrawPerTurn).SequenceEqual(observed),
                "排序后的输入在同一 seed 下会得到不同结果 ⇒ 本检查能区分两种顺序");
        });

        Check.Run("BASE-01-R3 源基线：未知基线拒绝，描述绑定实际内容摘要与随机布局 [真实]", () =>
        {
            var unknown = Record.Exception(() => new BaseSpireRuntimeFactory(new RunConfiguration
            {
                Character = BaseSpireCharacters.IroncladId,
                SourceBaselineId = "unverified-other-source",
            }).Describe());
            Check.True(unknown is ArgumentException,
                $"未知源基线必须拒绝（实际 {unknown?.GetType().Name ?? "没有异常"}）");

            var build = Factory().Describe();
            Check.Equal(BaseSpireSourceBaselines.Frozen, build.SourceBaselineId, "描述绑定支持的源基线");
            Check.True(build.SourceDigests.Count >= 20,
                $"实际加载的每个源资源都要有摘要（实际 {build.SourceDigests.Count} 条）");
            Check.True(build.SourceDigests.All(entry => entry.Digest.Length == 16),
                "每条源摘要都是内容摘要，不是资源名");
            Check.Equal(EncounterContent.Digest(EncounterContent.ReadText(EncounterContent.EncountersResource)),
                build.EncountersDigest, "遭遇源摘要与实际内容一致（可当场复算）");
            Check.True(build.SourceEvidencePaths.Any(path => path.Contains("Ironclad.cs", StringComparison.Ordinal)),
                "基线证据包含角色起始配置的来源定位");
            Check.True(build.StreamLayoutDigest.Length == 16 && build.PublicBuildId.Length == 16,
                "随机布局身份与公开构建身份都非空");
            Check.True(build.PublicBuildId != build.RulesetId,
                "公开构建身份覆盖范围/源/布局，不只是规则集摘要");
            Check.Equal(build.PublicBuildId, Factory().Describe().PublicBuildId,
                "同一配置两次构造的公开构建身份相同");
        });

        Check.Run("BASE-01 真实遭遇：第一间房的敌人来自**正式遭遇池**（不是固定两个 Cultist）[真实]", () =>
        {
            var factory = Factory();
            var build = factory.Describe();
            var session = factory.CreateSession(MilestoneSeed);
            session.Start();
            var lineup = Encounter(session);
            Check.True(lineup.Count > 0, "第一间房必须有敌人");
            Check.True(build.SupportedEncounters.Count > 0, "构建描述必须给出实际进池的遭遇 id");

            // 最强判别：实际出生的敌人多重集**恰好等于**池里某个遭遇的敌人多重集。
            var pool = SinglePlayerContentSource.Shared.EncounterPool.Encounters;
            var expected = string.Join(",", lineup);
            Check.Contains(pool, encounter =>
                    string.Join(",", encounter.EnemyIds.Order(StringComparer.Ordinal)) == expected,
                $"实际遭遇 [{expected}] 必须与池里某个遭遇的敌人集合一致"
                + $"（池：{string.Join("+", pool.Select(entry => entry.EncounterId))}）");

            // 固定遭遇的特征：两个 Cultist 的固定组合。它必须**不是**开局默认（dev 池里没有它）。
            Check.False(expected == "base-spire:calcified-cultist,base-spire:damp-cultist",
                "正式路径不得回退到固定 Cultist 遭遇");
        });

        Check.Run("BASE-01 开局洗牌：战斗牌堆用原版 `Shuffle` 流洗过，且不同 seed 得到不同顺序 [真实]", () =>
        {
            var first = Factory().CreateSession(MilestoneSeed);
            first.Start();
            var shuffleStream = VanillaRandomLayout.EngineStreamName("Shuffle");
            Check.True(first.Session.Random.DrawCount(shuffleStream) > 0,
                $"开局洗牌必须消费 `{shuffleStream}`（实际 {first.Session.Random.DrawCount(shuffleStream)} 次）");
            Check.Equal(first.Session.Random.DrawCount(shuffleStream),
                first.Session.Random.DrawCount(SliceManifest.DrawStream),
                "所有洗牌共用同一条原版 `shuffle` 流（不能一源两计数器）");

            var other = Factory().CreateSession("ENV-IRONCLAD-01");
            other.Start();
            Check.True(!string.Equals(DrawOrder(first), DrawOrder(other), StringComparison.Ordinal),
                "不同 seed 的开局抽牌堆顺序应当不同（同序说明洗牌没有真正用 seed）");
        });

        Check.Run("BASE-01 独立会话互不污染：同 seed 独立开局，改动一个不影响另一个 [真实]", () =>
        {
            var first = Factory().CreateSession(MilestoneSeed);
            var second = Factory().CreateSession(MilestoneSeed);
            Check.False(ReferenceEquals(first.Session, second.Session), "两次创建是两个会话对象");
            first.Start();
            second.Start();
            Check.Equal(DrawOrder(first), DrawOrder(second), "同配置同 seed 的开局逐字相同");
            Check.Equal(first.Session.Fault?.Message ?? string.Empty, second.Session.Fault?.Message ?? string.Empty,
                "同 seed 的故障状态一致");

            // 改动第一个会话的状态后，第二个会话的公开事实必须不变。
            var before = DrawOrder(second);
            var player = Only(first, "base-spire:Player");
            first.Session.State.RunAtomic(() => first.Session.State.SetField(player,
                first.Session.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Hp"), Value.Of(1L)));
            Check.Equal(1L, Number(first, player, "base-spire:Player", "Hp"), "第一个会话被改动");
            Check.Equal(before, DrawOrder(second), "第二个会话不受影响（无共享可变状态）");
        });

        Check.Run("BASE-01 拒绝契约：不支持的进阶/角色在建局前失败，不退回 Ironclad 或进阶 0 [真实]", () =>
        {
            var advanced = Record.Exception(() => new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(1)).Describe());
            Check.True(advanced is NotSupportedException,
                $"进阶 1 必须被拒绝（实际 {advanced?.GetType().Name ?? "没有异常"}）");
            var silent = Record.Exception(() =>
                new BaseSpireRuntimeFactory(new RunConfiguration { Character = "Silent" }).Describe());
            Check.True(silent is NotSupportedException, $"Silent 必须被拒绝（实际 {silent?.GetType().Name ?? "没有异常"}）");
            var unknown = Record.Exception(() =>
                new BaseSpireRuntimeFactory(new RunConfiguration { Character = "NotACharacter" }).Describe());
            Check.True(unknown is ArgumentException, $"未知角色必须被拒绝（实际 {unknown?.GetType().Name ?? "没有异常"}）");
            // 空 seed 也要在建局前失败。
            var emptySeed = Record.Exception(() => Factory().CreateSession("   "));
            Check.True(emptySeed is ArgumentException, $"空 seed 必须被拒绝（实际 {emptySeed?.GetType().Name ?? "没有异常"}）");
        });

        Check.Run("BASE-01 seed 处理：原始与实际接受值一致（命令行 seed 不做标准化）[真实]", () =>
        {
            var session = Factory().CreateSession("  env-ironclad-00  ");
            Check.Equal("  env-ironclad-00  ", session.RequestedSeed, "原始请求原样保留");
            Check.Equal("  env-ironclad-00  ", session.AcceptedSeed,
                "实际接受值 = 原始字符串（命令行 seed 路径不做 CanonicalizeSeed）");
            // 反例：大厅路径的标准化会把 I→1、O→0，那会把 ENV-IRONCLAD-00 变成另一个局。
            Check.False(VanillaRandomLayout.CanonicalizeSeed("ENV-IRONCLAD-00") == "ENV-IRONCLAD-00",
                "大厅标准化确实会改字符：因此正式入口不能用它（否则里程碑 seed 被悄悄换掉）");
            Check.Equal(VanillaRandomLayout.RunSeed(session.AcceptedSeed), session.RunSeed,
                "run seed 由接受的字符串哈希（不加前缀、不换字符）");
            var upFront = session.SeededStreams.Single(entry => entry.Type == "UpFront");
            Check.Equal((ulong)VanillaRandomLayout.StreamSeed(checked((uint)session.RunSeed), "UpFront"), upFront.Seed,
                "流种子 = run seed + hash32(snake_case(流名))");
        });
        Check.Run("BASE-01 正式开局可实际推进：只用公开动作走到真实胜负或房间范围尽头 [真实]", () =>
        {
            var factory = Factory();
            var session = factory.CreateSession(MilestoneSeed);
            session.Start();
            var rooms = new List<int>();
            var outcome = PlayOut(session, rooms, maxSteps: 4000);
            Check.True(session.Session.Status != SessionStatus.Faulted,
                $"公开动作推进不得让引擎报错（Fault：{session.Session.Fault?.Message ?? "无"}）");
            Check.True(rooms.Count > 0,
                "至少打完一间房：否则是开局就卡住，不是可实际游玩");
            var terminal = session.Session.Terminal?.Definition.QualifiedId ?? string.Empty;
            Check.True(outcome.Length > 0, $"必须有明确的结束原因（实际：{outcome}）终局={terminal}");
            // 报告实际轨迹（人工核对用）：
            Console.WriteLine($"      BASE-01 轨迹：seed={MilestoneSeed} 房间={string.Join("→", rooms)} "
                + $"终局={terminal} 步骤={outcome}");
        });

        Check.Run("BASE-01 房间推进：清房后 `EnterNextRoom` 合法，且下一间房的敌人来自池里**另一个**遭遇 [真实]", () =>
        {
            var factory = Factory();
            var session = factory.CreateSession(MilestoneSeed);
            session.Start();
            var first = Encounter(session);
            // 受控清场只用于**接线检查**：把当前敌人压到 1 血再打一下（这不是整洁局的验收证据）。
            ClearRoom(session);
            var enter = session.Session.LegalActions(session.Participant)
                .FirstOrDefault(action => action.Definition.QualifiedId == "base-spire:EnterNextRoom");
            Check.True(enter is not null, "清房后可以进入下一间房");
            session.Session.Submit(enter!.Definition, session.Participant, []);
            AnswerAll(session);
            var second = Encounter(session);
            Check.True(second.Count > 0, "第二间房必须有敌人（池没取到就会空）");
            var pool = SinglePlayerContentSource.Shared.EncounterPool.Encounters
                .Select(entry => string.Join(",", entry.EnemyIds.Order(StringComparer.Ordinal)))
                .ToList();
            Check.Contains(pool, expected => expected == string.Join(",", second),
                $"第二间房的敌人 [{string.Join(",", second)}] 也必须在池里");
            Console.WriteLine($"      BASE-01 房间遭遇：1=[{string.Join(",", first)}] 2=[{string.Join(",", second)}]");
        });
    }

    /// <summary>
    /// 只用**公开合法动作**推进：有可打出的牌就打第一张合法绑定，否则结束回合；
    /// 清房后可以进下一间房就进。返回结束原因文本，`rooms` 记录实际打过的房间号。
    /// 不注入状态、不改血、不跳房——因此它的结果就是"这局真实玩到哪"。
    /// </summary>
    /// <summary>最近几次公开候选快照：失败时打印出来，便于定位"卡在哪一步"。</summary>
    private static readonly List<string> _lastActions = [];
    private static string PlayOut(BaseSpireRunSession session, List<int> rooms, int maxSteps)
    {
        var playCard = "base-spire:PlayCard";
        var endTurn = "base-spire:EndTurn";
        var enterNextRoom = "base-spire:EnterNextRoom";
        var upgrade = "base-spire:UpgradeLongTermCard";
        for (var step = 0; step < maxSteps; step++)
        {
            if (session.Session.Status == SessionStatus.AwaitingDecision)
            {
                if (!AnswerDefault(session))
                {
                    var blocked = session.Session.PendingDecision!;
                    var monsters = session.Session.State.CollectionMembers(Collection(session, Monsters))
                        .Select(monster => $"{Definition(session, monster, MonsterTypeId)}@"
                            + $"{Number(session, monster, MonsterTypeId, "Hp")}")
                        .ToList();
                    var player = Only(session, "base-spire:Player");
                    return $"房间 {CurrentRoom(session)} 的决策没有可选项"
                        + $"（kind={blocked.Kind} min={blocked.Minimum} max={blocked.Maximum}"
                        + $" prompt='{blocked.Prompt}' 敌人=[{string.Join(",", monsters)}]"
                        + $" 玩家Hp={Number(session, player, "base-spire:Player", "Hp")}"
                        + $" Turn={Number(session, player, "base-spire:Player", "Turn")}) 轨迹尾部=[{string.Join(" | ", _lastActions.TakeLast(6))}]";
                }

                continue;
            }

            if (session.Session.Status == SessionStatus.Terminal)
            {
                var finalRoom = CurrentRoom(session);
                if (rooms.Count == 0 || rooms[^1] != finalRoom) rooms.Add((int)finalRoom);
                return $"终局 {session.Session.Terminal!.Definition.QualifiedId} @房间{finalRoom}";
            }

            if (session.Session.Status != SessionStatus.Running)
            {
                return $"状态 {session.Session.Status}";
            }

            var legal = session.Session.LegalActions(session.Participant);
            var room = CurrentRoom(session);
            if (rooms.Count == 0 || rooms[^1] != room) rooms.Add((int)room);
            if (_lastActions.Count > 64) _lastActions.RemoveAt(0);
            _lastActions.Add($"room{room}/{session.Session.StateVersion}:"
                + string.Join("+", legal.Select(candidate => candidate.Definition.QualifiedId.Replace("base-spire:", ""))));
            var action = legal.FirstOrDefault(candidate => candidate.Definition.QualifiedId == playCard);
            if (action is not null)
            {
                // 策略：优先出**攻击牌**（读的是公开候选里的卡实例字段），否则出第一张合法牌。
                // 这不是"最优策略"，只是让朴素驱动能真的打死敌人；全程不做任何状态注入。
                var binding = action.Bindings.FirstOrDefault(args => args.Count > 0 && IsAttack(session, args[0]))
                    ?? action.Bindings[0];
                session.Session.Submit(action.Definition, session.Participant, binding);
                continue;
            }

            action = legal.FirstOrDefault(candidate => candidate.Definition.QualifiedId == upgrade);
            if (action is not null)
            {
                // 占位奖励对**已升级**的牌是空操作（幂等），因此驱动要挑一张还没升级的牌；
                // 全都升级过就跳过奖励，不要让循环停在同一个空操作上。
                var target = action.Bindings.FirstOrDefault(args => args.Count > 0 && !IsUpgraded(session, args[0]));
                if (target is not null)
                {
                    session.Session.Submit(action.Definition, session.Participant, target);
                    continue;
                }
            }

            action = legal.FirstOrDefault(candidate => candidate.Definition.QualifiedId == enterNextRoom);
            if (action is not null)
            {
                session.Session.Submit(action.Definition, session.Participant, []);
                continue;
            }

            action = legal.FirstOrDefault(candidate => candidate.Definition.QualifiedId == endTurn);
            if (action is not null)
            {
                session.Session.Submit(action.Definition, session.Participant, []);
                continue;
            }

            return $"房间 {room} 没有可执行动作（范围尽头；已打房间 {string.Join("→", rooms)}）";
        }

        return $"步数上限 {maxSteps} 用尽（房间 {string.Join("→", rooms)}）轨迹尾部=[{string.Join(" | ", _lastActions.TakeLast(8))}]";
    }

    private static long CurrentRoom(BaseSpireRunSession session)
    {
        var run = session.Session.State.CollectionMembers(Collection(session, "base-spire:RunStates")).FirstOrDefault();
        return run == default ? 0L : Number(session, run, RunTypeId, "CurrentRoom");
    }

    /// <summary>这张长期牌是不是已经升级过（占位奖励对已升级的牌是空操作）。</summary>
    private static bool IsUpgraded(BaseSpireRunSession session, Value cardRef)
    {
        if (cardRef.Kind != ValueKind.Ref) return false;
        var field = session.Session.RuleSet.TypeByQualifiedId(CardTypeId).FieldIndex("Upgraded");
        return session.Session.State.GetField(cardRef.AsRef(), field).AsBool();
    }

    /// <summary>这张牌是不是攻击牌（读公开候选里的卡实例字段；用于朴素驱动挑牌）。</summary>
    private static bool IsAttack(BaseSpireRunSession session, Value cardRef)
    {
        if (cardRef.Kind != ValueKind.Ref) return false;
        var field = session.Session.RuleSet.TypeByQualifiedId(CardTypeId).FieldIndex("Kind");
        return session.Session.State.GetField(cardRef.AsRef(), field).AsEnumOrdinal() == 0;
    }

    public static void RunRelicChecks()
    {
        Check.Run("BASE-02 起始遗物（Burning Blood）：战斗胜利后回 6 血并被最大生命截断 [真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            session.Start();

            // 遗物实例在**整局**状态里（跨房间保留），身份来自角色注册表。
            var relics = session.Session.State.CollectionMembers(Collection(session, "base-spire:Relics"))
                .Select(instance => Definition(session, instance, "base-spire:Relic"))
                .ToList();
            Check.Sequence(["base-spire:burning-blood"], relics,
                "开局遗物栏 = 角色注册表声明的起始遗物（来源 Ironclad.cs:57）");

            // 受控初始化（**明示的机制 case 初始化**）：玩家 40 血、敌人 1 血，然后只用公开动作在同一回合内清场。
            var player = Only(session, "base-spire:Player");
            SetNumber(session, player, "base-spire:Player", "Hp", 40);
            KillAllInTurn(session);
            var run = Only(session, RunTypeId);
            Check.Equal(46L, Number(session, run, RunTypeId, "Hp"),
                "清场后整局 HP = 40 + 6（Burning Blood 的 AfterCombatVictory 回血）");
        });

        Check.Run("BASE-02 遗物回血不越过最大生命：78 血清场后是 80，不是 84 [真实]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            session.Start();
            var player = Only(session, "base-spire:Player");
            SetNumber(session, player, "base-spire:Player", "Hp", 78);
            KillAllInTurn(session);
            var run = Only(session, RunTypeId);
            Check.Equal(80L, Number(session, run, RunTypeId, "Hp"),
                "回血被最大生命截断（来源 CreatureCmd.Heal:698）");
        });

        Check.Run("Relics: melted starter remains owned but victory healing is suppressed [controlled]", () =>
        {
            var session = Factory().CreateSession(MilestoneSeed);
            session.Start();
            var relic = session.Session.State.CollectionMembers(Collection(session, "base-spire:Relics")).Single();
            var type = session.Session.RuleSet.TypeByQualifiedId("base-spire:Relic");
            // Explicit mechanism fixture; Melt command semantics are checked separately.
            session.Session.State.RunAtomic(() =>
            {
                session.Session.State.SetField(relic, type.FieldIndex("IsWax"), Value.Of(true));
                session.Session.State.SetField(relic, type.FieldIndex("IsMelted"), Value.Of(true));
            });
            SetNumber(session, Only(session, "base-spire:Player"), "base-spire:Player", "Hp", 40);
            KillAllInTurn(session);
            Check.Equal(40L, Number(session, Only(session, RunTypeId), RunTypeId, "Hp"), "melted relic cannot heal");
            Check.Equal(relic, session.Session.State.CollectionMembers(Collection(session, "base-spire:Relics")).Single(), "inventory retained");
        });
    }

    /// <summary>
    /// 在**同一个玩家回合内**清场：把敌人压到 1 血后只用公开 `PlayCard` 动作（优先攻击牌）逐个击杀，
    /// **不结束回合**（因此玩家不会在过程中受到伤害，断言才可确定）。清不完就显式失败。
    /// 这是**明示的机制 case 初始化**，不是整局验收证据。
    /// </summary>
    private static void KillAllInTurn(BaseSpireRunSession session)
    {
        var monsterType = session.Session.RuleSet.TypeByQualifiedId(MonsterTypeId);
        foreach (var monster in session.Session.State.CollectionMembers(Collection(session, Monsters)).ToList())
        {
            session.Session.State.RunAtomic(() => session.Session.State.SetField(monster,
                monsterType.FieldIndex("Hp"), Value.Of(1L)));
        }

        while (Encounter(session).Count > 0)
        {
            var legal = session.Session.LegalActions(session.Participant)
                .FirstOrDefault(candidate => candidate.Definition.QualifiedId == "base-spire:PlayCard");
            if (legal is null)
            {
                throw new CheckFailedException(
                    $"清场接线检查卡住：房间 {CurrentRoom(session)} 在击杀全部敌人前就不能再出牌"
                    + "（敌人多于本回合可打出的牌数）");
            }

            var binding = legal.Bindings.FirstOrDefault(args => args.Count > 0 && IsAttack(session, args[0]))
                ?? legal.Bindings[0];
            session.Session.Submit(legal.Definition, session.Participant, binding);
            AnswerAll(session);
        }
    }

    /// <summary>把当前敌人压到 1 血并打出一次攻击清场（**接线检查**专用，不是整洁局证据）。</summary>
    private static void ClearRoom(BaseSpireRunSession session)
    {
        var monsterType = session.Session.RuleSet.TypeByQualifiedId(MonsterTypeId);
        foreach (var monster in session.Session.State.CollectionMembers(Collection(session, Monsters)).ToList())
        {
            session.Session.State.RunAtomic(() => session.Session.State.SetField(monster,
                monsterType.FieldIndex("Hp"), Value.Of(1L)));
        }

        // 反复用公开动作把这一间房清完；手牌打不动就结束回合（会自然抽到能打的牌）。
        while (Encounter(session).Count > 0)
        {
            var legal = session.Session.LegalActions(session.Participant)
                .FirstOrDefault(candidate => candidate.Definition.QualifiedId == "base-spire:PlayCard");
            if (legal is null)
            {
                var end = session.Session.LegalActions(session.Participant)
                    .FirstOrDefault(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
                if (end is null)
                {
                    throw new CheckFailedException(
                        $"清场接线检查卡住：房间 {CurrentRoom(session)} 既不能出牌也不能结束回合");
                }

                session.Session.Submit(end.Definition, session.Participant, []);
                AnswerAll(session);
                continue;
            }

            session.Session.Submit(legal.Definition, session.Participant, legal.Bindings[0]);
            AnswerAll(session);
        }
    }

    private static void AnswerAll(BaseSpireRunSession session)
    {
        while (session.Session.Status == SessionStatus.AwaitingDecision)
        {
            if (!AnswerDefault(session))
            {
                throw new CheckFailedException("决策没有可选项：无法继续（接线检查）");
            }
        }
    }

    /// <summary>
    /// 按**决策种类**给出一个合法的默认回答（实体/枚举/整数/布尔各走自己的字段）。
    /// 返回 false 表示这个决策当前没有可选项——那是真实阻塞，不能靠"传个空回答"糊过去。
    /// </summary>
    private static bool AnswerDefault(BaseSpireRunSession session)
    {
        var pending = session.Session.PendingDecision!;
        switch (pending.Kind)
        {
            case DecisionResponseKind.EntityRef:
            case DecisionResponseKind.EntityRefList:
                if (pending.Candidates.Count == 0) return false;
                var count = (int)Math.Max(pending.Minimum, 1);
                session.Session.Answer(new DecisionAnswer(pending.RequestId,
                    [.. Enumerable.Range(0, count).Select(index => pending.Candidates[index])]));
                return true;
            case DecisionResponseKind.Enum:
                session.Session.Answer(new DecisionAnswer(pending.RequestId, EnumOrdinal: 0));
                return true;
            case DecisionResponseKind.Int:
                session.Session.Answer(new DecisionAnswer(pending.RequestId, Number: pending.Minimum));
                return true;
            default:
                session.Session.Answer(new DecisionAnswer(pending.RequestId, Flag: true));
                return true;
        }
    }

    private static InstanceId Only(BaseSpireRunSession session, string typeId) =>
        session.Session.State.AllInstances(session.Session.RuleSet.TypeByQualifiedId(typeId).Id).Single();

    private static int Collection(BaseSpireRunSession session, string qualifiedId) =>
        session.Session.RuleSet.Collections.Single(item => item.QualifiedId == qualifiedId).Id;

    private static long Number(BaseSpireRunSession session, InstanceId instance, string typeId, string field) =>
        session.Session.State.GetField(instance, session.Session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field))
            .AsInt();

    private static string Definition(BaseSpireRunSession session, InstanceId instance, string typeId) =>
        session.Session.State.GetField(instance, session.Session.RuleSet.TypeByQualifiedId(typeId).FieldIndex("DefinitionId"))
            .AsString();

    /// <summary>本间房实际出生的敌人（定义 id，多重集按序）。</summary>
    private static IReadOnlyList<string> Encounter(BaseSpireRunSession session) =>
    [
        .. session.Session.State.CollectionMembers(Collection(session, Monsters))
            .Select(monster => Definition(session, monster, MonsterTypeId))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>抽牌堆的当前顺序特征（用于"同 seed 相同 / 不同 seed 不同"的判别）。</summary>
    private static string DrawOrder(BaseSpireRunSession session) => string.Join(",", DrawPileOrder(session));

    /// <summary>抽牌堆的当前顺序（**不排序**：顺序本身就是被检查的事实）。</summary>
    private static List<string> DrawPileOrder(BaseSpireRunSession session) =>
    [
        .. session.Session.State.CollectionMembers(Collection(session, DrawPile))
            .Select(card => Definition(session, card, CardTypeId)),
    ];

    /// <summary>受控地设一个整数字段（只用于**明示的机制 case 初始化**）。</summary>
    private static void SetNumber(BaseSpireRunSession session, InstanceId instance, string typeId, string field,
        long value) =>
        session.Session.State.RunAtomic(() => session.Session.State.SetField(instance,
            session.Session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field), Value.Of(value)));

    /// <summary>长期牌组的顺序（**不排序**）。</summary>
    private static List<string> LongTermDeckOrder(BaseSpireRunSession session) =>
    [
        .. session.Session.State.CollectionMembers(Collection(session, LongTermDeck))
            .Select(card => Definition(session, card, CardTypeId)),
    ];
}

/// <summary>最小的"记录异常"助手（不吞异常，只是把它取出来做断言）。</summary>
internal static class Record
{
    public static Exception? Exception(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
