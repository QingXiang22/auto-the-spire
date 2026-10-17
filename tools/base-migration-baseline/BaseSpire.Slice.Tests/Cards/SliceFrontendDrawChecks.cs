using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第四小批）：**抽牌原语 `draw:<n>`** + 两张来源对照的基础牌。
///
/// 来源：`Cards/Content/ironclad-cards.spire`
/// <list type="bullet">
/// <item><c>PommelStrike</c>（:2265）：伤害 9（升级 10）+ 抽 1 张（升级抽 **2** 张）——伤害数与抽牌数是
/// **两个独立事实**，因此抽牌写成字面量步骤 `draw:1` / `draw:2`；</item>
/// <item><c>ShrugItOff</c>（:2307）：格挡 8（升级 11）+ 抽 1 张（两形态都是 1）。</item>
/// </list>
/// 抽牌的**重洗与随机流消费点**沿用既有的 `DrawCards` 过程（抽牌堆空 → 用包的命名流重洗弃牌堆，
/// 且重洗会作废已知位置）；本批没有新增随机消费点。
/// </summary>
internal static class SliceFrontendDrawChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  pommel-strike  shrug-it-off }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 200  tough-hp = 201  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 200  tough-hp = 201  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card pommel-strike {
          name = PommelStrike  kind = Attack  cost = 1
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 2265
          normal   { amount = 9  steps = [target, damage, draw:1] }
          upgraded { amount = 10  steps = [target, damage, draw:2] }
        }
        card shrug-it-off {
          name = ShrugItOff  kind = Skill  cost = 1
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 2307
          normal   { amount = 8  steps = [block, draw:1] }
          upgraded { amount = 11  steps = [block, draw:1] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 抽牌：`draw:<n>` 被解析（两形态可不同），非法字面量被拒绝", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "draw");
            var pommel = model.Cards.Single(card => card.Id == "base-spire:pommel-strike");
            Check.Equal(2265, pommel.SourceLine, "来源行");
            Check.Sequence([AuthorSteps.Target, AuthorSteps.Damage, AuthorSteps.Draw + ":1"],
                pommel.NormalSteps.Select(step => step.Kind), "普通：伤害后抽 1");
            Check.Sequence([AuthorSteps.Target, AuthorSteps.Damage, AuthorSteps.Draw + ":2"],
                pommel.UpgradedSteps.Select(step => step.Kind), "升级：伤害后抽 **2**（与伤害数独立）");
            Check.Equal(9, pommel.NormalAmount, "普通伤害 9");
            Check.Equal(10, pommel.UpgradedAmount, "升级伤害 10");

            var shrug = model.Cards.Single(card => card.Id == "base-spire:shrug-it-off");
            Check.Equal(2307, shrug.SourceLine, "来源行");
            Check.Sequence([AuthorSteps.Block, AuthorSteps.Draw + ":1"], shrug.NormalSteps.Select(step => step.Kind),
                "格挡后抽 1");
            Check.Equal(8, shrug.NormalAmount, "普通格挡 8");
            Check.Equal(11, shrug.UpgradedAmount, "升级格挡 11");

            foreach (var bad in new[] { "draw:x", "draw:-1", "draw:1000" })
            {
                try
                {
                    SliceAuthorFrontend.Parse(ScenarioText.Replace("draw:1", bad), "draw");
                    Check.True(false, $"'{bad}' 必须被拒绝");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal("invalid_value", failure.Code, $"错误码（{bad}）");
                    Check.True(failure.Path.Contains("steps", StringComparison.Ordinal), $"路径（{bad}）：{failure.Path}");
                }
            }
        });

        Check.Run("B-06a 抽牌：PommelStrike 打出后手牌净增（普通 +1、升级 +2）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "draw");

            long HandDelta(bool upgraded)
            {
                var session = Build(model);
                var hand = SliceFixtures.Collection(session, "base-spire:Hand");
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                var card = SliceFixtures.PutInHand(session, "base-spire:pommel-strike", upgraded,
                    upgraded ? (Amount: 10, PowerStacks: 0) : null);
                var before = session.State.CollectionMembers(hand).Count;
                var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                SliceFixtures.PlayAt(session, card, target);
                var after = session.State.CollectionMembers(hand).Count;
                // 打出的牌离开手牌（-1），再抽 n 张：净值 = 抽牌数 - 1。
                Check.Equal(upgraded ? 10L : 9L,
                    hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                    $"伤害（{(upgraded ? 10 : 9)}）");
                return after - before;
            }

            Check.Equal(0L, HandDelta(false), "普通：抽 1 - 打出 1 = 0");
            Check.Equal(1L, HandDelta(true), "升级：抽 2 - 打出 1 = +1");
        });

        Check.Run("B-06a 抽牌：ShrugItOff 先格挡再抽牌；抽牌堆耗尽时按命名流重洗 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "draw");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var draw = SliceFixtures.Collection(session, "base-spire:DrawPile");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");

            // 先把这张牌拿到手里，再清空抽牌堆：抽牌必须触发**重洗**（命名流的消费点因此有来源）。
            var shrug = SliceFixtures.PutInHand(session, "base-spire:shrug-it-off", upgraded: false);
            session.State.RunAtomic(() =>
            {
                foreach (var card in session.State.CollectionMembers(draw).ToList())
                {
                    session.State.RemoveFromCollection(draw, card);
                    session.State.AddToCollection(discard, card);
                }
            });
            Check.Equal(0, session.State.CollectionMembers(draw).Count, "抽牌堆已空");
            Check.True(session.State.CollectionMembers(discard).Count > 0, "弃牌堆里有牌");

            var blockBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            var handBefore = session.State.CollectionMembers(hand).Count;
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(shrug)]);
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                    [session.PendingDecision!.Candidates[0]]));
            }

            Check.Equal(blockBefore + 8,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"), "格挡 +8");
            Check.Equal(handBefore, session.State.CollectionMembers(hand).Count,
                "抽 1 - 打出 1 = 手牌数不变（且真的抽到了：重洗把弃牌堆搬回抽牌堆）");
        });

        Check.Run("B-06a 抽牌：冻结作者文本声明了这两张卡（来源行与两形态抽牌数）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var pommel = frozen.Cards.Single(card => card.Id == "base-spire:pommel-strike");
            Check.Equal(2265, pommel.SourceLine, "PommelStrike 来源行");
            Check.Sequence([AuthorSteps.Draw + ":2"], pommel.UpgradedSteps.Skip(2).Select(step => step.Kind),
                "升级抽 2 张");
            var shrug = frozen.Cards.Single(card => card.Id == "base-spire:shrug-it-off");
            Check.Equal(2307, shrug.SourceLine, "ShrugItOff 来源行");
            Check.Equal(AuthorSteps.Draw + ":1", shrug.UpgradedSteps.Last().Kind, "两形态都抽 1 张");
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
