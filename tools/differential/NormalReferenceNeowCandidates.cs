#if REFERENCE_SIM
using System.Globalization;
using System.Reflection;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Differential;

internal sealed record NormalNeowEligibility(string Group, string Id, bool Eligible);

// The private source arrays and pure eligibility/compatibility helpers operate only
// on diagnostic copies; the live event and its RNG are never driven a second time.
internal sealed class NormalReferenceNeowCandidates
{
    private readonly Type[] curses;
    private readonly Type[] positives;
    private readonly Dictionary<Type, bool> eligible;
    internal NormalNeowEligibility[] Sources { get; }
    internal NormalReferenceNeowCandidates(RunState run)
    {
        Type[] Source(string field) => ((Type[])typeof(Neow).GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).ToArray();
        curses = Source("CurseTypes"); positives = Source("PositiveTypes"); var extras = Source("ExtraPositiveTypes");
        eligible = curses.Concat(positives).Concat(extras).Distinct().ToDictionary(t => t,
            t => ((RelicModel)ModelDb.Get(t).MutableClone()).IsAllowedAtNeow(run));
        Sources = new[] { ("curse", curses), ("positive", positives), ("extra", extras) }
            .SelectMany(g => g.Item2.Select(t => new NormalNeowEligibility(g.Item1, Id(t), eligible[t]))).ToArray();
    }
    internal NormalNeowCandidates Capture(NormalInitializationDraw[] draws, string[] options)
    {
        var index = 0; var calls = new List<NormalNeowCall>();
        NormalInitializationDraw Next(string operation, int arity)
        {
            if (index >= draws.Length) throw NormalNeowTrace.Missing("missing native Neow draw");
            var draw = draws[index++];
            if (draw.Operation != operation || draw.Arity != arity || draw.Before != index - 1 || draw.After != index)
                throw NormalNeowTrace.Missing("native Neow operation or cursor");
            return draw;
        }
        int Integer(string kind, Type[] candidates, int arity)
        {
            var draw = Next("NextInt", arity); var selected = int.Parse(draw.Result, CultureInfo.InvariantCulture);
            if (draw.Minimum != 0 || draw.Maximum != candidates.Length || selected < 0 || selected >= candidates.Length)
                throw NormalNeowTrace.Missing("native Neow bounds or index");
            calls.Add(new(kind, draw.Before, draw.After, selected, candidates.Select(Id).ToArray())); return selected;
        }
        var allowedCurses = curses.Where(t => eligible[t]).ToArray();
        var curse = allowedCurses[Integer("curse", allowedCurses, 2)];
        var pool = positives.ToList();
        typeof(Neow).GetMethod("RemoveMutuallyExclusivePositives", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [pool, curse]);
        void Pair(Type left, Type right)
        {
            var draw = Next("NextBool", 0);
            if (draw.Minimum != 0 || draw.Maximum != 0) throw NormalNeowTrace.Missing("native Neow bool parameters");
            var selected = bool.Parse(draw.Result) ? 0 : 1;
            calls.Add(new("pair", draw.Before, draw.After, selected, [Id(left), Id(right)])); pool.Add(selected == 0 ? left : right);
        }
        if (curse != typeof(LargeCapsule)) Pair(typeof(LavaRock), typeof(SmallCapsule));
        Pair(typeof(NutritiousOyster), typeof(StoneHumidifier));
        Pair(typeof(NeowsTalisman), typeof(Pomander));
        var allowed = pool.Where(t => eligible[t]).ToList(); var beforeShuffle = allowed.Select(Id).ToArray();
        for (var i = allowed.Count - 1; i > 0; i--)
        {
            var selected = Integer("shuffle", allowed.Take(i + 1).ToArray(), 1);
            (allowed[i], allowed[selected]) = (allowed[selected], allowed[i]);
        }
        if (index != draws.Length || options.Length != 3 || !allowed.Take(2).Append(curse).Select(Id).SequenceEqual(options))
            throw NormalNeowTrace.Missing("native Neow draws disagree with actual options");
        return new(positives.Where(t => eligible[t]).Select(Id).ToArray(), beforeShuffle, calls.ToArray(), options);
    }
    private static string Id(Type type) => NormalReferenceRun.Id(ModelDb.GetId(type));
}
#endif
