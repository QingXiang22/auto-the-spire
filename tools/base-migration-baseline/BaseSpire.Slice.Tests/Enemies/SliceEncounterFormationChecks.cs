using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-02 批 5：**遭遇编队**（`formations` / `enemySlots`）。
///
/// 三层证据：
/// <list type="number">
/// <item><b>形状 vs 源码</b>：五个带编队的 act-1 遭遇的编队/槽位逐条对照各遭遇自己的
///       `GenerateMonsters()`（`SlimesWeak.cs:48-59`、`SlimesNormal.cs:33-45`、`FlyconidNormal.cs:42-49`、
///       `RubyRaidersNormal.cs:38-50`、`SlitheringStranglerNormal.cs:54-100`）；</item>
/// <item><b>派生素子</b>：每个带编队的遭遇在每个楼层上一条流，种子 = `(uint)(runSeed + 楼层 + hash(entry))`
///       （`EncounterModel.cs:268`），entry = 源 `ModelDb.GetEntry` 的大写下划线形式；</item>
/// <item><b>运行时实例</b>：整局逐房间读**实际出生的敌人**，与测试侧按源码语义的**独立复算**
///       （编队均匀抽一次 → 每个随机槽在"组内未用"的候选里抽一次 → 顺序 = 名单顺序）逐位比较。</item>
/// </list>
/// </summary>
internal static class SliceEncounterFormationChecks
{
    private const string MonsterTypeId = "base-spire:Monster";
    private const string RunTypeId = "base-spire:Run";
    private const string Monsters = "base-spire:Monsters";

    public static void Run()
    {

        Check.Run("[BASE-02 编队] 第一间房带编队时：阵容 = 独立复算（含组内不重复与位次顺序）[真实]", () =>
        {
            var pool = SinglePlayerContentSource.Shared.EncounterPool;
            var byId = pool.Encounters.ToDictionary(encounter => encounter.EncounterId, StringComparer.Ordinal);
            var matched = false;
            for (var index = 0; index < 24 && !matched; index++)
            {
                var seed = $"ENV-IRONCLAD-{index:D2}";
                var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession(seed);
                session.Start();
                var encounterId = CurrentEncounter(session);
                if (!byId.TryGetValue(encounterId, out var encounter) || !encounter.HasFormationProgram)
                {
                    continue;
                }

                var observed = Lineup(session);
                var expected = Replay(encounter, session.RunSeed, floor: 1);
                Check.Sequence(expected, observed, $"seed {seed}（遭遇 {encounterId}）的阵容必须等于独立复算");
                Console.WriteLine($"      BASE-02 编队：seed={seed} 遭遇={encounterId} 阵容=["
                    + $"{string.Join(",", observed.Select(id => id.Replace("base-spire:", "")))}]");
                matched = true;
            }

            Check.True(matched, "24 个固定 seed 里必须有一个第一间房就是带编队遭遇（否则这条检查没有判别力）");
        });

        Check.Run("[BASE-02 编队] 五个带编队遭遇的槽位/编队形状逐条等于源码", () =>
        {
            // 用**内容声明**核对形状（不是池成员）：`slithering-strangler-normal` 仍因 `ConstrictPower`
            // 未进池，但它的编队形状必须与源码一致，否则能力一落地就会带着错误阵容进包。
            var declarations = EncounterContent.LoadEncounters();

            var slimesWeak = Declaration(declarations, "base-spire:slimes-weak");
            Check.Equal(0, slimesWeak.Formations.Count, "slimes-weak 用顶层 enemySlots（单一编队形状）");
            Check.Sequence(["2", "2", "2"], slimesWeak.Slots.Select(slot => slot.EnemyChoices.Count.ToString()),
                "三个槽各两个候选（SlimesWeak.cs:51-57）");
            Check.Sequence(
                ["base-spire:distinct-small-slimes", "", "base-spire:distinct-small-slimes"],
                slimesWeak.Slots.Select(slot => slot.UniqueChoiceGroup),
                "小史莱姆两槽同组（不得重复）、中史莱姆槽无组");
            Check.Sequence([true.ToString(), true.ToString(), true.ToString()],
                slimesWeak.Slots.Select(slot => slot.ChooseRandomly.ToString()), "三个槽都随机");

            var slimesNormal = Declaration(declarations, "base-spire:slimes-normal");
            Check.Equal(2, slimesNormal.Formations.Count, "slimes-normal 两条编队（= NextBool 两个结果）");
            Check.Sequence(
                ["base-spire:twig-slime-m", "base-spire:leaf-slime-m", "base-spire:leaf-slime-s", "base-spire:twig-slime-s"],
                slimesNormal.Formations[0].EnemyIds, "编队 0 = flag true（SlimesNormal.cs:40-43）");
            Check.Sequence(
                ["base-spire:twig-slime-m", "base-spire:leaf-slime-m", "base-spire:twig-slime-s", "base-spire:leaf-slime-s"],
                slimesNormal.Formations[1].EnemyIds, "编队 1 = flag false");

            var flyconid = Declaration(declarations, "base-spire:flyconid-normal");
            Check.Sequence(["2", "1"], flyconid.Slots.Select(slot => slot.EnemyChoices.Count.ToString()),
                "flyconid-normal：先随机中等史莱姆、再固定飞菌（FlyconidNormal.cs:46-47）");
            Check.Sequence([true.ToString(), false.ToString()],
                flyconid.Slots.Select(slot => slot.ChooseRandomly.ToString()), "顺序与随机性");

            var raiders = Declaration(declarations, "base-spire:ruby-raiders-normal");
            Check.Equal(3, raiders.Slots.Count, "ruby-raiders-normal 三个槽（RubyRaidersNormal.cs:42-48）");
            foreach (var slot in raiders.Slots)
            {
                Check.Equal(5, slot.EnemyChoices.Count, "每槽候选 = 五个 Ruby Raider");
                Check.True(slot.ChooseRandomly, "每槽随机");
                Check.Equal("base-spire:distinct-ruby-raiders", slot.UniqueChoiceGroup, "三槽同组 ⇒ 三个不同成员");
            }

            var strangler = Declaration(declarations, "base-spire:slithering-strangler-normal");
            Check.Equal(3, strangler.Formations.Count, "三条编队（SecondaryEnemyType 三值，SlitheringStranglerNormal.cs:57）");
            Check.Sequence(["1", "1", "1"], strangler.Formations.Select(formation => formation.Weight.ToString()),
                "权重全 1 ⇒ 均匀抽一次");
            Check.Sequence(["2", "2", "3"],
                strangler.Formations.Select(formation => formation.Slots.Count.ToString()),
                "槽位含蛇形勒杀者自己：编队 0 = 蛇果+勒杀者、编队 1 = 中等史莱姆+勒杀者、"
                + "编队 2 = 两个小史莱姆+勒杀者（SlitheringStranglerNormal.cs:57-99）");
            Check.Sequence([false.ToString(), false.ToString()],
                strangler.Formations[0].Slots.Select(slot => slot.ChooseRandomly.ToString()),
                "编队 0 的两个槽都是固定候选（0 次抽取）；编队 1/2 的史莱姆槽才是随机槽");
            Check.True(strangler.Formations.All(formation =>
                    formation.Slots.All(slot => slot.UniqueChoiceGroup.Length == 0)),
                "小史莱姆两次抽取是**有放回**的（源码不 Remove）⇒ 没有唯一选择组");
        });

        Check.Run("[BASE-02 编队] 派生流：每个 (带编队遭遇, 楼层) 一条流，种子按源公式显式种好", () =>
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["base-spire:slimes-weak"] = "SLIMES_WEAK",
                ["base-spire:slimes-normal"] = "SLIMES_NORMAL",
                ["base-spire:flyconid-normal"] = "FLYCONID_NORMAL",
                ["base-spire:ruby-raiders-normal"] = "RUBY_RAIDERS_NORMAL",
                ["base-spire:slithering-strangler-normal"] = "SLITHERING_STRANGLER_NORMAL",
            };
            foreach (var (id, entry) in entries)
            {
                Check.Equal(entry, VanillaRandomLayout.SourceEntry(id),
                    $"{id} 的源 entry（`ModelDb.GetEntry` = Slugify(类名)）");
                // 种子公式：`(uint)(runSeed + 楼层 + hash(entry))`（EncounterModel.cs:268）。
                Check.Equal(VanillaRandomLayout.ContentSeed(0x1234_5678u, 3, entry),
                    VanillaRandomLayout.EncounterSeed(0x1234_5678u, 3, entry),
                    "遭遇派生种子 = runSeed + 楼层 + hash(entry)");
                Check.Equal($"base-spire:encounter:{entry}:3",
                    VanillaRandomLayout.EncounterStreamName(entry, 3), "派生流名（引擎侧键）");
            }

            // 建局时**必须已经**把派生流种好（每个已支持的编队遭遇 × 房间数），且不消费任何随机数。
            var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession("ENV-IRONCLAD-00");
            var pool = SinglePlayerContentSource.Shared.EncounterPool;
            Check.Equal(0L, session.Session.Random.TotalDraws, "种流不消费随机数（尚未 Start）");
            Check.Equal(pool.FormationEncounters.Count * pool.RoomCount, session.EncounterStreams.Count,
                "每个已进池的带编队遭遇在 1..房间数 上各一条派生流");
        });

        Check.Run("[BASE-02 编队] 三个编队遭遇已进池：派生流按 (遭遇 × 楼层) 条数种好、且不消费随机数", () =>
        {
            var pool = SinglePlayerContentSource.Shared.EncounterPool;
            var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession("ENV-IRONCLAD-00");
            Check.Equal(0L, session.Session.Random.TotalDraws, "种流不消费随机数（尚未 Start）");
            var formationEncounters = pool.FormationEncounters.Select(entry => entry.EncounterId).ToList();
            Check.Sequence(
                ["base-spire:flyconid-normal", "base-spire:ruby-raiders-normal", "base-spire:slimes-normal",
                    "base-spire:slimes-weak", "base-spire:slithering-strangler-normal"],
                formationEncounters.Order(StringComparer.Ordinal), "第一幕五条带编队的遭遇都已进池");
            Check.Equal(formationEncounters.Count * pool.RoomCount, session.EncounterStreams.Count,
                "每个带编队遭遇在 1..房间数 上各一条派生流（4 × 15 = 60）");
            foreach (var id in formationEncounters)
            {
                var entry = VanillaRandomLayout.SourceEntry(id);
                var stream = VanillaRandomLayout.EncounterStreamName(entry, 1);
                Check.Equal((ulong)VanillaRandomLayout.EncounterSeed(checked((uint)session.RunSeed), 1, entry),
                    session.EncounterStreams.Single(record => record.Stream == stream).Seed,
                    $"{id} 楼层 1 的种子 = runSeed + 楼层 + hash(entry)");
            }
        });

        Check.Run("[BASE-02 编队] 未进池的编队遭遇（缺 ConstrictPower）逐条带原因，不被静默丢掉", () =>
        {
            var content = SinglePlayerContentSource.Shared;
            var act = EncounterContent.LoadActPools().First();
            var byId = EncounterContent.LoadEncounters().ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            var formationIds = act.WeakNormalEncounters.Concat(act.RegularEncounters)
                .Where(id => byId.TryGetValue(id, out var declaration)
                    && (declaration.Formations.Count > 0 || declaration.Slots.Count > 0))
                .ToList();
            Check.Equal(5, formationIds.Count, "第一幕的普通战斗房间里正好五条带编队的遭遇");
            var inPool = content.EncounterPool.FormationEncounters
                .Select(encounter => encounter.EncounterId).ToHashSet(StringComparer.Ordinal);
            foreach (var id in formationIds)
            {
                // 四条已进池；剩下的（蛇形勒杀者）必须**显式**列在排除清单里，不能被静默丢掉。
                if (inPool.Contains(id))
                {
                    continue;
                }

                Check.True(content.ExcludedEncounters.Any(entry => entry.Item == id),
                    $"{id} 既不在池里也不在排除清单里");
                Console.WriteLine($"      BASE-02 编队：{id} 未进池 —— "
                    + content.ExcludedEncounters.Single(entry => entry.Item == id).Reason);
            }
        });

        Check.Run("[BASE-02 编队] 整幕队列里出现编队遭遇时，房间出生的阵容仍等于独立复算 [真实]", () =>
        {
            // 跨房间：驱动整局（公开动作清房 + 进入下一间），遇到编队遭遇就按 (遭遇, 房间号=楼层) 复算比较。
            var pool = SinglePlayerContentSource.Shared.EncounterPool;
            var byId = pool.Encounters.ToDictionary(encounter => encounter.EncounterId, StringComparer.Ordinal);
            var compared = 0;
            var rooms = new List<string>();
            for (var index = 0; index < 6 && compared < 1; index++)
            {
                var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad())
                    .CreateSession($"ENV-IRONCLAD-{index:D2}");
                session.Start();
                for (var room = 1; room <= pool.RoomCount; room++)
                {
                    var encounterId = CurrentEncounter(session);
                    if (!byId.TryGetValue(encounterId, out var encounter))
                    {
                        break;
                    }

                    if (encounter.HasFormationProgram)
                    {
                        var observed = Lineup(session);
                        Check.Sequence(Replay(encounter, session.RunSeed, room), observed,
                            $"seed {index:D2} 房间 {room}（{encounterId}）的阵容必须等于独立复算（楼层 = 房间号）");
                        rooms.Add($"{room}:{encounterId}");
                        compared++;
                        break;
                    }

                    // 只走公开动作：打赢本间房（把敌人压到 1 血只为不依赖抽牌顺序），再进入下一间。
                    if (session.Session.Status != SessionStatus.Running)
                    {
                        break;
                    }

                    ClearRoom(session);
                }
            }

            Check.True(compared >= 1, "固定 seed 序列里至少要遇到一间带编队的房间并逐位比较");
            Console.WriteLine($"      BASE-02 编队：跨房间比较 {compared} 间 [{string.Join(",", rooms)}]");
        });
    }

    /// <summary>
    /// 用**公开动作**结束本间房：把敌人压到 1 血（只为不依赖抽牌顺序），然后每步打第一张能打的牌、
    /// 打不出来就结束回合，直到 `EnterNextRoom` 可用。
    /// </summary>
    private static void ClearRoom(BaseSpireRunSession session)
    {
        var monsterType = session.Session.RuleSet.TypeByQualifiedId(MonsterTypeId);
        var hp = monsterType.FieldIndex("Hp");
        foreach (var monster in session.Session.State.CollectionMembers(Collection(session, Monsters)))
        {
            session.Session.State.SetField(monster, hp, Value.Of(1L));
        }

        for (var step = 0; step < 128; step++)
        {
            var actions = session.Session.LegalActions(session.Participant);
            var enter = actions.FirstOrDefault(candidate =>
                candidate.Definition.QualifiedId == "base-spire:EnterNextRoom");
            if (enter is not null)
            {
                session.Session.Submit(enter.Definition, session.Participant, []);
                return;
            }

            var play = actions.FirstOrDefault(candidate =>
                candidate.Definition.QualifiedId == "base-spire:PlayCard" && candidate.Bindings.Count > 0);
            var endTurn = actions.FirstOrDefault(candidate =>
                candidate.Definition.QualifiedId == "base-spire:EndTurn");
            var chosen = play ?? endTurn;
            if (chosen is null)
            {
                return;
            }

            session.Session.Submit(chosen.Definition, session.Participant,
                chosen.Bindings.Count > 0 ? chosen.Bindings[0] : []);
            while (session.Session.Status == SessionStatus.AwaitingDecision)
            {
                session.Session.Answer(new DecisionAnswer(session.Session.PendingDecision!.RequestId,
                    [session.Session.PendingDecision!.Candidates[0]]));
            }

            if (session.Session.Status != SessionStatus.Running)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 独立复算：按冻结源码的语义重放一次编队生成。
    /// <list type="number">
    /// <item>抽编队：`Rng.NextItem(formations)`（权重全 1 ⇒ `DrawInt(0, 数量)`，一次抽取）；</item>
    /// <item>槽位按**组块**顺序抽（同一唯一组在第一个成员处连续抽完，`SlimesWeak.cs:51-57`）；</item>
    /// <item>`chooseRandomly` 槽：在"组内尚未选中"的候选里 `NextItem`（一次抽取）；否则取唯一候选（0 次）；</item>
    /// <item>阵容顺序 = 名单/槽位顺序（抽取顺序不影响出生顺序）。</item>
    /// </list>
    /// </summary>
    private static IReadOnlyList<string> Replay(SliceEncounter encounter, ulong runSeed, int floor)
    {
        var entry = VanillaRandomLayout.SourceEntry(encounter.EncounterId);
        var random = new RandomService(runSeed, VanillaRandomLayout.AlgorithmId,
            VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
        random.SeedStream(VanillaRandomLayout.EncounterStreamName(entry, floor),
            VanillaRandomLayout.EncounterSeed(checked((uint)runSeed), floor, entry));

        var formations = encounter.EffectiveFormations;
        var chosen = formations[0];
        if (formations.Count > 1)
        {
            chosen = formations[(int)random.DrawInt(VanillaRandomLayout.EncounterStreamName(entry, floor), 0,
                formations.Count)];
        }

        if (chosen.EnemyIds.Count > 0)
        {
            return chosen.EnemyIds;
        }

        var stream = VanillaRandomLayout.EncounterStreamName(entry, floor);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var picks = new string[chosen.Slots.Count];
        foreach (var index in GroupBlockOrder(chosen.Slots))
        {
            var slot = chosen.Slots[index];
            if (!slot.ChooseRandomly)
            {
                picks[index] = slot.EnemyChoices[0];
                continue;
            }

            var eligible = slot.UniqueChoiceGroup.Length == 0
                ? slot.EnemyChoices
                : [.. slot.EnemyChoices.Where(choice => !used.Contains(choice))];
            var pick = eligible[(int)random.DrawInt(stream, 0, eligible.Count)];
            picks[index] = pick;
            if (slot.UniqueChoiceGroup.Length > 0)
            {
                used.Add(pick);
            }
        }

        return picks;
    }

    /// <summary>槽位处理顺序：同一个唯一组的槽位在**第一个成员的位置**连续处理，其余保持原顺序。</summary>
    private static IReadOnlyList<int> GroupBlockOrder(IReadOnlyList<EncounterSlot> slots)
    {
        var order = new List<int>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < slots.Count; index++)
        {
            var group = slots[index].UniqueChoiceGroup;
            if (group.Length == 0)
            {
                order.Add(index);
                continue;
            }

            if (!done.Add(group))
            {
                continue; // 该组已经在第一个成员处整块处理过
            }

            for (var other = index; other < slots.Count; other++)
            {
                if (string.Equals(slots[other].UniqueChoiceGroup, group, StringComparison.Ordinal))
                {
                    order.Add(other);
                }
            }
        }

        return order;
    }

    private static SliceEncounter Encounter(SliceEncounterPool pool, string id) =>
        pool.Encounters.Single(encounter => encounter.EncounterId == id);

    private static string CurrentEncounter(BaseSpireRunSession session)
    {
        var run = session.Session.State.CollectionMembers(Collection(session, "base-spire:RunStates")).First();
        return session.Session.State.GetField(run, session.Session.RuleSet.TypeByQualifiedId(RunTypeId)
            .FieldIndex("EncounterId")).AsString();
    }

    private static IReadOnlyList<string> Lineup(BaseSpireRunSession session)
    {
        var type = session.Session.RuleSet.TypeByQualifiedId(MonsterTypeId);
        return
        [
            .. session.Session.State.CollectionMembers(Collection(session, Monsters)).Select(monster =>
                session.Session.State.GetField(monster, type.FieldIndex("DefinitionId")).AsString()),
        ];
    }

    private static int Collection(BaseSpireRunSession session, string qualifiedId) =>
        session.Session.RuleSet.Collections.Single(entry => entry.QualifiedId == qualifiedId).Id;

    /// <summary>按内容声明取一条遭遇（用于形状核对，不要求它已进池）。</summary>
    private static EncounterDeclaration Declaration(IReadOnlyList<EncounterDeclaration> declarations, string id) =>
        declarations.Single(encounter => encounter.Id == id);
}
