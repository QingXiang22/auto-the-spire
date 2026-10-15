using BaseSpire.SinglePlayer;
using CoreTheSpire.Engine;
using static BaseSpire.Slice.Tests.FormalSliceFixture;

namespace BaseSpire.Slice.Tests;

internal static class SliceByrdonisChecks
{
    private const string Byrd = "base-spire:byrdonis";
    private const string Territory = "base-spire:territorial-power";

    public static void Run()
    {
        Check.Run("Byrdonis 正式注册、普通HP范围与 tough/deadly 声明不倒置", () =>
        {
            var enemy = SinglePlayerContentSource.Shared.Model.Enemies.Single(e => e.Id == Byrd);
            Check.Equal(81, enemy.NormalMinHp, "普通下限");
            Check.Equal(84, enemy.NormalMaxHp, "普通上限");
            Check.Equal(90, enemy.ToughMinHp, "tough 下限");
            Check.Equal(90, enemy.ToughMaxHp, "tough 上限");
            var session = Battle();
            var monster = Cultist(session, Byrd);
            Check.Equal(81L, Number(session, monster, MonsterTypeId, "Hp"), "受控无HP随机入口取正式范围下限");
            Check.Equal(1L, PowerStacks(session, monster, Territory), "出生Territorial1");
            Check.Equal(0L, PowerStacks(session, monster, SliceManifest.StrengthId), "应用时不提前加力量");
        });
        Check.Run("Byrdonis SWOOP17 → PECK(3+1)×3 → SWOOP19；Territorial不消耗且固定循环不抽AI", () =>
        {
            var session = Battle();
            var monster = Cultist(session, Byrd);
            var player = Player(session);
            Check.Sequence(new long[] { 17, 12, 19 }, Enumerable.Range(0, 3).Select(turn =>
            {
                var expectedMove = turn % 2 == 0 ? "byrdonisSwoop" : "byrdonisPeck";
                Check.Equal(expectedMove, SliceFixtures.Text(session, monster, MonsterTypeId, "NextMove"), "交替动作状态");
                var before = Number(session, player, PlayerTypeId, "Hp");
                EndTurn(session);
                Check.Equal(turn + 1L, PowerStacks(session, monster, SliceManifest.StrengthId), "第一次回合末即加力量");
                Check.Equal(1L, PowerStacks(session, monster, Territory), "源层数不递减/移除");
                return before - Number(session, player, PlayerTypeId, "Hp");
            }).ToArray(), "力量影响后续每一击而非只加一次");
            Check.Equal(0L, session.Random.DrawCount(VanillaRandomLayout.EngineStreamName("MonsterAi")), "固定动作转移不抽MonsterAi");
        });
        Check.Run("Territorial按当前层数与拥有者侧触发；玩家不是敌方participants，不得额外触发", () =>
        {
            var session = Battle();
            var player = Player(session);
            var monster = Cultist(session, Byrd);
            GivePower(session, player, Territory, 2);
            var powerType = session.RuleSet.TypeByQualifiedId(PowerTypeId);
            var relation = session.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:Powers");
            var source = session.State.RelationTargets(relation.Id, monster).Single(p => Definition(session, p, PowerTypeId) == Territory);
            SetNumber(session, source, PowerTypeId, "Stacks", 3);
            EndTurn(session);
            Check.Equal(2L, PowerStacks(session, player, SliceManifest.StrengthId), "玩家回合末+2，敌方回合末不再+2");
            Check.Equal(3L, PowerStacks(session, monster, SliceManifest.StrengthId), "怪物读取当前3层，而非出生1层");
            Check.Equal(3L, Number(session, source, PowerTypeId, "Stacks"), "非Ritual、不消费");
        });
    }

    // Controlled mechanism fixture only. Public replay must use the unchanged production encounter pool.
    private static Session Battle()
    {
        var encounter = new SliceEncounter("base-spire:byrdonis-elite", "weak", [Byrd], [], [], []);
        var pool = new SliceEncounterPool([encounter], [encounter], 1, 1,
            "fixture:pool", "fixture:shuffle");
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: SinglePlayerContentSource.Shared.Model,
            encounterPool: pool).Build());
        var session = new Session(builder.Build(), 20261013UL, [SliceManifest.PlayerRole],
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile, computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }
}
