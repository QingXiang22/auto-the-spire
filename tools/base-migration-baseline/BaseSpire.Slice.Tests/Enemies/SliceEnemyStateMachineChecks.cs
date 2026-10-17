using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-02 批 1：**敌人行动状态机**（正式 `.spire` 的 `behavior` → 同一 Engine IR）。
///
/// 两层证据：
/// <list type="number">
/// <item><b>形状</b>：状态图逐条对照冻结源码（`Mawler.cs` / `Flyconid.cs` / `ShrinkerBeetle.cs` /
///       `Nibbit.cs` 的 `GenerateMoveStateMachine`），证明"作者声明 → 状态/后继/权重/门槛"没有走样；</item>
/// <item><b>行为</b>：合成敌人 + 独立复算器（按源码语义重放权重桶与三个门槛，用同一 `monster_ai` 种子）
///       逐回合比较**实际选出的行动**；再单独核对首动不掷、冷却窗口、有序条件分支不掷点、
///       全部门槛挡住时"仍然抽一次并命中第一分支"。</item>
/// </list>
///
/// 独立复算**不调用**被测的 IR 程序：它拿的是作者模型里的状态图 + 一条独立 <see cref="RandomService"/>，
/// 门槛按源码从**完整日志**现算（被测实现用计数 + 有界窗口，两种编码互相独立）。
///
/// ENV-T01 起加权选取按**源 float32 语义**实现（`RandomBranchState.cs:115-128` + `Rng.cs:156-164`）：
/// 一次 `SampleInt(monster_ai, 0, 2^53)` 取 53 位、纯 compute 资产 `SourceWeightedPick` 做
/// `Sum`（double 累加器）→ `NextFloat(total)` → 逐项 float32 减法 / `&lt;= 0f` 命中。
/// 本节新增五条检查：手算边界表、与**第二份**源语义转写的差分扫描、分数权重端到端复算（含
/// "float32 分桶 ≠ 旧整数累积权重"的判别力证据）、身份/后端强制证据，以及**真实内容**的分数权重
/// 与"尚未加载"原因（Fogmog/Two-Tailed Rat 这类带分数权重但依赖未实现原语的敌人）。
/// </summary>
internal static class SliceEnemyStateMachineChecks
{
    private const string MonsterTypeId = "base-spire:Monster";
    private const string MonsterAiStream = "base-spire:run:monster_ai";

    /// <summary>
    /// 合成夹具的基础作者文本（卡/能力/牌组/两间房的敌人槽位）。两个敌人的**行为**随后由
    /// `.spire` 文本替换成状态机——正式源和冻结 `.author` 文本用同一套运行时，因此这里能直接跑 IR。
    /// </summary>
    private const string FixtureText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 44  tough-hp = 45  first-move-ritual = 3  strike-damage = 7
          strike-damage-deadly = 9  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 51  tough-hp = 52  first-move-ritual = 5  strike-damage = 1
          strike-damage-deadly = 3  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = 2 }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 1
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        """;

    /// <summary>Mawler 的形状：首动固定，之后在三个候选里加权选择（含一次性行动与不可连续重复）。</summary>
    private const string WeightedSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(
              machineClaw = repeat(2, attack(3, target=player)),
              machineRipAndTear = attack(6, target=player),
              machineRoar = gainBlock(9)
            )
            behavior = startWith(machineClaw, then=chooseWithLimits(
              candidate(machineRipAndTear, maxConsecutive=1),
              candidate(machineRoar, maxUsesPerCombat=1),
              candidate(machineClaw, maxConsecutive=1)
            ))
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(
              machineLash = attack(2, target=player),
              machineHeavy = attack(4, target=player)
            )
            behavior = startWith(machineLash, then=chooseWithLimits(
              candidate(machineLash, cooldown=2),
              candidate(machineHeavy, maxConsecutive=1)
            ))
          }
        }
        """;

    /// <summary>有序条件分支（`chooseByCondition`）：按生命阈值切换，全程不掷点。</summary>
    private const string ConditionalSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(10, 10)
            moves = actions(
              machineLash = attack(2, target=player),
              machineHeavy = attack(4, target=player)
            )
            behavior = startWith(machineLash, then=chooseByCondition(
              candidate(machineHeavy, when=metric(EnemyHp, LessThan, 30)),
              candidate(machineLash, when=metric(Turn, AtLeast, 0))
            ))
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(
              machineLash = attack(2, target=player),
              machineHeavy = attack(4, target=player)
            )
            behavior = startWith(machineLash, then=chooseByCondition(
              candidate(machineHeavy, when=metric(EnemyHp, LessThan, 30)),
              candidate(machineLash, when=metric(Turn, AtLeast, 0))
            ))
          }
        }
        """;

    /// <summary>全部门槛挡住：唯一候选是一次性行动，且它已经作为初始行动进过日志。</summary>
    private const string BlockedSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(machineLash = attack(2, target=player))
            behavior = startWith(machineLash, then=chooseWithLimits(
              candidate(machineLash, maxUsesPerCombat=1)))
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(machineLash = attack(2, target=player))
            behavior = startWith(machineLash, then=chooseWithLimits(
              candidate(machineLash, maxUsesPerCombat=1)))
          }
        }
        """;

    /// <summary>
    /// ENV-T01：**分数权重** + `when` 门槛置 0。权重照 `TwoTailedRat.cs:125-128` 的形状
    /// （可召援时三条 `1f/12f`、召援 `0.75f`），`0.083333336` 是与 `1f/12f` 位型相同的十进制写法。
    /// 第二条 `machineBite` 的门槛恒假（`Turn >= 99`）：它的权重必须是精确 no-op（永远不会被选中）。
    /// 后排 DampCultist 是**有序条件分支**（不消费 `monster_ai`）的对照组，让"共享一条流"的复算保持同步。
    /// </summary>
    private const string FractionalSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(300, 300)
            moves = actions(
              machineInit = attack(1, target=player),
              machineScratch = attack(3, target=player),
              machineBite = attack(6, target=player),
              machineScreech = gainBlock(9),
              machineBackup = attack(2, target=player)
            )
            behavior = startWith(machineInit, then=chooseWithLimits(
              candidate(machineScratch, weight=0.083333336, when=metric(Turn, AtLeast, 0)),
              candidate(machineBite, weight=1, when=metric(Turn, AtLeast, 99)),
              candidate(machineScreech, weight=0.083333336, when=metric(Turn, AtLeast, 0)),
              candidate(machineBackup, weight=0.75, when=metric(Turn, AtLeast, 0))
            ))
          }
          enemy DampCultist {
            hp = range(300, 300)
            moves = actions(
              machineInit = attack(1, target=player),
              machineScratch = attack(3, target=player)
            )
            behavior = startWith(machineInit, then=chooseByCondition(
              candidate(machineScratch, when=metric(Turn, AtLeast, 0))
            ))
          }
        }
        """;

    public static void Run()
    {
        RunSourceShapeChecks();
        RunRuntimeChecks();
        RunWeightBucketChecks();
        RunRealContentWeightChecks();
    }

    // -- 形状：作者声明 vs 冻结源码 -------------------------------------------------------------------------

    private static void RunSourceShapeChecks()
    {
        Check.Run("[BASE-02 状态机] 正式源敌人全部降级成完整状态图（行动都有后继、引用都在声明里）", () =>
        {
            var content = SinglePlayerContentSource.Shared;
            var enemies = content.Model.Enemies;
            Check.True(enemies.Count > 0, "正式源必须加载出敌人");
            foreach (var enemy in enemies)
            {
                Check.True(enemy.Behavior is not null, $"{enemy.Id} 必须带状态机（正式源不再用固定序列）");
                var behavior = enemy.Behavior!;
                Check.True(behavior.States.Count > 0, $"{enemy.Id} 的状态图不能为空");
                var declared = enemy.Moves.Select(move => move.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var move in behavior.ReferencedMoves)
                {
                    Check.True(declared.Contains(move), $"{enemy.Id} 的状态图引用了未声明的行动 '{move}'");
                }

                var ids = behavior.States.Select(state => state.Id).ToList();
                Check.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count(),
                    $"{enemy.Id} 的状态 id 必须唯一");
                Check.True(behavior.MoveStates.All(state => ids.Contains(state.FollowUp)),
                    $"{enemy.Id} 的每个行动都必须指向一个存在的后继状态");
            }

            Console.WriteLine($"      BASE-02 状态机：正式源敌人 {enemies.Count} 只全部带状态图；"
                + $"行动 {enemies.Sum(enemy => enemy.Moves.Count)} 个，分支 "
                + $"{enemies.Sum(enemy => enemy.Behavior!.States.Count(s => s is AuthorRandomState or AuthorOrderedState))} 个");
            foreach (var (item, reason) in content.Unsupported.Where(entry =>
                         entry.Item.StartsWith("enemy:", StringComparison.Ordinal)))
            {
                Console.WriteLine($"      BASE-02 敌人未支持：{item} ← {reason}");
            }
        });

        Check.Run("[BASE-02 状态机] Mawler 的状态图逐条等于 Mawler.cs（首动 Claw、三候选权重/门槛）", () =>
        {
            var mawler = Enemy("Mawler");
            Check.Equal("mawlerClaw", mawler.Behavior!.InitialState, "初始状态是爪击（Mawler.cs:43）");
            Check.True(mawler.Behavior.InitialIsMove, "初始状态是**行动**：开局那次选取因此不掷点");
            var claw = Move(mawler, "mawlerClaw");
            var branch = mawler.Behavior.States.Single(state => state.Id == claw.FollowUp);
            Check.True(branch is AuthorRandomState, "爪击的后继是随机分支（Mawler.cs:35）");
            var candidates = ((AuthorRandomState)branch).Candidates;
            Check.Sequence(["mawlerRipAndTear", "mawlerRoar", "mawlerClaw"],
                candidates.Select(candidate => candidate.Move), "候选顺序＝声明顺序");
            Check.Sequence([EnemyRepeatKinds.NoRepeat, EnemyRepeatKinds.Once, EnemyRepeatKinds.NoRepeat],
                candidates.Select(candidate => candidate.Repeat.Kind),
                "重复限制：Rip/Claw=CannotRepeat、Roar=UseOnlyOnce（Mawler.cs:36-38）");
            Check.Sequence([1, 1, 1], candidates.Select(candidate => candidate.Weight),
                "三个候选的权重都是 1（源码 AddBranch(…, 1f)）");
        });

        Check.Run("[BASE-02 状态机] Flyconid 的开场池/后续池与候选权重、冷却逐条等于 Flyconid.cs", () =>
        {
            var flyconid = Enemy("Flyconid", includeUnsupportedPowers: true);
            var behavior = flyconid.Behavior!;
            Check.False(behavior.InitialIsMove, "初始状态是**分支**（Flyconid.cs:49 的 INITIAL）");
            var initial = behavior.States.OfType<AuthorRandomState>().Single(state => state.Id == behavior.InitialState);
            Check.Sequence(["flyconidFrailSpores", "flyconidSmash"],
                initial.Candidates.Select(candidate => candidate.Move), "开场只在前两个候选里选（Flyconid.cs:42-43）");
            Check.Sequence([2, 0], initial.Candidates.Select(candidate => candidate.Cooldown),
                "开场候选的冷却 = 2 / 0（Flyconid.cs:42-43）");
            var later = behavior.States.OfType<AuthorRandomState>()
                .Single(state => state.Id != behavior.InitialState);
            Check.Equal(3, later.Candidates.Count, "后续池有 3 个候选（Flyconid.cs:39-41）");
            Check.Sequence(["flyconidVulnerableSpores", "flyconidFrailSpores", "flyconidSmash"],
                later.Candidates.Select(candidate => candidate.Move), "后续池的三个候选");
            Check.Sequence([3, 2, 0], later.Candidates.Select(candidate => candidate.Cooldown),
                "后续池冷却 = 3 / 2 / 0（Flyconid.cs:39-41：三个候选的冷却分别是 3、2、0）");
            Check.Sequence([1, 1, 1], later.Candidates.Select(candidate => candidate.Weight),
                "后续池权重都是 1（源码三个 AddBranch 的权重都是 1f）");
            Check.True(behavior.MoveStates.All(state => state.FollowUp == later.Id),
                "三个行动的后继都指回后续池（Flyconid.cs:36-38）");
        });

        Check.Run("[BASE-02 状态机] ShrinkerBeetle / Nibbit 的固定链与 cycle 展开一致（含首个行动的入口）", () =>
        {
            var beetle = Enemy("ShrinkerBeetle");
            Check.Equal("shrinkerBeetleShrink", beetle.Behavior!.InitialState, "初始状态是 Shrink");
            Check.Equal("shrinkerBeetleChomp", Move(beetle, "shrinkerBeetleShrink").FollowUp, "Shrink → Chomp");
            Check.Equal("shrinkerBeetleStomp", Move(beetle, "shrinkerBeetleChomp").FollowUp, "Chomp → Stomp");
            Check.Equal("shrinkerBeetleChomp", Move(beetle, "shrinkerBeetleStomp").FollowUp,
                "Stomp → Chomp（cycle 从**列表开头**循环，不是从最后一个）");

            var nibbit = Enemy("Nibbit");
            Check.Equal("nibbitButt", nibbit.Behavior!.InitialState, "初始状态是 Butt");
            Check.Equal("nibbitSlice", Move(nibbit, "nibbitButt").FollowUp, "Butt → Slice");
            Check.Equal("nibbitHiss", Move(nibbit, "nibbitSlice").FollowUp, "Slice → Hiss");
            Check.Equal("nibbitButt", Move(nibbit, "nibbitHiss").FollowUp, "Hiss → Butt");
        });

        Check.Run("[BASE-02 状态机] 未支持的候选/条件/图形态在解析期逐条拒绝并附原因（不产生近似敌人）", () =>
        {
            foreach (var (behavior, fragment, label) in new[]
                     {
                         // 原版没有"整场最多 n 次"：UseOnlyOnce 只等于 1 次，CanRepeatXTimes 是**连续**上限。
                         ("startWith(machineClaw, then=chooseWithLimits("
                             + "candidate(machineRipAndTear, maxUsesPerCombat=2)))",
                             "maxUsesPerCombat", "整场次数上限"),
                         // 具名条件需要敌人的 `conditions = […]` 声明表（本批未接入）。
                         ("startWith(machineClaw, then=chooseWithLimits("
                             + "candidate(machineRipAndTear, when=condition(SummonAvailable))))",
                             "具名条件", "具名条件"),
                         // 候选必须指向已声明的行动。
                         ("startWith(machineClaw, then=chooseWithLimits(candidate(machineMissing)))",
                             "没有声明的行动", "悬空候选"),
                         // 行动没有后继：源码会在转移时抛错（`MoveState.GetNextState`），不能静默当循环。
                         ("graph(start=machineClaw, edges=[edge(machineRipAndTear, machineClaw)])",
                             "没有后继", "缺后继"),
                         // 有序条件分支本身不能带权重/冷却（它按第一个成立项选择，与权重无关）。
                         ("startWith(machineClaw, then=chooseByCondition("
                             + "candidate(machineRipAndTear, weight=3, when=metric(EnemyHp, LessThan, 30))))",
                             "weight", "有序分支带权重"),
                         // 未知条件形态（正式源里确实出现的其它 metric/谓词）。
                         ("startWith(machineClaw, then=chooseByCondition("
                             + "candidate(machineRipAndTear, when=metric(LivingAllies, LessThan, 4))))",
                             "未支持", "未实现的条件"),
                     })
            {
                var source = SpireEnemySource.Parse(EnemyText(behavior), "machine-reject");
                Check.True(source.Enemies.Count == 0, $"必须整条拒绝（{label}）");
                var reasons = string.Join(" | ", source.Unsupported.Select(entry => entry.Reason));
                Check.True(reasons.Contains(fragment, StringComparison.Ordinal),
                    $"拒绝原因必须点名 '{fragment}'（{label}，实际：{reasons}）");
            }
        });
    }

    /// <summary>只含一个敌人的最小 `.spire` 文本，用于替换 `behavior` 做拒绝检查。</summary>
    private static string EnemyText(string behavior) => $$"""
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(
              machineClaw = repeat(2, attack(3, target=player)),
              machineRipAndTear = attack(6, target=player),
              machineRoar = gainBlock(9)
            )
            behavior = {{behavior}}
          }
        }
        """;

    // -- 行为：状态机在 Engine 里跑出来的东西 ---------------------------------------------------------------

    private static void RunRuntimeChecks()
    {
        Check.Run("[BASE-02 状态机] 首动不掷点、意图 = 初始行动，且首动之后立刻被 CannotRepeat 挡住 [真实]", () =>
        {
            var (session, front, back) = Battle(WeightedSpire);
            Check.Equal(0L, Draws(session), "开局那次选取不掷点（初始状态是行动）");
            Check.Equal("machineClaw", Next(session, front), "开局意图就是初始行动");
            Check.Equal("machineLash", Next(session, back), "后排同理");

            EndTurn(session);
            Check.Equal(2L, Draws(session),
                "第一次玩家回合开始：两只怪各落到一个随机分支 ⇒ 各掷 1 次（共 2 次，没有多余的抽取）");
            var second = Next(session, front);
            Check.True(second is "machineRipAndTear" or "machineRoar",
                $"首动之后爪击被 CannotRepeat 挡住（实际选了 {second}）");
            Check.Equal("machineHeavy", Next(session, back),
                "后排的 Lash 冷却窗口包含它自己刚用过的行动（cooldown=2，日志=[Lash]）⇒ 只剩 Heavy 可选");
        });

        Check.Run("[BASE-02 状态机] 权重桶/一次性/连续上限的实际序列 = 独立复算（同一 monster_ai 种子）[真实]", () =>
        {
            var (session, front, _) = Battle(WeightedSpire);
            var behavior = FixtureEnemy(WeightedSpire, "CalcifiedCultist").Behavior!;
            var replay = new MachineReplay(behavior, MonsterAiStream, SessionSeed, StreamSeed);
            var observed = new List<string> { Next(session, front) };
            var expected = new List<string> { replay.Roll() };
            for (var turn = 0; turn < 5; turn++)
            {
                EndTurn(session);
                replay.Perform();
                observed.Add(Next(session, front));
                expected.Add(replay.Roll());
            }

            Check.Sequence(expected, observed, "逐回合的已选行动必须与独立复算一致");
            Check.True(observed.Distinct(StringComparer.Ordinal).Count() >= 2,
                "序列必须真的换过行动（否则这条检查没有判别力）");
            Check.True(observed.Count(move => move == "machineRoar") <= 1, "一次性行动最多出现一次");
            Check.True(!observed.Skip(1).Where((move, index) => move == "machineClaw" && observed[index] == "machineClaw")
                    .Any(),
                "爪击不能连续出现（CannotRepeat）");
        });

        Check.Run("[BASE-02 状态机] 冷却窗口与「全部门槛挡住」的实际序列 = 独立复算（含计数证据）[真实]", () =>
        {
            var (session, _, back) = Battle(WeightedSpire);
            var behavior = FixtureEnemy(WeightedSpire, "DampCultist").Behavior!;
            var replay = new MachineReplay(behavior, MonsterAiStream, SessionSeed, StreamSeed);
            var observed = new List<string> { Next(session, back) };
            var expected = new List<string> { replay.Roll() };
            for (var turn = 0; turn < 6; turn++)
            {
                EndTurn(session);
                replay.Perform();
                observed.Add(Next(session, back));
                expected.Add(replay.Roll());
            }

            Check.Sequence(expected, observed,
                "逐回合的已选行动必须与独立复算一致（含冷却窗口与全挡回落）");
            Check.True(observed.Distinct(StringComparer.Ordinal).Count() >= 2,
                $"序列必须真的换过行动（实际：{string.Join(",", observed)}）");
            Check.True(replay.BlockedByCooldown > 0,
                "冷却门槛必须在这段序列里真的生效过（否则这条检查没有判别力）");
            Check.True(replay.BlockedFallbacks > 0,
                "必须真的走到「全部门槛挡住」的分支（原版仍然抽一次并命中第一个候选）");
        });

        Check.Run("[BASE-02 状态机] 有序条件分支：生命阈值决定行动、两只怪结果不同、全程不掷点 [真实]", () =>
        {
            var (session, front, back) = Battle(ConditionalSpire);
            EndTurn(session);
            Check.Equal(0L, Draws(session), "条件分支不消耗 monster_ai");
            Check.Equal("machineHeavy", Next(session, front), "生命 10 < 30 ⇒ 命中第一个候选");
            Check.Equal("machineLash", Next(session, back), "生命 60 ≥ 30 ⇒ 落到第二个候选");
            EndTurn(session);
            Check.Equal(0L, Draws(session), "仍然不掷点");
            Check.Equal("machineHeavy", Next(session, front), "条件持续成立");
        });

        Check.Run("[BASE-02 状态机] 全部门槛挡住：仍然恰好抽一次，并命中第一个分支 [真实]", () =>
        {
            var (session, _, back) = Battle(BlockedSpire);
            Check.Equal(0L, Draws(session), "开局不掷点");
            EndTurn(session);
            Check.Equal(2L, Draws(session), "全部门槛挡住时原版仍然抽一次/只（NextFloat(0)）：两只怪各一次");
            Check.Equal("machineLash", Next(session, back), "命中第一个分支");
            EndTurn(session);
            Check.Equal(4L, Draws(session), "每次选取仍然恰好一次");
            Check.Equal("machineLash", Next(session, back), "选择不变");
        });
    }

    // -- ENV-T01：源 float32 加权分桶 ----------------------------------------------------------------------

    private static void RunWeightBucketChecks()
    {
        // 手算表：期望值按源语义（`RandomBranchState.cs:115-128` + `Rng.cs:156-164`）算出后**写死**，
        // 不复用被测实现。`1f/12f` 用位型 0x3DAAAAAB 取值，避免依赖解析。
        var twelfth = BitConverter.Int32BitsToSingle(0x3DAAAAAB);
        var one = 1f;
        var zero = 0f;
        var threeQuarters = 0.75f;
        var pair = new[] { one, one };
        var fractional = new[] { twelfth, twelfth, twelfth, threeQuarters };
        var gated = new[] { twelfth, 0f, twelfth, threeQuarters };
        var zerosLast = new[] { twelfth, twelfth, threeQuarters, 0f };
        var plain = new[] { twelfth, twelfth, threeQuarters };
        var labels = new[] { "scratch", "dead(gated)", "screech", "backup" };
        var zerosLastLabels = new[] { "scratch", "screech", "backup", "dead(zerosLast)" };
        var plainLabels = new[] { "scratch", "screech", "backup" };

        static string Selected(long bits, float[] weights, string[] names)
        {
            var index = SourceWeightedPick.Pick(bits, weights);
            return index < 0 ? "(none)" : names[index];
        }

        Check.Run("[ENV-T01 权重] 源 float32 分桶：`<= 0f` 命中、`NextFloat` 只转一次、边界与整数近似不同", () =>
        {
            Check.Equal(0L, SourceWeightedPick.Pick(0, pair), "rd=0 ⇒ 第一项立即命中（`0f - 1f <= 0f`）");
            Check.Equal(0L, SourceWeightedPick.Pick(1L << 52, pair),
                "rd=0.5 是边界：`num = (float)(0.5 * 2) = 1f`、`1f - 1f <= 0f` ⇒ 第一项");
            Check.Equal(1, IntegerPick(1L << 52, pair),
                "同一 bits 上旧的整数累积权重给出第二项 ⇒ 这条手算表真的能区分两种算术");
            Check.Equal(1L, SourceWeightedPick.Pick((1L << 52) + 268435457L, pair),
                "再往上一个 float32 间距（2^-25）之后 `num` 才会舍入到 1 以上 ⇒ 第二项");
            Check.Equal(1L, SourceWeightedPick.Pick((1L << 53) - 1, pair),
                "rd 逼近 1 ⇒ 末项命中");
            Check.Equal(0L, SourceWeightedPick.Pick(0, [zero, zero]),
                "所有权重为 0：源仍抽一次（`NextFloat(0) = 0f`）并命中**第一个**项（`0f - 0f <= 0f`）");
            Check.Equal(0L, SourceWeightedPick.Pick((1L << 53) - 1, [zero, zero]), "全 0 时抽取结果不影响命中项");
            Check.Equal(-1L, SourceWeightedPick.Pick(0, ReadOnlySpan<float>.Empty),
                "没有候选 ⇒ `-1`（源在这里抛 `InvalidOperationException`）");
            Check.Equal(0L, SourceWeightedPick.Pick(0, fractional),
                "分数权重：rd=0 ⇒ 第一条 `1f/12f` 命中");
            Check.Equal(3L, SourceWeightedPick.Pick(1L << 52, fractional),
                "rd=0.5 ⇒ 三条 `1f/12f` 各扣一次后落在 `0.75f`（第四条）");
            Check.Equal(-1L, SourceWeightedPick.Pick((1L << 53) - 1, fractional),
                "rd 逼近 1：`num` 舍入到 1f 而 `Sum` 也是 1f（源权重和恰好是 1）⇒ 逐项减法留下正残差、"
                + "源码在这里抛 `InvalidOperationException`；纯计算返回 -1 交给 IR 显式失败");
            foreach (var value in new[] { 0L, 1L, 1L << 51, 2L << 51, 1L << 52, (1L << 53) - 1 })
            {
                // 0 权重项是**精确 no-op**：把它挪到末尾、或整个去掉，"选中的行动"都不变
                // （下标会随之平移，所以按行动名比较）。`enemies.spire` 的 `twoTailedRat` 就是靠这条
                // 把"同一行动按 `when` 拆成两行"写成与源码单行 lambda 等价的。
                Check.Equal(Selected(value, plain, plainLabels), Selected(value, gated, labels),
                    $"bits={value}：门槛置 0 的行不改变命中行动（与去掉它相比）");
                Check.Equal(Selected(value, plain, plainLabels), Selected(value, zerosLast, zerosLastLabels),
                    $"bits={value}：0 权重项放在末尾也不改变命中行动");
            }
        });

        Check.Run("[ENV-T01 权重] 分桶算术 = 判别检查自带的第二份源语义转写（含 bits 全边界与随机采样）", () =>
        {
            var vectors = new[]
            {
                pair,
                fractional,
                gated,
                new[] { zero, zero },
                new[] { one },
                new[] { twelfth, one },
                // LINQ `IEnumerable<float>.Sum` 的 double 累加器判别向量：逐项 float32 相加会丢这 2。
                new[] { 16777216f, 1f, 1f },
            };
            var bits = new List<long> { 0, 1, (1L << 52) - 1, 1L << 52, (1L << 52) + 1, 268435456L + (1L << 52), 1L << 53 };
            var state = 0x9E3779B97F4A7C15UL;
            for (var index = 0; index < 256; index++)
            {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                bits.Add((long)(state % (1UL << 53)));
            }

            foreach (var weights in vectors)
            {
                foreach (var value in bits)
                {
                    var expected = SourcePick(value, weights);
                    var actual = SourceWeightedPick.Pick(value, weights);
                    Check.Equal(expected, actual,
                        $"bits={value} weights=[{string.Join(",", weights)}] 必须与源语义转写一致");
                }
            }

            var accumulator = new[] { 16777216f, 1f, 1f };
            var naive = 0f;
            var precise = 0.0;
            foreach (var weight in accumulator)
            {
                naive += weight;
                precise += weight;
            }

            Check.True((float)precise != naive,
                "判别向量必须真的能区分 double 累加器与逐项 float32 相加（否则上面那轮没有判别力）");
            Check.True(bits.Any(value => SourceWeightedPick.Pick(value, pair) != IntegerPick(value, pair)),
                "采样里必须存在 float32 与整数近似给出不同候选的 bits（判别力证据）");
        });

        Check.Run("[ENV-T01 权重] 分数权重/门槛置 0 的真实选取 = 独立复算，且整数近似会给出不同候选 [真实]", () =>
        {
            var (session, front, _) = Battle(FractionalSpire);
            var behavior = FixtureEnemy(FractionalSpire, "CalcifiedCultist").Behavior!;
            var replay = new MachineReplay(behavior, MonsterAiStream, SessionSeed, StreamSeed);
            var observed = new List<string> { Next(session, front) };
            var expected = new List<string> { replay.Roll() };
            var disagreements = 0;
            var draws = 0L;
            for (var turn = 0; turn < 9; turn++)
            {
                EndTurn(session);
                draws = Draws(session);
                replay.Perform();
                observed.Add(Next(session, front));
                expected.Add(replay.Roll());
                if (replay.LastIndex != IntegerPick(replay.LastBits, replay.LastWeights.ToArray()))
                {
                    disagreements++;
                }
            }

            Check.Sequence(expected, observed, "逐回合的已选行动必须与独立复算（源 float32 语义）一致");
            Check.Equal(9L, draws,
                "恰好消费 9 次：前排每次落到随机分支抽 1 次，后排是有序条件分支（0 次）——"
                + "这样单条复算器才能与共享的 `monster_ai` 流保持同步");
            Check.False(observed.Contains("machineBite", StringComparer.Ordinal),
                "`when` 恒假的候选权重必须是精确 0 ⇒ 永远不被选中");
            Check.True(disagreements > 0,
                "这段真实序列里必须存在「float32 分桶 ≠ 整数累积权重」的回合（判别力证据）");
            Check.True(observed.Count(move => move == "machineBackup") > observed.Count(move => move == "machineScratch"),
                $"0.75 的桶必须比 1/12 的桶更容易命中（实际：{string.Join(",", observed)}）");
        });

        Check.Run("[ENV-T01 身份] 权重是资产内容（进规则身份），且资产声明的后端由宿主强制", () =>
        {
            // `RuleSet.Identity` 是摘要（16 位十六进制），所以这里只能断言"不同 ⇒ 身份不同"。
            var one = Rules(WeightFixture("1")).Identity;
            var fractional = Rules(WeightFixture("0.75")).Identity;
            Check.True(!string.Equals(one, fractional, StringComparison.Ordinal),
                "只改一个权重字面量也必须改变规则身份（权重是 compute 资产的内容，不是注释）");

            // 声明的 `BackendId` 不是装饰：装错后端时执行必须显式故障（冻结契约
            // `docs/contracts/core-engine-baseline.md:57`：规则身份绑定后端；`developer-guide.md` §7.3：未安装后端 = Unsupported）。
            var wrong = new Session(Rules(FractionalSpire), SessionSeed, [SliceManifest.PlayerRole],
                randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
                randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
                randomProfile: VanillaRandomLayout.Profile,
                computeBackend: new WrongBackend());
            wrong.Random.SeedStream(MonsterAiStream, StreamSeed);
            wrong.Start();
            var fault = Check.Fault(EngineFaultCode.Unsupported, () => EndTurn(wrong),
                "资产声明的后端不是 `SourceWeightedPickBackend` ⇒ 执行期 Unsupported，而不是静默走别的算术");
            Check.True(fault.Message.Contains(SourceWeightedPickBackend.BackendId, StringComparison.Ordinal)
                    && fault.Message.Contains(SourceWeightedPick.AssetName, StringComparison.Ordinal),
                $"故障消息必须点名资产与声明的后端（实际：{fault.Message}）");

            // 反向证据：只有有序条件分支的敌人不声明加权分桶 ⇒ 一次 `monster_ai` 抽取都不发生。
            // 注意：这次回合开始会真的重洗（夹具牌组只有 5 张），而重洗现在会执行**另一个**包内 compute
            // 资产（源预排序），所以这里装正确后端、只断言"没有掷点"；"装错后端即 Unsupported" 已由上面
            // 那次执行（加权分桶资产）覆盖。
            var conditional = new Session(Rules(ConditionalSpire), SessionSeed, [SliceManifest.PlayerRole],
                randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
                randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
                randomProfile: VanillaRandomLayout.Profile,
                computeBackend: SourceWeightedPickBackend.Instance);
            conditional.Random.SeedStream(MonsterAiStream, StreamSeed);
            conditional.Start();
            EndTurn(conditional);
            Check.Equal(0L, Draws(conditional), "有序条件分支不掷点（加权分桶资产未被声明/执行）");
        });
    }

    /// <summary>
    /// ENV-T01（复核修正）：**真实内容**的分数权重证据与"尚未加载"的逐条原因。
    ///
    /// 全仓只有两份源敌人带非 1 的分数权重：`Fogmog.cs:46-47` 的 `0.4f`/`0.6f` 与
    /// `TwoTailedRat.cs:125-128` 的 `1f/12f`、`0.75f`。两者**都还没进包**（行动里用到未实现原语），
    /// 所以这里把三件事分开钉住，避免把"分数权重已支持"读成"这两只敌人已经在跑"：
    ///   ① 真实 `enemies.spire` 按**源数值**写成小数（不是把 0.4/0.6 转成整数比 2/3）；
    ///   ② 解析层能拿到的随机分支（Fogmog 的 `chooseWithLimits`）权重逐位等于源 float32；
    ///   ③ 未加载的事实与原因逐条点名（`summonEnemy` 等），遭遇被**显式排除**而不是静默消失。
    /// 能力侧的判别证据在 <see cref="RunWeightBucketChecks" />（合成夹具 + 手算表 + 后端强制）。
    /// </summary>
    private static void RunRealContentWeightChecks()
    {
        Check.Run("[ENV-T01 真实内容] 源里的分数权重按源写成小数、逐位等于 float32，且未加载原因逐条点名 [真实]", () =>
        {
            // ① 文本：两个分数权重声明按源数值写（0.4/0.6 与 1f/12f→0.083333336、0.75）。
            var fogmogSource = EnemySource("Fogmog");
            var text = EncounterContent.ReadText(fogmogSource.Resource);
            Check.True(text.Contains("candidate(fogmogSwipeRandom, weight=0.4, maxConsecutive=1)", StringComparison.Ordinal)
                    && text.Contains("candidate(fogmogHeadbutt, weight=0.6, maxConsecutive=1)", StringComparison.Ordinal),
                "Fogmog 的两个权重按源写 0.4/0.6（不是整数比 2/3）");
            Check.True(text.Contains(
                        "candidate(twoTailedRatScratchMove, weight=0.083333336, when=condition(SummonAvailable))",
                        StringComparison.Ordinal)
                    && text.Contains(
                        "candidate(twoTailedRatCallForBackup, weight=0.75, maxUsesPerCombat=1, "
                        + "when=condition(SummonAvailable))",
                        StringComparison.Ordinal),
                "Two-Tailed Rat 的 1f/12f 与 0.75 按九位十进制写成可精确往返的 0.083333336 与 0.75");
            // ② 解析层：把真实内容里的候选行**原样**交给生产前端解析，权重必须逐位等于源 float32。
            //    整只敌人的声明解析不出来（Fogmog 的 `summonEnemy`、Two-Tailed Rat 的
            //    `condition(SummonAvailable)` 都是致命问题），因此这里抽候选行做判别：
            //    权重字面量与门槛照抄内容，只替换未支持的原语（`moves` 用等价的攻击动作）。
            var fogmogLines = CandidateLines(text, "fogmogSwipeRandom")
                .Concat(CandidateLines(text, "fogmogHeadbutt")).ToList();
            Check.Equal(2, fogmogLines.Count, "真实内容里 Fogmog 的两个候选行都在");
            var ratLines = CandidateLines(text, "twoTailedRatScratchMove")
                .Concat(CandidateLines(text, "twoTailedRatCallForBackup")).ToList();
            Check.Equal(3, ratLines.Count, "真实内容里 Two-Tailed Rat 的三条分数/0.75 权重行都在");

            var fogmogParsed = ParseCandidateWeights(fogmogLines);
            Check.Sequence([0.4f, 0.6f], fogmogParsed.Select(entry => entry.Weight),
                "Fogmog：`0.4`/`0.6` 经生产前端解析后逐位等于源 `0.4f`/`0.6f`");
            Check.Sequence([1, 1], fogmogParsed.Select(entry => entry.Streak),
                "Fogmog：两个候选都是源里的 `MoveRepeatType.CannotRepeat`");
            Check.Sequence(["fogmogSwipeRandom", "fogmogHeadbutt"],
                fogmogParsed.Select(entry => entry.Move), "候选顺序照源（Fogmog.cs:46-47）");

            var ratParsed = ParseCandidateWeights(ratLines);
            Check.Sequence([0.083333336f, 1f, 0.75f], ratParsed.Select(entry => entry.Weight),
                "Two-Tailed Rat：`1f/12f`、`1f`、`0.75f` 逐位一致");
            Check.Equal(BitConverter.SingleToInt32Bits(1f / 12f),
                BitConverter.SingleToInt32Bits(ratParsed[0].Weight),
                "`0.083333336` 解析出的位型就是 `1f/12f`（0x3DAAAAAB）");
            Check.Equal(1, ratParsed[2].MaxUses, "召援候选是 `maxUsesPerCombat=1`（一次性）");
            Check.Equal(0, ratParsed[2].Streak, "召援候选没有连续上限（两个门槛是两件事）");

            var screech = ParseCandidateWeights(CandidateLines(text, "twoTailedRatScreechMove"));
            Check.Sequence([0.083333336f, 1f], screech.Select(entry => entry.Weight),
                "尖啸候选的两条权重行（可召援 `1f/12f` / 不可召援 `1`）");
            Check.Equal(1, screech[0].Streak, "尖啸候选是 `maxConsecutive=1`");
            Check.Equal(3, screech[0].Cooldown, "尖啸候选是 `cooldown=3`");

            // ③ 未加载的**事实**与**原因**：两条内容都不在已加载敌人里，原因逐条点名缺失原语。
            var content = SinglePlayerContentSource.Shared;
            Check.False(content.EnemyIds.Contains("base-spire:fogmog"),
                "Fogmog 尚未进入已加载敌人（行动用 `summonEnemy`）");
            Check.False(content.EnemyIds.Contains("base-spire:two-tailed-rat"),
                "Two-Tailed Rat 尚未进入已加载敌人");
            var fogmogReason = content.Unsupported.Single(entry => entry.Item == "enemy:Fogmog").Reason;
            Check.True(fogmogReason.Contains("summonEnemy", StringComparison.Ordinal),
                $"Fogmog 的未支持原因点名 `summonEnemy`（实际：{fogmogReason}）");
            var ratReason = content.Unsupported.Single(entry => entry.Item == "enemy:TwoTailedRat").Reason;
            foreach (var missing in new[]
                     {
                         "summonEnemy", "modifyCombatState", "modifyEnemyVariable",
                         "condition(SummonAvailable)", "variables", "conditions",
                     })
            {
                Check.True(ratReason.Contains(missing, StringComparison.Ordinal),
                    $"Two-Tailed Rat 的未支持原因点名 '{missing}'（实际：{ratReason}）");
            }

            Check.True(content.ExcludedEncounters.Any(entry =>
                    entry.Item == "base-spire:fogmog-normal"
                    && entry.Reason.Contains("未加载", StringComparison.Ordinal)),
                "Fogmog 的遭遇被**显式排除**（不静默消失）");
        });
    }

    // -- 夹具 -----------------------------------------------------------------------------------------------

    // 夹具用**显式种子**的 `monster_ai` 流：独立复算才能拿同一个种子重放（正式构建按原版公式种流）。
    private const ulong SessionSeed = 20261001UL;
    private const ulong StreamSeed = 0x5EED_1234UL;

    private static (Session Session, InstanceId Front, InstanceId Back) Battle(string enemySpire)
    {
        var session = new Session(Rules(enemySpire), SessionSeed, [SliceManifest.PlayerRole],
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile,
            computeBackend: SourceWeightedPickBackend.Instance);
        // 显式种 `monster_ai`：必须在第一次抽数之前（引擎会拒绝晚种）。
        session.Random.SeedStream(MonsterAiStream, StreamSeed);
        session.Start();
        return (session, SliceFixtures.Cultist(session, "base-spire:calcified-cultist"),
            SliceFixtures.Cultist(session, "base-spire:damp-cultist"));
    }

    /// <summary>故意声明错误 `Id` 的后端：验证"宿主必须装上资产声明的那个后端"。</summary>
    private sealed class WrongBackend : IComputeBackend
    {
        public string Id => "not-base-spire-source-float32";

        public void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs) =>
            throw new CheckFailedException("错误后端不应被执行（会话应在执行前拒绝）");
    }

    /// <summary>只链接切片规则集、不建会话：用于规则身份检查。</summary>
    private static RuleSet Rules(string enemySpire)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false,
            authorModel: Model(enemySpire)).Build());
        return builder.Build();
    }

    /// <summary>两个敌人、只有**一个权重字面量**不同的最小夹具。</summary>
    private static string WeightFixture(string weight) => $$"""
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(
              machineInit = attack(1, target=player),
              machineLash = attack(2, target=player)
            )
            behavior = startWith(machineInit, then=chooseWithLimits(
              candidate(machineLash, weight={{weight}})))
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(
              machineInit = attack(1, target=player),
              machineLash = attack(2, target=player)
            )
            behavior = startWith(machineInit, then=chooseWithLimits(
              candidate(machineLash, weight={{weight}})))
          }
        }
        """;

    /// <summary>
    /// 判别检查自带的**第二份**源语义转写（不调用 <see cref="SourceWeightedPick.Pick"/>）：
    /// 用显式 double 累加器复刻 LINQ `IEnumerable&lt;float&gt;.Sum`，乘法在 double 里做、只转一次 float32，
    /// 再按声明顺序做 float32 减法、`&lt;= 0f` 命中。
    /// </summary>
    private static int SourcePick(long bits, ReadOnlySpan<float> weights)
    {
        var precise = 0.0;
        foreach (var weight in weights)
        {
            precise += weight;
        }

        var number = (float)(bits * (1.0 / 9007199254740992.0) * (float)precise);
        for (var index = 0; index < weights.Length; index++)
        {
            number -= weights[index];
            if (number <= 0f)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// ENV-T01 **之前**的实现（`SampleInt(0, total)` + 整数累积权重与 `draw &lt; cumulative` 链），
    /// 只用来证明判别检查有判别力。`total == 0` 时源与旧实现都命中第一个候选。
    /// </summary>
    private static int IntegerPick(long bits, ReadOnlySpan<float> weights)
    {
        var total = 0L;
        foreach (var weight in weights)
        {
            total += (long)weight;
        }

        if (total == 0)
        {
            return 0;
        }

        var draw = (long)(bits * (1.0 / 9007199254740992.0) * (double)total);
        var cumulative = 0L;
        for (var index = 0; index < weights.Length; index++)
        {
            cumulative += (long)weights[index];
            if (draw < cumulative)
            {
                return index;
            }
        }

        return weights.Length - 1;
    }

    private static SliceAuthorModel Model(string enemySpire)
    {
        var author = SliceAuthorFrontend.Parse(FixtureText, "machine-fixture");
        var machine = SpireEnemySource.Parse(enemySpire, "machine-fixture.spire", PowerLinks());
        Check.Equal(2, machine.Enemies.Count, "夹具必须解析出两个敌人");
        return author with { Enemies = machine.Enemies };
    }

    private static AuthorEnemy Enemy(string name, bool includeUnsupportedPowers = false)
    {
        var loaded = SinglePlayerContentSource.Shared.Model.Enemies
            .FirstOrDefault(enemy => enemy.Name == name);
        if (loaded is not null && !includeUnsupportedPowers)
        {
            return loaded;
        }

        // 未加载的敌人（引用了尚未实现的 Power，例如 Flyconid 的 Frail）：为了让**形状**检查能跑，
        // 这里用同一份正式源资源 + 一张补全的名字→id 表解析它（只解析，不进运行时规则集）。
        foreach (var resource in SpireEnemySource.EnemyResources())
        {
            var source = SpireEnemySource.LoadResource(resource, PowerLinks(includeUnsupportedPowers));
            var match = source.Enemies.FirstOrDefault(enemy => enemy.Name == name);
            if (match is not null)
            {
                return match;
            }
        }

        throw new CheckFailedException($"正式源里没有敌人 '{name}'");
    }

    /// <summary>真实内容文本里某个行动的候选行（去空白，保持声明顺序）。</summary>
    private static IReadOnlyList<string> CandidateLines(string text, string move) =>
        [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith($"candidate({move},", StringComparison.Ordinal))];

    /// <summary>
    /// 把**真实内容里抄出来的**候选行交给生产前端（<see cref="SpireEnemySource.Parse" />）解析，取出权重、
    /// 重复门槛与行动名。未支持的 `when=` 子句被去掉（它在真实声明里就是当前不支持的部分），
    /// `moves` 用等价的最小攻击动作代替——**权重字面量与门槛照抄**，因此这条检查判别的正是
    /// "内容写的权重经生产前端解析后等于源的 float32"。
    /// </summary>
    private static List<(string Move, float Weight, int Streak, int MaxUses, int Cooldown)> ParseCandidateWeights(
        IReadOnlyList<string> lines)
    {
        var moves = lines.Select(line => line["candidate(".Length..].Split(',')[0].Trim())
            .Distinct(StringComparer.Ordinal).ToList();
        var text = $$"""
            package base-spire {
              enemy CalcifiedCultist {
                hp = range(60, 60)
                moves = actions(
            {{string.Join("\n", moves.Select(move => $"      {move} = attack(2, target=player),"))}}
                )
                behavior = startWith({{moves[0]}}, then=chooseWithLimits(
            {{string.Join("\n", lines.Select(WithoutWhen))}}
                ))
              }
            }
            """;
        var enemy = FixtureEnemy(text, "CalcifiedCultist");
        var branch = enemy.Behavior!.States.OfType<AuthorRandomState>().Single();
        return [.. branch.Candidates.Select(candidate =>
            (candidate.Move, candidate.Weight, candidate.Repeat.StreakLimit, candidate.MaxUsesPerCombat,
                candidate.Cooldown))];
    }

    /// <summary>去掉候选行里未支持的 `when=` 子句（其余部分逐字保留）。</summary>
    private static string WithoutWhen(string line)
    {
        var cut = line.IndexOf(", when=", StringComparison.Ordinal);
        if (cut < 0)
        {
            return line;
        }

        var trailingComma = line.EndsWith(",", StringComparison.Ordinal) ? "," : string.Empty;
        return line[..cut] + ")" + trailingComma;
    }

    /// <summary>
    /// 正式源里某个敌人**声明所在**的源（解析结果：含"能解析但未降级/未支持"的敌人，
    /// 正是 Fogmog / Two-Tailed Rat 这一类）。
    /// </summary>
    private static (string Resource, SpireEnemySource.Source Source) EnemySource(string name)
    {
        foreach (var resource in SpireEnemySource.EnemyResources())
        {
            var source = SpireEnemySource.LoadResource(resource, PowerLinks(includeUnsupported: true));
            if (source.Declared.Any(entry => entry.Name == name))
            {
                return (resource, source);
            }
        }

        throw new CheckFailedException($"正式源里没有敌人声明 '{name}'");
    }

    private static IReadOnlyDictionary<string, string> PowerLinks(bool includeUnsupported = false)
    {
        var links = SinglePlayerContentSource.Shared.Model.Powers
            .GroupBy(power => power.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
        if (includeUnsupported)
        {
            // 只用于**形状**检查：源码里声明了但运行时白名单还没实现的能力（Frail 等）也要能解析出图，
            // 因此这里按**声明名**补一张名字→id 表（不进运行时规则集，运行时的"引用未加载能力"检查不受影响）。
            foreach (var resource in SpirePowerSource.PowerResources()
                         .Concat(SpireEnemySource.EnemyResources())
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                var source = SpirePowerSource.LoadResource(resource);
                foreach (var (name, _) in source.Declared)
                {
                    links.TryAdd(name, $"base-spire:{SpireEnemySource.ToId(name)}");
                }
            }
        }

        return links;
    }

    private static AuthorMoveState Move(AuthorEnemy enemy, string move) =>
        enemy.Behavior!.MoveStates.Single(state => state.Move == move);

    /// <summary>夹具文本里的某个敌人（形状检查 / 独立复算用）。</summary>
    private static AuthorEnemy FixtureEnemy(string enemySpire, string name) =>
        SpireEnemySource.Parse(enemySpire, "machine-fixture.spire", PowerLinks())
            .Enemies.Single(enemy => enemy.Name == name);

    private static long Draws(Session session) => session.Random.DrawCount(MonsterAiStream);

    private static string Next(Session session, InstanceId monster) =>
        SliceFixtures.Text(session, monster, MonsterTypeId, "NextMove");

    private static void EndTurn(Session session)
    {
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                [session.PendingDecision!.Candidates[0]]));
        }
    }

    /// <summary>
    /// 独立复算器：按冻结源码的语义重放状态机（`MonsterMoveStateMachine` + `RandomBranchState`）：
    /// 首动不转换；否则走到行动为止；每次落到**随机分支**恰好抽一次；门槛从**完整日志**现算
    /// （`UseOnlyOnce` = 日志里出现过，`CannotRepeat`/`CanRepeatXTimes` = 尾部连续次数，`cooldown` =
    /// 最近 c 个行动里出现过）；权重全为 0 时仍然抽一次并命中第一个候选。
    /// ENV-T01：权重桶按源 **float32** 语义（`Sum` 的 double 累加器 + `NextFloat` + 逐项减法），
    /// 与生产侧的纯 compute 资产是两份独立实现。
    /// </summary>
    private sealed class MachineReplay(AuthorEnemyBehavior behavior, string stream, ulong sessionSeed, ulong streamSeed)
    {
        private readonly RandomService _random = Seed(stream, sessionSeed, streamSeed);
        private readonly List<string> _log = behavior.InitialIsMove ? [behavior.InitialMove] : [];
        private string _current = behavior.InitialState;
        private bool _performedFirstMove;

        /// <summary>冷却门槛真的把某个候选压成 0 的次数（判别力证据）。</summary>
        public int BlockedByCooldown { get; private set; }

        /// <summary>整个分支的分量全为 0、走了"仍然抽一次并命中第一个候选"的次数。</summary>
        public int BlockedFallbacks { get; private set; }

        /// <summary>ENV-T01 判别检查用：最近一次随机分支抽到的 53 位值。</summary>
        public long LastBits { get; private set; }

        /// <summary>ENV-T01 判别检查用：最近一次随机分支**生效后**的权重（门槛已置 0）。</summary>
        public List<float> LastWeights { get; private set; } = [];

        /// <summary>ENV-T01 判别检查用：最近一次随机分支命中的候选下标（未命中为 -1）。</summary>
        public int LastIndex { get; private set; } = -1;

        private static RandomService Seed(string stream, ulong sessionSeed, ulong streamSeed)
        {
            var random = new RandomService(sessionSeed, VanillaRandomLayout.AlgorithmId,
                VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
            random.SeedStream(stream, streamSeed);
            return random;
        }

        /// <summary>一次 `RollMove`：返回这次选出的行动。</summary>
        public string Roll()
        {
            if (_performedFirstMove || !IsMove(_current))
            {
                _current = Evaluate(FollowUp(_current));
                _log.Add(_current);
            }

            return _current;
        }

        /// <summary>`MonsterModel.PerformMove` → `OnMovePerformed`。</summary>
        public void Perform() => _performedFirstMove = true;

        private bool IsMove(string state) => behavior.States.Single(entry => entry.Id == state) is AuthorMoveState;

        private string FollowUp(string state) => behavior.States.Single(entry => entry.Id == state) switch
        {
            AuthorMoveState move => move.FollowUp,
            _ => state,
        };

        private string Evaluate(string state)
        {
            switch (behavior.States.Single(entry => entry.Id == state))
            {
                case AuthorMoveState move:
                    return move.Move;
                case AuthorOrderedState ordered:
                    foreach (var (move, condition) in ordered.Candidates)
                    {
                        if (Condition(condition))
                        {
                            return move;
                        }
                    }

                    throw new CheckFailedException("独立复算：有序条件分支没有命中（源码会抛错）");
                case AuthorRandomState random:
                {
                    // 独立复算**源 float32 语义**（`RandomBranchState.cs:115-128` + `Rng.cs:156-164`），
                    // 不复用生产侧的 `SourceWeightedPick`：
                    //   float max = States.Sum(GetStateWeight)   ← LINQ `IEnumerable<float>.Sum`（double 累加器，末尾一次转 float32）
                    //   float num = rng.NextFloat(max)           ← `(float)(NextDouble() * (double)max)`
                    //   foreach: num -= weight; if (num <= 0f) return 该候选
                    var weights = random.Candidates.Select(Weight).ToList();
                    var max = weights.Sum();
                    if (max == 0f)
                    {
                        // 原版 `NextFloat(0)` 仍然消费一次抽取；`num = 0f` 让第一个候选（权重 0）
                        // 在 `0f - 0f <= 0f` 处命中。
                        BlockedFallbacks++;
                    }

                    // 源 `NextDouble()`：与 `VanillaRandomProfile.DrawDouble` 同一条 53 位恒等式
                    // （`bits = rd * 2^53`，`rd` 是 2^-53 的整数倍，因此这里逐位还原）。
                    var bits = _random.DrawInt(stream, 0, 1L << 53);
                    LastBits = bits;
                    LastWeights = weights;
                    LastIndex = -1;
                    var num = (float)(bits * (1.0 / 9007199254740992.0) * (double)max);
                    for (var index = 0; index < weights.Count; index++)
                    {
                        num -= weights[index];
                        if (num <= 0f)
                        {
                            LastIndex = index;
                            return random.Candidates[index].Move;
                        }
                    }

                    throw new CheckFailedException(
                        "独立复算：随机分支没有命中（源码会抛 InvalidOperationException）");
                }
                default:
                    throw new CheckFailedException($"独立复算：状态 '{state}' 不支持");
            }
        }

        private float Weight(AuthorMoveCandidate candidate)
        {
            if ((candidate.Repeat.Kind == EnemyRepeatKinds.Once || candidate.MaxUsesPerCombat == 1)
                && _log.Contains(candidate.Move, StringComparer.Ordinal))
            {
                return 0f;
            }

            var limit = candidate.Repeat.StreakLimit;
            if (limit > 0)
            {
                var tail = 0;
                for (var index = _log.Count - 1; index >= 0 && _log[index] == candidate.Move; index--)
                {
                    tail++;
                }

                if (tail >= limit)
                {
                    return 0f;
                }
            }

            if (candidate.Cooldown > 0
                && _log.Skip(Math.Max(0, _log.Count - candidate.Cooldown)).Contains(candidate.Move, StringComparer.Ordinal))
            {
                BlockedByCooldown++;
                return 0f;
            }

            if (candidate.When is { } condition && !Condition(condition))
            {
                return 0f;
            }

            return candidate.Weight;
        }

        /// <summary>夹具里的条件只有"自身生命"与"回合数"两类；数值由夹具固定，因此这里就地取值。</summary>
        private bool Condition(AuthorEnemyCondition condition) => condition switch
        {
            TurnAtLeastCondition turn => turn.Turn <= 0,
            EnemyHpBelowCondition => false,
            _ => throw new CheckFailedException($"独立复算：条件 '{condition.Describe()}' 不在夹具范围内"),
        };
    }
}
