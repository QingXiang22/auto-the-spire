using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.SinglePlayer;

/// <summary>
/// **正式单人的唯一组包/会话工厂**（ENV/BASE-01）。B 的对拍后端与 C 的公开 Host 都从这里开局，
/// 不各自造一套"开局"。
///
/// 契约：
/// <list type="number">
/// <item>构造时按 <see cref="RunConfiguration"/> 建**一份只读规则集**，并可在建局前被拒绝
///       （未知角色 / 不支持的进阶 / 未知范围都失败，不退回 Ironclad 或进阶 0）；</item>
/// <item><see cref="CreateSession"/> 对给定**每局字符串 seed** 产出**已配置、尚未 Start** 的独立
///       <see cref="Session"/>：原版随机 Profile、按原版布局种好的全部线流，一个随机数都没消费；</item>
/// <item><see cref="Describe"/> 给出只读规则/构建描述（不含 seed，同配置逐字节相同）；
///       实际 seed 只出现在会话句柄与证据里。</item>
/// </list>
///
/// 禁止事项（与 ENV 约束一致）：不读取 `.author` 原型、不用诊断牌组、不用固定遭遇、
/// 不在 Start 之后用 `SetField` 补造开局、不返回共享状态；两局相同配置必须互不污染。
/// </summary>
public sealed class BaseSpireRuntimeFactory
{
    private readonly SinglePlayerContent _content;
    private readonly RuleSet _rules;
    private readonly BasePublicSemantics _publicSemantics;
    private readonly BaseSpireBuildDescription _description;
    private readonly RunActPlan? _actPlan;
    private readonly SliceEncounterPool _runtimePool;

    public BaseSpireRuntimeFactory(RunConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        // 建局前校验：不支持的角色/进阶在这里就失败（不产生"半配置"的规则集）。
        Character = configuration.Resolve();
        _content = SinglePlayerContentVersions.Apply(SinglePlayerContentSource.Shared, configuration.ContentMetadataVersion);
        _actPlan = configuration.ScopeId == BaseSpireScope.DevRun ? RunActPlan.Default : null;
        _runtimePool = (_actPlan?.Combined ?? _content.EncounterPool) with { RandomLayoutId = configuration.RandomLayoutId };
        if (_actPlan is not null)
        {
            var loaded = _content.EnemyIds.ToHashSet(StringComparer.Ordinal);
            bool Executable(SliceEncounter encounter) => encounter.AllEnemyIds.Count > 0 && encounter.AllEnemyIds.All(loaded.Contains);
            _content = _content with
            {
                WeakEncounters = _runtimePool.WeakEncounters.Where(Executable).ToArray(),
                NormalEncounters = _runtimePool.RegularEncounters.Where(Executable).ToArray(),
                BossEncounters = _runtimePool.Bosses.Where(Executable).ToArray(),
                ExcludedEncounters = _runtimePool.AllEncounters.Where(e => !Executable(e))
                    .Select(e => (e.EncounterId, "Source identity retained; enemy behavior or spawn formation is not implemented")).ToArray(),
                SourceDigests = [.. _content.SourceDigests, (RunActPlan.Resource, _actPlan.Digest)],
            };
        }

        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(
            includeTestHeadbutt: false,
            fromAuthorText: false,
            authorModel: _content.Model,
            packageVersion: configuration.ScopeId,
            encounterPool: _runtimePool,
            // 正式构建：战斗开始时洗战斗牌堆（原版用 run 的 `Shuffle` 流）。
            shuffleBattleDeck: true,
            // 正式构建：**不按房间计数发 Victory**——原版胜利只在最终 BOSS 之后的终局事件发生。
            terminalOnConfiguredRoomCount: false,
            // BASE-02：起始遗物（声明表 + 本局实际持有）。
            relicLibrary: SpireRelics.All,
            startingRelics: [.. _content.StartingRelics.Select(relic => relic.ContentId)],
            // BASE-02：敌人出生生命按原版 `Niche` 流掷点（每次出生恰好一次抽取）。
            spawnHpStream: VanillaRandomLayout.EngineStreamName("Niche"),
            // T05：**默认关**。只有 dev-map scope 打开地图物化/`ChooseMapPoint`；
            // 开与关决定包里有没有 `MapNodes`/`MapChildren` 与新动作，因此是范围身份的一部分。
            actMapEnabled: ActMapEnabled, initializeNewRun: true, actPlan: _actPlan).Build());
        _rules = builder.Build();
        // ENV-T03：公开出口所必需的**不可变游戏语义元数据**。它在构造期就与实际规则集、所选投影和
        // **实际加载内容**逐条闭合（缺集合用途/终局解释/内容字段都会在这里显式失败），因此 Host 与工具
        // 读到的不是"另一份手写登记表"，而是这一份构建的语义。base 不引用 net：翻译由公开出口一侧做。
        _publicSemantics = BasePublicSemanticsBuilder.Build(
            _rules,
            ProjectionId,
            SliceManifest.PlayerRole,
            new BasePublicContentSource(
                _content.CardIds,
                _content.PowerIds,
                _content.EnemyIds,
                [.. SpireRelics.All.Select(relic => relic.ContentId)])
                { PotionIds = [.. _content.Model.PotionPool.Select(p => p.Id)] });
        _description = Describe(_rules, _publicSemantics);
    }

    public RunConfiguration Configuration { get; }

    public BaseSpireCharacter Character { get; }

    /// <summary>
    /// T05：本工厂是否把地图/房间接线接进包（只有 <see cref="BaseSpireScope.DevMap"/> 打开）。
    /// 这决定包里有没有 `MapNodes`/`MapChildren`/`ChooseMapPoint`、种子集合里有没有地图流、
    /// 以及修为哪个计算后端，因此它是**范围身份的一部分**，不是运行期开关。
    /// </summary>
    public bool ActMapEnabled =>
        Configuration.ScopeId is BaseSpireScope.DevMap or BaseSpireScope.DevRun;

    /// <summary>只读规则集（可共享；会话状态不在这里）。</summary>
    public RuleSet Rules => _rules;

    /// <summary>
    /// ENV-T03：本构建的**公开语义元数据**（只读、不含每局 seed/可变状态）。只补通用声明缺的四类游戏含义：
    /// 内容 ID 目录、集合用途与位置/归属、已知位置角色、终局胜负解释。它在构造期已与
    /// <see cref="Rules"/>、所选投影及实际加载内容闭合；外部拿到的字典是只读包装，不能被篡改。
    /// </summary>
    public BasePublicSemantics PublicSemantics => _publicSemantics;

    /// <summary>只读规则/构建描述。同一配置下逐字节相同，且不含每局 seed。</summary>
    public BaseSpireBuildDescription Describe() => _description;

    /// <summary>
    /// 为一个**每局字符串 seed** 建一个已配置、尚未 Start 的独立会话。
    ///
    /// seed 处理按源：**不做标准化**。冻结源码里 `SeedHelper.CanonicalizeSeed`（大写 / O→0 / I→1）
    /// 只作用于**大厅里玩家输入的** seed 与每日挑战（`StartRunLobby.cs:725`、
    /// `NDailyRunScreen.cs:281-282`）；命令行的 `--seed` 与 bootstrap 路径
    /// （`NGame.cs:299`、`NSceneBootstrapper.cs:59`）把原始字符串直接交给哈希。
    /// 本工厂对应后者，因此原始请求与实际接受值是同一个字符串——既不改大小写也不换字符，
    /// 否则 `ENV-IRONCLAD-00` 这类里程碑 seed 会被悄悄改成另一个局。
    ///
    /// 之后的流程：`hash32(seed)` → run seed；每条原版线流（12 run + 3 player）按
    /// `seed + hash32(snake_case(名))` 显式种进引擎，**在任何抽数之前**。
    /// </summary>
    public BaseSpireRunSession CreateSession(string gameSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameSeed);
        var accepted = gameSeed;
        var layout = new RunRandomLayout(Configuration.RandomLayoutId);
        var runSeed = layout.RootSeed(accepted);
        var session = new Session(
            _rules,
            // 会话种子就是本局的 run seed：显式种流的那些用不到它，但未显式种的流由它派生，
            // 因此必须是同一个数（不能另取一个"引擎种子"制造第二套随机来源）。
            runSeed,
            [SliceManifest.PlayerRole],
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile,
            // ENV-T01：正式敌人状态机的加权选取是一个**纯计算资产**（`SourceWeightedPick`），
            // 宿主必须装上它的后端；装载后 `DrawBounded` 等既有语义不变。
            // T05：dev-map 下换成 `BaseSpireComputeBackend`——它对非地图资产**逐字转发**同一个
            // `SourceWeightedPickBackend`，只是多认三个地图资产（id 相同，因此解析仍按同一个后端 id）。
            computeBackend: ActMapEnabled ? new BaseSpireComputeBackend() : SourceWeightedPickBackend.Instance);

        // 关键顺序：**先种流，再由调用方 Start**。种流在第一次抽数之前才被接受（引擎会拒绝晚种）。
        var seeded = layout.Streams(accepted).ToList();
        foreach (var item in seeded) session.Random.SeedStream(item.Stream, item.Seed);
        // BASE-02：**遭遇派生流**。带编队的遭遇在进入房间时按源公式
        // `(uint)(runSeed + TotalFloor + hash(entry))`（`EncounterModel.cs:268`）用**独立** Rng 抽取阵容，
        // 与 run 级流互不干扰；这里在幕开始前把每个 (遭遇, 楼层) 的流显式种好（生成点不能临场重种）。
        // 地图范围按实际访问数选择流，首幕含Boss最多16次；非地图范围才以战斗房间号替代。
        var encounterStreams = new List<(string Type, string Stream, ulong Seed)>();
        foreach (var encounter in _runtimePool.FormationEncounters)
        {
            var entry = VanillaRandomLayout.SourceEntry(encounter.EncounterId);
            for (var floor = 1; floor <= (_actPlan?.Acts.Sum(a => a.NormalRooms + 2)
                ?? (ActMapEnabled ? MapRoomWiring.MaxRow : _content.EncounterPool.RoomCount)); floor++)
            {
                var stream = VanillaRandomLayout.EncounterStreamName(entry, floor);
                var seed = layout.ContentSeed(runSeed, floor, entry);
                session.Random.SeedStream(stream, seed);
                encounterStreams.Add(("Encounter", stream, seed));
            }
        }

        // T05：**地图流**。原版 `act_{n}_map` 是 run 级命名流（种子 = `runSeed + hash("act_1_map")`），
        // 7 列地图生成所需的每个随机数都从它抽；只在地图 scope 种 —— 默认路径的 SeededStreams
        // 内容与顺序因此逐字节不变（同一条断言 12+3 仍然成立）。
        if (ActMapEnabled)
        {
            var mapStream = MapRoomWiring.ActMapStreamName;
            var mapSeed = layout.NamedSeed(runSeed, VanillaRandomLayout.ActMapStreamType(1));
            session.Random.SeedStream(mapStream, mapSeed);
            seeded.Add(("ActMap", mapStream, mapSeed));
            if (_actPlan is not null)
                foreach (var act in _actPlan.Acts.Skip(1))
                {
                    var stream = VanillaRandomLayout.ActMapStreamName(act.Index + 1);
                    var seed = layout.NamedSeed(runSeed, VanillaRandomLayout.ActMapStreamType(act.Index + 1));
                    session.Random.SeedStream(stream, seed); seeded.Add(("ActMap", stream, seed));
                }

            // ENV-T09-unknown-room-modules-20261012：**事件级流**（源 `EventModel.BeginEvent` 的
            // `new Rng(runSeed + slot + hash(Id.Entry))`）。它们是内容派生流，因此只在用到事件的
            // dev-map scope 种；按**源事件池全量**种（不按"有没有可执行模块"筛），这样将来加模块时
            // 流位置已经对齐，且未实现事件被选中时也不会因为缺流而变成另一种失败。
            foreach (var sourceName in _actPlan is null ? RunSourcePoolManifest.Events : _actPlan.Acts.SelectMany(a => a.Events).Distinct(StringComparer.Ordinal))
            {
                var entry = VanillaRandomLayout.SourceEventEntry(sourceName);
                var stream = VanillaRandomLayout.EventStreamName(entry);
                var eventSeed = layout.ContentSeed(runSeed, 0, entry);
                session.Random.SeedStream(stream, eventSeed);
                seeded.Add(("Event", stream, eventSeed));
            }
        }

        // Configuration must finish before any random consumption.
        if (session.Random.TotalDraws != 0)
        {
            throw new InvalidOperationException(
                $"建局阶段消费了 {session.Random.TotalDraws} 个随机数：配置阶段不能开局");
        }
        return new BaseSpireRunSession(
            Configuration,
            _description,
            gameSeed,
            accepted,
            runSeed,
            session,
            session.Participant(0),
            SliceManifest.PlayerRole,
            ProjectionId,
            seeded)
        {
            EncounterStreams = encounterStreams,
        };
    }

    /// <summary>公开投影标识（与 net 侧观看者名对应；不含 seed）。</summary>
    public const string ProjectionId = "base-spire:PlayerView";

    private BaseSpireBuildDescription Describe(RuleSet rules, BasePublicSemantics semantics)
    {
        var pool = _content.EncounterPool;
        var limitations = new List<string>
        {
            "遭遇队列：已按原版 `ActModel.GenerateRooms` 预生成整幕队列（前 3 槽取弱池、补齐到 15 槽；"
                + "`GrabBag` 权重全 1 的下标抽取、与上一次遭遇的**标签回避**重抽、空袋按池顺序重装）；"
                + "原版在队列**之前**的前置消费也已接进同一个 `up_front` 流：共享遗物袋 118 项、"
                + "玩家遗物袋 122 项（共享池 + 角色池，按 `RelicGrabBag._rarities` 过滤）、事件列表 31 项与"
                + "共享古代人单元素洗牌，随后才是弱/普通 15 槽（弱 3 + 普通 12）与**独立的**精英 15 槽"
                + "（源 `_rooms.eliteEncounters`）、BOSS 与幕古代人各一次有界抽取。"
                + "普通与精英是**两个独立队列**、各有自己的 per-type 访问计数"
                + "（源 `RoomSet.NextNormalEncounter` / `NextEliteEncounter`），精英槽不进入普通队列。"
                + "队列及其前置消费的输入池都是 `RunSourcePoolManifest` 的**源身份全池**"
                + "（`Overgrowth.GenerateAllEncounters` 顺序，含未支持的遭遇），不是按可执行性缩过的子集。"
                + "仍有的差别：① 未支持遭遇保留真实 ID 与源顺序，但敌人/编队为空 ⇒ 只有其**执行**被阻塞，"
                + "并逐条列在 `ExcludedEncounters`；② 重抽循环按 256 次上限实现（源码无上限），到上限会显式失败；"
                + "③ 首次运行（`NumberOfRuns == 0`）的固定房间顺序未建模。"
                + "遭遇**编队**已接入（均匀抽编队 + `uniqueChoiceGroup` 组块顺序 + `chooseRandomly` 槽位、"
                + "按名单顺序出生、遭遇派生流）；权重不一致的编队构建期显式拒绝（源是 `NextItem` 均匀抽取）。",
            "房间流程：默认 dev 是按遭遇池序号推进的普通战斗房间；精英槽在独立队列里预生成，但默认 scope 没有"
                + "地图去消费它。地图物化与营火/宝箱/精英/BOSS 的房间选择只在 dev-map scope 下存在。",
            "地图：原版 `act_{n}_map` 命名流生成 7 列地图与房间类型；当前范围按遭遇池长度推进，"
                + "不使用地图（未覆盖范围已在描述里列出）。",
            "敌人出生生命按原版 `Niche` 流在声明范围内掷点（双端包含、排除同侧已用值、每次出生恰好一次抽取）；"
                + "整幕前置消费（遗物袋/共享古代人/事件列表洗牌，再到队列、精英队列与 BOSS/幕古代人）现已按源顺序"
                + "在同一个 `up_front` 流上执行，因此敌人的出生与意图在**流位置上**与原版对齐；"
                + "未支持遭遇仍然只有身份、其出生无法执行（见上一条）。",
            "敌人 `startingPowers`（出生期挂能力）已接入，且受控切片/固定遭遇/正式池三条出生路径共用同一实现；"
                + "已知范围：`skipFirstWhenAppliedToEnemy` 只被识别、「跳过第一次衰减」仍由**施加点**决定，"
                + "因此**出生自带**的 tick 型能力会在敌人第一个回合末就递减一次（`SliceConstrictChecks` 有对应检查）。",
            "获得格挡的修正阶段已接入**加性 + 乘性**两段，牌与怪物行动两条路径共用同一实现："
                + "`role = Dexterity` 的能力按层数**先加**（每层 +1，`DexterityPower.ModifyBlockAdditive`），"
                + "再乘声明的能力乘数——`blockGainMultiplier = 0.75`（Frail，无次数上限）与 "
                + "`blockGainModifiers`（Unmovable：本回合前 `层数 × priorEntriesPerStack` 次 ×2）——"
                + "全程定点、只在最后截断一次。已知范围：原版按**出牌**排除（同一次出牌里的多次获得互不计入），"
                + "切片按**每次获得**计数；当前内容没有「一次出牌里多次获得格挡」的形状，其它修正阶段"
                + "（更多 `modifier(...)` 形态）仍未接入。",
            "敌人行动已是**状态机**（权重桶 / 一次性 / 连续上限 / 冷却窗口 / 有序条件分支 / 图转移，"
                + "每次落到随机分支恰好一次 `monster_ai` 抽取，首动不掷）。**加权选取已按原版 float32 语义**"
                + "（ENV-T01：一次 `SampleInt(monster_ai, 0, 2^53)` 取 53 位，取代原先的整数有界抽取；"
                + "纯计算资产 `base-spire:SourceWeightedPick`（后端 `base-spire-source-float32@1`）按源"
                + "`RandomBranchState.cs:115-128` 做 `Sum` 的 double 累加器 → `NextFloat(max)` 的 double 乘法后"
                + "一次转 float32 → 按声明顺序逐项 float32 减法、`<= 0f` 命中；命中不下时与原版一样显式失败）。"
                + "作者文本的候选权重因此可以是小数，`enemies.spire` 里两份带分数权重的声明按源写成"
                + "**Fogmog `0.4`/`0.6`**（`sts2/…/Monsters/Fogmog.cs:46-47`）与 **Two-Tailed Rat 的 `0.083333336`"
                + "（= `1f/12f`）与 `0.75`**（`TwoTailedRat.cs:125-128`）；门槛被挡住的候选按原版置 0 权重、"
                + "不改变命中项。**证据边界（不计为已加载内容）**：分数权重的判别检查"
                + "（`SliceEnemyStateMachineChecks`）用的是受控敌人文本；上面这两条**真实内容目前都未加载**"
                + "——Fogmog 的行动用 `summonEnemy`，Two-Tailed Rat 另外还用敌人 `variables`/`conditions` 声明表、"
                + "`modifyEnemyVariable`、`modifyCombatState` 与具名 `condition(SummonAvailable)`，这些原语尚未实现，"
                + "两者因此逐条记在未支持内容里、其遭遇记在排除列表（不静默省略）。仍有的差异："
                + "① `maxUsesPerCombat > 1` 未支持（原版只有 UseOnlyOnce 与连续上限）；"
                + "② 具名条件 `condition(Name)`、`metric(LivingAllies/EnemyCount/…)`、`power(...)`、`allyAction(...)` "
                + "与 `escape`/`summonEnemy` 一类行动效果未支持（逐条记为未支持内容；`addCards` **已实现**，"
                + "见 `SliceEnemyAddCardsChecks`，不再列为缺口）；"
                + "③ 依赖**遭遇位次**的开场条件（例如 Nibbit 的 `_isAlone`/`IsFront`）尚未建模，"
                + "该敌人的开场按 `cycle` 的第一项（= 单独出现时的原版行为）执行。",
            "奖励仍是切片占位（每间房一次长期牌升级）：真实战斗奖励/选牌/跳过/容量未接入（BASE-02）。",
            "战斗结算写回：**只有一个写回点**（`CommitPermanent`），把玩家战斗副本的 `Hp`/`MaxHp` 整体写回整局；"
                + "`Run.Gold` 在战斗内没有来源，因此不参与写回；牌面/牌组变化只由奖励升级写长期牌组。结算按房间幂等"
                + "（`SettledRoom`），换房时 `StartNextRoom` 会用整局值重建战斗副本。"
                + "ENV-T02：胜利判定挂在**动作体末尾**（`BattleEndTail`：`PlayCard` / `EndTurn` / `UsePotion` 各一处），"
                + "不在伤害步骤里、也不在效果步骤表里，因此最后一次击杀之后的父效果余效（Feed 的 "
                + "`on_fatal_gain_max_hp`、遗物回血）与出牌收尾（`after_card_played` 钩子、离开 `InPlay`、"
                + "`consumeOnPlay` 消耗、结果摆放）一定会先跑完再结算写回；"
                + "长期牌组与战斗副本按**实例**建立来源（`Card.LongTermSource`，同定义的不同实例各自独立）。",
            "遗物：只有起始遗物 Burning Blood 的效果已接入（战斗胜利回 6 血，截断到最大生命）；"
                + "其余遗物只有身份、没有效果。药水初始栏为空；正式池48项元数据均可生成/领取/丢弃，11项使用程序已接入，其余具名未支持。",
            "胜利条件：正式构建不按房间计数发 Victory；完成最终 BOSS 后的终局事件未接入，"
                + "因此当前范围是打完池里的战斗房间后停在 Running（公开动作合法性可核对）。",
        };

        if (ActMapEnabled)
        {
            // T05：上面三条"未接入"的限制在 dev-map 下不再准确。**按前缀替换**成地图 scope 的真实状态；
            // 默认 scope 的文本逐字节不变（它进描述摘要与公开构建 id，属于范围身份的一部分）。
            void Replace(string prefix, string text)
            {
                var index = limitations.FindIndex(entry => entry.StartsWith(prefix, StringComparison.Ordinal));
                if (index < 0)
                {
                    throw new InvalidOperationException($"构建描述里缺少以 '{prefix}' 开头的限制条目");
                }

                limitations[index] = text;
            }

            Replace("房间流程：",
                "房间流程：地图物化、普通战斗、精英房与已加载的Boss遭遇已接线（`ChooseMapPoint` 沿 `MapChildren` 选择，"
                + "普通战斗消费普通遭遇队列、精英房消费独立精英队列、Boss使用独立遭遇）；营火Rest/Smith与真实宝箱候选/开箱/领取或跳过已接入，"
                + "商店已接入（购买/删牌/离店）；**问号房（Unknown）已接入**：按源 `UnknownMapPointOdds` 判定实际房型"
                + "（Monster/Elite/Treasure/Shop/Event，赔率命中复位、未命中按源累加、Shop 黑名单按源求值），"
                + "地图节点类型保持 Unknown、实际房型另存 `Run.ActualRoomType`，判出的战斗/宝箱/商店复用同一批房间入口与守卫；"
                + "判定为事件时按源 `PullNextEvent` 消费 Start 预生成的源事件队列（资格 + 已访问跳过 + 访问计数），"
                + "进入该事件的独立模块。幕转换仍未接入（精英房战利品差异未建模，按普通战斗同形结算）。"
                + "选中未实现房型或未实现事件身份时具名失败，节点保留、整局状态不变，不伪装成空事件。"
                + "完整Ironclad身份仍须全房型/全事件/多幕及独立对拍。");
            Replace("地图：",
                "地图：原版 `act_{n}_map` 命名流已接入（幕 1 的引擎流名 `base-spire:run:act_1_map`，"
                + "种子 = run seed + hash(`act_1_map`)）。Start 时按该流抽 53 位 payload（每个 payload 恰好"
                + "一次 `SampleInt`，抽到重放完毕为止、不补齐随机数），词表交给纯计算资产 "
                + "`base-spire:SourceActMapGenerate` 逐字重放移植件 `SourceActMapGenerator`"
                + "（7 列、房型计数来自 `NextGaussianInt(7,1,6,7)` + `StandardRandomUnknownCount`），"
                + "物化成 `MapNodes`（列优先顺序、`Index` 为物化下标）与 `MapChildren` 关系。"
                + "已知范围：只做幕1（不含幕间事件与幕转换），已接普通战斗、精英房与独立Boss遭遇。");
            Replace("奖励仍是切片占位",
                "奖励：地图scope胜利后进入 `Run.MapPhase = 3`，提供领取金币、三选一卡牌或跳过卡牌，"
                + "领取卡牌加入长期牌组。药水掉落后按完整48项池真实生成，满栏保留奖励，可先丢弃后领取或跳过；使用支持由实例字段明确声明。"
                + "金币、卡牌及掉落药水全部解决后才关闭奖励，不能提前旅行。");
            Replace("胜利条件：",
                "胜利条件：地图scope的Boss战斗结算先打开奖励，全部奖励解决后写 `Run.ActCompleted = true` 与相位6；"
                + "普通房间仅返回选图相位。幕完成保持Running；幕转换尚未接线，不能继续，也不代表整局Victory；"
                + "正常开局轨迹的证据按本构建单独记录，不以身份声明替代验收。");
            Replace("遗物：", "遗物：BurningBlood胜利回血与Pear/Mango/OldCoin获得效果已接入；"
                + "完整共享宝箱身份袋不等于完整遗物行为，其他候选领取具名失败，合法跳过保留；药水领取、固定槽位、显式丢弃及已支持的使用效果已接入。");
            Replace("遭遇队列：", "遭遇队列：dev-map与默认scope共用同一条整幕初始化，前置消费（共享/玩家遗物袋、"
                + "共享古代人、事件列表）、弱/普通队列、精英队列与BOSS/幕古代人都按源顺序在同一 `up_front` 流上执行；"
                + "普通队列与精英队列是**两个独立集合**、各有自己的 per-type 访问计数（源 `RoomSet`），"
                + "精英槽不进入普通队列；"
                + "输入池是源身份全池（含未支持遭遇，保留真实ID与源顺序，其执行被阻塞）；"
                + "Boss池按源补足 3 个身份，不再只有 Vantom。普通遭遇队列与地图楼层分开计数；"
                + "仍有的差别：重抽上限256、未建模首发固定房间顺序。");
            limitations.Add(
                "地图相位机（T05）：`Run.MapPhase` 0 = 开局选图、1 = 战斗进行中、2 = 奖励关闭后的选图、"
                + "3 = 奖励开放、4 = 营火、5 = 宝箱、6 = 幕完成、7 = 商店、8 = 事件（均不得旅行）；"
                + "`Run.CurrentNode` 指向当前节点，`Run.ActualRoomType` 是本次访问判定的**实际房型**"
                + "（问号节点类型保持 `Unknown`，营火/商店/宝箱/战斗守卫按实际房型适用），"
                + "`Run.MapNodeCount`/`Run.MapDraws` 记录地图物化与随机词表。地图上限来自地图楼层，"
                + "不与普通战斗队列计数混用；Boss独立派发；"
                + "营火Rest按最大生命30%截断并封顶，Smith选一张可升级长期牌并保留实例成长；"
                + "成功选项后返回选图，未确认Smith不消费选项。营火不生成战斗或奖励；"
                + "宝箱共享袋118身份按源分组顺序洗牌，稀有度走独立TreasureRoomRelics流，开箱金币42..52走Rewards流；不按已实现效果缩池。"
                + "事件按源 `unknown_map_point` 流判定、按源事件队列消费，`Run.CurrentEventId` 是当前事件身份，"
                + "`Run.EventsVisited` 是源访问计数，已访问身份存在 `RunVisitedEvents`（不发布）。"
                + "换房建场与结算仍使用 `SettledRoom == CurrentRoom`；地图不另存战斗状态。");
            if (_actPlan is not null)
            {
                Replace("遭遇队列：", "遭遇队列：dev-run显式使用默认Overgrowth→Hive→Glory。开局先洗共享古代人并分配各幕子集，再逐幕生成事件、普通/精英队列、Boss与古代人；身份全池不按可执行性过滤。切幕只启用预生成库存，不重洗遗物袋或重复抽取UpFront。重抽保护仍为256次；未支持出生行为/编队选中明确失败。");
                Replace("地图：", "地图：dev-run各幕使用各自act_1_map/act_2_map/act_3_map流、15/14/13个普通房间长度及各幕源房型分布。切幕销毁旧地图并物化新节点，TotalFloor累计已访问节点；后续幕先选古代人起始点，其效果尚未实现时具名失败，不能直接跳到普通房间。");
                Replace("房间流程：", "房间流程：dev-run在Boss全部奖励关闭后允许ContinueToNextAct，保留HP/金币/永久牌/遗物/药水真实实例与全局战斗结算键，清理旧战斗和访问/问号赔率状态。其他房间/事件效果仍为已实现子集。");
                Replace("胜利条件：", "胜利条件：默认三幕的Boss奖励关闭只标记幕完成，首两次ContinueToNextAct真实切幕，第三次明确停在未实现的TheArchitect最终事件；不提前发布整局Victory，不等于完整Ironclad四幕验收。后续幕古代人、敌人/事件和完整hook仍有明确缺口。");
            }
        }

        return new BaseSpireBuildDescription(
            _content.Model.PackageId,
            Configuration.ScopeId,
            Configuration.ScopeId,
            Configuration.SourceBaselineId,
            Character.CharacterId,
            Character.Name,
            Configuration.Ascension,
            Configuration.Unlocks.ToString(),
            Configuration.Mode.ToString(),
            VanillaRandomLayout.AlgorithmId,
            VanillaRandomLayout.DrawProfileId,
            RulesetDigest(rules),
            _content.CardIds.Count,
            _content.PowerIds.Count,
            _content.EnemyIds.Count,
            Character.Deck.Count,
            Character.MaxHp,
            Character.Gold,
            Character.Relics,
            _content.WeakEncounters.Count,
            _content.NormalEncounters.Count,
            pool.RoomCount,
            [.. pool.Encounters.Select(encounter => encounter.EncounterId).Order(StringComparer.Ordinal)],
            [.. new RunRandomLayout(Configuration.RandomLayoutId).Streams(string.Empty)
                .Select(entry => entry.Stream)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            _content.ExcludedEncounters,
            _content.Unsupported,
            limitations,
            EncounterContent.Digest(EncounterContent.ReadText(EncounterContent.EncountersResource)),
            EncounterContent.Digest(EncounterContent.ReadText(EncounterContent.ActsResource)),
            _content.ContentDigest,
            _content.SourceDigests,
            BaseSpireSourceBaselines.Current.EvidencePaths,
            StreamLayoutDigest,
            semantics.Digest,
            PublicBuildId(Configuration.ScopeId, RulesetDigest(rules), StreamLayoutDigest, _content.ContentDigest,
                semantics.Digest)) { RandomLayoutId = Configuration.RandomLayoutId, ContentMetadataVersion = Configuration.ContentMetadataVersion };
    }

    /// <summary>
    /// 随机**布局**身份：流名映射（原版流类型 → 引擎流名）+ 算法/映射 id 的摘要。
    /// 布局变了（例如把 `Shuffle` 映射到别的流名）即使规则集没变也必须换身份，否则同 seed 行为不同却同名。
    /// </summary>
    private string StreamLayoutDigest => Digest(string.Join("\n",
        new[]
        {
            $"algorithm={VanillaRandomLayout.AlgorithmId}",
            $"draw={VanillaRandomLayout.DrawProfileId}",
        }
        .Concat(Configuration.RandomLayoutId == RunRandomLayout.Frozen ? Array.Empty<string>()
            : new[] { $"seed-layout={Configuration.RandomLayoutId}" })
        .Concat(VanillaRandomLayout.RunStreamTypes.Select(type =>
            $"run:{type}->{new RunRandomLayout(Configuration.RandomLayoutId).EngineStreamName(type)}"))
        .Concat(VanillaRandomLayout.PlayerStreamTypes.Select(type =>
            $"player:{type}->{VanillaRandomLayout.EnginePlayerStreamName(type)}"))));

    /// <summary>
    /// 公开构建身份：所有会改变行为/公开事实的维度合成一个 id。范围、源基线、规则集摘要、内容摘要、
    /// 随机 Profile 与流布局、**公开语义元数据**任一变化都会换 id；每局 seed **不在这里**（它是会话证据）。
    /// </summary>
    private string PublicBuildId(string scopeId, string rulesetDigest, string streamLayoutDigest,
        string contentDigest, string semanticsDigest) =>
        Digest(string.Join("\n",
            $"scope={scopeId}",
            $"source={BaseSpireSourceBaselines.Current.Id}",
            $"ruleset={rulesetDigest}",
            $"content={contentDigest}",
            $"layout={streamLayoutDigest}",
            $"semantics={semanticsDigest}")
            + (Configuration.ContentMetadataVersion == SinglePlayerContentVersions.Frozen ? string.Empty
                : $"\ncontent-metadata={Configuration.ContentMetadataVersion}"));

    private static string Digest(string text) => BasePublicSemanticsBuilder.Digest(text);

    /// <summary>
    /// 公开静态规则身份：对 Engine 的 `RuleSet.Identity` 取摘要。它含内容/链接/编译/数值/随机 Profile
    /// 与范围身份，但**不含**每局 seed（seed 只进 `Session.Identity`）。取摘要是因为身份文本很长，
    /// 而"同一构建同一个 id"才是公开契约；完整文本由证据侧保存。
    /// </summary>
    private static string RulesetDigest(RuleSet rules) => Digest(rules.Identity);
}
