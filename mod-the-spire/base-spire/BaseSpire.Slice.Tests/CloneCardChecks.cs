using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T05（Anger）：**战斗克隆** —— `cloneCard = { pile = discard }`。
///
/// 源形状：正式 `ContentSource/ironclad-cards.spire` 里 Anger 的 `play` 在 `attack` 之后声明
/// `cloneCard = { pile = discard }`（原版 `CardModel.CreateClone` 的受限叶）。本包只支持
/// "**当前牌** → **弃牌堆**" 这一叶形状：其它牌区、缺 `pile`、额外字段都在降级期带**源位置**失败，
/// 而不是被静默丢掉或泛化成别的机制。
///
/// 判据（声明 → 解析 → 降级 → 加载 → 行为）：
/// <list type="number">
/// <item>正式 Anger 不再因 `cloneCard` 被记为未支持；</item>
/// <item>普通/升级各打出一次：伤害 6/8、费用 0、只问一次目标；原牌与克隆都在弃牌堆，
/// `InstanceId` 不同而 `DefinitionId` 相同，克隆保留来源的升级形态；</item>
/// <item>克隆保存**创建时**的实例字段与变量：之后改源不污染克隆；`CostModifier` 按源当时的值复制
/// （与 `StartNextRoom` 的 `CopyCardFields` 明确分列，后者清零）；</item>
/// <item>最后一击打死目标之后复制仍然执行，原牌走完出牌尾（离开 in-play、进结果区）；</item>
/// <item>克隆没有 `LongTermSource`、长期牌组数量不增长，进入下一场战斗也不会把克隆带过去；</item>
/// <item>hook 里复制继续被拒绝；`.author` 作者步骤同样可用且不可批次。</item>
/// </list>
/// **不在本包声称范围内**的字段（例如原版 `ExhaustOnNextPlay`）这里也不断言——见交接里的复制矩阵。
/// </summary>
internal static class CloneCardChecks
{
    private const string AngerId = "base-spire:anger";
    private const string AuthorCloneCardId = "base-spire:test-clone";
    private const string MonstersPile = "base-spire:Monsters";

    public static void Run()
    {
        FormalSourceLowersAnger();
        UnsupportedShapesFailWithSourceLocation();
        NormalFormalAngerLeavesAnIndependentClone();
        UpgradedFormalAngerLeavesAnUpgradedClone();
        CloneKeepsInstanceStateAtCreationTime();
        CloneRunsAfterTheFatalBlow();
        CloneStaysOutOfTheLongTermDeck();
        HookCloneIsRejected();
        AuthorStepIsRegisteredAndExecutes();
    }

    /// <summary>① 降级：正式源不再因为这张叶把 Anger 记为未支持。</summary>
    private static void FormalSourceLowersAnger()
    {
        Check.Run("ENV-T05 Anger：正式源的 `cloneCard` 叶被降级，anger 不再出现在未支持名单里", () =>
        {
            var loaded = FormalSpirePackage.Load();
            Check.Contains(loaded.CardIds, id => id == AngerId, "正式源降级结果里包含 anger");
            Check.DoesNotContain(loaded.UnsupportedCards, name => name == "Anger",
                "anger 不再因 cloneCard 未支持而被降级掉；未支持名单=["
                + string.Join("; ", loaded.UnsupportedCards) + "]");
        });
    }

    /// <summary>② 叶的边界：只支持 `pile = discard`，其余形状带源位置失败（不得静默省略）。</summary>
    private static void UnsupportedShapesFailWithSourceLocation()
    {
        Check.Run("ENV-T05 Anger：只支持 `pile = discard`；别的牌区/缺 pile/额外字段带源位置失败", () =>
        {
            foreach (var (play, fragment) in new[]
                     {
                         ("cloneCard = { pile = hand }", "'hand' 未支持"),
                         ("cloneCard = { }", "缺少 pile"),
                         ("cloneCard = { pile = discard position = top }", "'position' 未支持"),
                         ("cloneCard = { pile = hand pile = discard }", "重复 pile"),
                         ("cloneCard = { pile = discard extra ignored { } }", "嵌套声明未支持"),
                         ("cloneCard = 1", "不是块"),
                     })
            {
                var source = ParseProbe(play + " attack = { damage = 1 }");
                Check.Equal(0, source.Cards.Count, $"'{play}' 不得被注册成卡（不得回退成空效果）");
                var reason = source.Unsupported.Single().Reason;
                Check.True(reason.Contains("clone-probe.spire", StringComparison.Ordinal),
                    $"诊断带源位置：{reason}");
                Check.True(reason.Contains(fragment, StringComparison.Ordinal),
                    $"诊断点名 '{fragment}'：{reason}");
            }
        });
    }

    /// <summary>③ 普通形态：伤害 6、费用 0、一次目标选择，弃牌堆多出一个独立克隆。</summary>
    private static void NormalFormalAngerLeavesAnIndependentClone()
    {
        Check.Run("ENV-T05 正式 Anger（普通）：费用 0、造成 6、只问一次目标，弃牌堆多出一个同定义的独立克隆 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var target = FormalSliceFixture.Cultist(session);
            var anger = FormalSliceFixture.MoveToHand(session, AngerId);
            Check.Equal(0L, Card(session, anger, "Cost"), "正式 Anger 的费用是 0");
            var hp = Hp(session, target);

            Check.Equal(1, FormalSliceFixture.PlayAtCountingDecisions(session, anger, target),
                "只选一次目标");
            Check.Equal(6L, hp - Hp(session, target), "普通 Anger 造成 6");

            var inDiscard = CardsOf(session, FormalSliceFixture.DiscardPile, AngerId);
            Check.Equal(2, inDiscard.Count,
                $"弃牌堆里有原牌与克隆两张 anger：[{string.Join(",", inDiscard.Select(card => card.Value))}]");
            var clone = inDiscard.Single(card => card != anger);
            Check.Equal(AngerId, FormalSliceFixture.Definition(session, clone), "克隆的定义与原牌相同");
            Check.False(CardFlag(session, anger, "Upgraded"), "普通来源没有被升级");
            Check.False(CardFlag(session, clone, "Upgraded"), "普通来源的克隆也不是升级形态");
            Check.Equal(6L, Card(session, clone, "Amount"), "克隆带着源当时的数值 6");
            Check.Equal(anger, Reference(session, clone, "CloneOf"), "克隆的来源引用指向**本次源卡**（不是初始定义）");
            Check.True(session.State.IsMember(FormalSliceFixture.Collection(session, FormalSliceFixture.DiscardPile), anger),
                "原牌走完出牌尾进了结果区");
        });
    }

    /// <summary>④ 升级形态：伤害 8，克隆保留升级形态与升级数值。</summary>
    private static void UpgradedFormalAngerLeavesAnUpgradedClone()
    {
        Check.Run("ENV-T05 正式 Anger（升级）：造成 8，克隆保留升级形态与升级数值", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var target = FormalSliceFixture.Cultist(session);
            var anger = FormalSliceFixture.MoveToHand(session, AngerId);
            // 正式源每张定义各一个实例：把战斗副本置成升级形态（与它升级变体的声明一致）。
            FormalSliceFixture.SetNumber(session, anger, FormalSliceFixture.CardTypeId, "Amount", 8L);
            SetFlag(session, anger, "Upgraded", true);
            Check.Equal(0L, Card(session, anger, "Cost"), "升级 Anger 的费用仍是 0");
            var hp = Hp(session, target);

            Check.Equal(1, FormalSliceFixture.PlayAtCountingDecisions(session, anger, target), "只选一次目标");
            Check.Equal(8L, hp - Hp(session, target), "升级 Anger 造成 8");

            var clone = CardsOf(session, FormalSliceFixture.DiscardPile, AngerId).Single(card => card != anger);
            Check.True(CardFlag(session, clone, "Upgraded"), "克隆保留升级形态");
            Check.Equal(8L, Card(session, clone, "Amount"), "克隆带着升级数值 8");
            Check.Equal(anger, Reference(session, clone, "CloneOf"), "克隆指向本次源卡");
        });
    }

    /// <summary>⑤ 复制矩阵：创建时的实例变量与临时费用状态被复制，之后改源不污染克隆。</summary>
    private static void CloneKeepsInstanceStateAtCreationTime()
    {
        Check.Run("ENV-T05 克隆：保存创建时的实例变量与临时费用状态，之后改源不污染克隆 [真实]", () =>
        {
            var source = ParseProbe("cloneCard = { pile = discard } "
                + "modifyCardVariable = { variable = Charge amount = 3 } "
                + "scaledAttack = { base = 0 perCount = 1 count = { cardVariable = { variable = Charge } } }",
                variables: "variables = { variable = { name = Charge default = 1 upgraded = 2 } }");
            var session = NewProbeSession(source);
            var probe = ProbeCard(session);
            var probeId = FormalSliceFixture.Definition(session, probe);
            var target = ProbeMonster(session);
            // 打出**之前**给源一个临时费用修正：克隆应当带着它，而长期副本（CopyCardFields）会清零。
            FormalSliceFixture.SetNumber(session, probe, FormalSliceFixture.CardTypeId, "CostModifier", 1L);
            var hp = Hp(session, target);

            Check.Equal(1, FormalSliceFixture.PlayAtCountingDecisions(session, probe, target), "只选一次目标");
            Check.Equal(4L, hp - Hp(session, target),
                "克隆（Charge=1）之后写入 3 再读，本次攻击读到 4 ⇒ 写入确实发生在克隆之后");

            var clone = CardsOf(session, FormalSliceFixture.DiscardPile, probeId).Single(card => card != probe);
            // 变量名按 kebab 规范化（`name = Charge` → 字段 `Variable_charge`，见 SpireCardSource.ParseCardVariables）。
            Check.Equal(1L, Card(session, clone, "Variable_charge"), "克隆保存的是**创建时**的变量值 1");
            Check.Equal(4L, Card(session, probe, "Variable_charge"), "源实例之后被改写成 4（两者独立）");
            Check.Equal(1L, Card(session, clone, "CostModifier"),
                "克隆保留源当时的临时费用修正（不是像长期副本那样清零）");
            Check.True(session.State.GetField(clone, CardIndex(session, "LongTermSource")).IsNone,
                "克隆的长期来源留空");
        });
    }

    /// <summary>⑥ 最后击杀之后复制仍然执行，原牌走完整出牌尾。</summary>
    private static void CloneRunsAfterTheFatalBlow()
    {
        Check.Run("ENV-T05 克隆：最后击杀之后复制仍然执行，原牌走完出牌尾 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var target = FormalSliceFixture.Cultist(session);
            var anger = FormalSliceFixture.MoveToHand(session, AngerId);
            FormalSliceFixture.SetNumber(session, target, FormalSliceFixture.MonsterTypeId, "Hp", 3L);
            var hp = Hp(session, target);

            Check.Equal(1, FormalSliceFixture.PlayAtCountingDecisions(session, anger, target), "只选一次目标");
            Check.Equal(3L, hp - Hp(session, target), "6 点伤害把 3 点血打到 0");
            Check.Equal(0L, Hp(session, target), "致命一击确实打死了目标（结算由出牌尾负责，不是复制叶）");

            Check.Equal(2, CardsOf(session, FormalSliceFixture.DiscardPile, AngerId).Count,
                "击杀之后复制照旧执行（复制的源不依赖目标是否还活着）");
            Check.False(session.State.IsMember(FormalSliceFixture.Collection(session, "base-spire:InPlay"), anger),
                "原牌已经离开 in-play（出牌尾完整走完，不是被击杀提前截断）");
        });
    }

    /// <summary>⑦ 长期隔离：克隆没有长期来源，长期牌组不增长，也不会被带进下一场战斗。</summary>
    private static void CloneStaysOutOfTheLongTermDeck()
    {
        Check.Run("ENV-T05 克隆：没有长期来源、长期牌组不增长，进入下一场战斗也不会被带过去 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var longTerm = FormalSliceFixture.Collection(session, "base-spire:LongTermDeck");
            var before = session.State.CollectionMembers(longTerm).ToHashSet();
            var calcified = FormalSliceFixture.Cultist(session, SliceManifest.CalcifiedCultistId);
            var damp = FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId);
            FormalSliceFixture.SetNumber(session, calcified, FormalSliceFixture.MonsterTypeId, "Hp", 1L);
            FormalSliceFixture.SetNumber(session, damp, FormalSliceFixture.MonsterTypeId, "Hp", 1L);

            // 清房要打两下（正式源每张卡各一个物理实例）：第一下用 Anger 顺手留下克隆，第二下用 Strike。
            var anger = FormalSliceFixture.MoveToHand(session, AngerId);
            FormalSliceFixture.PlayAt(session, anger, calcified);
            FormalSliceFixture.PlayAt(session,
                FormalSliceFixture.MoveToHand(session, "base-spire:strike-ironclad"), damp);

            var clone = CardsOf(session, FormalSliceFixture.DiscardPile, AngerId).Single(card => card != anger);
            Check.True(session.State.GetField(clone, CardIndex(session, "LongTermSource")).IsNone,
                "克隆没有长期来源（不是长期实例）");
            Check.Equal(before.Count, session.State.CollectionMembers(longTerm).Count,
                "打出一张克隆，长期牌组数量不变");

            var enter = session.LegalActions(SliceManifest.Player)
                .SingleOrDefault(action => action.Definition.QualifiedId == "base-spire:EnterNextRoom")
                ?? throw new CheckFailedException("EnterNextRoom 当前不合法（房间尚未结算？）");
            session.Submit(enter.Definition, SliceManifest.Player, []);
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                var pending = session.PendingDecision!;
                session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
            }

            var after = session.State.CollectionMembers(longTerm);
            Check.False(after.Contains(anger), "原牌（战斗副本）没有写回长期牌组");
            Check.False(after.Contains(clone), "克隆没有写回长期牌组");
            Check.Equal(before.Count, after.Count, "进入下一场战斗后长期牌组数量仍然不变");

            var newCopies = session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile))
                .Concat(session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, FormalSliceFixture.DrawPile)))
                .Where(card => FormalSliceFixture.Definition(session, card) == AngerId)
                .ToList();
            Check.Equal(1, newCopies.Count, "下一间房每个长期实例各有一个战斗副本");
            Check.False(newCopies[0] == clone, "新副本不是上一场那个克隆实例");
            Check.False(session.State.GetField(newCopies[0], CardIndex(session, "LongTermSource")).IsNone,
                "新副本按实例记录了长期来源（与克隆明确不同）");
        });
    }

    /// <summary>⑧ hook 里复制继续被拒绝（步骤不在 hook 允许面内）。</summary>
    private static void HookCloneIsRejected()
    {
        Check.Run("ENV-T05 克隆：hook 里复制继续被拒绝（步骤不在 hook 允许面内）", () =>
        {
            var source = ParseProbe("attack = { damage = 1 }",
                hooks: "afterCardPlayed = { cloneCard = { pile = discard } }");
            Check.Equal(0, source.Cards.Count, "hook 里复制不得被注册成卡");
            var reason = source.Unsupported.Single().Reason;
            Check.True(reason.Contains("hook", StringComparison.OrdinalIgnoreCase),
                $"错误说明这是 hook 边界：{reason}");
        });
    }

    /// <summary>⑨ 作者步骤：词表登记、可解析、不可批次；`.author` 路径也能真的造出克隆。</summary>
    private static void AuthorStepIsRegisteredAndExecutes()
    {
        Check.Run("ENV-T05 作者步骤：`clone_card_to_discard` 在词表里、可解析、不可批次、不在 hook 允许面内", () =>
        {
            Check.Contains(AuthorSteps.All, step => step == AuthorSteps.CloneCardToDiscard,
                "步骤登记进作者词表（否则 .author 文本会报 unknown_step）");
            Check.DoesNotContain(AuthorSteps.HookSteps, step => step == AuthorSteps.CloneCardToDiscard,
                "步骤不在 hook 允许面内");
            Check.Contains(AuthorSteps.NonRepeatable, step => step == AuthorSteps.CloneCardToDiscard,
                "步骤不可批次");

            var model = SliceAuthorFrontend.Parse(AuthorScenarioText, "clone-author");
            var card = model.Cards.Single(entry => entry.Id == AuthorCloneCardId);
            Check.Sequence([AuthorSteps.CloneCardToDiscard], card.StepKinds, "扁平步骤被解析成克隆步骤");

            try
            {
                SliceAuthorFrontend.Parse(
                    AuthorScenarioText.Replace("steps = [clone_card_to_discard]",
                        "steps = [clone_card_to_discard x2]"), "clone-author");
                Check.True(false, "'x2' 批次必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unsupported_combination", failure.Code, "批次拒绝的错误码");
                Check.True(failure.Message.Contains("clone_card_to_discard", StringComparison.Ordinal),
                    $"错误点名步骤：{failure.Message}");
            }
        });

        Check.Run("ENV-T05 作者步骤：`.author` 路径打出后弃牌堆多出一个同定义克隆 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFromAuthorText(AuthorScenarioText);
            var discard = FormalSliceFixture.Collection(session, FormalSliceFixture.DiscardPile);
            var card = FormalSliceFixture.MoveToHand(session, AuthorCloneCardId);
            FormalSliceFixture.PlayAt(session, card, FormalSliceFixture.Cultist(session));

            var clones = session.State.CollectionMembers(discard)
                .Where(candidate => FormalSliceFixture.Definition(session, candidate) == AuthorCloneCardId)
                .ToList();
            Check.Equal(2, clones.Count, "原牌与克隆都在弃牌堆");
            Check.True(clones.Contains(card), "原牌是结果区里的那一张");
            Check.Equal(1, clones.Count(candidate => candidate != card), "多出来的确实是新实例");
        });
    }

    // -- 受控 `.spire` 夹具（与 --effects-only 的探针同形：走正式源解析，不用 .author 合成牌） -----------

    private static SpireCardSource.Source ParseProbe(string play, string hooks = "",
        string variables = "", string cost = "1")
    {
        string Variant(string body, string hookText) => "type = attack rarity = common cost = " + cost
            + " target = { selectOne = { from = context.opponents where = alive } } keywords = {} "
            + "play = { " + body + " } hooks = { " + hookText + " }";
        return SpireCardSource.Parse(
            "using namespace base-spire; card CloneProbe { " + variables + " normal = { " + Variant(play, hooks)
            + " } upgraded = { " + Variant(play, hooks) + " } }",
            "clone-probe.spire",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Strength"] = "base-spire:strength",
                ["Vulnerable"] = "base-spire:vulnerable",
            });
    }

    private static Session NewProbeSession(SpireCardSource.Source source)
    {
        Check.Equal(1, source.Cards.Count, string.Join("; ", source.Unsupported.Select(entry => entry.Reason)));
        var model = FormalSpirePackage.Load().Model with
        {
            Cards = source.Cards,
            Deck = [new(source.Cards[0].Id, 1)],
        };
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole],
            computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    private static InstanceId ProbeCard(Session session) => session.State
        .CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile)).Single();

    private static InstanceId ProbeMonster(Session session) => session.State
        .CollectionMembers(FormalSliceFixture.Collection(session, MonstersPile)).First();

    // -- 读/写卡实例字段的小工具（只读公开状态，不改规则） --------------------------------------------------

    private static int CardIndex(Session session, string field) =>
        session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex(field);

    private static long Card(Session session, InstanceId card, string field) =>
        session.State.GetField(card, CardIndex(session, field)).AsInt();

    private static bool CardFlag(Session session, InstanceId card, string field) =>
        session.State.GetField(card, CardIndex(session, field)).AsBool();

    private static InstanceId Reference(Session session, InstanceId card, string field) =>
        session.State.GetField(card, CardIndex(session, field)).AsRef();

    private static void SetFlag(Session session, InstanceId card, string field, bool value)
    {
        var index = CardIndex(session, field);
        session.State.RunAtomic(() => session.State.SetField(card, index, Value.Of(value)));
    }

    private static List<InstanceId> CardsOf(Session session, string pile, string definitionId) =>
    [
        .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, pile))
            .Where(card => FormalSliceFixture.Definition(session, card) == definitionId),
    ];

    private static long Hp(Session session, InstanceId monster) =>
        FormalSliceFixture.Number(session, monster, FormalSliceFixture.MonsterTypeId, "Hp");

    private static int MonsterCount(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, MonstersPile)).Count;

    /// <summary>
    /// `.author` 路径的受控场景（与 B-05c 生成批次同形）：一张只含克隆步骤的牌。
    /// 这一路径走的是扁平 `steps`（`NormalProgram` 为空），用来证明同一个具名步骤在**两条前端**上都成立。
    /// </summary>
    private const string AuthorScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  test-clone }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 90  tough-hp = 91  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 90  tough-hp = 91  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 3  steps = [target, damage] }
          upgraded { amount = 7  steps = [target, damage] }
        }
        card test-clone {
          name = TestClone  kind = Skill  cost = 0
          normal   { amount = 0  steps = [clone_card_to_discard] }
          upgraded { amount = 0  steps = [clone_card_to_discard] }
        }
        """;
}
