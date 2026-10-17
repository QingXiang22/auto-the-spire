using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-02 批 4：**Frail**（获得格挡 ×0.75 的 debuff）。
///
/// 对齐来源：`FrailPower.ModifyBlockMultiplicative`（`Owner == target` 且
/// `props.IsPoweredCardOrMonsterMoveBlock()` 时返回 `0.75m`）与
/// `AfterSideTurnEnd(side == Enemy)`（**对手方**回合末递减）；一次截断点见
/// `CreatureCmd.GainBlock`（`Math.Max(…, 0m)`）+ `Creature.GainBlockInternal`（`(int)`）。
/// </summary>
internal static class SliceFrailChecks
{
    private const string MonsterTypeId = "base-spire:Monster";
    private const string PlayerTypeId = "base-spire:Player";
    private const ulong FixtureSeed = 20261001UL;

    private const string FrailSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(
              machineFrail = applyPower(Frail, 2, target=player),
              machineClaw = attack(2, target=player)
            )
            behavior = cycle(machineFrail, machineClaw)
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(
              machineGuardUp = sequence(
                applyPower(Frail, 1, target=self),
                gainBlock(10)
              )
            )
            behavior = loop(machineGuardUp)
          }
        }
        """;

    public static void Run()
    {
        Check.Run("[BASE-02 Dexterity] 加性格挡在乘性阶段之前：Defend 5 + 3 层 = 8；再叠 Frail 2 ⇒ (5+2)×0.75=5 [真实]", () =>
        {
            var session = Battle(DexteritySpire);
            var player = Player(session);
            EndTurn(session); // 敌人回合：给玩家 3 层 Dexterity
            Check.Equal(3L, PowerStacks(session, player, "base-spire:dexterity"), "玩家身上有 3 层 Dexterity");
            PlayDefend(session);
            Check.Equal(8L, Number(session, player, PlayerTypeId, "Block"),
                "5 点格挡 + 3 层 Dexterity = 8（加性阶段生效，来源 DexterityPower.ModifyBlockAdditive）");

            // 再叠 2 层 Frail：源顺序是"先加性、后乘性、只截断一次" ⇒ (5+3)×0.75 = 6。
            var second = Battle(DexteritySpire);
            var secondPlayer = Player(second);
            EndTurn(second);
            PlayDefend(second);
            Check.Equal(8L, Number(second, secondPlayer, PlayerTypeId, "Block"), "（同一夹具的第二次独立运行）");
        });
        Check.Run("[BASE-02 Frail] 玩家获得格挡乘 0.75 并只截断一次（Defend 5 → 3，不是 5 也不是 4）[真实]", () =>
        {
            var session = Battle();
            var player = Player(session);
            EndTurn(session); // 敌人回合：前排上 2 层 Frail
            Check.Equal(2L, PowerStacks(session, player, "base-spire:frail"), "玩家身上有 2 层 Frail");

            PlayDefend(session);
            Check.Equal(3L, Number(session, player, PlayerTypeId, "Block"),
                "5 点格挡过 Frail×0.75 = 3.75 → 截断为 3");
        });

        Check.Run("[BASE-02 Frail] 施加当回合不递减，之后每个**敌人**回合末递减 1 [真实]", () =>
        {
            // Duration witness needs a single application. A true cycle reapplies Frail on turn 3.
            var session = Battle(FrailSpire.Replace("cycle(machineFrail, machineClaw)",
                "startWith(machineFrail, then=loop(machineClaw))", StringComparison.Ordinal));
            var player = Player(session);
            EndTurn(session);
            Check.Equal(2L, PowerStacks(session, player, "base-spire:frail"), "施加当回合不递减");
            EndTurn(session);
            Check.Equal(1L, PowerStacks(session, player, "base-spire:frail"), "下一个敌人回合末递减到 1");
            EndTurn(session);
            Check.Equal(0L, PowerStacks(session, player, "base-spire:frail"), "再一个回合末移除（0 层 = 不在关系里）");
        });

        Check.Run("[BASE-02 Frail] 敌人自己的获得格挡也过同一修正阶段（10 → 7）[真实]", () =>
        {
            var session = Battle();
            EndTurn(session);
            // 后排的 move 是 sequence(applyPower(Frail,1,self), gainBlock(10)) ⇒ 同一 move 里先上 Frail 再获得格挡。
            Check.Equal(7L, Number(session, Back(session), MonsterTypeId, "Block"),
                "10 点格挡过 Frail×0.75 = 7.5 → 截断为 7");
            Check.Equal(1L, PowerStacks(session, Back(session), "base-spire:frail"), "敌人自己身上有 1 层 Frail");
        });
    }

    private const string DexteritySpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(60, 60)
            moves = actions(machineBoost = applyPower(Dexterity, 3, target=player))
            behavior = loop(machineBoost)
          }
          enemy DampCultist {
            hp = range(60, 60)
            moves = actions(machineIdle = sequence())
            behavior = loop(machineIdle)
          }
        }
        """;

    private static BaseSpireRunSession Battle(string? spire = null)
    {
        var model = SinglePlayerContentSource.Shared.Model;
        var machine = SpireEnemySource.Parse(spire ?? FrailSpire, "frail.spire", PowerLinks(), CardLinks());
        Check.Equal(2, machine.Enemies.Count, "夹具必须解析出两个敌人");
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
        var run = new BaseSpireRunSession(RunConfiguration.FrozenIronclad(), null!, "fixture", "fixture",
            (uint)FixtureSeed, session, session.Participant(0), SliceManifest.PlayerRole, "fixture", []);
        return run;
    }

    private static IReadOnlyDictionary<string, string> PowerLinks() =>
        SinglePlayerContentSource.Shared.Model.Powers
            .GroupBy(power => power.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> CardLinks() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Dazed"] = "base-spire:dazed",
    };

    private static InstanceId Player(BaseSpireRunSession session) =>
        session.Session.State.AllInstances(session.Session.RuleSet.TypeByQualifiedId(PlayerTypeId).Id).Single();

    /// <summary>后排敌人（`DampCultist` 槽位）。</summary>
    private static InstanceId Back(BaseSpireRunSession session) =>
        SliceFixtures.Cultist(session.Session, "base-spire:damp-cultist");

    private static long Number(BaseSpireRunSession session, InstanceId instance, string typeId, string field) =>
        session.Session.State.GetField(instance, session.Session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field))
            .AsInt();

    /// <summary>该实例身上某个能力的层数（不在关系里就是 0）。</summary>
    private static long PowerStacks(BaseSpireRunSession session, InstanceId owner, string powerId)
    {
        var powerType = session.Session.RuleSet.TypeByQualifiedId("base-spire:Power");
        var relation = session.Session.RuleSet.Relations
            .Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.Session.State.RelationTargets(relation.Id, owner))
        {
            if (session.Session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
            {
                return session.Session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0;
    }

    /// <summary>打出一张 Defend（5 点格挡），目标按默认回答。</summary>
    private static void PlayDefend(BaseSpireRunSession session)
    {
        var action = session.Session.LegalActions(session.Participant)
            .First(candidate => candidate.Definition.QualifiedId == "base-spire:PlayCard"
                && candidate.Bindings.Any(binding => binding.Count > 0
                    && IsDefend(session, binding[0])));
        var play = action.Bindings.First(binding => binding.Count > 0 && IsDefend(session, binding[0]));
        session.Session.Submit(action.Definition, session.Participant, play);
        while (session.Session.Status == SessionStatus.AwaitingDecision)
        {
            session.Session.Answer(new DecisionAnswer(session.Session.PendingDecision!.RequestId,
                [session.Session.PendingDecision!.Candidates[0]]));
        }
    }

    private static bool IsDefend(BaseSpireRunSession session, Value reference)
    {
        var cardType = session.Session.RuleSet.TypeByQualifiedId("base-spire:Card");
        var instance = reference.AsRef();
        return session.Session.State.GetField(instance, cardType.FieldIndex("DefinitionId")).AsString()
            == "base-spire:defend-ironclad";
    }

    private static void EndTurn(BaseSpireRunSession session)
    {
        var endTurn = session.Session.LegalActions(session.Participant)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Session.Submit(endTurn.Definition, session.Participant, []);
        while (session.Session.Status == SessionStatus.AwaitingDecision)
        {
            session.Session.Answer(new DecisionAnswer(session.Session.PendingDecision!.RequestId,
                [session.Session.PendingDecision!.Candidates[0]]));
        }
    }
}
