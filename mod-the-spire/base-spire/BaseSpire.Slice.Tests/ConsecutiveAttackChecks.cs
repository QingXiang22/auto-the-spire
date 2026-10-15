using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T05 A 线：**挂起态只读查询**下的真实连续攻击（修复前必失败、修复后通过）。
///
/// 根因（`.audit/dsh-env-t05-consecutive-attack-2026-10-08/diagnosis.md`）：第二张攻击牌需要选目标 ⇒
/// 会话停在 `AwaitingDecision`；宿主每次结算后都会做只读查询（net `ControlSession.Snapshot` →
/// `Session.LegalActions`）；PlayCard 守卫里**只对「能量付不起的牌」才求值的那一支** `CardRuleCostAllows`
/// （`Slice/CombatSlicePackage.cs:3939-3960`）含 `CallPure(base-spire:PowerStacksOf)`，而挂起态 `DrainTo`
/// 不推进 ⇒ `Session.Expressions.cs:133` 抛 `SessionStateInvalid`，`Fatal` 再把只读查询升级成会话级
/// `Faulted`（宿主看到 `backend_fault`）。
///
/// 只走**真实生产包 + 真实动作/守卫路径**，不加镜像实现断言。五个场景：无 Power（复现 + 连续两张攻击
/// 真实打成）、有 Corruption（技能牌 0 费规则真实生效）、无 Corruption（0 费规则不得生效）、
/// 同定义多实例（归属/堆叠语义不变）、重复 observe/LegalActions（不改玩法/RNG）。
/// 注册：需在 `SliceChecks.RunAll()` 中挂载 `ConsecutiveAttackChecks.Run();`。
/// </summary>
internal static class ConsecutiveAttackChecks
{
    private const string Strike = "base-spire:strike-ironclad";
    private const string Impervious = "base-spire:impervious";
    private const string Bash = "base-spire:bash";
    private const string Defend = "base-spire:defend-ironclad";
    private const string Corruption = "base-spire:corruption-power";

    public static void Run()
    {
        Check.Run("ENV-T05 挂起态 LegalActions 不得报错：第二张攻击牌 + 手牌含不可支付牌 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var enemy = FormalSliceFixture.Cultist(session);
            var hpBefore = FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp");

            // 两张**不同牌**的真实攻击：打第二张时能量 0，手牌里还有 1 费的 Defend ⇒ 守卫必须求值
            // 「付不起」的那一支（修复前正是这一支里的 CallPure 把会话打成 faulted）。
            var first = EnsureInHand(session, Strike);
            var second = EnsureInHand(session, Bash);
            EnsureInHand(session, Defend);

            // 第一张攻击：真实打完（含回答目标决策）；花 1 费后能量 3→2。
            FormalSliceFixture.PlayAt(session, first, enemy);

            // 第二张攻击：付 2 费后停在目标决策上（能量 0）。
            SubmitPlay(session, second);
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "第二张攻击牌应停在目标决策上");

            var energy = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Energy");
            Check.True(energy < 1,
                $"挂起时能量须低于手牌里 1 费牌（实得 {energy}）——否则守卫左侧短路、复现不到根因");

            // 宿主每次结算后都会做的只读查询（net ControlSession.Snapshot → LegalActions）。
            Exception? fault = null;
            try
            {
                _ = session.LegalActions(SliceManifest.Player);
            }
            catch (Exception error)
            {
                fault = error;
            }

            Check.True(fault is null,
                "挂起态的只读合法动作查询不得抛错（实得 "
                + (fault is null ? "无异常" : $"{fault.GetType().Name}: {fault.Message}") + "）");
            Check.Equal(SessionStatus.AwaitingDecision, session.Status,
                "只读查询不得改变会话状态（不得把会话打成 Faulted）");

            // 真实连续两张攻击：回答目标决策后第二张真的打出去。
            AnswerPending(session, enemy);
            Check.Equal(SessionStatus.Running, session.Status, "回答目标决策后回到 Running");
            var hpAfter = FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
            Check.True(hpAfter < hpBefore, $"第二张攻击真实造成伤害（{hpBefore} → {hpAfter}）");
        });

        Check.Run("ENV-T05 有 Corruption 时技能牌 0 费规则真实生效，且挂起态查询安全 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.GivePower(session, player, Corruption, 1);

            // 能量压到 1：Impervious(2 费技能) 按基础费用不可支付 ⇒ 守卫必须求值 CardRuleCostAllows。
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Energy", 1);
            SubmitPlay(session, EnsureInHand(session, Impervious)); // 有 Power ⇒ 技能 0 费 ⇒ 合法

            // 挂起态只读查询（第二张攻击牌产生目标决策时）。
            SubmitPlay(session, EnsureInHand(session, Strike));
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "第二张攻击牌应停在目标决策上");
            Exception? fault = null;
            try
            {
                _ = session.LegalActions(SliceManifest.Player);
            }
            catch (Exception error)
            {
                fault = error;
            }

            Check.True(fault is null,
                "有 Power 时挂起态只读查询也不得抛错（实得 "
                + (fault is null ? "无异常" : $"{fault.GetType().Name}: {fault.Message}") + "）");
        });

        Check.Run("ENV-T05 无 Corruption 时技能牌不享受 0 费规则（有/无 Power 可区分）[真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Energy", 1);

            var fault = Check.Fault(EngineFaultCode.IllegalAction,
                () => SubmitPlay(session, EnsureInHand(session, Impervious)),
                "没有 Corruption 时 2 费技能牌在 1 能量下不合法（守卫拒，会话状态不变）");
            Check.True(fault.Message.Contains("guard", StringComparison.OrdinalIgnoreCase),
                $"拒绝原因应来自守卫而不是候选域（实得：{fault.Message}）");
            Check.Equal(SessionStatus.Running, session.Status, "守卫拒绝不得改变会话状态");
        });

        Check.Run("ENV-T05 同定义多实例：归属/堆叠语义不变，挂起态查询仍安全 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.GivePower(session, player, Corruption, 1);
            FormalSliceFixture.GivePower(session, player, Corruption, 1);

            var instances = FormalSliceFixture.PowerIds(session, player)
                .Count(id => id.StartsWith(Corruption, StringComparison.Ordinal));
            Check.True(instances >= 1, $"身上应有 {Corruption} 实例（实得 {instances} 个）");
            Check.True(FormalSliceFixture.PowerStacks(session, player, Corruption) >= 1,
                "按先找到的实例读层数应为正值（与 PowerStacksOf 体同序）");

            // 多实例下 0 费规则仍然生效（归属判定按 owner + 定义 id）。
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Energy", 1);
            SubmitPlay(session, EnsureInHand(session, Impervious));

            SubmitPlay(session, EnsureInHand(session, Strike));
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "第二张攻击牌应停在目标决策上");
            Exception? fault = null;
            try
            {
                _ = session.LegalActions(SliceManifest.Player);
            }
            catch (Exception error)
            {
                fault = error;
            }

            Check.True(fault is null,
                "多实例下挂起态只读查询仍不得抛错（实得 "
                + (fault is null ? "无异常" : $"{fault.GetType().Name}: {fault.Message}") + "）");
        });

        Check.Run("ENV-T05 重复 observe/LegalActions 不改变玩法与随机流 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var enemy = FormalSliceFixture.Cultist(session);
            var first = EnsureInHand(session, Strike);
            var second = EnsureInHand(session, Bash);
            EnsureInHand(session, Defend);
            FormalSliceFixture.PlayAt(session, first, enemy);
            SubmitPlay(session, second);

            var hashBefore = session.State.StateHash();
            var versionBefore = session.State.Version;
            var drawsBefore = session.Random.TotalDraws;
            for (var round = 0; round < 3; round++)
            {
                _ = session.LegalActions(SliceManifest.Player);
                _ = session.PendingDecision;
                _ = session.Observe(SliceManifest.Player, "base-spire:PlayerView");
                _ = FormalSliceFixture.PowerIds(session, player);
            }

            Check.Equal(hashBefore, session.State.StateHash(), "重复只读查询后状态哈希不变");
            Check.Equal(versionBefore, session.State.Version, "重复只读查询后状态版本不变");
            Check.Equal(drawsBefore, session.Random.TotalDraws, "重复只读查询不消耗随机数");
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "重复只读查询不改变会话状态");
        });
    }

    /// <summary>用真实 `base-spire:PlayCard` 动作提交一张牌（不自动回答决策，留给调用者断言挂起态）。</summary>
    private static void SubmitPlay(Session session, InstanceId card)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == FormalSliceFixture.PlayCardAction);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
    }

    private static void AnswerPending(Session session, InstanceId preferred)
    {
        var pending = session.PendingDecision
            ?? throw new CheckFailedException("期望有挂起的决策，实际没有");
        var chosen = pending.Candidates.Contains(preferred) ? preferred : pending.Candidates[0];
        session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
    }

    /// <summary>`FormalSliceFixture.MoveToHand` 只在抽/弃/消耗堆里找，已入手牌时抛异常 ⇒ 这里兜住。</summary>
    private static InstanceId EnsureInHand(Session session, string cardId)
    {
        try
        {
            return FormalSliceFixture.MoveToHand(session, cardId);
        }
        catch (CheckFailedException)
        {
            // 这张牌已经在手牌里。
        }

        var hand = FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile);
        return session.State.CollectionMembers(hand).Single(card =>
            FormalSliceFixture.Definition(session, card) == cardId);
    }
}
