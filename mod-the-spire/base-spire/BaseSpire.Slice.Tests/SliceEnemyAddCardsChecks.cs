using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-02 批 3：敌人行动的 **`addCards`**（往玩家牌区放状态牌）。
///
/// 对齐来源：`CardPileCmd.AddToCombatAndPreview&lt;T&gt;(targets, pileType, count, creator, position)`
/// （`Chomper.cs:82` 的 3 张 Dazed）+ `CardPileCmd.Add` 的位置语义（`CardPileCmd.cs:510-515`）：
/// `Bottom` = 追加（末尾）、`Top` = 插到最前、`Random` = **每张**各抽一次
/// `Rng.Shuffle.NextInt(当前牌区张数 + 1)`（⇒ 本包 `combat-draw` 流）。
/// </summary>
internal static class SliceEnemyAddCardsChecks
{
    private const string MonsterTypeId = "base-spire:Monster";
    private const string DrawStream = SliceManifest.DrawStream;
    private const ulong FixtureSeed = 20261001UL;
    private const ulong FixtureStreamSeed = 0xADD_1234UL;

    private const string DiscardSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(44, 44)
            moves = actions(machineAdd = addCards(Dazed, count=3, pile=discard, target=player))
            behavior = loop(machineAdd)
          }
          enemy DampCultist {
            hp = range(51, 51)
            moves = actions(machineBite = attack(1, target=player))
            behavior = loop(machineBite)
          }
        }
        """;

    private const string RandomSpire = """
        package base-spire {
          enemy CalcifiedCultist {
            hp = range(44, 44)
            moves = actions(machineAdd = addCards(Dazed, count=2, pile=draw, target=player, position=random))
            behavior = loop(machineAdd)
          }
          enemy DampCultist {
            hp = range(51, 51)
            moves = actions(machineBite = attack(1, target=player))
            behavior = loop(machineBite)
          }
        }
        """;

    public static void Run()
    {
        Check.Run("[BASE-02 addCards] 解析：牌区/位置/张数逐项校验，未支持写法与悬空卡名在解析期拒绝", () =>
        {
            var links = CardLinks();
            var good = SpireEnemySource.Parse(DiscardSpire, "add-cards", null, links);
            Check.Equal(2, good.Enemies.Count, "两个敌人都应解析成功");
            Check.Sequence(["add_cards = base-spire:dazed:3:discard:bottom"],
                good.Enemies[0].Moves[0].Steps.Select(step => step.ToString()), "discard/bottom 的编码");
            var random = SpireEnemySource.Parse(RandomSpire, "add-cards-random", null, links);
            Check.Sequence(["add_cards = base-spire:dazed:2:draw:random"],
                random.Enemies[0].Moves[0].Steps.Select(step => step.ToString()), "draw/random 的编码");

            foreach (var (text, fragment) in new[]
                     {
                         (DiscardSpire.Replace("addCards(Dazed, count=3, pile=discard, target=player)",
                             "addCards(Dazed, count=3, pile=exhaust, target=player)"), "牌区"),
                         (DiscardSpire.Replace("addCards(Dazed, count=3, pile=discard, target=player)",
                             "addCards(Dazed, count=3, pile=discard, target=self)"), "target=player"),
                         (RandomSpire.Replace("position=random", "position=middle"), "位置"),
                         (DiscardSpire.Replace("addCards(Dazed, count=3, pile=discard, target=player)",
                             "addCards(Unknown, count=3, pile=discard, target=player)"), "不在**已加载**的卡牌源"),
                     })
            {
                var source = SpireEnemySource.Parse(text, "add-cards-reject", null, links);
                var reasons = string.Join(" | ", source.Unsupported.Select(entry => entry.Reason));
                Check.True(reasons.Contains(fragment, StringComparison.Ordinal),
                    $"拒绝原因必须点名 '{fragment}'（实际：{reasons}）");
            }
        });

        Check.Run("[BASE-02 addCards] discard/bottom：每次行动把 3 张状态牌追加到弃牌堆，且不抽任何随机数 [真实]", () =>
        {
            var (session, front, _) = Battle(DiscardSpire);
            var discard = Pile(session, "base-spire:DiscardPile");
            var dazedBefore = TotalDazed(session);
            var drawsBefore = Draws(session);
            EndTurn(session);
            // 玩家端末会把整手牌弃掉（+5），随后**下一回合开始**还可能把弃牌堆洗回抽牌堆，
            // 因此这里按"全场 Dazed 张数"判断加牌效果，并单独核对"追加在弃牌堆末尾"。
            Check.Equal(dazedBefore + 3, TotalDazed(session), "这一回合净增 3 张 Dazed");
            Check.True(CountDefinition(session, discard, "base-spire:dazed") >= 3,
                "刚追加的 3 张应在弃牌堆里");
            Check.Sequence(Enumerable.Repeat("base-spire:dazed", 3),
                DefinitionOrder(session, discard, 3), "bottom 位置 ⇒ 追加在弃牌堆**末尾**");
            Check.Equal(drawsBefore, Draws(session), "bottom 位置不消费随机数");
            EndTurn(session);
            Check.Equal(dazedBefore + 6, TotalDazed(session), "第二次行动再追加 3 张（全场净增 6）");
            Check.Equal("machineAdd", NextMove(session.Session, front), "行动状态机仍在跑同一个行动");
        });

        Check.Run("[BASE-02 addCards] draw/random：位置 = 独立复算的 `NextInt(当前张数+1)`，每张各一次抽取 [真实]", () =>
        {
            var (session, front, _) = Battle(RandomSpire);
            var draw = Pile(session, "base-spire:DrawPile");
            var hand = Pile(session, "base-spire:Hand");
            var sizeBefore = Count(session, draw);
            var pileBefore = DefinitionOrder(session, draw, sizeBefore);
            var drawsBefore = Draws(session);
            var streamSeed = SessionStreamSeed();

            EndTurn(session);

            Check.Equal(drawsBefore + 2, Draws(session), "两张随机位置的牌各抽一次 `combat-draw`");
            Check.Equal(2, CountDefinition(session, draw, "base-spire:dazed")
                + CountDefinition(session, hand, "base-spire:dazed"), "两张 Dazed 都已创建（在抽牌堆或已被抽进手牌）");

            // 独立复算：同一 `combat-draw` 种子，跳过本回合之前已经发生的抽取（开局洗牌等），
            // 再按"当前张数 + 1"的范围逐张抽下标并插入；随后**玩家下一回合抽 5 张**会把牌堆前 5 张移到手牌。
            var probe = new RandomService(FixtureSeed, VanillaRandomLayout.AlgorithmId,
                VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
            probe.SeedStream(DrawStream, streamSeed);
            for (var skip = 0; skip < drawsBefore; skip++)
            {
                probe.DrawDouble(DrawStream);
            }

            var expected = new List<int>();
            var size = sizeBefore;
            for (var card = 0; card < 2; card++)
            {
                expected.Add((int)probe.DrawInt(DrawStream, 0, size + 1));
                size++;
            }

            // 复算插入后的抽牌堆（"D" = 本次加入的 Dazed，"." = 原有牌），再按抽 5 张模拟一次。
            var simulated = Enumerable.Repeat(".", sizeBefore).ToList();
            for (var card = 0; card < 2; card++)
            {
                simulated.Insert(expected[card], "D");
            }

            var expectedDrawnDazed = simulated.Take(SliceManifest.DrawPerTurn).Count(entry => entry == "D");
            var expectedRemaining = simulated.Skip(SliceManifest.DrawPerTurn)
                .Select((entry, index) => (entry, index))
                .Where(item => item.entry == "D")
                .Select(item => item.index)
                .Order()
                .ToList();

            Check.Equal(expectedDrawnDazed, CountDefinition(session, hand, "base-spire:dazed"),
                "被抽进手牌的 Dazed 张数必须与复算一致");
            Check.Sequence(expectedRemaining.Order(), Positions(session, draw, "base-spire:dazed").Order(),
                "抽牌堆里剩下的 Dazed 下标必须与复算一致（插入下标 = 当前张数+1 范围内的抽取）");
            Console.WriteLine($"      BASE-02 addCards：draw/random 插入下标 复算={string.Join(",", expected)}"
                + $"（抽牌堆 {sizeBefore}→{size}，被抽走 {expectedDrawnDazed} 张）");
        });
    }

    private static IReadOnlyDictionary<string, string> CardLinks() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Dazed"] = "base-spire:dazed",
    };

    private static ulong SessionStreamSeed() => FixtureStreamSeed;

    private static (BaseSpireRunSession Session, InstanceId Front, InstanceId Back) Battle(string enemySpire)
    {
        var model = SliceAuthorFrontend.Load();
        var machine = SpireEnemySource.Parse(enemySpire, "add-cards.spire", null, CardLinks());
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
        session.Random.SeedStream(DrawStream, FixtureStreamSeed);
        session.Start();
        var run = new BaseSpireRunSession(RunConfiguration.Ironclad(), null!, "fixture", "fixture",
            (uint)FixtureSeed, session, session.Participant(0), SliceManifest.PlayerRole, "fixture", []);
        return (run, SliceFixtures.Cultist(session, "base-spire:calcified-cultist"),
            SliceFixtures.Cultist(session, "base-spire:damp-cultist"));
    }

    private static string NextMove(Session session, InstanceId monster) =>
        SliceFixtures.Text(session, monster, MonsterTypeId, "NextMove");

    private static long Draws(BaseSpireRunSession session) => session.Session.Random.DrawCount(DrawStream);

    private static int Pile(BaseSpireRunSession session, string qualifiedId) =>
        session.Session.RuleSet.Collections.Single(entry => entry.QualifiedId == qualifiedId).Id;

    private static int Count(BaseSpireRunSession session, int pile) =>
        session.Session.State.CollectionMembers(pile).Count;

    private static int CountDefinition(BaseSpireRunSession session, int pile, string definitionId)
    {
        var type = session.Session.RuleSet.TypeByQualifiedId("base-spire:Card");
        return session.Session.State.CollectionMembers(pile).Count(instance =>
            session.Session.State.GetField(instance, type.FieldIndex("DefinitionId")).AsString() == definitionId);
    }

    /// <summary>全场（手牌/抽牌堆/弃牌堆/消耗区）的 Dazed 张数：加牌效果不因洗牌而失真。</summary>
    private static int TotalDazed(BaseSpireRunSession session) =>
        new[] { "base-spire:Hand", "base-spire:DrawPile", "base-spire:DiscardPile", "base-spire:ExhaustPile" }
            .Sum(name => CountDefinition(session, Pile(session, name), "base-spire:dazed"));

    /// <summary>牌区**最后 <paramref name="count"/> 个成员**的内容 id（用于核对追加/插入位置）。</summary>
    private static IReadOnlyList<string> DefinitionOrder(BaseSpireRunSession session, int pile, int count)
    {
        var type = session.Session.RuleSet.TypeByQualifiedId("base-spire:Card");
        var members = session.Session.State.CollectionMembers(pile);
        return
        [
            .. members.Skip(Math.Max(0, members.Count - count)).Select(instance =>
                session.Session.State.GetField(instance, type.FieldIndex("DefinitionId")).AsString()),
        ];
    }

    private static IReadOnlyList<int> Positions(BaseSpireRunSession session, int pile, string definitionId)
    {
        var type = session.Session.RuleSet.TypeByQualifiedId("base-spire:Card");
        var members = session.Session.State.CollectionMembers(pile);
        var positions = new List<int>();
        for (var index = 0; index < members.Count; index++)
        {
            if (session.Session.State.GetField(members[index], type.FieldIndex("DefinitionId")).AsString()
                == definitionId)
            {
                positions.Add(index);
            }
        }

        return positions;
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
