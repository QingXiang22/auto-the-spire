#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;

namespace Differential;

// Capture every possible owner's actual branch state before a draw, then identify
// the owner by its real transition after the draw. Never roll a move a second time.
internal sealed class NormalReferenceMoveCandidates(CombatState state)
{
    private sealed record Before(string Owner, MonsterMoveStateMachine Machine, RandomBranchState Node, NormalMoveEntry[] Entries);
    private static readonly FieldInfo Current = typeof(MonsterMoveStateMachine).GetField("_currentState", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo Weight = typeof(RandomBranchState).GetMethod("GetStateWeight", BindingFlags.Static | BindingFlags.NonPublic)!;
    private Before[]? before;
    private (long Before, long After, float Result)? draw;
    private float maximum;
    private readonly List<NormalMoveCall> calls = [];
    internal void Begin(float maximum)
    {
        Finish();
        this.maximum = maximum;
        before = state.SpawnedEnemies.Select((owner, index) => (owner, index))
            .Where(p => p.owner.Monster?.MoveStateMachine is { } machine && Current.GetValue(machine) is RandomBranchState)
            .Select(p =>
            {
                var machine = p.owner.Monster!.MoveStateMachine!; var node = (RandomBranchState)Current.GetValue(machine)!;
                var entries = node.States.Select(s => new NormalMoveEntry(s.StateId,
                    BitConverter.SingleToUInt32Bits((float)Weight.Invoke(null, [s, p.owner])!),
                    s.RepeatType == MoveRepeatType.CannotRepeat ? 1 : s.RepeatType == MoveRepeatType.CanRepeatXTimes ? s.MaxTimes : 0,
                    s.Cooldown, s.RepeatType == MoveRepeatType.UseOnlyOnce)).ToArray();
                return new Before("enemy:" + p.index, machine, node, entries);
            }).ToArray();
        if (before.Length == 0) throw NormalCombatCandidateTrace.Missing("no native branch owner for actual AI draw");
    }
    internal void Result(long previous, long after, float result) => draw = (previous, after, result);
    internal void Finish()
    {
        if (before is null) return;
        if (draw is not { } d) throw NormalCombatCandidateTrace.Missing("missing native move result");
        var changed = before.Where(b => !ReferenceEquals(Current.GetValue(b.Machine), b.Node)).ToArray();
        if (changed.Length != 1) throw NormalCombatCandidateTrace.Missing("native move owner transition is ambiguous");
        var owner = changed[0]; var selected = NormalCombatCandidateTrace.Select(owner.Entries, d.Result);
        if (owner.Entries.Aggregate(0f, (sum, e) => sum + BitConverter.UInt32BitsToSingle((uint)e.WeightBits)) != maximum)
            throw NormalCombatCandidateTrace.Missing("native move weight source differs from actual draw bounds");
        if (((MonsterState)Current.GetValue(owner.Machine)!).Id != owner.Entries[selected].Id)
            throw NormalCombatCandidateTrace.Missing("native weighted draw disagrees with actual move transition");
        calls.Add(new(owner.Owner, owner.Node.Id, d.Before, d.After, selected, owner.Entries)); before = null; draw = null;
    }
    internal NormalMoveCall[] Capture() { Finish(); return calls.ToArray(); }
}
#endif
