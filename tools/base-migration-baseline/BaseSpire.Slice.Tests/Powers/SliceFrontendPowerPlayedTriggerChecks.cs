using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十小批）：**`after_card_played` 触发点 + `when-card-type` 过滤 + 回合末整体移除** + `Rage`。
///
/// 来源（两处）：
/// <list type="bullet">
/// <item>`Powers/Content/ironclad-powers.spire:33` —— `power RagePower { trigger = afterCardPlayed
///   whenCardType = attack  effect = gainBlock(1, perPowerStack=RagePower)
///   removeAfterPlayerTurnEnd = true }`；</item>
/// <item>`Cards/Content/ironclad-cards.spire:3565` —— 卡 `Rage`：`cost = 0`、给自己上 3 层（升级 **5** 层）。</item>
/// </list>
///
/// 与上一批（消耗触发）相比，这批多两件事：**牌类过滤**（只对攻击触发）与**回合末整体移除**
/// （`owner-turn-end = remove`；引擎已有 `inst.destroyEntity`，不需要新原语）。
/// </summary>
internal static class SliceFrontendPowerPlayedTriggerChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  rage  defend-ironclad x4 }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power rage-power {
          name = RagePower
          role = RagePower
          effect = "after you play an Attack: gain 1 Block per stack; removed at the end of the turn"
          source = base-spire/Powers/Content/ironclad-powers.spire
          line = 33
          trigger = after_card_played
          when-card-type = Attack
          trigger-effect = gain-block-per-stack:1
          owner-turn-end = remove
        }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 300  tough-hp = 301  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 300  tough-hp = 301  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 6  steps = [target, damage] }
        }
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 0
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 5  steps = [block] }
        }
        card rage {
          name = Rage  kind = Power  cost = 0
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 3565
          normal   { amount = 0  power = rage-power  stacks = 3  steps = [apply_power_self] }
          upgraded { amount = 0  power = rage-power  stacks = 5  steps = [apply_power_self] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 出牌触发点：解析 after_card_played + when-card-type + remove；非法值被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rage");
            var power = model.Powers.Single(entry => entry.Id == "base-spire:rage-power");
            Check.Equal(AuthorPowerTrigger.AfterCardPlayed, power.Trigger, "触发点 = 打出后");
            Check.Equal("Attack", power.WhenCardType, "只对攻击触发");
            Check.Equal(PowerTriggerEffect.GainBlockPerStackPrefix + "1", power.TriggerEffect, "每层 1 点格挡");
            Check.Equal(PowerTurnEnd.Remove, power.OwnerTurnEnd, "回合末整体移除");
            Check.Equal(33, power.SourceLine, "能力来源行");

            var card = model.Cards.Single(entry => entry.Id == "base-spire:rage");
            Check.Equal(3565, card.SourceLine, "卡来源行");
            Check.Equal(0, card.NormalCost, "费用 0");
            Check.Equal(3, card.NormalStacks, "普通 3 层");
            Check.Equal(5, card.UpgradedStacks, "升级 5 层");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("when-card-type = Attack", "when-card-type = AttackAttack"),
                             "invalid_value"),
                         (ScenarioText.Replace("owner-turn-end = remove", "owner-turn-end = vanish"), "invalid_value"),
                         (ScenarioText.Replace("trigger = after_card_played", "trigger = before_card_played"),
                             "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "rage");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                }
            }
        });

        Check.Run("B-06a 出牌触发点：打出**攻击**加格挡，打出**技能**不加；升级 5 层给 +5 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rage");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var rage = SliceFixtures.PutInHand(session, "base-spire:rage", upgraded: false);
            SliceFixtures.PlayAt(session, rage, player);

            // 注意 Rage 自己也是"打出的牌"，但它是 Power 类 → 不触发（filter 生效）。
            var blockAfterRage = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            Check.Equal(0L, blockAfterRage, "打出 Power 类不触发（when-card-type = Attack）");

            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(3L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "打出攻击 → +3 格挡（3 层）");

            var defend = SliceFixtures.PutInHand(session, "base-spire:defend-ironclad", upgraded: false);
            SliceFixtures.PlayAt(session, defend, player);
            // 技能自己给 5 点格挡，但**不**触发 Rage。
            Check.Equal(8L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "技能只给自身格挡 5（3 + 5 = 8），没有额外的触发");

            var strike2 = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike2, target);
            Check.Equal(11L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "再打一次攻击 → 再 +3");
        });

        Check.Run("B-06a 出牌触发点：升级 5 层 → 每次攻击 +5 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rage");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var rage = SliceFixtures.PutInHand(session, "base-spire:rage", upgraded: true,
                (Amount: 0, PowerStacks: 5));
            SliceFixtures.PlayAt(session, rage, player);

            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(5L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "5 层 → +5 格挡");
        });

        Check.Run("B-06a 出牌触发点：回合末把整个能力移除（下一回合不再触发）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rage");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var rage = SliceFixtures.PutInHand(session, "base-spire:rage", upgraded: false);
            SliceFixtures.PlayAt(session, rage, player);
            Check.Equal(3L, Stacks(session, player, "base-spire:rage-power"), "打出后 3 层");

            EndTurn(session);
            Check.Equal(0L, Stacks(session, player, "base-spire:rage-power"),
                "回合末整体移除（不是减 1 层）");

            // 移除之后：再打攻击不再给格挡（新回合格挡从 0 开始，触发会体现在数值上）。
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(0L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "能力已移除：攻击不再给格挡");
        });

        Check.Run("B-06a 出牌触发点：冻结作者文本声明了 RagePower 与 Rage（来源行与两形态层数）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var power = frozen.Powers.Single(entry => entry.Id == "base-spire:rage-power");
            Check.Equal(33, power.SourceLine, "能力来源行");
            Check.Equal(AuthorPowerTrigger.AfterCardPlayed, power.Trigger, "触发点");
            Check.Equal("Attack", power.WhenCardType, "只对攻击");
            Check.Equal(PowerTurnEnd.Remove, power.OwnerTurnEnd, "回合末移除");
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:rage");
            Check.Equal(3565, card.SourceLine, "卡来源行");
            Check.Equal(3, card.NormalStacks, "普通 3 层");
            Check.Equal(5, card.UpgradedStacks, "升级 5 层");
        });
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
