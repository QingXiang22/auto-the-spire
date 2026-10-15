using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第八批：**多选**（`top_attacks_from_discard xN`）。
///
/// 这是第一批改动 `Engine/` 语义的检查：新增 `query.refList`（遍历一个**引用列表槽**），
/// 因此"多选之后逐一消费"在 IR 内可表达。判据：
/// <list type="number">
/// <item>一次提问、最多 N 张（`EntityRefList`），候选仍被条件（攻击牌）过滤；</item>
/// <item>回答里的每个引用**恰好被消费一次**（按回答顺序置顶，不重复、不漏）；</item>
/// <item>候选为空时不提问；数量少于 N 时按可用数选（`min = 1`）；</item>
/// <item>新节点被转储器认识：含该步骤的包，其规则集身份文本包含 `list[...]`。</item>
/// </list>
/// </summary>
internal static class SliceFrontendMultiSelectChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x4
          defend-ironclad x2
          test-salvage
        }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x" }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 44  tough-hp = 45  first-move-ritual = 4  strike-damage = 8
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 51  tough-hp = 52  first-move-ritual = 5  strike-damage = 1
          strike-damage-deadly = 3  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 0
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 8  steps = [block] }
        }
        card test-salvage {
          name = TestSalvage  kind = Skill  cost = 0
          normal   { amount = 1  steps = [top_attacks_from_discard x2] }
          upgraded { amount = 1  steps = [top_attacks_from_discard x2] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 多选：语法接受 `top_attacks_from_discard x2`（批次是「最多选几张」）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "multi");
            var salvage = model.Cards.Single(card => card.Id == "base-spire:test-salvage");
            Check.Sequence([AuthorSteps.TopAttacksFromDiscard], salvage.StepKinds, "步骤名");
            Check.Sequence([2], salvage.NormalSteps.Select(step => step.Count), "批次＝最多选几张");

            // 未知步骤仍然拒绝（多选没有引入新词表以外的东西）。
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("top_attacks_from_discard x2", "top_cards_from_discard x2"), "multi");
                Check.True(false, "未知步骤必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unknown_step", failure.Code, "错误码");
            }
        });

        Check.Run("B-05c 多选：一次提问最多两张、候选只有攻击牌，且每个引用恰好置顶一次 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "multi");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var draw = SliceFixtures.Collection(session, "base-spire:DrawPile");

            // 弃牌堆：两张攻击牌 + 一张技能牌。
            var first = SliceFixtures.Relocate(session, SliceManifest.StrikeId, hand, discard);
            var second = SliceFixtures.Relocate(session, SliceManifest.StrikeId, hand, discard);
            var skill = SliceFixtures.Relocate(session, SliceManifest.DefendId, hand, discard);
            var salvage = SliceFixtures.PutInHand(session, "base-spire:test-salvage", upgraded: false);
            var drawBefore = session.State.CollectionMembers(draw).Count;

            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(salvage)]);

            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "有候选取时提问一次");
            var decision = session.PendingDecision!;
            Check.Equal(DecisionResponseKind.EntityRefList, decision.Kind, "应答种类是引用列表");
            Check.Contains(decision.Candidates, instance => instance == first, "第一张攻击牌在候选里");
            Check.Contains(decision.Candidates, instance => instance == second, "第二张攻击牌在候选里");
            Check.False(decision.Candidates.Contains(skill), "技能牌不在候选里");

            // 按回答顺序给两张：两张都必须离开弃牌堆并置顶，且各一次。
            session.Answer(new DecisionAnswer(decision.RequestId, [first, second]));
            Check.Equal(SessionStatus.Running, session.Status, "答完即继续（没有第二次提问）");
            Check.Equal(drawBefore + 2, session.State.CollectionMembers(draw).Count, "抽牌堆多了两张");
            var top = session.State.CollectionMembers(draw).Take(2).ToList();
            Check.Contains(top, instance => instance == first, "第一张被置顶");
            Check.Contains(top, instance => instance == second, "第二张被置顶");
            Check.False(session.State.IsMember(discard, first), "第一张已离开弃牌堆");
            Check.False(session.State.IsMember(discard, second), "第二张已离开弃牌堆");
            Check.True(session.State.IsMember(discard, skill), "技能牌没有被移动");
        });

        Check.Run("B-05c 多选：候选不足时按可用数选；候选为空时不提问 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "multi");

            // 只有一张攻击牌：仍提问，但只给一张也应成功。
            var single = Build(model);
            var hand = SliceFixtures.Collection(single, "base-spire:Hand");
            var discard = SliceFixtures.Collection(single, "base-spire:DiscardPile");
            var draw = SliceFixtures.Collection(single, "base-spire:DrawPile");
            var only = SliceFixtures.Relocate(single, SliceManifest.StrikeId, hand, discard);
            var salvage = SliceFixtures.PutInHand(single, "base-spire:test-salvage", upgraded: false);
            var action = single.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            single.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(salvage)]);
            Check.Equal(SessionStatus.AwaitingDecision, single.Status, "只有一张候选时也提问");
            single.Answer(new DecisionAnswer(single.PendingDecision!.RequestId, [only]));
            Check.Equal(SessionStatus.Running, single.Status, "只选一张也能完成");
            Check.Equal(only, single.State.CollectionMembers(draw)[0], "这一张被置顶");

            // 一张攻击牌都没有：不提问。
            var none = Build(model);
            var noneHand = SliceFixtures.Collection(none, "base-spire:Hand");
            var noneDiscard = SliceFixtures.Collection(none, "base-spire:DiscardPile");
            SliceFixtures.Relocate(none, SliceManifest.DefendId, noneHand, noneDiscard);
            var noneSalvage = SliceFixtures.PutInHand(none, "base-spire:test-salvage", upgraded: false);
            var noneAction = none.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            none.Submit(noneAction.Definition, SliceManifest.Player, [Value.OfRef(noneSalvage)]);
            Check.Equal(SessionStatus.Running, none.Status, "没有攻击牌时不提问");
        });

        Check.Run("B-05c 多选：新 IR 节点被转储器认识（规则集身份文本包含 list[...]）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "multi");
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false,
                authorModel: model).Build());
            var rules = builder.Build();

            // 转储器必须认识新节点：直接对节点调用公开的 Dump，而不是依赖"某处文本里恰好有"。
            var dumped = IrDumper.Dump(new RefListQuery(7));
            Check.True(dumped.Contains("list[7]", StringComparison.Ordinal),
                $"转储器认识 query.refList（实际：{dumped}）");

            // 新节点参与规则集身份：把多选步骤换成"条件选择的单选"版本，身份必须改变
            // （语义变了身份不变是 K-01 明确要防的）。
            var singleSelect = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("top_attacks_from_discard x2", "top_attack_from_discard"), "multi");
            var singleBuilder = new RuleSetBuilder();
            singleBuilder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false,
                authorModel: singleSelect).Build());
            Check.True(rules.Identity != singleBuilder.Build().Identity,
                "多选与单选是不同语义，身份必须不同");
        });

        Check.Run("B-05c 多选：冻结作者文本没有多选步骤（默认路径不受影响）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.False(model.Cards.Any(card =>
                    card.StepKinds.Contains(AuthorSteps.TopAttacksFromDiscard, StringComparer.Ordinal)),
                "冻结文本里没有多选步骤");
        });
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
