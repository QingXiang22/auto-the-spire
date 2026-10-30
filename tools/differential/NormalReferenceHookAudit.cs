#if REFERENCE_SIM
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Hooks;

namespace Differential;

internal sealed record NormalNativeHookCount(NormalHookMethod Method, int Calls);
internal sealed record NormalNativeHookAudit(int Records, int Calls, int Notifications,
    NormalNativeHookCount[] DefaultImplementations, NormalNativeHookCount[] Overrides, NormalNativeHookCount[] UnclassifiedBase);

// A complete call journal is necessary for semantic comparison, but does not
// certify that the calls have a cross-backend mapping. Unknown bodies stay visible.
internal static class NormalReferenceHookAudit
{
    internal static NormalNativeHookAudit Check(NormalNativeHookRecord[] records, NormalHookMethod[] manifest)
    {
        var declared = manifest.ToHashSet();
        var stack = new Stack<NormalNativeHookRecord>();
        var entries = new List<NormalNativeHookRecord>(); var notifications = 0; var call = 0;
        for (var i = 0; i < records.Length; i++)
        {
            var r = records[i];
            if (r.Sequence != i + 1 || r.Context is not ("gameplay" or "query")) throw Missing("missing, duplicate or reordered record/context");
            if (r.Stage == "exit")
            {
                if (!stack.TryPop(out var entry) || entry.Call != r.Call || entry.Parent != r.Parent || entry.Name != r.Name
                    || entry.Kind != r.Kind || entry.Context != r.Context || entry.Method != r.Method || entry.Receiver?.Reference != r.Receiver?.Reference)
                    throw Missing("unmatched entry/return");
                if (r.Result is null || r.Result is { Kind: "task", Scalar: not "completed" }) throw Missing("missing or unfinished return snapshot");
            }
            else if (r.Parent != (stack.TryPeek(out var parent) ? parent.Call : 0)) throw Missing("wrong parent");
            if (r.Kind == "observer")
            {
                if (r.Stage != "point" || r.Call != 0 || r.Method is not null) throw Missing("invalid observer record");
                notifications++; continue;
            }
            if (r.Kind is not ("listener" or "dispatch") || r.Stage is not ("enter" or "exit") || r.Method is null
                || !declared.Contains(r.Method) || !r.Method.Signature.StartsWith(r.Name + "(", StringComparison.Ordinal)
                || r.Arguments.Length != r.Method.ArgumentCount || r.Receiver is null
                || (r.Method.DeclaringType == typeof(Hook).FullName) != (r.Kind == "dispatch"))
                throw Missing("missing method or argument snapshot");
            if (r.Kind == "listener" && (r.Receiver.Reference <= 0 || r.Method.DefaultImplementation is not null
                && r.Method.DeclaringType != typeof(AbstractModel).FullName)) throw Missing("override classified as default");
            if (r.Stage == "enter")
            {
                if (r.Call != ++call || r.Result is not null) throw Missing("missing or duplicate invocation");
                entries.Add(r); stack.Push(r);
            }
        }
        if (stack.Count != 0) throw Missing("unfinished call");
        NormalNativeHookCount[] Count(Func<NormalHookMethod, bool> predicate) => entries.Where(r => r.Kind == "listener" && predicate(r.Method!))
            .GroupBy(r => r.Method!).Select(g => new NormalNativeHookCount(g.Key, g.Count()))
            .OrderBy(g => g.Method.DeclaringType, StringComparer.Ordinal).ThenBy(g => g.Method.Signature, StringComparer.Ordinal).ToArray();
        return new(records.Length, call, notifications, Count(m => m.DefaultImplementation is not null),
            Count(m => m.DeclaringType != typeof(AbstractModel).FullName),
            Count(m => m.DeclaringType == typeof(AbstractModel).FullName && m.DefaultImplementation is null));
    }
    private static NotSupportedException Missing(string reason) => new("missing_dimension: native hook journal " + reason);
}
#endif
