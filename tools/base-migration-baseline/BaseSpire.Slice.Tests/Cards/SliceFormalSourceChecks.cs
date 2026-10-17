using BaseSpire.Slice;
using CoreTheSpire;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// W-06：**正式 0.0.1 卡牌源 → 通用 Engine** 的纵向证据。
///
/// 这条路径与切片原型 `.author` **共用同一个作者模型与同一条 IR/加载/执行路径**：
/// 正式 `ContentSource/*-cards.spire`（嵌入资源）→ 正式解析器 AST（`DslSourceParser`）
/// → <see cref="SpireCardSource"/> 降级成 `AuthorCard` → `CombatSlicePackage`（Engine IR）→ 会话执行。
///
/// 覆盖口径按**四态分开**：声明（源文件里有多少 `card`）/ 解析 / 降级 / 未支持（附原因）；
/// 行为证据只来自**真的跑起来**的牌，不用声明数冒充加载数或行为数。
/// </summary>
internal static class SliceFormalSourceChecks
{
    /// <summary>本批次真正降级并逐张验证行为的代表牌。</summary>
    private static readonly string[] BehaviorVerifiedIds =
        ["strike-ironclad", "defend-ironclad", "bludgeon", "giant-rock", "impervious", "slimed", "wound",
            "dazed", "iron-wave", "bash", "pommel-strike", "inflame", "breakthrough", "body-slam",
            "ashen-strike", "expect-a-fight", "entrench", "feel-no-pain", "rage", "barricade", "tank",
            "flame-barrier", "demon-form", "corruption", "unrelenting", "juggernaut", "infection", "uppercut",
            "spite", "evil-eye", "twin-strike", "headbutt", "burning-pact", "brand", "sword-boomerang",
            "true-grit", "cinder", "armaments", "not-yet", "feed", "dominate", "bully",
            // ENV-02：Colossus 的减伤能力带"来源条件"（攻击者身上有易伤），行为证据见
            // SliceFrontendColossusChecks（三组对照，含"条件挂在 dealer 而不是 target"）。
            "colossus",
            // ENV-02：BattleTrance 施加的 NoDraw（抽不了牌），行为证据见 SliceFrontendNoDrawChecks。
            "battle-trance",
            // ENV-02：Pyre 的最大能量加成（每层 +1），行为证据见 SliceFrontendPyreChecks。
            "pyre",
            // ENV-02：Cruelty 抬高易伤倍率（攻击者每层 +1 个百分点），见 SliceFrontendCrueltyChecks。
            "cruelty",
            // ENV-02：StoneArmor 施加的 Plating（回合开始递减 + 回合末按层数给格挡），见 SliceFrontendPlatingChecks。
            "stone-armor",
            // ENV-02：SetupStrike 施加的临时力量（施加时 +2、回合末移除并回补），见 SliceFrontendTemporaryStrengthChecks。
            "setup-strike",
            // ENV-02：Mangle 的尾部条件段 + targetAlive + 负号临时力量，见 SliceFrontendManglePowerChecks。
            "mangle",
            // ENV-02：MoltenFist 的**嵌套**条件段 + targetAlive + hasPower，见 SliceFrontendMoltenFistChecks。
            "molten-fist",
            // ENV-02：Unmovable 的 blockGainModifiers（前 N 次由牌获得格挡翻倍），见 SliceFrontendUnmovableChecks。
            "unmovable",
            // ENV-02：Rampage 的卡牌实例变量（伤害读变量 + 每次打出成长），见 SliceFrontendRampageVariableChecks。
            "rampage",
            // ENV-02：Vicious 的 power 事件（施加易伤后按层数抽牌），见 SliceFrontendViciousChecks。
            "vicious"];

    /// <summary>
    /// 自报"行为已验证"的内容 id —— ENV-06 的 <c>behavior_verified</c> 列由这份**机器可读产物**给出，
    /// 而不是让结算工具去解析测试源码。
    ///
    /// 自报同时做一致性检查：每个 id 必须真的出现在正式前端的**降级集**里；
    /// 否则就是"声称验证了没加载的内容"，直接失败（这比事后对账更早暴露问题）。
    /// </summary>
    public static (IReadOnlyList<string> Ids, IReadOnlyList<string> NotLowered) BehaviorReport()
    {
        var links = PowerLinks();
        var lowered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in SpireCardSource.CardResources())
        {
            foreach (var card in SpireCardSource.LoadResource(resource, links).Cards)
            {
                lowered.Add(Unqualified(card.Id));
            }
        }

        foreach (var resource in SpirePowerSource.PowerResources())
        {
            foreach (var power in SpirePowerSource.LoadResource(resource, links).Powers)
            {
                lowered.Add(Unqualified(power.Id));
            }
        }

        return (BehaviorVerifiedIds, [.. BehaviorVerifiedIds.Where(id => !lowered.Contains(id))]);
    }

    private static void RunBehaviorVerifiedReport()
    {
        Check.Run("W-06 正式源：自报的'行为已验证'内容 id 都在正式降级集里 [真实]", () =>
        {
            var (ids, notLowered) = BehaviorReport();
            Check.True(ids.Count > 0, "自报清单不能为空");
            Check.True(notLowered.Count == 0,
                $"以下 id 声称已验证却不在降级集里：{string.Join(",", notLowered)}");
        });
    }

    public static void Run()
    {
        RunBehaviorVerifiedReport();
        Check.Run("W-06 正式源：嵌入资源枚举到全部 `*-cards.spire`（真实加载入口）", () =>
        {
            var resources = SpireCardSource.CardResources();
            Check.True(resources.Contains("BaseSpire.ContentSource.ironclad-cards.spire"),
                $"资源清单：{string.Join(",", resources)}");
            Check.True(resources.Contains("BaseSpire.ContentSource.status-cards.spire"),
                "状态牌源也在其中");
        });

        Check.Run("W-06 正式源：声明/解析/降级/未支持**四态分开**，降级集确定 [真实]", () =>
        {
            var ironclad = SpireCardSource.LoadResource("BaseSpire.ContentSource.ironclad-cards.spire", PowerLinks());
            var status = SpireCardSource.LoadResource("BaseSpire.ContentSource.status-cards.spire", PowerLinks());
            Console.WriteLine($"  {ironclad.Summary}");
            Console.WriteLine($"  {status.Summary}");
            Console.WriteLine("  unsupported sample: "
                + string.Join(" | ", ironclad.Unsupported.Take(3).Select(entry => $"{entry.Name}: {entry.Reason}")));
            Console.WriteLine("  representative problems: "
                + string.Join(" | ", ironclad.Unsupported.Concat(status.Unsupported)
                    .Where(entry => BehaviorVerifiedIds.Contains(SpireCardSource.ToId(entry.Name), StringComparer.Ordinal))
                    .Select(entry => $"{entry.Name}: {entry.Reason}")));
            var linkFailures = ironclad.Unsupported
                .Where(entry => entry.Reason.Contains("无法链接", StringComparison.Ordinal)).ToList();
            Console.WriteLine($"  链接失败（引用了本模型未声明的能力）: {linkFailures.Count} 张，例："
                + string.Join(", ", linkFailures.Take(4).Select(entry => entry.Name)));
            foreach (var powerResource in SpirePowerSource.PowerResources())
            {
                var powerSource = SpirePowerSource.LoadResource(powerResource, PowerIds());
                Console.WriteLine($"  {powerSource.Summary}");
                Console.WriteLine("    未支持: " + string.Join(" | ", powerSource.Unsupported.Take(4)
                    .Select(entry => $"{entry.Name}: {entry.Reason}")));
                Console.WriteLine("    已降级: " + string.Join(",", powerSource.Powers.Select(power => power.Id)));
                var filter = Environment.GetEnvironmentVariable("W06_DUMP_POWER");
                if (!string.IsNullOrEmpty(filter))
                {
                    foreach (var entry in powerSource.Unsupported.Where(entry =>
                                 entry.Name.Contains(filter, StringComparison.Ordinal)))
                    {
                        Console.WriteLine($"    POWERDIAG {entry.Name} = {entry.Reason}");
                    }
                }
            }

            // 敌人源里也声明了能力（`enemies*.spire` 的 `power` 块）：同一个诊断要能覆盖它们。
            foreach (var enemyResource in SpireEnemySource.EnemyResources())
            {
                var enemyPowerSource = SpirePowerSource.LoadResource(enemyResource, PowerIds());
                var enemyFilter = Environment.GetEnvironmentVariable("W06_DUMP_POWER");
                if (string.IsNullOrEmpty(enemyFilter))
                {
                    continue;
                }

                foreach (var entry in enemyPowerSource.Unsupported.Where(entry =>
                             entry.Name.Contains(enemyFilter, StringComparison.Ordinal)))
                {
                    Console.WriteLine($"    POWERDIAG {enemyResource} {entry.Name} = {entry.Reason}");
                }

                foreach (var power in enemyPowerSource.Powers.Where(power =>
                             power.Id.Contains(enemyFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"    POWERDIAG-LOADED {power.Id}");
                }
            }

            // 声明数是**从源文件数出来的**，不是写死的常量。
            Check.True(ironclad.DeclaredCount > 80, $"铁甲战士源声明了 {ironclad.DeclaredCount} 张卡");
            Check.True(new[] { "base-spire:dazed", "base-spire:beckon", "base-spire:debris" }.All(id => status.Cards.Any(c => c.Id == id)),
                "required native status definitions actually lower; extra status content is allowed");
            Check.Equal(status.DeclaredCount, status.LoweredCount, "every declared status actually lowers");

            Check.True(ironclad.ParsedCount <= ironclad.DeclaredCount, "解析数不得超过声明数");
            Check.True(ironclad.LoweredCount <= ironclad.ParsedCount, "降级数不得超过解析数");
            Check.True(ironclad.Unsupported.Count >= ironclad.ParsedCount - ironclad.LoweredCount,
                "未支持清单覆盖每一个未降级的牌（附原因）");
            Check.True(ironclad.Unsupported.All(entry => !string.IsNullOrWhiteSpace(entry.Reason)),
                "未支持项必须带原因");
            if (Environment.GetEnvironmentVariable("W06_DUMP_UNSUPPORTED") == "1")
            {
                foreach (var entry in ironclad.Unsupported)
                {
                    Console.WriteLine($"    DIAG {entry.Name} = {entry.Reason}");
                }
            }

            var ids = ironclad.LoweredIds.Concat(status.LoweredIds).ToHashSet(StringComparer.Ordinal);
            Check.True(BehaviorVerifiedIds.All(ids.Contains),
                $"代表牌都已降级：{string.Join(",", BehaviorVerifiedIds.Where(id => !ids.Contains(id)))}");

            // selectNone is now supported; the existing empty-play status and its hand-end hook must survive lowering.
            var beckon = status.Cards.Single(card => card.Id == "base-spire:beckon");
            Check.Equal(1, beckon.Cost, "Beckon keeps its real empty-play cost");
            Check.True(beckon.NormalHooks.Has(AuthorSteps.HookTurnEndInHand), "Beckon keeps its native hand-end hook");
            Console.WriteLine($"  {ironclad.Summary}");
            Console.WriteLine($"  {status.Summary}");
        });

        Check.Run("W-06 正式源：AST 声明集与旧后端路径一致（同一解析器，双后端）", () =>
        {
            // 新路径（Slice/SpireCardSource）与旧后端路径（Legacy/DslSourceParser 的消费者）
            // 读的是**同一个通用解析器**产出的 AST：声明集必须逐一相同（任一侧不再接受某张卡都会失败）。
            foreach (var resource in SpireCardSource.CardResources())
            {
                var assembly = typeof(SpireCardSource).Assembly;
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                var mine = SpireCardSource.Parse(text, resource);
                var legacy = DslSourceParser.Parse(text, resource);
                var legacyNames = legacy.Members.OfType<DslDeclarationNode>()
                    .Where(node => node.Kind == "card")
                    .Select(node => node.Name)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                var myNames = mine.DeclaredNames.Order(StringComparer.Ordinal).ToList();
                Check.Equal(legacyNames.Count, myNames.Count,
                    $"{resource}: 两个解析器的声明数必须一致");
                Check.True(legacyNames.SequenceEqual(myNames, StringComparer.Ordinal),
                    $"{resource}: 声明名集合必须一致"
                    + $"(仅旧有 {string.Join(",", legacyNames.Except(myNames))}"
                    + $" / 仅新有 {string.Join(",", myNames.Except(legacyNames))})");
            }
        });

        Check.Run("W-06 正式源：能力引用要**链接**（名字→id）；链接不上记为未支持而不是猜 id [真实]", () =>
        {
            var links = PowerLinks();
            var linked = SpireCardSource.LoadResource(
                "BaseSpire.ContentSource.ironclad-cards.spire", links);
            // 链接表里有 `NoEnergyGainPower` → `base-spire:no-energy-gain`：ExpectAFight 因此可降级，
            // 且它的能力 id 是**表里的值**（不是把名字 kebab-case 出来的 `no-energy-gain-power`）。
            Check.True(links.ContainsKey("NoEnergyGainPower"), "链接表里有 NoEnergyGainPower");
            var expect = linked.Cards.Single(card => card.Id == "base-spire:expect-a-fight");
            Check.Equal("base-spire:no-energy-gain", expect.PowerId,
                "能力 id 来自**链接表**（与冻结内容一致）");

            // JugglingPower 现在由正式能力源声明并链接；它的规则由正式前端降级为 cloneThirdAttack 行为。
            var jugglingCard = linked.Cards.Single(card => card.Id == "base-spire:juggling");
            Check.Equal("base-spire:juggling-power", jugglingCard.PowerId,
                "能力 id 来自正式能力源链接表");
            Check.True(linked.LoweredIds.Contains("juggling"), "Juggling 已在正式能力声明可用后进入降级集");

            // 真正未声明的能力仍必须记为未支持，不能猜一个 kebab-case id。
            var missingPowerSource = SpireCardSource.LoadResource(
                "BaseSpire.ContentSource.ironclad-cards.spire",
                new Dictionary<string, string>(links, StringComparer.Ordinal)
                {
                    ["MissingPower"] = "base-spire:missing-power"
                });
            Check.True(missingPowerSource.Unsupported.All(entry =>
                    !entry.Reason.Contains("猜", StringComparison.Ordinal)),
                "未支持能力必须保留明确原因");
        });

        Check.Run("W-06 正式源：多原语按**源内顺序**降级；未支持的构造被明确记录（不静默丢弃）", () =>
        {
            var text = """
                using namespace base-spire;
                card TwoPrimitives {
                    normal = {
                        type = attack
                        rarity = common
                        cost = 1
                        target = { selectOne = { from = context.opponents where = alive } }
                        keywords = { }
                        play = { gainBlock = { amount = 5 } attack = { damage = 6 } }
                        hooks = { }
                    }
                    upgraded = {
                        type = attack
                        rarity = common
                        cost = 1
                        target = { selectOne = { from = context.opponents where = alive } }
                        keywords = { }
                        play = { gainBlock = { amount = 8 } attack = { damage = 9 } }
                        hooks = { }
                    }
                }
                card XCostCard {
                    normal = {
                        type = attack
                        rarity = common
                        cost = X
                        target = { selectOne = { from = context.opponents where = alive } }
                        keywords = { }
                        play = { attack = { damage = 6 } }
                        hooks = { }
                    }
                    upgraded = {
                        type = attack
                        rarity = common
                        cost = X
                        target = { selectOne = { from = context.opponents where = alive } }
                        keywords = { innate }
                        play = { attack = { damage = 9 } }
                        hooks = { }
                    }
                }
                card UnknownPrimitive {
                    normal = {
                        type = skill
                        rarity = common
                        cost = 1
                        target = { selectSelf = {} }
                        keywords = { }
                        play = { neverImplementedEffect = { from = hand } }
                        hooks = { }
                    }
                    upgraded = {
                        type = skill
                        rarity = common
                        cost = 1
                        target = { selectSelf = {} }
                        keywords = { }
                        play = { neverImplementedEffect = { from = hand } }
                        hooks = { }
                    }
                }
                """;
            var source = SpireCardSource.Parse(text, "unit/formal-three.spire");
            Check.Equal(3, source.DeclaredCount, "声明三张");
            Check.Equal(2, source.LoweredCount, "多原语与 X 费卡均已降级");

            // 多原语：目标步骤在最前、其余按**源内顺序**（`gainBlock` 在 `attack` 之前）。
            var two = source.Cards.Single(card => card.Id == "base-spire:two-primitives");
            Check.True(two.NormalSteps.Select(step => step.Kind)
                    .SequenceEqual(["target", "block", "damage"], StringComparer.Ordinal),
                $"多原语步骤顺序：{string.Join(",", two.NormalSteps.Select(step => step.Kind))}");
            Check.Equal(6, two.NormalAmount,
                "变体级 amount 取**攻击**数值（与冻结 `.author` 的 IronWave/Bash 口径一致）");

            Check.True(source.LoweredIds.Contains("x-cost-card"), "X 费卡已进入降级集");
            var unknownReason = source.Unsupported.Single(entry => entry.Name == "UnknownPrimitive").Reason;
            Check.True(unknownReason.Contains("未支持", StringComparison.Ordinal),
                $"未知原语必须点名未支持：{unknownReason}");
        });

        Check.Run("W-06 正式源：基础/升级伤害与格挡按声明生效，且来源逐字来自正式源 [真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var monsterType = session.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var startHp = session.State.GetField(target, monsterType.FieldIndex("Hp")).AsInt();

            var strike = SliceFixtures.PutInHand(session, "base-spire:strike-ironclad", upgraded: false);
            Play(session, "strike/normal", strike, target);
            Check.Equal(startHp - 6, session.State.GetField(target, monsterType.FieldIndex("Hp")).AsInt(),
                "正式源 StrikeIronclad 普通形态造成 6 点");

            var upgradedStrike = SliceFixtures.PutInHand(session, "base-spire:strike-ironclad", upgraded: true);
            Play(session, "strike/upgraded", upgradedStrike, target);
            Check.Equal(startHp - 6 - 9, session.State.GetField(target, monsterType.FieldIndex("Hp")).AsInt(),
                "正式源 StrikeIronclad 升级形态造成 9 点（升级数值逐字来自源）");

            var player = SliceFixtures.Player(session);
            var protect = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            Play(session, "defend/normal", protect, player);
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "正式源 DefendIronclad 普通形态给 5 点格挡");

            var upgradedProtect = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: true);
            // 受控检查：前三张牌已经花掉 3 点能量，这里补足能量以便打出第四张（不改变任何卡牌行为）。
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(3L)));
            Play(session, "defend/upgraded", upgradedProtect, player);
            Check.Equal(13L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "升级形态给 8 点（累计 13）");
        });

        Check.Run("W-06 正式源：`keywords = { exhaust }` 降级成消耗（进入消耗区而非弃牌堆）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var card = SliceFixtures.PutInHand(session, "base-spire:impervious", upgraded: false);
            SliceFixtures.PlayAt(session, card, player);

            Check.Equal(30L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "Impervious 给 30 点格挡（数值来自正式源）");
            Check.True(session.State.CollectionMembers(SliceFixtures.Collection(session, "base-spire:ExhaustPile"))
                    .Contains(card),
                "关键词 exhaust ⇒ 牌进消耗区");
            Check.False(session.State.CollectionMembers(SliceFixtures.Collection(session, "base-spire:DiscardPile"))
                    .Contains(card),
                "并且不在弃牌堆");
        });

        Check.Run("W-06 正式源：`keywords = { unplayable }` 的牌不作为可出牌候选 [真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var wound = SliceFixtures.PutInHand(session, "base-spire:wound", upgraded: false);
            var playable = PlayCardCandidates(session);
            Check.False(playable.Contains(wound), "Wound（unplayable）不在 PlayCard 候选里");

            // 对照：同一副牌组里的可打出牌仍在候选里（证明候选不是空的）。
            var strike = SliceFixtures.PutInHand(session, "base-spire:strike-ironclad", upgraded: false);
            var candidates = PlayCardCandidates(session);
            Check.True(candidates.Contains(strike), "可打出的打击仍在候选里");

            // 强制提交也必须被拒（guard 真的在把关，而不只是候选列表好看）。
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            var statusBefore = session.Status;
            try
            {
                session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(wound)]);
                Check.True(false, "强制提交 unplayable 的牌必须被拒绝");
            }
            catch (EngineFaultException failure)
            {
                Check.True(failure.Message.Contains("guard", StringComparison.OrdinalIgnoreCase),
                    $"拒绝原因应指向 guard：{failure.Message}");
            }

            Check.Equal(statusBefore, session.Status, "被拒绝的提交不改变会话状态");
        });

        Check.Run("W-06 正式源：`draw` 原语按声明抽牌，且消耗型状态牌照常消耗 [真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");

            var slimed = SliceFixtures.PutInHand(session, "base-spire:slimed", upgraded: false);
            var before = session.State.CollectionMembers(hand).Count();
            SliceFixtures.PlayAt(session, slimed, player);

            var after = session.State.CollectionMembers(hand).Count();
            Check.Equal(before, after, "打出 Slimed（draw 1 + exhaust）后手牌数不变：打出 1 张、抽 1 张");
            Check.True(session.State.CollectionMembers(SliceFixtures.Collection(session, "base-spire:ExhaustPile"))
                    .Contains(slimed),
                "Slimed 同时带 exhaust ⇒ 进消耗区");
        });

        Check.Run("W-06 正式源：`selectSelf` 的牌不需要目标（目标语义随声明降级）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var protect = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);

            Check.Equal("self", DeclaredTarget("defend-ironclad"), "DefendIronclad 声明 selectSelf");
            Check.Equal("one-opponent-alive", DeclaredTarget("strike-ironclad"),
                "StrikeIronclad 声明 selectOne/opponents/alive");

            // 自己为目标的牌打出后**不产生目标提问**（会话继续可操作）。
            SliceFixtures.PlayAt(session, protect, player);
            Check.Equal(SessionStatus.Running, session.Status, "打出 selectSelf 的牌不需要选择敌方目标");
            _ = cardType;
        });

        Check.Run("W-06 正式源：多原语牌按源内顺序生效（IronWave：先格挡后伤害）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var startHp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            var wave = SliceFixtures.PutInHand(session, "base-spire:iron-wave", upgraded: false);
            Play(session, "iron-wave", wave, target);

            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "先结算 gainBlock（5）");
            Check.Equal(startHp - 5, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "再结算 attack（5）");
        });

        Check.Run("W-06 正式源：`applyPower` 打在**所选目标**上，且顺序是「先伤害后能力」 [真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var other = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var startHp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            var bash = SliceFixtures.PutInHand(session, "base-spire:bash", upgraded: false);
            Play(session, "bash", bash, target);

            // 顺序判据：伤害在易伤**之前** ⇒ 8 点（若顺序颠倒会是 8×1.5=12）。
            Check.Equal(startHp - 8, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "Bash 先打 8 点（不是被自己的易伤放大后的 12）");
            Check.Equal(2L, Stacks(session, target, "base-spire:vulnerable"), "所选目标获得 2 层易伤");
            Check.Equal(0L, Stacks(session, other, "base-spire:vulnerable"), "另一个敌人**没有**拿到（不是全体）");

            // 后续打击被易伤放大：6 × 1.5 = 9（证明能力真的生效在自己的目标上）。
            var strike = SliceFixtures.PutInHand(session, "base-spire:strike-ironclad", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            Play(session, "strike-after-bash", strike, target);
            Check.Equal(9L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "易伤生效：后续打击 6 → 9");
        });

        Check.Run("W-06 正式源：`applyPower`（自己）与 `attack`+`draw` 组合 [真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var startHp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            var pommel = SliceFixtures.PutInHand(session, "base-spire:pommel-strike", upgraded: false);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var handBefore = session.State.CollectionMembers(hand).Count();
            Play(session, "pommel-strike", pommel, target);
            Check.Equal(startHp - 9, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "PommelStrike 打 9 点");
            Check.Equal(handBefore, session.State.CollectionMembers(hand).Count(),
                "打出 1 张 + 抽 1 张 ⇒ 手牌数不变");

            var inflame = SliceFixtures.PutInHand(session, "base-spire:inflame", upgraded: false);
            Play(session, "inflame", inflame, player);
            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId),
                "Inflame（`target = context.owner`）给自己 2 层力量");
            Check.Equal(0L, Stacks(session, target, SliceManifest.StrengthId), "敌人没有拿到");
        });

        Check.Run("W-06 正式源：`loseHp` + `target = allEnemies` 的全体攻击（Breakthrough）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var playerHp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            var frontHp = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backHp = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");
            // 先给玩家一点格挡：`lose_hp` 是**不经格挡**的 HP 损失（来源 properties = unblockable）。
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Block"), Value.Of(5L)));

            var card = SliceFixtures.PutInHand(session, "base-spire:breakthrough", upgraded: false);
            // 全体攻击不需要选目标：直接提交。
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);

            Check.Equal(playerHp - 1, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "自损 1 点走 HP 损失（格挡不吸收）");
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "格挡没有被自损消耗");
            Check.Equal(frontHp - 9, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "前排 -9");
            Check.Equal(backHp - 9, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "后排 -9（`target = allEnemies` 降级为 damage_all）");
        });
        Check.Run("W-06 正式源：`block-count` 数值来源（BodySlam 打=自己的格挡；Entrench 格挡翻倍）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var startHp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            // 先靠 Defend 攒 5 点格挡。
            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            Play(session, "defend-before-body-slam", defend, player);
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "先有 5 点格挡");

            // BodySlam：`damageValue = { scaled = { base = 0 perCount = 1 count = { block } } }` ⇒ 伤害 = 当前格挡。
            var slam = SliceFixtures.PutInHand(session, "base-spire:body-slam", upgraded: false);
            Play(session, "body-slam", slam, target);
            Check.Equal(startHp - 5, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "BodySlam 打出的伤害等于**当时**的格挡（5），不是某个字面量");

            // Entrench：`gainBlockFromCurrentBlock` ⇒ 格挡 = 当前格挡（加倍）。
            var entrench = SliceFixtures.PutInHand(session, "base-spire:entrench", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(3L)));
            Play(session, "entrench", entrench, player);
            Check.Equal(10L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "Entrench 把 5 点格挡变成 10 点（数值来源 = 当前格挡）");
        });

        Check.Run("W-06 正式源：`exhaust-count` 数值来源（AshenStrike = 6 + 3×消耗数）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var startHp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            // 消耗堆为空时：伤害 = base 6。
            var first = SliceFixtures.PutInHand(session, "base-spire:ashen-strike", upgraded: false);
            Play(session, "ashen-strike/empty-exhaust", first, target);
            Check.Equal(startHp - 6, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "消耗堆为空 ⇒ 只打 base 6 点");

            // 消耗两张牌（Impervious 与 Slimed 都带 exhaust）后：伤害 = 6 + 3×2 = 12。
            var player = SliceFixtures.Player(session);
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(9L)));
            foreach (var (id, label) in new[] { ("base-spire:impervious", "exhaust-1"), ("base-spire:slimed", "exhaust-2") })
            {
                var card = SliceFixtures.PutInHand(session, id, upgraded: false);
                Play(session, label, card, player);
            }

            var second = SliceFixtures.PutInHand(session, "base-spire:ashen-strike", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            Play(session, "ashen-strike/two-exhausted", second, target);
            Check.Equal(12L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "消耗 2 张 ⇒ 6 + 3×2 = 12（数值来源随公开状态变化）");
        });

        Check.Run("W-06 正式源：`hand-count:Attack` 数值来源（ExpectAFight 能量=手牌攻击数）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            var expect = SliceFixtures.PutInHand(session, "base-spire:expect-a-fight", upgraded: false);
            // 受控输入：能量正好等于它的费用（2）⇒ 打完后能量 0，再按"手牌攻击数"获得能量。
            session.State.RunAtomic(() => session.State.SetField(player,
                playerType.FieldIndex("Energy"), Value.Of(2L)));

            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            // 受控卡组里起手抽到的攻击牌就是 5 张打击（`Kind` 是枚举字段，因此按 DefinitionId 数）。
            var attacksInHand = session.State.CollectionMembers(hand).Count(card =>
                session.State.GetField(card, cardType.FieldIndex("DefinitionId")).AsString()
                == SliceManifest.StrikeId);
            Check.Equal(5, attacksInHand, "手上 5 张攻击牌（起手抽到的打击）");

            Play(session, "expect-a-fight", expect, player);
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "能量 = 手牌里的攻击牌数（base 0 + perCount 1 × 5）");
            Check.Equal(1L, Stacks(session, player, "base-spire:no-energy-gain"),
                "同时给自己 1 层 NoEnergyGainPower（与来源一致：先加能量、再施加限制）");
        });

        Check.Run("W-06 正式源：FeelNoPain 的**消耗触发**按声明给格挡（正式能力源）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            // FeelNoPain（正式卡）声明 `trigger = afterCardExhausted` +
            // `effect = gainBlock(1, perPowerStack=FeelNoPain)`，能力来自正式能力源。
            var feel = SliceFixtures.PutInHand(session, "base-spire:feel-no-pain", upgraded: false);
            Play(session, "feel-no-pain", feel, player);
            Check.Equal(3L, Stacks(session, player, "base-spire:feel-no-pain"), "获得 3 层 FeelNoPain");

            var blockBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            var impervious = SliceFixtures.PutInHand(session, "base-spire:impervious", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"), Value.Of(6L)));
            Play(session, "impervious-exhausts", impervious, player);
            Check.True(session.State.CollectionMembers(SliceFixtures.Collection(session, "base-spire:ExhaustPile"))
                    .Contains(impervious),
                "Impervious 确实进了消耗区（触发条件成立）");
            Check.Equal(blockBefore + 30 + 3,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "消耗触发：Impervious 的 30 点格挡 + FeelNoPain 3 层 × 1 = 3 点");
        });

        Check.Run("W-06 正式源：RagePower 的**打出攻击后触发**与回合末移除（正式能力源）[真实]", () =>
        {
            var model = BuildModel();
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            // Rage：`trigger = afterCardPlayed`、`whenCardType = attack`、
            // `gainBlock(1, perPowerStack=Rage)`、`removeAfterPlayerTurnEnd = true`。
            var rage = SliceFixtures.PutInHand(session, "base-spire:rage", upgraded: false);
            Play(session, "rage", rage, player);
            Check.Equal(3L, Stacks(session, player, "base-spire:rage-power"), "获得 3 层 RagePower");

            // 技能牌不触发（when-card-type = attack）。
            var blockBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            Play(session, "defend-no-trigger", defend, player);
            Check.Equal(blockBefore + 5,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "技能牌不触发 RagePower（只多了 Defend 的 5 点）");

            // 攻击牌触发：按层数加 3 点格挡。
            var blockBeforeAttack = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            Play(session, "strike-triggers-rage", strike, target);
            Check.Equal(blockBeforeAttack + 3,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "攻击触发：3 层 × 1 = 3 点格挡");

            // `removeAfterPlayerTurnEnd` ⇒ `owner-turn-end = remove`。
            var endTurn = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
            session.Submit(endTurn.Definition, SliceManifest.Player, []);
            Check.Equal(0L, Stacks(session, player, "base-spire:rage-power"), "回合末被移除");
        });
        Check.Run("W-06 正式源：`preventsBlockClear` 机制（Barricade 让格挡跨回合保留）[真实]", () =>
        {
            // 对照组：**没有** Barricade 时，回合开始格挡清零（证明机制是它带来的差异）。
            var plain = Build(BuildModel());
            var plainPlayer = SliceFixtures.Player(plain);
            var plainDefend = SliceFixtures.PutInHand(plain, "base-spire:defend-ironclad", upgraded: false);
            Play(plain, "control-defend", plainDefend, plainPlayer);
            Check.Equal(5L, SliceFixtures.Number(plain, plainPlayer, SliceFixtures.PlayerTypeId, "Block"),
                "对照组先攒 5 点格挡");
            EndTurn(plain);
            Check.Equal(0L, SliceFixtures.Number(plain, plainPlayer, SliceFixtures.PlayerTypeId, "Block"),
                "没有 Barricade ⇒ 回合开始格挡清零（对照组）");

            // 实验组：Barricade 的 `preventsBlockClear = true`（能力来自正式能力源）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var barricade = SliceFixtures.PutInHand(session, "base-spire:barricade", upgraded: false);
            Play(session, "barricade", barricade, player);
            Check.Equal(1L, Stacks(session, player, "base-spire:barricade"), "获得 1 层 Barricade");

            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            // 受控输入：Barricade 花掉了这一回合的能量，补足能量以便再打一张 Defend（不影响机制本身）。
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(3L)));
            Play(session, "defend-with-barricade", defend, player);
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "攒到 5 点格挡");
            EndTurn(session);
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "有 Barricade ⇒ 格挡**跨回合保留**（5 点仍在）");
        });

        Check.Run("W-06 正式源：`incomingDamageModifiers` 机制（Tank 让受击伤害翻倍）[真实]", () =>
        {
            // 对照组：没有 Tank 时，敌人两个回合打出的伤害（第一动 Incantation 不造成伤害，第二动 DarkStrike）。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            var controlHp = SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Hp");
            EndTurn(control);
            EndTurn(control);
            var controlDamage = controlHp
                - SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Hp");
            Check.True(controlDamage > 0, $"对照组确实受到了 {controlDamage} 点伤害");

            // 实验组：Tank 给自己 1 层（`incomingDamageModifiers = [modifier(multiplier=2, …)]`）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var tank = SliceFixtures.PutInHand(session, "base-spire:tank", upgraded: false);
            Play(session, "tank", tank, player);
            Check.Equal(1L, Stacks(session, player, "base-spire:tank-power"), "获得 1 层 TankPower");

            var hp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            EndTurn(session);
            EndTurn(session);
            var tankDamage = hp - SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            // 用**实测的对照伤害**做比较（不写死数字）：受击伤害应为对照的整整 2 倍。
            Check.Equal(controlDamage * 2, tankDamage,
                $"有 Tank ⇒ 受击伤害翻倍（对照 {controlDamage} ⇒ 实测 {tankDamage}）");
            Check.Equal(1L, Stacks(session, player, "base-spire:tank-power"),
                "TankPower 没有声明移除 ⇒ 跨回合仍在");
        });

        Check.Run("W-06 正式源：`events` 机制（FlameBarrier 受伤后反击 + 对手回合末移除）[真实]", () =>
        {
            // 对照组：没有 FlameBarrier 时，敌人打完之后自己不会掉血。
            var control = Build(BuildModel());
            var controlFront = SliceFixtures.Cultist(control, SliceManifest.CalcifiedCultistId);
            var controlBack = SliceFixtures.Cultist(control, SliceManifest.DampCultistId);
            var controlFrontHp = SliceFixtures.Number(control, controlFront, SliceFixtures.MonsterTypeId, "Hp");
            var controlBackHp = SliceFixtures.Number(control, controlBack, SliceFixtures.MonsterTypeId, "Hp");
            EndTurn(control);
            EndTurn(control);
            Check.Equal(controlFrontHp, SliceFixtures.Number(control, controlFront,
                    SliceFixtures.MonsterTypeId, "Hp"), "对照组：前排没被反击");
            Check.Equal(controlBackHp, SliceFixtures.Number(control, controlBack,
                    SliceFixtures.MonsterTypeId, "Hp"), "对照组：后排没被反击");

            // 实验组 A：FlameBarrier 卡本身（12 格挡 + 4 层）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var barrier = SliceFixtures.PutInHand(session, "base-spire:flame-barrier", upgraded: false);
            Play(session, "flame-barrier", barrier, player);
            Check.Equal(4L, Stacks(session, player, "base-spire:flame-barrier"), "获得 4 层 FlameBarrier");
            Check.Equal(12L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "同时给 12 点格挡（多原语：gainBlock + applyPower）");

            // 实验组 B（触发与移除）：**受控输入**把 4 层挂在玩家身上，再进入敌人的**攻击**回合。
            // 为什么需要受控输入：这个受控场景里敌人的第一动是念咒（不造成伤害），而
            // `removeAfterOpponentTurnEnd` 只会覆盖**一个**敌人回合 —— 因此必须在敌人的攻击回合前挂上，
            // 才能观察到"受伤 → 反击 → 对手回合末移除"这条完整链路。
            var trigger = Build(BuildModel());
            var triggerPlayer = SliceFixtures.Player(trigger);
            var triggerFront = SliceFixtures.Cultist(trigger, SliceManifest.CalcifiedCultistId);
            var triggerBack = SliceFixtures.Cultist(trigger, SliceManifest.DampCultistId);
            var triggerFrontHp = SliceFixtures.Number(trigger, triggerFront, SliceFixtures.MonsterTypeId, "Hp");
            var triggerBackHp = SliceFixtures.Number(trigger, triggerBack, SliceFixtures.MonsterTypeId, "Hp");
            var playerHp = SliceFixtures.Number(trigger, triggerPlayer, SliceFixtures.PlayerTypeId, "Hp");

            EndTurn(trigger);  // 敌人第一动：Incantation（不造成伤害）
            AttachPower(trigger, triggerPlayer, "base-spire:flame-barrier", 4);
            Check.Equal(4L, Stacks(trigger, triggerPlayer, "base-spire:flame-barrier"),
                "受控挂上 4 层（敌人的攻击回合即将开始）");

            EndTurn(trigger);  // 敌人第二动：DarkStrike（powered 伤害）

            Check.True(SliceFixtures.Number(trigger, triggerPlayer, SliceFixtures.PlayerTypeId, "Hp") < playerHp,
                "敌人确实打中了玩家（触发条件成立）");
            Check.Equal(triggerFrontHp - 4,
                SliceFixtures.Number(trigger, triggerFront, SliceFixtures.MonsterTypeId, "Hp"),
                "前排（造成 9 点的那只）被反击 4 点 = 层数 × 1");
            Check.Equal(triggerBackHp - 4,
                SliceFixtures.Number(trigger, triggerBack, SliceFixtures.MonsterTypeId, "Hp"),
                "后排（造成 1 点的那只）同样被反击 4 点 ⇒ 反击落在**每次命中的来源**上");
            Check.Equal(0L, Stacks(trigger, triggerPlayer, "base-spire:flame-barrier"),
                "`removeAfterOpponentTurnEnd` ⇒ 覆盖完这个敌人回合后在**敌人回合末**移除");
        });

        Check.Run("W-06 正式源：回合开始触发（DemonForm 每回合按层数加力量）[真实]", () =>
        {
            // 对照组：没有 DemonForm 时，回合推进不会带来力量。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            EndTurn(control);
            Check.Equal(0L, Stacks(control, controlPlayer, SliceManifest.StrengthId),
                "对照组：回合开始不会凭空获得力量");

            // 实验组：DemonForm（`trigger = afterPlayerTurnStart`、`effect = grantPower(Strength, sourceStacks)`）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            var demon = SliceFixtures.PutInHand(session, "base-spire:demon-form", upgraded: false);
            Play(session, "demon-form", demon, player);
            Check.Equal(2L, Stacks(session, player, "base-spire:demon-form"), "获得 2 层 DemonForm");
            Check.Equal(0L, Stacks(session, player, SliceManifest.StrengthId),
                "打出当回合还没有力量（触发点是**回合开始**）");

            EndTurn(session);  // 敌人第一动：Incantation；随后玩家第 2 回合开始 ⇒ 触发
            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId),
                "第 2 回合开始：按层数获得 2 层力量");

            // 力量真的生效：打击 6 + 2 = 8。
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            Play(session, "strike-with-strength", strike, target);
            Check.Equal(8L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "打击 6 + 力量 2 = 8");

            EndTurn(session);  // 玩家第 3 回合开始 ⇒ 再触发一次
            Check.Equal(4L, Stacks(session, player, SliceManifest.StrengthId),
                "第 3 回合开始：再 +2 ⇒ 累计 4（每回合都触发）");
        });

        Check.Run("W-06 正式源：牌规则 `cardRule`——Corruption 让技能牌 0 费且消耗 [真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");

            // 对照组：没有 Corruption 时，Defend 花 1 点能量并进弃牌堆。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            var controlDefend = SliceFixtures.PutInHand(control, "base-spire:defend-ironclad", upgraded: false);
            var controlEnergy = SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Energy");
            Play(control, "control-defend", controlDefend, controlPlayer);
            Check.Equal(controlEnergy - 1,
                SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Energy"),
                "对照组：Defend 花 1 点");
            Check.True(control.State.CollectionMembers(
                    SliceFixtures.Collection(control, "base-spire:DiscardPile")).Contains(controlDefend),
                "对照组：Defend 进弃牌堆");

            // 实验组：打出 Corruption（3 费，给自己 1 层 corruption-power）。
            var corruption = SliceFixtures.PutInHand(session, "base-spire:corruption", upgraded: false);
            Play(session, "corruption", corruption, player);
            Check.Equal(1L, Stacks(session, player, "base-spire:corruption-power"), "获得 1 层 Corruption");
            Check.Equal(0L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"), "Corruption 花掉 3 费");

            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            Play(session, "defend-with-corruption", defend, player);
            Check.Equal(0L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "牌规则：技能牌 0 费（能量仍为 0，没有变成负数或无法打出）");
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"), "Defend 的格挡照常生效");
            Check.True(session.State.CollectionMembers(exhaust).Contains(defend),
                "牌规则 `resultPile = exhaust` ⇒ 技能牌进**消耗区**");
            Check.False(session.State.CollectionMembers(discard).Contains(defend), "并且不在弃牌堆");
        });

        Check.Run("W-06 正式源：牌规则 `cardRule`——FreeAttack 让攻击牌 0 费且打出后消耗该能力 [真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // Unrelenting：2 费、14 点伤害，并给自己 1 层 FreeAttackPower。
            var unrelenting = SliceFixtures.PutInHand(session, "base-spire:unrelenting", upgraded: false);
            Play(session, "unrelenting", unrelenting, target);
            Check.Equal(1L, Stacks(session, player, "base-spire:free-attack-power"),
                "获得 1 层 FreeAttackPower");
            Check.Equal(1L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "Unrelenting 花 2 费（3 → 1）");

            // 第一次攻击：牌规则 ⇒ 0 费（能量不变），且打出后该能力被消耗。
            var first = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            Play(session, "strike-free", first, target);
            Check.Equal(1L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "牌规则：攻击牌 0 费（能量仍是 1）");
            Check.Equal(0L, Stacks(session, player, "base-spire:free-attack-power"),
                "`consumeOnPlay` ⇒ 打出一次攻击后该能力被消耗");

            // 第二次攻击恢复原价：能量 1 → 0（若规则还在，就会保持 1——这条是"一次性"的判据）。
            var second = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            Play(session, "strike-after-consume", second, target);
            Check.Equal(0L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "能力被消耗后，攻击牌恢复 1 费");
        });

        Check.Run("W-06 正式源：获得格挡后触发 + 随机目标攻击（Juggernaut，无修正）[真实]", () =>
        {
            // 对照组：没有 Juggernaut 时，获得格挡不会造成任何伤害。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            var controlFront = SliceFixtures.Cultist(control, SliceManifest.CalcifiedCultistId);
            var controlBack = SliceFixtures.Cultist(control, SliceManifest.DampCultistId);
            var controlTotal = SliceFixtures.Number(control, controlFront, SliceFixtures.MonsterTypeId, "Hp")
                + SliceFixtures.Number(control, controlBack, SliceFixtures.MonsterTypeId, "Hp");
            var controlDefend = SliceFixtures.PutInHand(control, "base-spire:defend-ironclad", upgraded: false);
            Play(control, "control-defend", controlDefend, controlPlayer);
            Check.Equal(controlTotal,
                SliceFixtures.Number(control, controlFront, SliceFixtures.MonsterTypeId, "Hp")
                + SliceFixtures.Number(control, controlBack, SliceFixtures.MonsterTypeId, "Hp"),
                "对照组：获得格挡不造成伤害");

            // 实验组：Juggernaut（2 费，6 层）⇒ 每次获得格挡随机打 6 点（`scaled(0, 1, count=自身层数)`）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var juggernaut = SliceFixtures.PutInHand(session, "base-spire:juggernaut", upgraded: false);
            Play(session, "juggernaut", juggernaut, player);
            Check.Equal(6L, Stacks(session, player, "base-spire:juggernaut-power"), "获得 6 层 Juggernaut");

            // 受控输入：给玩家 2 层力量，用来证明这次随机攻击是**无修正**的（`properties=[unpowered]`）。
            AttachPower(session, player, SliceManifest.StrengthId, 2);
            var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp")
                + SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");

            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(3L)));
            Play(session, "defend-triggers-juggernaut", defend, player);

            var after = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp")
                + SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");
            Check.Equal(6L, before - after,
                "获得格挡触发一次随机攻击：6 层 × 1 = 6 点，且**不**加玩家的 2 层力量（无修正）");
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "格挡照常记在玩家身上（触发不消耗它）");
        });

        Check.Run("W-06 正式源：`sequence` + `removePower` 机制（SelfFormingClay：格挡 3 点后移除自己）[真实]", () =>
        {
            // 该能力由遗物施加（语料里没有对应卡），因此用**受控输入**把它挂到玩家身上。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            AttachPower(session, player, "base-spire:self-forming-clay", 1);
            Check.Equal(1L, Stacks(session, player, "base-spire:self-forming-clay"), "受控挂上 1 层");

            // 对照组：同样推进一个回合，但玩家没有这个能力 ⇒ 回合开始不会凭空得到格挡。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            EndTurn(control);
            Check.Equal(0L, SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Block"),
                "对照组：回合开始不会凭空得到格挡");

            // 实验组：玩家第 2 回合开始 ⇒ `sequence(gainBlock(3, self), removePower(自己))`。
            EndTurn(session);
            Check.Equal(3L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "回合开始按声明给 3 点格挡（固定数值，不按层数）");
            Check.Equal(0L, Stacks(session, player, "base-spire:self-forming-clay"),
                "同一次触发里的第二个效果 `removePower(自己)` ⇒ 该能力被移除（一次性）");

            // 再推进一个回合：能力已移除 ⇒ 不会再加格挡（证明"一次性"而不是"每回合都给"）。
            var blockAfterFirst = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            EndTurn(session);
            Check.True(SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block") <= blockAfterFirst,
                "能力已移除 ⇒ 之后不再凭空加格挡（格挡被回合开始清零）");
        });

        Check.Run("W-06 正式源：卡级 hooks（Infection 的 turnEndInHand 扣 HP）[真实]", () =>
        {
            // 对照组：手里没有 Infection 时，回合结束不会掉 HP（敌人第一动是念咒，不造成伤害）。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            var controlHp = SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Hp");
            EndTurn(control);
            Check.Equal(controlHp,
                SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "Hp"),
                "对照组：回合结束不掉 HP");

            // 实验组：把 Infection 放进手里 ⇒ `hooks = { turnEndInHand = { loseHp 3 } }` 在回合末触发。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var infection = SliceFixtures.PutInHand(session, "base-spire:infection", upgraded: false);
            Check.True(infection != default, "Infection 在手里");
            var hp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");

            EndTurn(session);
            Check.Equal(hp - 3, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "回合末留在手里的 Infection 扣 3 点 HP（钩子来自正式源）");
        });

        Check.Run("W-06 正式源：小写能力名也能链接（Uppercut 的 weak/vulnerable 两个能力）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var hp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            // Uppercut：2 费、13 点伤害，并给所选目标 1 层 Weak + 1 层 Vulnerable（正式源里写作小写 `weak`）。
            var uppercut = SliceFixtures.PutInHand(session, "base-spire:uppercut", upgraded: false);
            Play(session, "uppercut", uppercut, target);

            Check.Equal(hp - 13, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "Uppercut 打 13 点");
            Check.Equal(1L, Stacks(session, target, "base-spire:weak"), "目标获得 1 层 Weak");
            Check.Equal(1L, Stacks(session, target, "base-spire:vulnerable"), "目标获得 1 层 Vulnerable");
            _ = player;
        });

        Check.Run("W-06 正式源：条件效果 `if`（Spite：本回合掉过 HP 才打两次）[真实]", () =>
        {
            // else 分支：没掉过 HP ⇒ 只打一次（5 点）。
            var plain = Build(BuildModel());
            var plainTarget = SliceFixtures.Cultist(plain, SliceManifest.CalcifiedCultistId);
            var plainHp = SliceFixtures.Number(plain, plainTarget, SliceFixtures.MonsterTypeId, "Hp");
            var plainSpite = SliceFixtures.PutInHand(plain, "base-spire:spite", upgraded: false);
            Play(plain, "spite/else", plainSpite, plainTarget);
            Check.Equal(5L, plainHp - SliceFixtures.Number(plain, plainTarget, SliceFixtures.MonsterTypeId, "Hp"),
                "没掉过 HP ⇒ 走 else：打 5 点");

            // then 分支：先用 Breakthrough 自损 1 点（`playerLostHpThisTurn`），再打 Spite ⇒ 打两次（10 点）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var breakthrough = SliceFixtures.PutInHand(session, "base-spire:breakthrough", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(9L)));
            Play(session, "breakthrough-self-harm", breakthrough, target);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            var spite = SliceFixtures.PutInHand(session, "base-spire:spite", upgraded: false);
            Play(session, "spite/then", spite, target);
            Check.Equal(10L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "本回合掉过 HP ⇒ 走 then：`repeat 2` ⇒ 打两次共 10 点");
        });

        Check.Run("W-06 正式源：条件效果 `if`（EvilEye：本回合消耗过牌才给两次格挡）[真实]", () =>
        {
            // else 分支：本回合没消耗过牌 ⇒ 只给 8 点格挡。
            var plain = Build(BuildModel());
            var plainPlayer = SliceFixtures.Player(plain);
            var plainEye = SliceFixtures.PutInHand(plain, "base-spire:evil-eye", upgraded: false);
            Play(plain, "evil-eye/else", plainEye, plainPlayer);
            Check.Equal(8L, SliceFixtures.Number(plain, plainPlayer, SliceFixtures.PlayerTypeId, "Block"),
                "没消耗过牌 ⇒ 走 else：8 点格挡");

            // then 分支：先消耗一张牌（Impervious 带 exhaust），再打 EvilEye ⇒ 16 点格挡。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");
            var impervious = SliceFixtures.PutInHand(session, "base-spire:impervious", upgraded: false);
            Play(session, "impervious-exhausts", impervious, player);
            Check.True(session.State.CollectionMembers(exhaust).Contains(impervious), "确实消耗了一张牌");
            Check.Equal(30L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "Impervious 的 30 点格挡先记上");

            var eye = SliceFixtures.PutInHand(session, "base-spire:evil-eye", upgraded: false);
            session.State.RunAtomic(() =>
            {
                session.State.SetField(player, playerType.FieldIndex("Energy"), Value.Of(1L));
                session.State.SetField(player, playerType.FieldIndex("Block"), Value.Of(0L));
            });
            Play(session, "evil-eye/then", eye, player);
            Check.Equal(16L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "本回合消耗过牌 ⇒ 走 then：`repeat 2` ⇒ 16 点格挡");
        });

        Check.Run("W-06 正式源：批次 `repeat`（TwinStrike 打两次）[真实]", () =>
        {
            var session = Build(BuildModel());
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var hp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var twin = SliceFixtures.PutInHand(session, "base-spire:twin-strike", upgraded: false);
            Play(session, "twin-strike", twin, target);
            Check.Equal(10L, hp - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "TwinStrike = `repeat 2 × attack 5` ⇒ 同一目标打两次共 10 点");
        });

        Check.Run("W-06 正式源：`chooseCard`（Headbutt：从弃牌堆选一张放到抽牌堆顶）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var draw = SliceFixtures.Collection(session, "base-spire:DrawPile");

            // 先打一张牌进弃牌堆（Defend），它就是要被"置顶"的候选。
            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            Play(session, "defend-into-discard", defend, player);
            var discardBefore = session.State.CollectionMembers(discard).ToList();
            Check.True(discardBefore.Contains(defend), "Defend 已在弃牌堆里");

            var hp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var headbutt = SliceFixtures.PutInHand(session, "base-spire:headbutt", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"),
                Value.Of(3L)));
            Play(session, "headbutt", headbutt, target);

            Check.Equal(9L, hp - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "Headbutt 先打 9 点");
            Check.True(!session.State.CollectionMembers(discard).Contains(defend),
                "选中的 Defend 离开了弃牌堆（此时弃牌堆里只剩刚打出的 Headbutt 自己）");
            Check.True(session.State.CollectionMembers(draw).Contains(defend),
                "选中的那张牌被放回抽牌堆（`to = draw, destinationPosition = top`）");
        });

        Check.Run("W-06 正式源：`chooseCard`（BurningPact：选一张手牌消耗并触发消耗链）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");

            // 先拿到 FeelNoPain（3 层）：选中的牌被消耗时应当触发它（证明"消耗的是那张被选的牌"）。
            var feel = SliceFixtures.PutInHand(session, "base-spire:feel-no-pain", upgraded: false);
            Play(session, "feel-no-pain", feel, player);
            Check.Equal(3L, Stacks(session, player, "base-spire:feel-no-pain"), "3 层 FeelNoPain");

            var handBefore = session.State.CollectionMembers(hand).ToList();
            var pact = SliceFixtures.PutInHand(session, "base-spire:burning-pact", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"),
                Value.Of(3L)));
            var hpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            Play(session, "burning-pact", pact, player);

            // 被选中的手牌进了消耗区，且 BurningPact 自己也带 `exhaust`。
            Check.True(handBefore.Any(card => session.State.CollectionMembers(exhaust).Contains(card)),
                "选中的那张手牌进了消耗区");
            Check.True(session.State.CollectionMembers(exhaust).Contains(pact),
                "BurningPact 自己带 `exhaust` ⇒ 也进消耗区");
            Check.True(SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block") >= hpBefore + 3,
                "选中的牌被消耗 ⇒ 触发 FeelNoPain（至少 +3 格挡）");
        });

        Check.Run("W-06 正式源：`chooseCard`（Brand：自损 1 + 选牌消耗 + 自加 1 层力量）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var handBefore = session.State.CollectionMembers(hand).ToList();

            var brand = SliceFixtures.PutInHand(session, "base-spire:brand", upgraded: false);
            var hp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            Play(session, "brand", brand, player);

            Check.Equal(hp - 1, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "Brand 自损 1 点 HP");
            Check.True(handBefore.Any(card => session.State.CollectionMembers(exhaust).Contains(card)),
                "选中的那张手牌进了消耗区");
            Check.Equal(1L, Stacks(session, player, SliceManifest.StrengthId), "给自己 1 层力量");
        });

        Check.Run("W-06 正式源：随机目标攻击（SwordBoomerang：3 次随机 3 点，力量照常生效）[真实]", () =>
        {
            // 对照组：没有力量 ⇒ 3 次 × 3 点 = 9 点（随机分布在两个敌人身上，因此断言**合计**）。
            var control = Build(BuildModel());
            var controlFront = SliceFixtures.Cultist(control, SliceManifest.CalcifiedCultistId);
            var controlBack = SliceFixtures.Cultist(control, SliceManifest.DampCultistId);
            long Total(CoreTheSpire.Engine.Session target) =>
                SliceFixtures.Number(target, controlFront, SliceFixtures.MonsterTypeId, "Hp")
                + SliceFixtures.Number(target, controlBack, SliceFixtures.MonsterTypeId, "Hp");
            var controlBefore = Total(control);
            var controlCard = SliceFixtures.PutInHand(control, "base-spire:sword-boomerang", upgraded: false);
            Play(control, "sword-boomerang/plain", controlCard, controlFront);
            Check.Equal(9L, controlBefore - Total(control),
                "`repeat 3 × randomAttack 3` ⇒ 合计 9 点（目标由命名随机流当场抽取）");

            // 实验组：受控给玩家 2 层力量 ⇒ 每次 3+2 = 5，3 次合计 15（证明每次都是**独立的 powered 攻击**）。
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            AttachPower(session, player, SliceManifest.StrengthId, 2);
            var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp")
                + SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");
            var card = SliceFixtures.PutInHand(session, "base-spire:sword-boomerang", upgraded: false);
            Play(session, "sword-boomerang/strength", card, front);
            Check.Equal(15L,
                before - (SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp")
                    + SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp")),
                "2 层力量 ⇒ 3 次 × (3+2) = 15 点（每次命中都算力量，且是同一张牌的批次）");
        });

        Check.Run("W-06 正式源：随机移动手牌 `randomMoveCard`（TrueGrit 随机消耗一张手牌）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");

            // FeelNoPain 3 层：被随机消耗的那张牌应当触发它（证明触发链作用在**被随机选中的那张牌**上）。
            var feel = SliceFixtures.PutInHand(session, "base-spire:feel-no-pain", upgraded: false);
            Play(session, "feel-no-pain", feel, player);
            var handBefore = session.State.CollectionMembers(hand).ToList();
            Check.True(handBefore.Count >= 2, "手牌足够（至少 2 张，才能区分「打出自己」与「随机消耗另一张」）");

            var grit = SliceFixtures.PutInHand(session, "base-spire:true-grit", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(3L)));
            Play(session, "true-grit", grit, player);

            Check.Equal(10L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "TrueGrit 的 7 点格挡 + FeelNoPain 的 3 点（被随机消耗的牌触发了消耗链）");
            var exhaustedFromHand = handBefore
                .Count(card => session.State.CollectionMembers(exhaust).Contains(card));
            Check.Equal(1L, (long)exhaustedFromHand,
                "恰好**一张**原来的手牌被随机消耗（不是打出的 TrueGrit 自己）");
        });

        Check.Run("W-06 正式源：随机移动手牌 `randomMoveCard`（Cinder 打 18 点并随机消耗一张手牌）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var exhaust = SliceFixtures.Collection(session, "base-spire:ExhaustPile");
            var handBefore = session.State.CollectionMembers(hand).ToList();
            var hp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");

            var cinder = SliceFixtures.PutInHand(session, "base-spire:cinder", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player,
                session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId).FieldIndex("Energy"), Value.Of(3L)));
            Play(session, "cinder", cinder, target);

            Check.Equal(18L, hp - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "Cinder 打 18 点");
            Check.Equal(1L, (long)handBefore.Count(card => session.State.CollectionMembers(exhaust).Contains(card)),
                "并随机消耗恰好一张原来的手牌");
        });

        Check.Run("W-06 正式源：手牌升级（Armaments：普通形态只升级选中那一张，升级形态升级整手）[真实]", () =>
        {
            // 普通形态：`chooseCard { from=hand, to=none, filter=upgradeable, onSelected=upgradeSelectedCard }`
            // 判据用**手牌实例的 Upgraded 字段**（不假设具体选中哪一张）：恰好一张被升级、其余保持原样。
            var normal = Build(BuildModel());
            var normalPlayer = SliceFixtures.Player(normal);
            var normalType = normal.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var normalCardType = normal.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var armaments = SliceFixtures.PutInHand(normal, "base-spire:armaments", upgraded: false);
            var hand = SliceFixtures.Collection(normal, "base-spire:Hand");
            var handBefore = normal.State.CollectionMembers(hand).ToList();
            normal.State.RunAtomic(() => normal.State.SetField(normalPlayer, normalType.FieldIndex("Energy"),
                Value.Of(9L)));
            Play(normal, "armaments/normal", armaments, normalPlayer);
            Check.Equal(5L, SliceFixtures.Number(normal, normalPlayer, SliceFixtures.PlayerTypeId, "Block"),
                "普通形态先给 5 点格挡");

            bool IsUpgraded(CoreTheSpire.Engine.Session session, InstanceId card) =>
                session.State.GetField(card, normalCardType.FieldIndex("Upgraded")).AsBool();
            var upgradedInHand = handBefore.Where(card => IsUpgraded(normal, card)).ToList();
            Check.Equal(1, upgradedInHand.Count, "**只有一张**手牌被升级（不是整手）");
            Check.Equal(9L, SliceFixtures.Number(normal, upgradedInHand[0], SliceFixtures.CardTypeId, "Amount"),
                "被选中的那张的升级数值生效（标准牌组起手是打击：6 → 9）");
            Check.True(handBefore.Any(card => !IsUpgraded(normal, card)), "其余手牌保持普通形态");

            // 升级形态：`upgradeCards { pile = hand }` ⇒ 整手一起升级。
            var upgraded = Build(BuildModel());
            var upgradedPlayer = SliceFixtures.Player(upgraded);
            var upgradedType = upgraded.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var upgradedCardType = upgraded.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var upgradedArmaments = SliceFixtures.PutInHand(upgraded, "base-spire:armaments", upgraded: false);
            // 正式源模型的升级实例由**运行时字段**表达（`PutInHand(upgraded: true)` 走的是冻结清单，
            // 不认正式源新增的卡）：把实例标记为升级 ⇒ 效果分派会走升级形态的步骤（`upgradeCards`）。
            upgraded.State.RunAtomic(() => upgraded.State.SetField(upgradedArmaments,
                upgradedCardType.FieldIndex("Upgraded"), Value.Of(true)));
            var upgradedHand = SliceFixtures.Collection(upgraded, "base-spire:Hand");
            var upgradedBefore = upgraded.State.CollectionMembers(upgradedHand).ToList();
            upgraded.State.RunAtomic(() => upgraded.State.SetField(upgradedPlayer,
                upgradedType.FieldIndex("Energy"), Value.Of(9L)));
            Play(upgraded, "armaments/upgraded", upgradedArmaments, upgradedPlayer);

            var stillPlain = upgradedBefore
                .Where(card => !upgraded.State.GetField(card, upgradedCardType.FieldIndex("Upgraded")).AsBool())
                .ToList();
            Check.Equal(0, stillPlain.Count, "升级形态：原来的手牌**全部**被升级（0 张保持普通形态）");
        });

        Check.Run("W-06 正式源：治疗 `heal`（NotYet 回 10 点，且不超过最大生命）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var maxHp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "MaxHp");

            // 情形一：低于上限很多 ⇒ 恰好回 10 点。
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Hp"),
                Value.Of(20L)));
            var notYet = SliceFixtures.PutInHand(session, "base-spire:not-yet", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"),
                Value.Of(3L)));
            Play(session, "not-yet/plain", notYet, player);
            Check.Equal(30L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "`heal 10` ⇒ 20 → 30");

            // 情形二：接近上限 ⇒ 被**最大生命**截断（不是回满 10 点）。
            var second = Build(BuildModel());
            var secondPlayer = SliceFixtures.Player(second);
            var secondType = second.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            second.State.RunAtomic(() => second.State.SetField(secondPlayer, secondType.FieldIndex("Hp"),
                Value.Of(maxHp - 4)));
            var secondNotYet = SliceFixtures.PutInHand(second, "base-spire:not-yet", upgraded: false);
            second.State.RunAtomic(() => second.State.SetField(secondPlayer, secondType.FieldIndex("Energy"),
                Value.Of(3L)));
            Play(second, "not-yet/capped", secondNotYet, secondPlayer);
            Check.Equal(maxHp, SliceFixtures.Number(second, secondPlayer, SliceFixtures.PlayerTypeId, "Hp"),
                $"最大生命 {maxHp} 截断：只回到上限而不是 {maxHp - 4 + 10}");
        });

        Check.Run("W-06 正式源：击杀奖励 `onFatal`（Feed：打死目标才 +3 最大生命）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var monsterType = session.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var maxHp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "MaxHp");

            // 受控输入：把目标压到 5 点生命 ⇒ Feed 的 10 点伤害必定击杀。
            session.State.RunAtomic(() => session.State.SetField(target, monsterType.FieldIndex("Hp"),
                Value.Of(5L)));
            var feed = SliceFixtures.PutInHand(session, "base-spire:feed", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"),
                Value.Of(3L)));
            Play(session, "feed/kill", feed, target);
            Check.Equal(0L, SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "目标被打死（0 血）");
            Check.Equal(maxHp + 3,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "MaxHp"),
                "击杀 ⇒ 最大生命 +3");

            // 对照组：目标血量充足（没死）⇒ 最大生命不变。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            var controlType = control.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var controlTarget = SliceFixtures.Cultist(control, SliceManifest.CalcifiedCultistId);
            var controlMaxHp = SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "MaxHp");
            var controlFeed = SliceFixtures.PutInHand(control, "base-spire:feed", upgraded: false);
            control.State.RunAtomic(() => control.State.SetField(controlPlayer,
                controlType.FieldIndex("Energy"), Value.Of(3L)));
            Play(control, "feed/no-kill", controlFeed, controlTarget);
            Check.Equal(controlMaxHp,
                SliceFixtures.Number(control, controlPlayer, SliceFixtures.PlayerTypeId, "MaxHp"),
                "没打死 ⇒ 最大生命不变（击杀奖励不是无条件加血）");
        });

        Check.Run("W-06 正式源：`applyScaledPower`（Dominate：按目标身上的易伤层数给自己加力量）[真实]", () =>
        {
            var session = Build(BuildModel());
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 受控输入：先给目标挂 3 层易伤（正式源里 Dominate 自己只会给 1 层）。
            AttachPower(session, target, "base-spire:vulnerable", 3);
            var dominate = SliceFixtures.PutInHand(session, "base-spire:dominate", upgraded: false);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"),
                Value.Of(3L)));
            Play(session, "dominate", dominate, target);

            Check.Equal(4L, Stacks(session, target, "base-spire:vulnerable"),
                "先给目标 +1 层易伤（3 → 4，声明里 applyPower 在前）");
            Check.Equal(4L, Stacks(session, player, SliceManifest.StrengthId),
                "再按目标**当时**的易伤层数给自己 4 层力量（`applyScaledPower`，读的是打出那一刻的目标状态）");

            // 对照组：目标身上没有易伤 ⇒ 自己得不到力量。
            var control = Build(BuildModel());
            var controlPlayer = SliceFixtures.Player(control);
            var controlType = control.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var controlTarget = SliceFixtures.Cultist(control, SliceManifest.CalcifiedCultistId);
            var controlDominate = SliceFixtures.PutInHand(control, "base-spire:dominate", upgraded: false);
            control.State.RunAtomic(() => control.State.SetField(controlPlayer, controlType.FieldIndex("Energy"),
                Value.Of(3L)));
            Play(control, "dominate/no-vulnerable", controlDominate, controlTarget);
            Check.Equal(1L, Stacks(control, controlPlayer, SliceManifest.StrengthId),
                "目标本来没有易伤 ⇒ 只拿到它自己刚上的那 1 层（不是凭空给力量）");
        });

        Check.Run("W-06 正式源：`targetPowerAmount` 数值来源（Bully：4 + 2 × 目标易伤层数，随后照常过易伤乘区）[真实]", () =>
        {
            // 数值来源只决定**基础伤害**；它之后仍走完整的伤害管线（见 `DamageChain`：加性 → 乘性 → 上限 →
            // 一次截断）。因此受击方身上的易伤既进 `targetPowerAmount` 的计数，也照常 ×1.5 —— 两者都要断言，
            // 否则"来源读没读到目标"和"乘性阶段跑没跑"分不开。
            // 来源：reference/sts2-sim/src/Sts2Sim.Core/Models/Cards/Bully.cs:25-27
            //   `DamageCmd.Attack(4m + _damagePerVulnerable * stacks)`（升级时 perCount 4→5→…，本卡 normal/upgraded 为 2/3）。
            static (long Hp, long Stacks) PlayBully(string label, bool upgraded, int vulnerable, int weak)
            {
                var session = Build(BuildModel());
                var player = SliceFixtures.Player(session);
                var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                if (vulnerable > 0)
                {
                    AttachPower(session, target, SliceManifest.VulnerableId, vulnerable);
                }

                if (weak > 0)
                {
                    AttachPower(session, target, SliceManifest.WeakId, weak);
                }

                var hp = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                // 正式源新增的卡不在冻结清单里，升级形态由**运行时字段**表达（与 Armaments 检查同一做法）：
                // 数值来自正式源模型的升级变体（`scaledAttack.perCount`），不来自实例上的 Amount 初值。
                var bully = SliceFixtures.PutInHand(session, "base-spire:bully", upgraded: false);
                if (upgraded)
                {
                    var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
                    session.State.RunAtomic(() =>
                        session.State.SetField(bully, cardType.FieldIndex("Upgraded"), Value.Of(true)));
                }

                session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Energy"),
                    Value.Of(3L)));
                Play(session, label, bully, target);
                return (hp - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                    Stacks(session, target, SliceManifest.VulnerableId));
            }

            // 情形一：目标 0 层易伤 ⇒ `4 + 2 × 0` = 4，乘性阶段没有易伤可乘。
            var plain = PlayBully("bully/zero", upgraded: false, vulnerable: 0, weak: 0);
            Check.Equal(4L, plain.Hp, "目标 0 层易伤 ⇒ `base 4 + 2 × 0` = 4 点");

            // 情形二：3 层易伤 ⇒ 来源给 4 + 2×3 = 10，受击方易伤再 ×1.5 = 15（一个截断点，10×1.5 是整数）。
            var scaled = PlayBully("bully/three", upgraded: false, vulnerable: 3, weak: 0);
            Check.Equal(15L, scaled.Hp,
                "3 层易伤 ⇒ (`4 + 2 × 3`) × 1.5 = 15 点（数值来源读目标状态，易伤乘区照常生效）");

            // 情形三（升级变体）：perCount 2→3 ⇒ (4 + 3×3) × 1.5 = 19.5 ⇒ 一次截断 = 19（不是 19.5 也不是 20）。
            var upgraded = PlayBully("bully/three-upgraded", upgraded: true, vulnerable: 3, weak: 0);
            Check.Equal(19L, upgraded.Hp,
                "升级变体 perCount = 3 ⇒ (4 + 3 × 3) × 1.5 = 19.5，单次截断 ⇒ 19 点");

            // 情形四（判别性对照）：目标挂的是**另一种**能力（3 层虚弱）⇒ 来源仍读到"易伤 0 层" ⇒ 4 点。
            // 虚弱是**出手方**的伤害修正，挂在受击方身上不影响 Bully 的伤害，因此这个数只能来自"读的是易伤"。
            var otherPower = PlayBully("bully/weak-only", upgraded: false, vulnerable: 0, weak: 3);
            Check.Equal(4L, otherPower.Hp,
                "目标是 3 层**虚弱**（不是易伤）⇒ `targetPowerAmount` 仍读到 0 层易伤 ⇒ 4 点");
        });
    }

    /// <summary>受控输入：把一层能力直接挂到某个实例上（其余检查文件也用同样的做法）。</summary>
    private static void AttachPower(Session session, InstanceId owner, string powerId, int stacks)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        var power = session.State.CreateInstance(powerType.Id, session.State.ScopeOf(owner));
        session.State.RunAtomic(() =>
        {
            session.State.SetField(power, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            session.State.SetField(power, powerType.FieldIndex("Stacks"), Value.Of((long)stacks));
            session.State.SetField(power, powerType.FieldIndex("Ticks"), Value.Of(true));
            session.State.SetField(power, powerType.FieldIndex("SkipNextTick"), Value.Of(true));
            session.State.AddRelation(relation.Id, owner, power);
        });
    }

    /// <summary>结束玩家回合（走真实动作，不直接改状态）。</summary>
    private static void EndTurn(Session session)
    {
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
    }

    private static long Stacks(Session session, InstanceId owner, string powerId)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.State.RelationTargets(relation.Id, owner))
        {
            if (session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
            {
                return session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0;
    }

    /// <summary>当前 `PlayCard` 的**合法提交组合**里的牌实例（guard 已筛过）。</summary>
    private static IReadOnlyList<InstanceId> PlayCardCandidates(Session session)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
        var domain = action.Domains.Keys.Single();
        return
        [
            .. action.Bindings
                .Where(binding => binding[domain].Kind == ValueKind.Ref)
                .Select(binding => binding[domain].AsRef()),
        ];
    }

    /// <summary>正式源里某张牌的**目标域**（证明目标语义来自声明而不是猜的）。</summary>
    private static string DeclaredTarget(string cardId)
    {
        var ironclad = SpireCardSource.LoadResource("BaseSpire.ContentSource.ironclad-cards.spire");
        var status = SpireCardSource.LoadResource("BaseSpire.ContentSource.status-cards.spire");
        var declaration = ironclad.Parsed.Concat(status.Parsed)
            .Single(entry => entry.Id == cardId);
        return declaration.Normal.TargetKind;
    }

    /// <summary>源级 id（去掉包前缀），用于把"源里的声明"与"降级出来的卡"对上。</summary>
    private static string Unqualified(string id) => id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id;

    /// <summary>打出一张牌；失败时打印现场（手牌/能量/合法动作）便于定位。</summary>
    private static void Play(Session session, string label, InstanceId card, InstanceId target)
    {
        try
        {
            SliceFixtures.PlayAt(session, card, target);
        }
        catch (Exception failure)
        {
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var player = SliceFixtures.Player(session);
            Console.WriteLine($"  diag[{label}]: {failure.GetType().Name} status={session.Status} "
                + $"hand={session.State.CollectionMembers(hand).Count} "
                + $"energy={SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy")} "
                + $"actions={string.Join(",", session.LegalActions(SliceManifest.Player).Select(a => a.Definition.QualifiedId))}");
            throw;
        }
    }

    /// <summary>正式能力名 → 规范内容 id（id 权威来自冻结内容的 id 表）。</summary>
    private static IReadOnlyDictionary<string, string> PowerIds() =>
        SliceAuthorFrontend.Load().Powers
            .GroupBy(power => power.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

    /// <summary>
    /// 正式能力源降级出来的能力（`*-powers.spire`）：卡牌链接表与非卡部分的能力声明用它。
    /// </summary>
    private static IReadOnlyList<AuthorPower> FormalPowers(out IReadOnlyList<string> summaries)
    {
        var powers = new List<AuthorPower>();
        var lines = new List<string>();
        var ids = PowerIds();
        foreach (var resource in SpirePowerSource.PowerDeclarationResources())
        {
            var source = SpirePowerSource.LoadResource(resource, ids);
            powers.AddRange(source.Powers);
            lines.Add(source.Summary);
        }

        summaries = lines;
        return powers;
    }

    /// <summary>正式能力名 → id（链接表；来自**正式能力源**，不再是作者文本）。</summary>
    private static IReadOnlyDictionary<string, string> PowerLinks() =>
        FormalPowers(out _)
            .GroupBy(power => power.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

    /// <summary>
    /// 用正式源降级出来的牌建模型：非卡部分沿用切片自身的作者文本（敌人/整局），
    /// **能力用正式能力源**，并把"能力名 → id"表交给卡牌前端做**链接**。
    /// </summary>
    private static SliceAuthorModel BuildModel()
    {
        var baseModel = SliceAuthorFrontend.Load();
        var powerLinks = PowerLinks();
        var ironclad = SpireCardSource.LoadResource("BaseSpire.ContentSource.ironclad-cards.spire", powerLinks);
        var status = SpireCardSource.LoadResource("BaseSpire.ContentSource.status-cards.spire", powerLinks);
        var cards = ironclad.Cards.Concat(status.Cards)
            .Where(card => BehaviorVerifiedIds.Contains(Unqualified(card.Id), StringComparer.Ordinal))
            .ToList();
        Check.Equal(BehaviorVerifiedIds.Length, cards.Count, "代表牌全部来自正式源降级结果");
        var powers = FormalPowers(out var powerSummaries);
        Check.True(powers.Count >= 7, $"正式能力源降级出 {powers.Count} 个能力（{string.Join(" / ", powerSummaries)}）");
        // 卡牌模型的能力声明**整体来自正式能力源**（不再借用作者文本的能力声明）。
        var model = baseModel with { Cards = cards, Powers = powers };
        // 卡组用**降级结果自己的 id**（带包前缀），不手写前缀：这样"卡组引用的卡"就是正式源里那些卡。
        string Qualified(string unqualified) =>
            cards.Single(card => Unqualified(card.Id) == unqualified).Id;
        return model with
        {
            Cards = cards,
            Powers = powers,
            Deck =
            [
                // 8 张打击：起手抽到的仍是前 5 张（手牌不变），但多回合之后抽牌堆里仍有打击可用。
                new AuthorDeckEntry(Qualified("strike-ironclad"), 8),
                new AuthorDeckEntry(Qualified("defend-ironclad"), 4),
                new AuthorDeckEntry(Qualified("bludgeon"), 1),
                new AuthorDeckEntry(Qualified("impervious"), 1),
                new AuthorDeckEntry(Qualified("slimed"), 1),
                // 状态牌也来自正式源（`unplayable`/`ethereal` 关键词的降级因此有行为证据）。
                new AuthorDeckEntry(Qualified("wound"), 1),
                new AuthorDeckEntry(Qualified("dazed"), 1),
                // 多原语/能力/自损的代表卡。
                new AuthorDeckEntry(Qualified("iron-wave"), 1),
                new AuthorDeckEntry(Qualified("bash"), 1),
                new AuthorDeckEntry(Qualified("pommel-strike"), 1),
                new AuthorDeckEntry(Qualified("inflame"), 1),
                new AuthorDeckEntry(Qualified("breakthrough"), 1),
                // 数值来源（scaled）代表卡。
                new AuthorDeckEntry(Qualified("body-slam"), 1),
                new AuthorDeckEntry(Qualified("ashen-strike"), 2),
                new AuthorDeckEntry(Qualified("expect-a-fight"), 1),
                new AuthorDeckEntry(Qualified("entrench"), 1),
                // 能力触发类代表卡（能力声明来自正式能力源）。
                new AuthorDeckEntry(Qualified("feel-no-pain"), 1),
                new AuthorDeckEntry(Qualified("rage"), 1),
                // 新机制：`preventsBlockClear`（Barricade）。
                new AuthorDeckEntry(Qualified("barricade"), 1),
                // 新机制：`incomingDamageModifiers`（Tank）。
                new AuthorDeckEntry(Qualified("tank"), 1),
                // 新机制：`events`（受伤后反击；FlameBarrier）。
                new AuthorDeckEntry(Qualified("flame-barrier"), 1),
                // 新机制：回合开始触发（DemonForm）。
                new AuthorDeckEntry(Qualified("demon-form"), 1),
                // 新机制：牌规则 `cardRule`（Corruption：技能 0 费 + 消耗；Unrelenting：攻击 0 费且一次性）。
                new AuthorDeckEntry(Qualified("corruption"), 1),
                new AuthorDeckEntry(Qualified("unrelenting"), 1),
                // 新机制：获得格挡后触发 + 随机目标攻击（Juggernaut）。
                new AuthorDeckEntry(Qualified("juggernaut"), 1),
                // 新机制：卡级 hooks（Infection 的 turnEndInHand）与小写能力名链接（Uppercut 的 weak/vulnerable）。
                new AuthorDeckEntry(Qualified("infection"), 1),
                new AuthorDeckEntry(Qualified("uppercut"), 1),
                // 新机制：条件效果 `if` 与批次 `repeat`（Spite/EvilEye 条件分支；TwinStrike 批次）。
                new AuthorDeckEntry(Qualified("spite"), 2),
                new AuthorDeckEntry(Qualified("evil-eye"), 2),
                new AuthorDeckEntry(Qualified("twin-strike"), 1),
                // 新机制：`chooseCard`（Headbutt 的弃牌堆置顶；BurningPact/Brand 的手牌消耗）。
                new AuthorDeckEntry(Qualified("headbutt"), 1),
                new AuthorDeckEntry(Qualified("burning-pact"), 1),
                new AuthorDeckEntry(Qualified("brand"), 1),
                // 新机制：随机目标攻击 `randomAttack` + 目标域 `selectRandom`（SwordBoomerang）。
                new AuthorDeckEntry(Qualified("sword-boomerang"), 1),
                // 新机制：随机移动手牌 `randomMoveCard`（TrueGrit / Cinder 的随机消耗）。
                new AuthorDeckEntry(Qualified("true-grit"), 1),
                new AuthorDeckEntry(Qualified("cinder"), 1),
                // 新机制：手牌升级（Armaments 的 `upgradeSelectedCard` 与 `upgradeCards`）。
                new AuthorDeckEntry(Qualified("armaments"), 1),
                // 新机制：治疗 `heal`（NotYet）与击杀奖励 `onFatal.gainMaxHp`（Feed）。
                new AuthorDeckEntry(Qualified("not-yet"), 1),
                new AuthorDeckEntry(Qualified("feed"), 1),
                // 新机制：`applyScaledPower` + `targetPowerAmount` 数值来源（Dominate / Bully）。
                new AuthorDeckEntry(Qualified("dominate"), 1),
                new AuthorDeckEntry(Qualified("bully"), 1),
            ],
        };
    }

    private static Session Build(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model)
            .Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }
}
