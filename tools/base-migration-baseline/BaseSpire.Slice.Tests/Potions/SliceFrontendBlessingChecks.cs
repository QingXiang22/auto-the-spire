using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第二十三批：**`BlessingOfTheForge`**——升级整手牌的药水。
///
/// 纯内容：它复用批 16 建立的 `upgrade_hand` 步骤（整手牌置为升级形态并换成各自的升级数值），
/// 因此本批不需要新机制，只需要声明 + 检查。冻结的药水语法里它是 `usage = combatOnly`、`target = self`、
/// `use = upgradeCards(pile=hand)`（`Potions/Content/potions.spire:19`）。
/// </summary>
internal static class SliceFrontendBlessingChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x6  defend-ironclad x2 }
        potions { blessing-of-the-forge }
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
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 0
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 8  steps = [block] }
        }
        potion blessing-of-the-forge {
          name = BlessingOfTheForge  rarity = uncommon  usage = combat-only  target = self
          use { amount = 0  steps = [upgrade_hand] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 整手升级药水：声明被解析，且用后整手牌变成升级形态 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "blessing");
            var potion = model.Potions.Single();
            Check.Equal(PotionTargets.Self, potion.Target, "target = self");
            Check.Sequence([AuthorSteps.UpgradeHand], potion.Steps.Select(step => step.Kind), "步骤＝upgrade_hand");

            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            var before = session.State.CollectionMembers(hand).ToList();
            Check.True(before.Count > 0, "开局手里有牌");
            Check.True(before.All(card => !session.State.GetField(card, cardType.FieldIndex("Upgraded")).AsBool()),
                "升级前都不是升级形态");

            UsePotion(session, "base-spire:blessing-of-the-forge");

            var after = session.State.CollectionMembers(hand).ToList();
            Check.True(after.All(card => session.State.GetField(card, cardType.FieldIndex("Upgraded")).AsBool()),
                "用后整手牌都是升级形态");
            Check.True(after.All(card => before.Contains(card)), "没有生成新牌（还是原来那几个实例）");

            // 数值也换成升级值：升级后的打击造成 9（而不是 6）。
            var strike = after.First(card =>
                session.State.GetField(card, cardType.FieldIndex("DefinitionId")).AsString() == SliceManifest.StrikeId);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(9L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "升级后的打击造成 9（整手升级换了数值）");
        });

        Check.Run("B-05c 整手升级药水：冻结作者文本声明了它是 self 目标、且已进药水栏", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var potion = frozen.Potions.Single(entry => entry.Id == "base-spire:blessing-of-the-forge");
            Check.Equal(PotionTargets.Self, potion.Target, "冻结声明：self");
            Check.Equal("uncommon", potion.Rarity, "冻结声明：uncommon");
            Check.Sequence([AuthorSteps.UpgradeHand], potion.Steps.Select(step => step.Kind), "冻结声明：upgrade_hand");
            Check.Contains(frozen.Belt, entry => entry.PotionId == "base-spire:blessing-of-the-forge",
                "它在药水栏里（可达）");

            // 声明了药水却不在栏位 → 不可达，必须被拒绝。
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("potions { blessing-of-the-forge }", "potions { }"), "blessing");
                Check.True(false, "不可达药水必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.True(failure.Code is "unreachable_potion" or "missing_key" or "unknown_block",
                    $"错误码：{failure.Code}");
            }
        });
        }

    private static void UsePotion(Session session, string potionId)
    {
        var belt = SliceFixtures.Collection(session, "base-spire:PotionBelt");
        var potionType = session.RuleSet.TypeByQualifiedId("base-spire:Potion");
        var potion = session.State.CollectionMembers(belt).Single(candidate =>
            session.State.GetField(candidate, potionType.FieldIndex("DefinitionId")).AsString() == potionId);
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:UsePotion");
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(potion)]);
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
