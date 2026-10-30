using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalDslMoveBranch(int Node, int Target, string Id, long WeightBits, int MaxRepeats, int Cooldown, bool Once);
internal static class NormalDslCombatCandidates
{
    internal static readonly string[] Captures = ["CreateSourceEncounterEnemy", "CreateEncounterEnemy", "RollEncounterEnemyHp", "ResolveEnemyMoveNode", "SaveEnemyMove"];
    internal static bool Relevant(SessionDebugRecord r) => r.Kind is SessionDebugKind.ProcedureEnter or SessionDebugKind.ProcedureExit
        && Captures.Contains(r.Subject?.Replace("base-spire:", "", StringComparison.Ordinal));
    internal static string[][] EncounterSources(RuleSet rules)
    {
        var branches = rules.Procedures.Single(p => p.QualifiedId == "base-spire:SpawnSourceSlimesWeak").Body.Instructions.OfType<IfInst>().ToArray();
        string Id(Block block) => ((ConstExpr)block.Instructions.OfType<CallInst>().Single().Arguments[0]).Value.AsString();
        return branches.Take(2).Select(b => new[] { Id(b.Then), Id(b.Else!) }).ToArray();
    }
    internal static NormalCombatCandidates Capture(IReadOnlyList<SessionDebugRecord> records, long dropped, string[][] encounterSources, NormalDslMoveBranch[] branches)
    {
        if (dropped != 0) throw NormalCombatCandidateTrace.Missing("dropped combat random evidence");
        var picks = new List<NormalCombatPick>(); var moves = new List<NormalMoveCall>();
        var factory = records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == "base-spire:encounter:monsters").ToArray();
        var births = records.Where(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:CreateEncounterEnemy").Select(r => Snapshot(r, 2)).ToArray();
        var hp = records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == "base-spire:run:niche").ToArray();
        var ranges = records.Where(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:CreateSourceEncounterEnemy").Select(r => Snapshot(r, 3)).ToArray();
        var rolls = records.Where(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:RollEncounterEnemyHp").Select(r => Snapshot(r, 2)).ToArray();
        int Pick(SessionDebugRecord r, string kind, string owner, string[] candidates)
        {
            if (r.TextTruncated || r.Random is not { Operation: "int", CandidatesTruncated: false } d || d.Minimum != "0"
                || d.Maximum != candidates.Length.ToString(CultureInfo.InvariantCulture) || d.After != d.Before + 1)
                throw NormalCombatCandidateTrace.Missing("combat source bounds or operation");
            var selected = int.Parse(d.Result, CultureInfo.InvariantCulture);
            if (selected < 0 || selected >= candidates.Length) throw NormalCombatCandidateTrace.Missing("combat source index");
            picks.Add(new(kind, owner, d.Before, d.After, selected, candidates)); return selected;
        }
        if (factory.Length > 0)
        {
            if (factory.Length != 3 || births.Length != 3 || ranges.Length != 3 || rolls.Length != 3 || hp.Length != 3) throw NormalCombatCandidateTrace.Missing("incomplete SlimesWeak birth evidence");
            var small = encounterSources[0].ToList(); var first = small[Pick(factory[0], "encounter", "small-first", small.ToArray())]; small.Remove(first);
            var last = small[Pick(factory[1], "encounter", "small-last", small.ToArray())];
            var medium = encounterSources[1][Pick(factory[2], "encounter", "medium", encounterSources[1])];
            if (!births.Select(s => Scalar(s.Values[0])).SequenceEqual(new[] { first, medium, last })) throw NormalCombatCandidateTrace.Missing("actual monster creation disagrees with encounter draws");
            var used = new List<long>();
            for (var i = 0; i < births.Length; i++)
            {
                if (Scalar(ranges[i].Values[0]) != Scalar(births[i].Values[0])) throw NormalCombatCandidateTrace.Missing("HP range belongs to another actual birth");
                var min = Integer(ranges[i].Values[1]); var max = Integer(ranges[i].Values[2]);
                if (Integer(rolls[i].Values[0]) != min || Integer(rolls[i].Values[1]) != max) throw NormalCombatCandidateTrace.Missing("actual HP roll parameters differ from creation context");
                var candidates = Enumerable.Range(checked((int)min), checked((int)(max - min + 1))).Select(n => (long)n).Where(n => !used.Contains(n)).ToArray();
                var selected = Pick(hp[i], "hp", "enemy:" + i, candidates.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray());
                var actual = Integer(births[i].Values[1]);
                if (candidates[selected] != actual) throw NormalCombatCandidateTrace.Missing("HP candidate result differs from actual enemy creation"); used.Add(actual);
            }
        }
        var history = new Dictionary<int, List<(int Node, long Sequence)>>();
        var stack = new Stack<SessionDebugSnapshot>(); (SessionDebugRecord Draw, SessionDebugSnapshot Context, NormalMoveEntry[] Entries)? pending = null;
        foreach (var r in records)
        {
            if (Relevant(r) && r.TextTruncated) throw NormalCombatCandidateTrace.Missing("truncated combat context");
            if (r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:ResolveEnemyMoveNode")
            {
                var s = Snapshot(r, 2);
                if (pending is { } p)
                {
                    var selected = NormalCombatCandidateTrace.Select(p.Entries, Binary32.Read(p.Draw.Random!.Float32!.ResultBits));
                    if (Integer(p.Context.Values[0]) != Integer(s.Values[0]) || Scalar(Field(Entity(s, Integer(s.Values[1])), "Id")) != p.Entries[selected].Id)
                        throw NormalCombatCandidateTrace.Missing("actual recursive move target disagrees with weighted candidates");
                    var state = Entity(p.Context, Integer(p.Context.Values[0])); var enemy = Entity(p.Context, checked((int)Integer(Field(state, "Enemy"))));
                    moves.Add(new("enemy:" + Scalar(Field(enemy, "SpawnOrder")), Scalar(Field(Entity(p.Context, Integer(p.Context.Values[1])), "Id")),
                        p.Draw.Random.Before, p.Draw.Random.After, selected, p.Entries)); pending = null;
                }
                stack.Push(s);
            }
            else if (r.Kind == SessionDebugKind.ProcedureExit && r.Subject == "base-spire:ResolveEnemyMoveNode")
            { if (stack.Count == 0) throw NormalCombatCandidateTrace.Missing("unmatched move procedure"); stack.Pop(); }
            else if (r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:SaveEnemyMove")
            {
                var s = Snapshot(r, 2); var id = checked((int)Integer(s.Values[0])); var state = Entity(s, id);
                if (!history.TryGetValue(id, out var list)) history.Add(id, list = []);
                list.Add((checked((int)Integer(s.Values[1])), Integer(Field(state, "Sequence")) + 1));
            }
            if (r.Kind != SessionDebugKind.RandomCall || r.Subject != "base-spire:run:monster_ai") continue;
            if (stack.Count == 0 || pending is not null || r.Random?.Float32 is not { } actual) throw NormalCombatCandidateTrace.Missing("missing weighted move context");
            var context = stack.Peek(); var stateId = checked((int)Integer(context.Values[0])); var nodeId = checked((int)Integer(context.Values[1]));
            var stateNow = Entity(context, stateId); var previous = Field(stateNow, "Current"); var sequence = Integer(Field(stateNow, "Sequence"));
            var entries = branches.Where(b => b.Node == nodeId).Select(b =>
            {
                var repeated = b.MaxRepeats > 0 && previous.Kind == ValueKind.Ref && Integer(previous) == b.Target && Integer(Field(stateNow, "Consecutive")) >= b.MaxRepeats;
                var used = history.GetValueOrDefault(stateId)?.Any(h => h.Node == b.Target && (b.Once || h.Sequence > sequence - b.Cooldown)) == true;
                return new NormalMoveEntry(b.Id, repeated || used ? 0 : b.WeightBits, b.MaxRepeats, b.Cooldown, b.Once);
            }).ToArray();
            var total = entries.Aggregate(0f, (sum, b) => sum + Binary32.Read(b.WeightBits));
            if (entries.Length == 0 || total <= 0 || BitConverter.SingleToUInt32Bits(total) != actual.MaximumBits || actual.MinimumBits != 0)
                throw NormalCombatCandidateTrace.Missing("actual move weight total differs from full source");
            pending = (r, context, entries);
        }
        if (pending is not null || stack.Count != 0 || moves.Count != records.Count(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == "base-spire:run:monster_ai"))
            throw NormalCombatCandidateTrace.Missing("unfinished move candidate history");
        return new(picks.ToArray(), moves.ToArray());
    }
    private static SessionDebugSnapshot Snapshot(SessionDebugRecord r, int count) => !r.TextTruncated && r.Snapshot is { Complete: true } s && s.ValueCount == count
        ? s : throw NormalCombatCandidateTrace.Missing("missing combat argument snapshot: " + r.Subject);
    private static SessionDebugEntitySnapshot Entity(SessionDebugSnapshot s, long id) => s.Entities.SingleOrDefault(e => e.Instance.Value == id)
        ?? throw NormalCombatCandidateTrace.Missing("missing combat historical entity");
    private static SessionDebugValueSnapshot Field(SessionDebugEntitySnapshot e, string name) => e.Fields.Single(f => f.Name == name).Value;
    private static string Scalar(SessionDebugValueSnapshot v) => !v.Truncated && v.Scalar is { } s ? s : throw NormalCombatCandidateTrace.Missing("missing combat scalar");
    private static long Integer(SessionDebugValueSnapshot v) => long.Parse(Scalar(v), CultureInfo.InvariantCulture);
}
