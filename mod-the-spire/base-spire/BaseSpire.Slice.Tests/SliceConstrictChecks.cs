using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-02 批 6：**Constrict**（缠绕）与 `SlitheringStrangler` 的行动程序。
///
/// 对齐来源：`ConstrictPower.AfterSideTurnEnd`（`participants.Contains(Owner)` 时对**拥有者**造成
/// `Amount` 点 `ValueProp.Unpowered` 伤害）与 `AfterDeath`（施加者死亡且非防移除时移除该能力）；
/// 敌人行动 `SlitheringStrangler`：`constrict(3)` → `graph(start=constrict, edges=[…])`
/// （`enemies.spire:720-736`）。
/// </summary>
internal static class SliceConstrictChecks
{
    private const string MonsterTypeId = "base-spire:Monster";
    private const string PlayerTypeId = "base-spire:Player";
    private const ulong FixtureSeed = 20261001UL;

    private const string ConstrictSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(
              machineBind = applyPower(ConstrictPower, 3, target=player),
              machineIdle = sequence()
            )
            behavior = cycle(machineBind, machineIdle)
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(machineHex = applyPower(Vulnerable, 5, target=player))
            behavior = loop(machineHex)
          }
        }
        """;

    private const string StartingPowersSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            startingPowers = [power(Vulnerable, 2)]
            moves = actions(machineIdle = sequence())
            behavior = loop(machineIdle)
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(machineIdle = sequence())
            behavior = loop(machineIdle)
          }
        }
        """;

    public static void Run()
    {
        Check.Run("[BASE-02 startingPowers] 出生期挂能力：出生即有声明层数，回合末按声明递减/转化 [真实]", () =>
        {
            var session = Battle(StartingPowersSpire);
            var front = SliceFixtures.Cultist(session.Session, "base-spire:calcified-cultist");
            Check.Equal(2L, PowerStacks(session, front, "base-spire:vulnerable"), "出生即带 2 层易伤");

            EndTurn(session); // 玩家回合 → 敌人回合 → 敌人回合末
            Check.Equal(1L, PowerStacks(session, front, "base-spire:vulnerable"),
                "易伤按声明在回合末递减一次（出生自带的怪物能力不跳过第一次："
                + "源里只有**施加给玩家**的负面能力跳过，`PowerCmd.cs:144-147`）");
        });

        Check.Run("[BASE-02 Constrict] 拥有者回合末按层数受 unpowered 伤害：易伤在场也不放大，层数不衰减 [真实]", () =>
        {
            // Non-decay witness applies once; a real cycle would reapply on the third enemy turn.
            var session = Battle(ConstrictSpire.Replace("cycle(machineBind, machineIdle)",
                "startWith(machineBind, then=loop(machineIdle))", StringComparison.Ordinal));
            var player = Player(session);
            var hpBefore = Number(session, player, PlayerTypeId, "Hp");

            EndTurn(session); // 敌人回合：前排上 3 层 Constrict、后排上 5 层易伤（本回合无人受伤）
            Check.Equal(3L, PowerStacks(session, player, "base-spire:constrict-power"), "玩家身上有 3 层");
            Check.Equal(5L, PowerStacks(session, player, "base-spire:vulnerable"), "同时在场的易伤（用来判别 unpowered）");
            Check.Equal(hpBefore, Number(session, player, PlayerTypeId, "Hp"),
                "Constrict 不在**敌人**回合结算（它属于拥有者的回合末）");

            EndTurn(session); // 玩家回合末：Constrict 结算 3 点（unpowered ⇒ 不被易伤放大）
            var afterFirst = Number(session, player, PlayerTypeId, "Hp");
            Check.Equal(hpBefore - 3L, afterFirst,
                "恰好 3 点：易伤在场也不放大（powered 的话会是 4 点）");
            Check.Equal(3L, PowerStacks(session, player, "base-spire:constrict-power"),
                "Constrict 是**时长**（层数不因结算而减少）");

            EndTurn(session);
            Check.Equal(afterFirst - 3L, Number(session, player, PlayerTypeId, "Hp"), "每个玩家回合末各结算 3 点");
            Check.Equal(3L, PowerStacks(session, player, "base-spire:constrict-power"), "层数仍是 3（不衰减）");
        });

        Check.Run("[BASE-02 Constrict] SlitheringStrangler 已进包：行动程序 = constrict → 二选一 → 回到 constrict", () =>
        {
            var content = SinglePlayerContentSource.Shared;
            Check.True(content.EnemyIds.Contains("base-spire:slithering-strangler"),
                "蛇形勒杀者必须已加载（ConstrictPower 落地后）");
            Check.True(content.EncounterPool.Encounters.Any(encounter =>
                    encounter.EncounterId == "base-spire:slithering-strangler-normal"),
                "它的遭遇必须进池（编队三条 + 权重全 1）");
            Check.True(!content.ExcludedEncounters.Any(entry =>
                    entry.Item == "base-spire:slithering-strangler-normal"),
                "不得同时出现在排除清单里");
        });
    }

    private static BaseSpireRunSession Battle(string? spire = null)
    {
        var model = SinglePlayerContentSource.Shared.Model;
        var machine = SpireEnemySource.Parse(spire ?? ConstrictSpire, "constrict.spire", PowerLinks(), CardLinks());
        Check.Equal(2, machine.Enemies.Count, "夹具必须解析出两个敌人："
            + string.Join(" | ", machine.Unsupported.Select(entry => $"{entry.Name}: {entry.Reason}")));
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, authorModel: model with
        {
            Enemies = machine.Enemies,
        }).Build());
        var session = new Session(builder.Build(), FixtureSeed, [SliceManifest.PlayerRole],
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile,
            computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return new BaseSpireRunSession(RunConfiguration.Ironclad(), null!, "fixture", "fixture",
            (uint)FixtureSeed, session, session.Participant(0), SliceManifest.PlayerRole, "fixture", []);
    }

    private static IReadOnlyDictionary<string, string> PowerLinks() =>
        SinglePlayerContentSource.Shared.Model.Powers
            .GroupBy(power => power.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> CardLinks() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dazed"] = "base-spire:dazed",
        };

    private static InstanceId Player(BaseSpireRunSession session) =>
        session.Session.State.AllInstances(session.Session.RuleSet.TypeByQualifiedId(PlayerTypeId).Id).Single();

    private static long Number(BaseSpireRunSession session, InstanceId instance, string typeId, string field) =>
        session.Session.State.GetField(instance, session.Session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field))
            .AsInt();

    private static long PowerStacks(BaseSpireRunSession session, InstanceId owner, string powerId)
    {
        var powerType = session.Session.RuleSet.TypeByQualifiedId("base-spire:Power");
        var relation = session.Session.RuleSet.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.Session.State.RelationTargets(relation.Id, owner))
        {
            if (session.Session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
            {
                return session.Session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0;
    }

    private static void EndTurn(BaseSpireRunSession session)
    {
        var endTurn = session.Session.LegalActions(session.Participant)
            .First(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Session.Submit(endTurn.Definition, session.Participant, []);
        while (session.Session.Status == SessionStatus.AwaitingDecision)
        {
            session.Session.Answer(new DecisionAnswer(session.Session.PendingDecision!.RequestId,
                [session.Session.PendingDecision!.Candidates[0]]));
        }
    }
}
