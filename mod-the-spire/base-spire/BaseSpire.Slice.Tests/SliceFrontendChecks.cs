using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05b 的检查：作者前端（`Slice/Content/slice-content.author`）→ 声明模型 → 通用 IR → 链接 →
/// 实际加载 → 基础/升级行为。
///
/// 关键判据：**同一批行为在程序化包与作者前端包上都必须成立**，因此下面的行为检查以
/// <c>fromAuthorText</c> 参数化跑两遍；两条路径共用同一个通用运行时（<c>EnginePackageBuilder</c>）。
/// 作者前端也必须在编译期拒绝未支持的声明（带路径），而不是静默忽略。
/// </summary>
internal static class SliceFrontendChecks
{
    public static void Run()
    {
        Check.Run("B-05b 作者文本解析为声明模型（卡牌/能力/敌人/卡组与冻结清单逐条一致）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.Equal("base-spire", model.PackageId, "package id");
            Check.Equal("combat-slice-v1", model.Version, "version");
            Check.Equal(SliceManifest.Cards.Count + SliceFixtures.AuthoredBeyondSlice.Count, model.Cards.Count,
                "作者文本的卡牌数（冻结切片 + B-06a 已迁内容）");
            foreach (var authored in SliceFixtures.AuthoredBeyondSlice)
            {
                Check.True(model.Cards.Any(card => card.Id == authored), $"已迁内容 {authored} 真的在作者文本里");
            }
            Check.Equal(SliceManifest.Powers.Count + SliceFixtures.AuthoredPowersBeyondSlice.Count,
                model.Powers.Count, "作者文本的能力数（切片 + B-06a 已迁能力）");
            foreach (var authored in SliceFixtures.AuthoredPowersBeyondSlice)
            {
                Check.True(model.Powers.Any(power => power.Id == authored), $"已迁能力 {authored} 真的在作者文本里");
            }
            Check.Equal(2, model.Enemies.Count, "enemy count");

            // 逐条镜像 SliceManifest：数据同源，区别只在效果的表达方式。
            // 逐条核对**冻结切片的那 4 张卡**（B-06a 之后还有别的卡，不在此清单里）。
            foreach (var frozenEntry in SliceManifest.Cards)
            {
                var card = model.Cards.Single(candidate => candidate.Id == frozenEntry.ContentId);
                var frozen = frozenEntry;
                Check.Equal(frozen.Kind, card.Kind, $"{card.Id} kind");
                Check.Equal(frozen.Cost, card.Cost, $"{card.Id} cost");
                Check.Equal(frozen.NormalAmount, card.NormalAmount, $"{card.Id} normal amount");
                Check.Equal(frozen.UpgradedAmount, card.UpgradedAmount, $"{card.Id} upgraded amount");
                Check.Equal(frozen.AppliedPowerId, card.PowerId, $"{card.Id} power");
                Check.Equal(frozen.NormalPowerStacks, card.NormalStacks, $"{card.Id} normal stacks");
                Check.Equal(frozen.UpgradedPowerStacks, card.UpgradedStacks, $"{card.Id} upgraded stacks");
                Check.Equal(frozen.SourceLine, card.SourceLine, $"{card.Id} source line");
                Check.True(card.NormalSteps.Count > 0 && card.UpgradedSteps.Count > 0,
                    $"{card.Id} 的普通/升级形态都必须显式声明效果步骤");
            }

            foreach (var enemy in model.Enemies)
            {
                var frozen = SliceManifest.Enemy(enemy.Id);
                Check.Equal(frozen.NormalMinHp, enemy.NormalMinHp, $"{enemy.Id} hp");
                Check.Equal(frozen.ToughMinHp, enemy.ToughMinHp, $"{enemy.Id} tough hp");
                Check.Equal(frozen.FirstMoveRitual, enemy.FirstMoveRitual, $"{enemy.Id} ritual");
                Check.Equal(frozen.StrikeDamageNormal, enemy.StrikeDamageNormal, $"{enemy.Id} strike damage");
                Check.Equal(frozen.StrikeDamageDeadly, enemy.StrikeDamageDeadly, $"{enemy.Id} deadly damage");
            }

            Check.Sequence(SliceManifest.StarterDeck(), SliceAuthorFrontend.ToStarterDeck(model),
                "作者卡组与冻结卡组一致");

            // 效果步骤：声明出来的就是运行时能执行的动作。
            Check.Sequence(["target", "damage"], model.Cards.Single(c => c.Id == SliceManifest.StrikeId).StepKinds,
                "Strike 步骤");
            Check.Sequence(["block"], model.Cards.Single(c => c.Id == SliceManifest.DefendId).StepKinds, "Defend 步骤");
            Check.Sequence(["target", "damage", "apply_power"],
                model.Cards.Single(c => c.Id == SliceManifest.BashId).StepKinds, "Bash 步骤");
            Check.Sequence(["target", "damage", "top_from_discard"],
                model.Cards.Single(c => c.Id == SliceManifest.HeadbuttId).StepKinds, "Headbutt 步骤");
        });

        Check.Run("B-05b 作者前端在编译期拒绝未支持/不一致的声明（带路径）", () =>
        {
            const string good = """
                package base-spire { version = combat-slice-v1 }
                deck { strike-ironclad }
                power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x" }
                card strike-ironclad {
                  name = StrikeIronclad  kind = Attack  cost = 1
                  normal   { amount = 6  steps = [target, damage] }
                  upgraded { amount = 9  steps = [target, damage] }
                }
                """;
            var parsed = SliceAuthorFrontend.Parse(good, "fixture");
            Check.Equal(1, parsed.Cards.Count, "合法输入应当通过");

            string[] cases =
            [
                // 未知块
                "cardX strike-ironclad { }",
                // 未知键
                good.Replace("kind = Attack", "kindness = Attack"),
                // 未支持的 step
                good.Replace("steps = [target, damage]", "steps = [target, damage, summon_minion]"),
                // damage 缺少前置 target
                good.Replace("steps = [target, damage]", "steps = [damage]"),
                // apply_power 缺少 power
                good.Replace("steps = [target, damage]", "steps = [target, damage, apply_power]"),
                // 缺少 upgraded 变体；raw string 保留源码换行，不把 LF 当作删除前提。
                good.Replace("  upgraded { amount = 9  steps = [target, damage] }", string.Empty),
                // 卡组引用未声明的卡
                good.Replace("deck { strike-ironclad }", "deck { not-a-card }"),
                // damage 与 damage_all 同时出现（目标域不明确）
                good.Replace("steps = [target, damage]", "steps = [target, damage, damage_all]"),
                // 完全省略 steps 键（注意：`steps = []` 是**显式的空效果**，是合法的，见下面的正向断言）
                good.Replace("  steps = [target, damage] ", " "),
                // 整数非法
                good.Replace("cost = 1", "cost = one"),
            ];

            // 正向：`steps = []` 是显式的空效果（状态牌），必须被接受。
            var empty = SliceAuthorFrontend.Parse(
                good.Replace("steps = [target, damage]", "steps = []"), "fixture");
            Check.True(empty.Cards.Single().NormalSteps.Count == 0, "steps = [] 被接受为空效果");

            var rejected = 0;
            foreach (var text in cases)
            {
                Check.True(text != good, "非法输入夹具必须实际改变合法输入");
                try
                {
                    SliceAuthorFrontend.Parse(text, "fixture");
                }
                catch (SliceAuthorError failure)
                {
                    rejected++;
                    Check.True(failure.Path.Length > 0, $"拒绝必须带路径：{failure.Message}");
                    Check.True(failure.Code.Length > 0, $"拒绝必须带错误码：{failure.Message}");
                }
            }

            Check.Equal(cases.Length, rejected, "每一条非法输入都必须在编译期被拒绝");
        });

        Check.Run("B-05b 作者前端包与程序化包产生同一份声明与行为（基础/升级分别核对）", () =>
        {
            foreach (var fromAuthor in new[] { false, true })
            {
                var label = fromAuthor ? "author" : "programmatic";
                var session = NewBattle(fromAuthor);

                // 声明层：卡牌/敌人/区域都在，且种类与字段由包声明（通用 Engine，不是 Legacy）。
                Check.Contains(session.RuleSet.Collections, c => c.QualifiedId == "base-spire:Hand",
                    $"{label} hand area");
                Check.Contains(session.RuleSet.Types, t => t.QualifiedId == "base-spire:Card",
                    $"{label} card type");

                // Strike 基础 6 / 升级 9。
                var basic = DamageOf(fromAuthor, SliceManifest.StrikeId, upgraded: false);
                Check.Equal(38L - 6, basic, $"{label} Strike 基础伤害必须为 6");
                var upgraded = DamageOf(fromAuthor, SliceManifest.StrikeId, upgraded: true);
                Check.Equal(38L - 9, upgraded, $"{label} Strike 升级伤害必须为 9");

                // Defend 基础 5 / 升级 8（格挡）。
                Check.Equal(5L, BlockOf(fromAuthor, SliceManifest.DefendId, upgraded: false),
                    $"{label} Defend 基础格挡必须为 5");
                Check.Equal(8L, BlockOf(fromAuthor, SliceManifest.DefendId, upgraded: true),
                    $"{label} Defend 升级格挡必须为 8");

                // Bash 基础 8 + Vulnerable 2 / 升级 10 + Vulnerable 3。
                var bash = AttackWithPower(fromAuthor, SliceManifest.BashId, upgraded: false);
                Check.Equal(38L - 8, bash.Damage, $"{label} Bash 基础伤害必须为 8");
                Check.Equal(2L, bash.Stacks, $"{label} Bash 基础易伤必须为 2");
                var bashUpgraded = AttackWithPower(fromAuthor, SliceManifest.BashId, upgraded: true);
                Check.Equal(38L - 10, bashUpgraded.Damage, $"{label} Bash 升级伤害必须为 10");
                Check.Equal(3L, bashUpgraded.Stacks, $"{label} Bash 升级易伤必须为 3");
            }
        });

        Check.Run("B-05b 作者前端包的 Headbutt 续接与已知位置事实不回归", () =>
        {
            foreach (var fromAuthor in new[] { false, true })
            {
                var label = fromAuthor ? "author" : "programmatic";
                var session = NewBattle(fromAuthor, includeHeadbutt: true);
                var rules = session.RuleSet;
                var drawPile = rules.Collections.Single(c => c.QualifiedId == "base-spire:DrawPile").Id;
                var hand = rules.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id;
                var discardPile = rules.Collections.Single(c => c.QualifiedId == "base-spire:DiscardPile").Id;

                var headbutt = SliceFixtures.Relocate(session, SliceManifest.HeadbuttId, drawPile, hand);
                var chosen = SliceFixtures.Relocate(session, SliceManifest.StrikeId, hand, discardPile);
                var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

                session.Submit(session.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == SliceFixtures.PlayCardActionId).Definition, SliceManifest.Player,
                    [Value.OfRef(headbutt)]);
                Check.Equal(SessionStatus.AwaitingDecision, session.Status, $"{label} 出牌后暂停等待目标");
                session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
                Check.Equal(SessionStatus.AwaitingDecision, session.Status, $"{label} 目标后暂停等待弃牌选择");
                session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [chosen]));

                Check.Equal(38L - 9, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"), $"{label} Headbutt 基础伤害 9");
                Check.Equal(chosen, session.State.CollectionMembers(drawPile)[0], $"{label} 置顶到位置 0");

                var view = session.Observe(SliceManifest.Player, "base-spire:PlayerView");
                var draw = view.Collections.Single(c => c.QualifiedId == "base-spire:DrawPile");
                Check.Sequence([chosen], draw.RevealedMembers, $"{label} 已知位置授权实例");
                Check.Equal(0, draw.Members.Count, $"{label} 隐藏牌堆不泄漏其余成员");
                Check.Contains(view.Instances, i => i.Id == chosen, $"{label} 已知牌是快照内实体");
            }
        });
    }

    // -- 夹具 -----------------------------------------------------------------------------------------------

    private static Session NewBattle(bool fromAuthorText, bool includeHeadbutt = false)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeHeadbutt, fromAuthorText).Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }

    /// <summary>把指定的牌放进手牌（可选择升级），打向指定敌人，返回敌人剩余 HP。</summary>
    private static long DamageOf(bool fromAuthorText, string contentId, bool upgraded)
    {
        var session = NewBattle(fromAuthorText);
        var card = SliceFixtures.PutInHand(session, contentId, upgraded);
        var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
        SliceFixtures.PlayAt(session, card, front);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
        }

        return SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
    }

    private static long BlockOf(bool fromAuthorText, string contentId, bool upgraded)
    {
        var session = NewBattle(fromAuthorText);
        var card = SliceFixtures.PutInHand(session, contentId, upgraded);
        var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
        SliceFixtures.PlayAt(session, card, front);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
        }

        var player = SliceFixtures.Player(session);
        return SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
    }

    private static (long Damage, long Stacks) AttackWithPower(bool fromAuthorText, string contentId, bool upgraded)
    {
        var session = NewBattle(fromAuthorText);
        var card = SliceFixtures.PutInHand(session, contentId, upgraded);
        var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
        SliceFixtures.PlayAt(session, card, front);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
        }

        var hp = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
        var power = session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:Power").Id)
            .Single();
        return (hp, SliceFixtures.Number(session, power, SliceFixtures.PowerTypeId, "Stacks"));
    }
}
