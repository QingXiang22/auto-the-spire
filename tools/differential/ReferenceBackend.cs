#if REFERENCE_SIM
using System.Globalization;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

/// <summary>
/// 参考侧（<c>reference/sts2-sim</c>，独立对拍后端）适配器入口。
///
/// 它的 ProjectReference 是**条件引用**：本地没有冻结参考目录时整个解决方案仍能构建，
/// 此时 <see cref="Available"/> 为 <c>false</c>，`compare` 明确报 <c>unsupported_reference_adapter</c>，
/// 而不是靠反射/包装参考执行冒充接入（ENV §4）。
/// </summary>
internal static class ReferenceBackends
{
#if REFERENCE_SIM
    // 故意用 static readonly 而不是 const：const 会被编译器折叠，导致"参考不可用"那条分支变成
    // 不可达代码（CS0162），而不是真正的编译期开关。
    public static readonly bool Available = true;

    public static CaseRecord Run(FixtureCase fixture) => ReferenceBackend.Run(fixture);
#else
    public static readonly bool Available = false;

    public static CaseRecord Run(FixtureCase fixture) =>
        throw new InvalidOperationException("本机构建未包含参考侧适配器（未找到 reference/sts2-sim）");
#endif
}

#if REFERENCE_SIM
/// <summary>
/// **参考后端适配器**：用同一份声明式夹具驱动 <c>Sts2Sim.Core</c>，产出与 base 侧同构的记录。
///
/// 两个后端的调度完全不是一个形状（我们把目标当决策点、参考把目标当参数；参考的选牌走决策源），
/// 所以这里做的是**规范化映射**而不是逐函数对照：
/// <list type="bullet">
/// <item>目标选择：用参考自己的合法性 API <c>CombatTargetCandidates.ForCard</c> 表达同一个逻辑决策点；</item>
/// <item>选牌：安装受控的 <c>ICardSelectionDecisionSource</c>，在被问到时记录决策点并返回夹具指定的牌；</item>
/// <item>事实：只产出与 base 侧**同一套路径**的规范化事实（对象图不同，不做全状态比较）。</item>
/// </list>
/// </summary>
internal static class ReferenceBackend
{
    public const string Name = "reference:sts2-sim";
    public const string Version = "env-00-compare@1";

    /// <summary>
    /// 敌人顺序照抄受控切片的声明（<c>SliceManifest.Enemies</c>：前排 CalcifiedCultist、后排 DampCultist，
    /// 来源 <c>sts2/src/Core/Models/Encounters/CultistsNormal.cs</c>）。参考侧必须点同一对敌人，否则不可比。
    /// </summary>
    private static readonly string[] EnemyOrder =
        ["base-spire:calcified-cultist", "base-spire:damp-cultist"];

    private static readonly string[] PileKeys = ["draw", "hand", "discard", "exhaust"];

    /// <summary>
    /// 按 case 种类分派：<c>kind=run</c> 走**运行层入口**（<see cref="ReferenceRunEntry"/>，自己的建局与首次洗牌），
    /// <c>kind=map</c> 走**独立地图生成入口**（<see cref="ReferenceMapEntry"/>，只生成被比较的那一张地图），
    /// 其余走受控单场适配器。三条路径都不复制 base 结果。
    /// </summary>
    public static CaseRecord Run(FixtureCase fixture) => fixture.Kind == CaseKinds.NormalReward
        ? NormalRewardEntry.RunReference(fixture)
        : fixture.Kind == CaseKinds.Run
        ? ReferenceRunEntry.Run(fixture)
        : fixture.Kind == CaseKinds.Map
            ? ReferenceMapEntry.Run(fixture)
            : RunControlled(fixture);

    private static CaseRecord RunControlled(FixtureCase fixture)
    {
        var notes = new List<string>
        {
            "随机读数：RngDiagnostics 逐次观察所有推进 Counter 的原生调用；初始消费与脚本消费分列。",
            "decisions: 目标决策用参考自己的合法性 API 表达；Kind/Prompt 不参与比较。",
        };

        using var random = new ReferenceRandomProbe();
        List<BoundaryRecord> boundaries = [];
        CombatState combat;
        Player player;
        CombatEngine engine;
        ScriptedSelection selection;
        try
        {
            (combat, player, engine, selection, var alignment) = Build(fixture, random);
            if (alignment is not null)
            {
                notes.Add(alignment);
            }
        }
        catch (Exception failure)
        {
            return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, "invalid_fixture",
                $"{failure.GetType().Name}: {failure.Message}", false, notes, boundaries);
        }

        var exactPileOrder = fixture.RngProfile == RngProfiles.VanillaCompatible || fixture.Setup.InitialPiles is not null;
        var before = random.Count;
        var beforeStreams = random.Streams();
        Add(boundaries, "start", "受控初始状态", combat, player, [], exactPileOrder, random);

        var status = "ok";
        string? detail = null;
        for (var index = 0; index < fixture.Script.Count; index++)
        {
            var step = fixture.Script[index];
            List<DecisionSnapshot> decisions = [];
            try
            {
                selection.Reset(step.Choose);
                Execute(combat, player, engine, selection, step, decisions);
            }
            catch (Exception failure)
            {
                status = failure is CheckFailed ? "invalid_action" : "faulted";
                detail = $"第 {index} 步 {step.Action}: {failure.GetType().Name}: {failure.Message}";
                Add(boundaries, $"abort:{index}", detail, combat, player, decisions, exactPileOrder, random);
                break;
            }

            Add(boundaries, $"step:{index}:{step.Action}", DescribeStep(step), combat, player, decisions,
                exactPileOrder, random, step.ObserveDrawTop);
        }

        return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, status, detail, random.Count != before, notes, boundaries)
        {
            InitialState = fixture.Setup.InitialPiles is null ? null : new InitialStateProof(fixture.Setup, true),
            ReferenceIdentity = fixture.Setup.InitialPiles is null ? null : ReferenceRunEntry.Identity(fixture.GameSeed!, RngSourceCompat.RunStreams(fixture.GameSeed!)),
            InitializationRandomCount = before,
            ScriptRandom = new RandomMeasurement("native-counter-increments", "RngDiagnostics all Rng raw calls (run/player/monster/dynamic)", before, random.Count, beforeStreams, random.Streams()),
            ReferenceStreams = random.Evidence(),
        };
    }

    // -- 构造与受控初始状态 ---------------------------------------------------------------------------------

    private static (CombatState Combat, Player Player, CombatEngine Engine, ScriptedSelection Selection,
        string? Alignment) Build(FixtureCase fixture, ReferenceRandomProbe random)
    {
        var setup = fixture.Setup;
        if (setup.InitialPiles is not null) ControlledInit.Validate(setup);
        CombatReplayDemo.EnsureModelsRegistered();

        var run = new RunState(setup.InitialPiles is null ? setup.Seed.ToString(CultureInfo.InvariantCulture) : fixture.GameSeed!, new Overgrowth());
        if (setup.InitialPiles is not null) RngSourceCompat.Apply(run.Rng, fixture.GameSeed!);
        var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), run);
        run.AddPlayer(player);

        // 从这里开始 run/player 的具名流派生已经确定：登记它们并把逐边界比较基线对齐到当前计数器。
        random.Attach(run.Rng, player);

        var combat = new CombatState(run);
        var selection = new ScriptedSelection(combat, player);
        combat.CardSelectionSource = selection;

        foreach (var enemyId in EnemyOrder)
        {
            var model = (MonsterModel)FindModel<MonsterModel>(enemyId).MutableClone();
            combat.AddMonster(model, CombatSide.Enemy);
        }

        var engine = new CombatEngine(combat);
        engine.StartCombatAsync().GetAwaiter().GetResult();

        Apply(combat, player, setup);
        string? alignment = null;
        if (setup.InitialPiles is not null)
        {
            ApplyOrdered(combat, player, setup.InitialPiles);

            // 受控注入之后把参考的开局洗牌偏移还回去：base 的受控切片按声明顺序发牌、不消费 Shuffle 流，
            // 参考的 StartCombatAsync 必然先抽一次开局洗牌（9 次/10 张）。不还回去，两侧重洗前的随机
            // 游标就不同，重洗比较会在纯工具侧偏移上分叉（见 RngSourceCompat.RestoreStream）。
            var restored = random.Restore("Shuffle", fixture.GameSeed!, RngSourceCompat.RestoreStream);
            alignment = $"受控状态注入后的工具侧随机游标对齐：参考 Shuffle 流（源流 'Shuffle' → "
                + $"引擎流 '{RngSourceCompat.EngineStreamName("Shuffle")}'）游标 {restored.Before} → "
                + $"{restored.After}（按 {RngSourceCompat.ProfileId} 重新装回源版本种子）；"
                + $"丢弃的是参考自身开局洗牌留下的偏移，base 受控切片消费 0；只对齐这一条流，"
                + "不改 seed/维度/Profile/牌区声明。";
        }

        // 建局（含参考自己的开局洗牌/起手与工具侧对齐）到此结束：这一份读数与脚本边界分开保存。
        random.MarkInitialization();
        return (combat, player, engine, selection, alignment);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CombatState, Dictionary<CardModel, string>> InitSlots = new();
    private static void ApplyOrdered(CombatState combat, Player player, InitialPiles init)
    {
        var state = player.PlayerCombatState!;
        var piles = new[] { state.Hand, state.DrawPile, state.DiscardPile, state.ExhaustPile };
        var declarations = new[] { init.Hand!, init.Draw!, init.Discard!, init.Exhaust! };
        var available = piles.SelectMany(p => p.Cards).ToList();
        var slots = new Dictionary<CardModel, string>();
        foreach (var pile in piles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card);
        for (var p = 0; p < piles.Length; p++)
            foreach (var declared in declarations[p])
            {
                var card = available.FirstOrDefault(c => Project(c.Id) == declared.Card && c.IsUpgraded == declared.Upgraded)
                    ?? (declared.Upgraded ? available.FirstOrDefault(c => Project(c.Id) == declared.Card && !c.IsUpgraded) : null)
                    ?? throw new CheckFailed("独立参考来源牌缺失: " + declared.Card);
                available.Remove(card);
                if (declared.Upgraded && !card.IsUpgraded) card.Upgrade();
                piles[p].AddInternal(card);
                slots.Add(card, declared.Slot);
            }
        if (available.Count != 0) throw new CheckFailed("独立参考来源有未声明牌");
        InitSlots.Add(combat, slots);
    }

    private static void Apply(CombatState combat, Player player, FixtureSetup setup)
    {
        var state = player.PlayerCombatState
            ?? throw new CheckFailed("StartCombatAsync 之后玩家仍没有战斗状态");

        // 起手全部退回抽牌堆：受控手牌只由夹具声明决定。
        foreach (var card in state.Hand.Cards.ToList())
        {
            state.Hand.RemoveInternal(card);
            state.DrawPile.AddInternal(card);
        }

        foreach (var declared in setup.Hand ?? [])
        {
            Place(player, state.DrawPile, state.Hand, declared);
        }

        foreach (var declared in setup.Discard ?? [])
        {
            Place(player, state.DrawPile, state.DiscardPile, declared);
        }

        if (setup.PlayerHp is { } hp)
        {
            player.Creature.SetMaxHpInternal(hp);
            player.Creature.SetCurrentHpInternal(hp);
        }

        if (setup.Energy is { } energy)
        {
            state.Energy = energy;
        }

        if (setup.EnemyHp is { } enemyHp)
        {
            var enemies = combat.Enemies;
            if (enemyHp.Count != enemies.Count)
            {
                throw new CheckFailed(
                    $"enemy_hp 给了 {enemyHp.Count} 个数值，但参考场景里有 {enemies.Count} 个敌人——受控状态必须逐个指定");
            }

            for (var index = 0; index < enemies.Count; index++)
            {
                enemies[index].SetMaxHpInternal(enemyHp[index]);
                enemies[index].SetCurrentHpInternal(enemyHp[index]);
            }
        }

        foreach (var declared in setup.Powers ?? [])
        {
            var owner = ResolveCreature(combat, player, declared.Owner);
            var power = (PowerModel)FindModel<PowerModel>(declared.Power).MutableClone();
            power.ApplyInternal(owner, declared.Stacks);
        }
    }

    /// <summary>
    /// 把夹具声明的一张牌放进目标牌区。牌不在起始牌组里时**造一张并先放进抽牌堆**——
    /// base 侧靠 <c>includeHeadbutt</c> 把该牌加进牌组，两侧的牌区构成必须一致才谈得上比较。
    /// </summary>
    private static void Place(Player player, CardPile from, CardPile to, FixtureCard declared)
    {
        var card = from.Cards.FirstOrDefault(candidate => Project(candidate.Id) == declared.Card);
        if (card is null)
        {
            card = (CardModel)FindModel<CardModel>(declared.Card).MutableClone();
            card.AssignOwner(player);
            from.AddInternal(card);
        }

        from.RemoveInternal(card);
        to.AddInternal(card);

        if (declared.Upgraded && !card.IsUpgraded)
        {
            card.Upgrade();
        }
    }

    // -- 脚本执行 -------------------------------------------------------------------------------------------

    private static void Execute(CombatState combat, Player player, CombatEngine engine,
        ScriptedSelection selection, FixtureStep step, List<DecisionSnapshot> decisions)
    {
        var state = player.PlayerCombatState
            ?? throw new CheckFailed("玩家没有战斗状态");

        switch (step.Action)
        {
            case "play_card":
            {
                var card = state.Hand.Cards.FirstOrDefault(candidate => Project(candidate.Id) == step.Card)
                    ?? throw new CheckFailed($"手牌里没有 '{step.Card}'");

                // 参考把目标当参数，但"选哪个目标"是同一个逻辑决策点：用参考自己的合法性 API 表达。
                var legal = CombatTargetCandidates.ForCard(combat, player, card.TargetType);
                Creature? target = null;
                if (legal.Count > 0)
                {
                    var token = step.Target ?? "first";
                    target = token == "first" ? legal[0] : ResolveCreature(combat, player, token);
                    if (!legal.Contains(target))
                    {
                        throw new CheckFailed($"候选 '{token}' 不在该牌的合法目标里");
                    }

                    decisions.Add(new DecisionSnapshot(
                        "Target",
                        string.Empty,
                        [.. legal.Select(candidate => SlotOf(combat, player, candidate))],
                        1,
                        1,
                        SlotOf(combat, player, target)));
                }

                engine.PlayCardAsync(player, card, target).GetAwaiter().GetResult();
                decisions.AddRange(selection.TakeDecisions());
                break;
            }

            case "end_turn":
                engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
                break;

            default:
                throw new CheckFailed($"未知动作 '{step.Action}'");
        }
    }

    // -- 规范化事实 -----------------------------------------------------------------------------------------

    private static IReadOnlyList<FactSnapshot> OrderedFacts(CombatState combat, Player player, IReadOnlyList<FactSnapshot> facts)
    {
        if (!InitSlots.TryGetValue(combat, out var slots)) return facts;
        var state = player.PlayerCombatState!;
        var piles = new[] { state.Hand, state.DrawPile, state.DiscardPile, state.ExhaustPile };
        var keys = new[] { "hand", "draw", "discard", "exhaust" };
        return Records.Facts(facts.Concat(piles.Select((pile, i) => new FactSnapshot("pile." + keys[i] + ".initial-slots", string.Join(",", pile.Cards.Select(c => slots.GetValueOrDefault(c, "unknown")))))));
    }

    private static void Add(List<BoundaryRecord> boundaries, string kind, string label,
        CombatState combat, Player player, IReadOnlyList<DecisionSnapshot> decisions, bool exactPileOrder,
        ReferenceRandomProbe random, bool observeDrawTop = false)
    {
        var index = boundaries.Count;
        boundaries.Add(new BoundaryRecord(
            index,
            kind,
            label,
            LegalActions(combat, player),
            decisions,
            OrderedFacts(combat, player, Facts(combat, player, exactPileOrder, observeDrawTop)),
            random.AtBoundary(index, kind),
            null));
    }

    /// <summary>
    /// 参考侧的合法动作：参考没有"合法动作集合"这个 API，它用 <c>ValidatePlayCard</c> 逐个校验。
    /// 这里按同一套语义（手牌 × 可打性 × 合法目标）枚举，只产出与 base 侧同名的动作与候选。
    ///
    /// <para>
    /// **终局投影（ENV-T04 修正）**：战斗已结束/正在结束时参考侧仍然会列出 <c>EndTurn</c>（它只按手牌枚举），
    /// 而 base 侧会话已 <c>Terminal</c>、不再报任何合法动作。两边说的是同一个逻辑边界（终局后没有外部动作），
    /// 不投影就会把"终局后无动作"误报成规则差异，并挡住后面真正要看的牌区/资源事实。这里用参考**自己的**
    /// <c>CombatState.IsOverOrEnding()</c>（<c>CombatEngine.IsOverOrEnding</c>，与 <c>CheckWinCondition</c> 同判据、
    /// 只读不改状态）表达这条边界，**不是**复制 base 侧结果。
    /// </para>
    /// </summary>
    internal static IReadOnlyList<ActionOption> LegalActions(CombatState combat, Player player)
    {
        var state = player.PlayerCombatState
            ?? throw new CheckFailed("玩家没有战斗状态");
        if (combat.IsOverOrEnding())
        {
            return [];
        }

        var playable = state.Hand.Cards
            .Where(card => card.CanPlay(out _))
            .Select(card => SlotOf(combat, player, card))
            .ToList();
        var actions = new List<ActionOption> { new("base-spire:EndTurn", [], 1) };
        if (playable.Count > 0)
        {
            // base 侧只在**存在可绑定参数组合**时才报出该动作，因此手牌为空时不报 PlayCard。
            actions.Insert(0, new ActionOption("base-spire:PlayCard", playable, playable.Count));
        }

        return actions;
    }

    internal static IReadOnlyList<FactSnapshot> Facts(CombatState combat, Player player, bool exactPileOrder,
        bool observeDrawTop = false)
    {
        var state = player.PlayerCombatState
            ?? throw new CheckFailed("玩家没有战斗状态");
        var facts = new List<FactSnapshot>
        {
            new("player.hp", Number(player.Creature.CurrentHp)),
            new("player.max_hp", Number(player.Creature.MaxHp)),
            new("player.block", Number(player.Creature.Block)),
            new("player.energy", Number(state.Energy)),
        };

        var enemies = combat.Enemies;
        var enemyKeys = EngineSnapshot.EnemyKeys(enemies.Select(enemy => Project(enemy.Monster?.Id)));
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            var prefix = $"enemy.{enemyKeys[index]}";
            facts.Add(new FactSnapshot($"{prefix}.hp", Number(enemy.CurrentHp)));
            facts.Add(new FactSnapshot($"{prefix}.max_hp", Number(enemy.MaxHp)));
            facts.Add(new FactSnapshot($"{prefix}.block", Number(enemy.Block)));
            facts.Add(new FactSnapshot($"{prefix}.alive", enemy.IsAlive ? "true" : "false"));
        }

        var piles = new Dictionary<string, CardPile>(StringComparer.Ordinal)
        {
            ["draw"] = state.DrawPile,
            ["hand"] = state.Hand,
            ["discard"] = state.DiscardPile,
            ["exhaust"] = state.ExhaustPile,
        };
        foreach (var key in PileKeys)
        {
            var cards = piles[key].Cards;
            facts.Add(new FactSnapshot($"pile.{key}.size", Number(cards.Count)));
            var keys = cards.Select(CardKey).ToList();
            facts.Add(new FactSnapshot($"pile.{key}.sorted", string.Join(",", keys.Order(StringComparer.Ordinal))));
            if (observeDrawTop && key == "draw")
            {
                facts.Add(new FactSnapshot("pile.draw.top", keys.FirstOrDefault() ?? "<empty>"));
            }
            if (exactPileOrder || key is "hand" or "exhaust")
            {
                facts.Add(new FactSnapshot($"pile.{key}.order", string.Join(",", keys)));
            }
        }

        AddPowerFacts(facts, player.Creature, "player");
        for (var index = 0; index < enemies.Count; index++)
        {
            AddPowerFacts(facts, enemies[index], $"enemy.{enemyKeys[index]}");
        }

        return Records.Facts(facts);
    }

    private static void AddPowerFacts(List<FactSnapshot> facts, Creature owner, string ownerSlot)
    {
        var powers = owner.Powers;
        facts.Add(new FactSnapshot($"power.{ownerSlot}.count", Number(powers.Count)));
        for (var index = 0; index < powers.Count; index++)
        {
            facts.Add(new FactSnapshot($"power.{ownerSlot}.{index}.id", Project(powers[index].Id)));
            facts.Add(new FactSnapshot($"power.{ownerSlot}.{index}.stacks", Number(powers[index].Amount)));
        }
    }

    private static string CardKey(CardModel card) =>
        Project(card.Id) + (card.IsUpgraded ? "+" : string.Empty);

    // -- 映射小工具 -----------------------------------------------------------------------------------------

    /// <summary>参考的 <c>ModelId</c>（<c>CARD.STRIKE_IRONCLAD</c> / <c>POWER.VULNERABLE_POWER</c>）→ 本项目内容 id。</summary>
    internal static string Project(ModelId? id)
    {
        if (id is null)
        {
            return "<none>";
        }

        var entry = id.Entry;
        // 原版的 entry 是"类型名"的 UPPER_SNAKE（VulnerablePower → VULNERABLE_POWER），
        // 而本项目的能力 id 去掉了 Power 后缀（base-spire:vulnerable）——这是规范 id 表的既定规则。
        if (entry.EndsWith("_POWER", StringComparison.Ordinal))
        {
            entry = entry[..^"_POWER".Length];
        }

        return "base-spire:" + entry.ToLowerInvariant().Replace('_', '-');
    }

    private static T FindModel<T>(string ourId) where T : AbstractModel =>
        ModelDb.All<T>().FirstOrDefault(model => Project(model.Id) == ourId)
        ?? throw new CheckFailed($"参考内容里找不到 '{ourId}'（该内容尚未迁移或命名规则不同）");

    private static string SlotOf(CombatState combat, Player player, CardModel card)
    {
        var state = player.PlayerCombatState!;
        foreach (var (pile, key) in new (CardPile Pile, string Key)[]
                 {
                     (state.DrawPile, "draw"), (state.Hand, "hand"),
                     (state.DiscardPile, "discard"), (state.ExhaustPile, "exhaust"),
                 })
        {
            var index = pile.Cards.ToList().IndexOf(card);
            if (index >= 0)
            {
                return $"card:{key}:{index}";
            }
        }

        return "card:unknown";
    }

    internal static string SlotOf(CombatState combat, Player player, Creature creature)
    {
        if (creature == player.Creature)
        {
            return "player";
        }

        var index = combat.Enemies.ToList().IndexOf(creature);
        return index >= 0 ? $"enemy:{index}" : "creature:unknown";
    }

    private static Creature ResolveCreature(CombatState combat, Player player, string token) => token switch
    {
        "player" => player.Creature,
        _ when token.StartsWith("enemy:", StringComparison.Ordinal) =>
            Index(combat.Enemies, token["enemy:".Length..]),
        _ => throw new CheckFailed($"无法解析的槽位 '{token}'"),
    };

    private static Creature Index(IReadOnlyList<Creature> enemies, string text)
    {
        var index = int.Parse(text, CultureInfo.InvariantCulture);
        if (index < 0 || index >= enemies.Count)
        {
            throw new CheckFailed($"敌人下标越界：{index}（当前 {enemies.Count} 个）");
        }

        return enemies[index];
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string DescribeStep(FixtureStep step) => step.Action switch
    {
        "play_card" => $"打出 {step.Card}" + (step.Target is { } target ? $" → {target}" : string.Empty),
        "end_turn" => "结束回合",
        _ => step.Action,
    };

    /// <summary>受控选牌源：被参考问到时记录逻辑决策点，并按夹具指定返回一张牌。</summary>
    private sealed class ScriptedSelection(CombatState combat, Player player) : ICardSelectionDecisionSource
    {
        private readonly List<DecisionSnapshot> _decisions = [];
        private string? _choose;

        public void Reset(string? choose) => _choose = choose;

        public IReadOnlyList<DecisionSnapshot> TakeDecisions()
        {
            var copy = _decisions.ToList();
            _decisions.Clear();
            return copy;
        }

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            var candidates = request.Candidates;
            _decisions.Add(new DecisionSnapshot(
                "CardSelection",
                string.Empty,
                [.. candidates.Select(card => SlotOf(combat, player, card))],
                request.MinCount,
                request.MaxCount,
                null));

            CardModel chosen;
            if (_choose is { } token && token != "first")
            {
                var index = candidates.ToList().FindIndex(card => SlotOf(combat, player, card) == token);
                if (index < 0)
                {
                    throw new CheckFailed($"选牌候选 '{token}' 不在候选集合里");
                }

                chosen = candidates[index];
            }
            else
            {
                chosen = candidates.FirstOrDefault()
                    ?? throw new CheckFailed("选牌请求没有任何候选");
            }

            _decisions[^1] = _decisions[^1] with { Chosen = SlotOf(combat, player, chosen) };
            return Task.FromResult<IReadOnlyList<CardModel>>([chosen]);
        }
    }
}
#endif
