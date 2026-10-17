using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// Controlled witnesses for bugs encountered by the separate, unmodified-seed normal Run suite.
internal static class CapacityPrerequisiteChecks
{
    public static void Run()
    {
        var content = SinglePlayerContentSource.Shared;
        var inklets = RunSourcePoolManifest.Complete(content.EncounterPool).Encounters
            .Single(e => e.EncounterId == "base-spire:inklets-normal");
        var model = content.Model with
        {
            Run = new AuthorRun(80, 80, 0),
            Deck = [new("base-spire:flame-barrier", 1), new("base-spire:headbutt", 1),
                new("base-spire:strike-ironclad", 3), new("base-spire:defend-ironclad", 4)],
        };
        var pool = content.EncounterPool with { WeakEncounters = [inklets with { Kind = SliceEncounterPool.WeakKind }], RegularEncounters = [],
            WeakRoomCount = 1, RoomCount = 1, EliteRoomCount = 0, EliteEncounters = [],
            BossEncounters = RunSourcePoolManifest.Complete(content.EncounterPool).Bosses };
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model, encounterPool: pool).Build());
        var rules = builder.Build();
        Session Start()
        {
            var session = new Session(rules, 20261016UL, [SliceManifest.PlayerRole], computeBackend: BaseSpireComputeBackend.Instance);
            session.Start(); return session;
        }
        InstanceId[] Enemies(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters")).ToArray();
        Check.Run("容量前置：中间墨仔三连击逐次反伤，滑溜耗尽销毁，后两次各受4伤害", () =>
        {
            var session = Start(); var enemies = Enemies(session);
            var hp = enemies.Select(e => FormalSliceFixture.Number(session, e, FormalSliceFixture.MonsterTypeId, "Hp")).ToArray();
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, "base-spire:flame-barrier"), FormalSliceFixture.Player(session));
            FormalSliceFixture.EndTurn(session);
            Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
            Check.Sequence(new[] { hp[0] - 1, hp[1] - 9, hp[2] - 1 }, enemies.Select(e => FormalSliceFixture.Number(session, e, FormalSliceFixture.MonsterTypeId, "Hp")), "jab / whirlwind x3 / jab retaliation");
            foreach (var enemy in enemies)
                Check.True(!FormalSliceFixture.PowerIds(session, enemy).Any(), "spent Slippery instance removed");
        });
        Check.Run("容量前置：完全格挡保留滑溜，首次掉血耗尽，下一击不再封顶", () =>
        {
            var session = Start(); var enemy = Enemies(session)[0];
            FormalSliceFixture.SetNumber(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 40);
            FormalSliceFixture.SetNumber(session, enemy, FormalSliceFixture.MonsterTypeId, "Block", 6);
            for (var hit = 0; hit < 3; hit++)
            {
                FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, "base-spire:strike-ironclad"), enemy);
                Check.Equal(new[] { 40L, 39L, 33L }[hit], FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "blocked / capped / uncapped");
                Check.Equal(hit == 0 ? 1L : 0L, FormalSliceFixture.PowerStacks(session, enemy, "base-spire:slippery-power"), "consumed only by unblocked damage");
            }
        });
        foreach (var lethal in new[] { false, true })
            Check.Run($"容量前置：头槌lethal={lethal}，单候选自动置顶，结束战斗不选牌", () =>
            {
                var session = Start(); var enemy = Enemies(session)[0];
                var discard = FormalSliceFixture.Collection(session, "base-spire:DiscardPile");
                var hand = FormalSliceFixture.Collection(session, "base-spire:Hand");
                var defend = FormalSliceFixture.MoveToHand(session, "base-spire:defend-ironclad");
                session.State.RunAtomic(() => { session.State.RemoveFromCollection(hand, defend); session.State.AddToCollection(discard, defend); });
                if (lethal)
                    session.State.RunAtomic(() =>
                    {
                        foreach (var other in Enemies(session).Skip(1)) session.State.RemoveFromCollection(FormalSliceFixture.Collection(session, "base-spire:Monsters"), other);
                        session.State.SetField(enemy, session.RuleSet.TypeByQualifiedId(FormalSliceFixture.MonsterTypeId).FieldIndex("Hp"), Value.Of(1L));
                    });
                var decisions = FormalSliceFixture.PlayAtCountingDecisions(session, FormalSliceFixture.MoveToHand(session, "base-spire:headbutt"), enemy);
                Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
                Check.Equal(1, decisions, "only target decision, no pile decision");
                Check.Equal(lethal, session.State.CollectionMembers(discard).Contains(defend), "ending suppresses movement, otherwise auto-selects singleton");
            });
    }
}
