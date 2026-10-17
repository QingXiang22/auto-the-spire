using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第七小批）：**每变体费用（`cost` 变体键）+ 以自身格挡为数值来源 + Entrench**。
///
/// 来源：`Cards/Content/ironclad-cards.spire:1752` —— `play = { gainBlockFromCurrentBlock = { } }`，
/// 普通 `cost = 2`、升级 `cost = 1`。两个来源事实各对应本批一项能力：
/// <list type="bullet">
/// <item>**费用逐变体不同**（"升级更便宜"）——作者模型原先只有单一 `cost`；</item>
/// <item>**数值来源是"自己当前的格挡"**——新增来源词 `block-count`（与既有的 hand/discard/draw-count 同形）。</item>
/// </list>
/// 判据：来源对照、两形态费用、翻倍语义（先有 7 点格挡 → 打出后 14）、以及**升级后费用真的变 1**。
/// </summary>
internal static class SliceFrontendEntrenchChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { defend-ironclad x12  entrench }
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
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 1
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 5  steps = [block] }
        }
        potion blessing-of-the-forge {
          name = BlessingOfTheForge  rarity = uncommon  usage = combat-only  target = self
          use { amount = 0  steps = [upgrade_hand] }
        }
        card entrench {
          name = Entrench  kind = Skill  cost = 2
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 1752
          normal   { cost = 2  amount-source = block-count  steps = [block] }
          upgraded { cost = 1  amount-source = block-count  steps = [block] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 每变体费用：`cost` 变体键覆盖卡牌级费用，越界被拒绝", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "entrench");
            var entrench = model.Cards.Single(card => card.Id == "base-spire:entrench");
            Check.Equal(1752, entrench.SourceLine, "来源行");
            Check.Equal(2, entrench.NormalCost, "普通费用 2");
            Check.Equal(1, entrench.UpgradedCost, "升级费用 1（来源事实）");
            Check.Equal(AmountSource.BlockCount, entrench.NormalAmountSource, "数值来源是自身格挡");
            Check.Equal(AmountSource.BlockCount, entrench.UpgradedAmountSource, "升级同样是自身格挡");

            // 切片那 4 张卡的升级费用与普通相同（冻結数据一字未改）。
            var frozen = SliceAuthorFrontend.Load();
            Check.True(SliceManifest.Cards.All(entry =>
                    frozen.Cards.Single(card => card.Id == entry.ContentId).NormalCost
                    == frozen.Cards.Single(card => card.Id == entry.ContentId).UpgradedCost),
                "切片那 4 张卡两形态费用相同（冻结数据未变）");

            foreach (var bad in new[] { "10", "-1", "two" })
            {
                try
                {
                    SliceAuthorFrontend.Parse(
                        ScenarioText.Replace("upgraded { cost = 1", $"upgraded {{ cost = {bad}"), "entrench");
                    Check.True(false, $"费用 '{bad}' 必须被拒绝");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal("invalid_value", failure.Code, $"错误码（{bad}）");
                }
            }
        });

        Check.Run("B-06a 以格挡为来源：Entrench 让格挡**翻倍**（读的是打出时的格挡）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "entrench");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            // 先造出 7 点格挡，再打出 Entrench。
            session.State.RunAtomic(() => session.State.SetField(player, type.FieldIndex("Block"), Value.Of(7L)));
            var entrench = SliceFixtures.PutInHand(session, "base-spire:entrench", upgraded: false);
            var energyBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            SliceFixtures.PlayAt(session, entrench, player);

            Check.Equal(14L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "7 → 14（获得等于当前格挡的格挡）");
            Check.Equal(energyBefore - 2, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "普通形态花 2 点能量");
        });

        Check.Run("B-06a 每变体费用：升级形态只花 1 点能量（费用随升级真的变了）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "entrench");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            session.State.RunAtomic(() => session.State.SetField(player, type.FieldIndex("Block"), Value.Of(3L)));
            // 走**真实的升级路径**（`upgrade_hand`，即 BlessingOfTheForge 的过程），而不是直接写字段：
            // 费用必须由升级路径改写。
            var entrench = SliceFixtures.PutInHand(session, "base-spire:entrench", upgraded: false);
            UsePotion(session, "base-spire:blessing-of-the-forge");

            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            Check.Equal(1L, session.State.GetField(entrench, cardType.FieldIndex("Cost")).AsInt(),
                "升级路径把费用改写成 1");
            Check.True(session.State.GetField(entrench, cardType.FieldIndex("Upgraded")).AsBool(), "它确实升级了");

            var energyBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            SliceFixtures.PlayAt(session, entrench, player);
            Check.Equal(energyBefore - 1, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "升级形态只花 1 点");
            Check.Equal(6L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "3 → 6（升级不影响翻倍语义）");
        });

        Check.Run("B-06a 以格挡为来源：冻结作者文本声明了 Entrench（来源行与两形态费用）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var entrench = frozen.Cards.Single(card => card.Id == "base-spire:entrench");
            Check.Equal(1752, entrench.SourceLine, "来源行");
            Check.Equal(2, entrench.NormalCost, "普通 2");
            Check.Equal(1, entrench.UpgradedCost, "升级 1");
            Check.Equal(AmountSource.BlockCount, entrench.UpgradedAmountSource, "升级也是读自身格挡");
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
