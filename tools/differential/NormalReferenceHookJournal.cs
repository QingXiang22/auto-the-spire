#if REFERENCE_SIM
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

internal sealed record NormalHookValue(string Kind, int Reference = 0, string? Scalar = null,
    Dictionary<string, NormalHookValue>? Fields = null, NormalHookValue[]? Items = null, string? Projection = null);
internal sealed record NormalHookMethod(string Signature, string DeclaringType, string BodyHash, int ArgumentCount,
    bool DefaultDeclaration, string? DefaultImplementation);
internal sealed record NormalNativeHookRecord(int Sequence, int Call, int Parent, string Stage, string Kind, string Context,
    string Name, NormalHookMethod? Method, NormalHookValue? Receiver, NormalHookValue[] Arguments, NormalHookValue? Result);
internal sealed record NormalHookProvenance(string Observer, int ReferenceDepth, string RuntimeLibrary, string RuntimeLibrarySha256, NormalHookMethod[] Targets);
internal sealed record NormalNativeHookJournal(NormalNativeHookRecord[] Records, NormalNativeHookAudit Audit, NormalHookProvenance Provenance);

// Diagnostic-only runtime entry/return observers. Prefixes never return a skip
// flag; postfixes never replace a result or argument. No source file is rewritten.
// Each session installs before execution and restores every entry on disposal.
internal sealed class NormalReferenceHookJournal : IDisposable
{
    private const string Owner = "auto-the-spire.normal-events";
    private static NormalReferenceHookJournal? active;
    private readonly Harmony harmony = new(Owner);
    private readonly Dictionary<object, int> identities = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> objects = [];
    private readonly List<NormalNativeHookRecord> records = [];
    private readonly Dictionary<MethodBase, NormalHookMethod> methods = [];
    private readonly Stack<NormalNativeHookRecord> stack = [];
    private readonly NormalHookProvenance provenance;
    private int nextCall;
    private bool rendering;
    private string context = "gameplay";
    private static int bridgeSequence;

    internal NormalReferenceHookJournal()
    {
        if (active is not null) throw Missing("native runtime observer already owned");
        var declared = typeof(AbstractModel).Assembly.GetTypes().Where(t => typeof(AbstractModel).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToArray();
        // Lifecycle callbacks have additional roots on Card/Relic/Power/Monster
        // models. Include their real Task implementations, including protected OnPlay.
        var roots = declared.Where(m => m.IsVirtual && !m.IsSpecialName && m.GetBaseDefinition() == m
            && m.Name is not ("CompareTo" or "ToString")
            && (m.DeclaringType == typeof(AbstractModel) && m.IsPublic || typeof(Task).IsAssignableFrom(m.ReturnType))).ToHashSet();
        var hookNames = typeof(Hook).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.Name.StartsWith("Iterate", StringComparison.Ordinal)).Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var targets = declared.Where(m => m.IsVirtual && !m.IsAbstract && roots.Contains(m.GetBaseDefinition()))
            .Append(typeof(MonsterModel).GetMethod(nameof(MonsterModel.RollMove))!)
            .Concat(typeof(Hook).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => hookNames.Contains(m.Name))).Distinct().ToArray();
        try
        {
            var bridge = Bridge(targets);
            foreach (var (method, index) in targets.Select((method, index) => (method, index)))
            {
                methods.Add(method, Describe(method));
                harmony.Patch(method, prefix: new(bridge.GetMethod("P" + index)!), postfix: new(bridge.GetMethod("Q" + index)!));
                // Rebuild async callers too: their virtual calls must remain visible even
                // when an earlier run has already JIT-compiled the source state machine.
                var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                    .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (machine is not null) harmony.Patch(machine, prefix: new(typeof(NormalReferenceHookJournal), nameof(Caller)));
            }
            provenance = new("value-only-positional@1", 2, typeof(Harmony).Assembly.GetName().FullName!, NormalPreflight.Hash(typeof(Harmony).Assembly.Location),
                methods.Values.OrderBy(m => m.DeclaringType, StringComparer.Ordinal).ThenBy(m => m.Signature, StringComparer.Ordinal).ToArray());
            active = this;
        }
        catch { harmony.UnpatchAll(Owner); throw; }
    }

    internal object Object(int reference) => objects[reference - 1];
    internal NormalNativeHookJournal Capture()
    {
        if (stack.Count != 0) throw Missing("unfinished native hook at stable boundary");
        var result = records.ToArray();
        return new(result, NormalReferenceHookAudit.Check(result, provenance.Targets), provenance);
    }
    internal IDisposable Query()
    {
        var previous = context; context = "query"; return new QueryScope(this, previous);
    }
    private sealed class QueryScope(NormalReferenceHookJournal journal, string previous) : IDisposable
    { public void Dispose() => journal.context = previous; }
    internal static bool HasPatches => Harmony.GetAllPatchedMethods().Any(m => Harmony.GetPatchInfo(m)?.Owners.Contains(Owner) == true);
    internal void Notification(string name, object?[] arguments) => Add(new(records.Count + 1, 0,
        stack.TryPeek(out var parent) ? parent.Call : 0, "point", "observer", context, name, null, null, Freeze(arguments), null));
    private static void Caller() { }
    private void Add(NormalNativeHookRecord record)
    {
        if (records.Count >= 100_000) throw Missing("native hook journal capacity");
        records.Add(record);
    }
    private static int Enter(MethodBase method, object? receiver, object?[] arguments)
    {
        if (active is not { rendering: false } journal) return 0;
        var state = ++journal.nextCall;
        var entry = new NormalNativeHookRecord(journal.records.Count + 1, state,
            journal.stack.TryPeek(out var parent) ? parent.Call : 0, "enter", receiver is null ? "dispatch"
                : method is MethodInfo { IsVirtual: false } ? "operation" : "listener",
            journal.context, method.Name, journal.methods[method], journal.Freeze(receiver), journal.Freeze(arguments), null);
        journal.Add(entry); journal.stack.Push(entry);
        return state;
    }
    private static void Leave(object?[] arguments, object? result, int call) => active?.Finish(call, arguments, result);
    private void Finish(int call, object?[] arguments, object? result)
    {
        if (call == 0) return;
        if (result is Task task && !task.IsCompletedSuccessfully) throw Missing("native hook task " + stack.Peek().Name + " is " + task.Status + ": " + task.Exception?.GetBaseException().Message);
        if (!stack.TryPop(out var entry) || entry.Call != call) throw Missing("native hook entry/return nesting");
        Add(entry with { Sequence = records.Count + 1, Stage = "exit", Arguments = Freeze(arguments), Result = Freeze(result),
            Receiver = entry.Receiver is { Reference: > 0 } receiver ? Freeze(Object(receiver.Reference)) : entry.Receiver });
    }

    private NormalHookValue[] Freeze(object?[] values) => values.Select(v => Freeze(v)).ToArray();
    private NormalHookValue Freeze(object? value, int depth = 0)
    {
        var previous = rendering; rendering = true;
        try { return Copy(value, depth); }
        finally { rendering = previous; }
    }
    private int Identity(object value)
    { if (!identities.TryGetValue(value, out var id)) { identities.Add(value, id = objects.Count + 1); objects.Add(value); } return id; }
    private NormalHookValue Copy(object? value, int depth)
    {
        if (value is null) return new("null");
        if (value is string text) return new("string", Scalar: text);
        if (value is bool flag) return new("bool", Scalar: flag ? "true" : "false");
        if (value is Enum e) return new(e.GetType().FullName!, Scalar: e.ToString());
        if (value is byte or short or int or long or uint or ulong or decimal or float or double)
            return new("number", Scalar: Convert.ToString(value, CultureInfo.InvariantCulture));
        if (value is Task task) return new("task", Scalar: task.IsCompletedSuccessfully ? "completed" : task.Status.ToString());
        var id = value.GetType().IsValueType ? 0 : Identity(value);
        if (depth >= 2) return new(value.GetType().FullName!, id, Projection: "reference-only");
        var fields = new Dictionary<string, NormalHookValue>(StringComparer.Ordinal);
        void Field(string name, object? item) => fields.Add(name, Freeze(item, depth + 1));
        switch (value)
        {
            case CardModel c:
                Field("definition", NormalReferenceRun.Id(c.Id)); Field("upgraded", c.IsUpgraded); Field("deck_source", c.DeckVersion); Field("owner", c.Owner); break;
            case PowerModel p:
                Field("definition", NormalReferenceRun.Id(p.Id)); Field("amount", p.Amount); Field("owner", p.Owner); break;
            case RelicModel r:
                Field("definition", NormalReferenceRun.Id(r.Id)); Field("owner", r.Owner); break;
            case MonsterModel monster:
                Field("definition", NormalReferenceRun.Id(monster.Id)); Field("creature", monster.Creature); Field("move", monster.NextMove?.Id); break;
            case AbstractModel model: Field("definition", NormalReferenceRun.Id(model.Id)); break;
            case Creature creature:
                Field("hp", creature.CurrentHp); Field("max_hp", creature.MaxHp); Field("block", creature.Block);
                Field("player", creature.Player); Field("monster", creature.Monster); Field("combat_id", creature.CombatId); break;
            case Player player:
                Field("creature", player.Creature); Field("energy", player.PlayerCombatState?.Energy); Field("turn", player.PlayerCombatState?.TurnNumber); Field("gold", player.Gold); break;
            case CardPlay play:
                Field("card", play.Card); Field("player", play.Player); Field("target", play.Target); Field("resources", play.Resources);
                Field("index", play.PlayIndex); Field("count", play.PlayCount); Field("ordinal", play.PlayOrdinal); Field("auto", play.IsAutoPlay); Field("pile", play.ResultPile); break;
            case AttackCommand attack:
                Field("attacker", attack.Attacker); Field("source", attack.ModelSource); Field("play", attack.CardPlay); Field("props", attack.DamageProps);
                Field("amount", typeof(AttackCommand).GetField("_damagePerHit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(attack));
                Field("hits", typeof(AttackCommand).GetField("_hitCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(attack));
                var target = typeof(AttackCommand).GetField("_singleTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(attack);
                Field("target", target); Field("target_side", attack.TargetSide); Field("random", attack.IsRandomlyTargeted);
                var context = (ICombatState?)typeof(AttackCommand).GetField("_combatState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(attack);
                Field("targets", target is not null ? new[] { (Creature)target } : context?.GetCreaturesOnSide(attack.TargetSide)); break;
            case ICombatState combat:
                Field("round", combat.RoundNumber); Field("side", combat.CurrentSide); Field("ending", combat.IsOverOrEnding()); break;
            case RunState run: Field("act", run.CurrentActIndex); Field("floor", run.TotalFloor); break;
            case AbstractRoom room: Field("room", room.RoomType); break;
            case IEnumerable sequence:
                var items = sequence.Cast<object?>().ToArray();
                if (items.Length > 512) throw Missing("native argument sequence capacity");
                return new("sequence", id, Items: items.Select(item => Freeze(item, depth + 1)).ToArray());
            default:
                // Read only compiler-generated backing fields, never arbitrary getters.
                foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => f.IsDefined(typeof(CompilerGeneratedAttribute), false))) Field(field.Name, field.GetValue(value));
                break;
        }
        return new(value.GetType().FullName!, id, Fields: fields, Projection: fields.Count == 0 ? "opaque" : null);
    }

    internal static NormalHookMethod Describe(MethodInfo method)
    {
        var body = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        string? neutral = null;
        var declaration = method.IsVirtual && method.DeclaringType!.IsAbstract && method.GetBaseDefinition() == method;
        if (declaration)
        {
            // Classify only exact locked default bodies. A constant-returning override,
            // arithmetic, a field read or a forwarding virtual call is not an empty hook.
            var code = PatchProcessor.GetOriginalInstructions(method).Where(i => i.opcode != OpCodes.Nop).ToArray();
            string Shape() => string.Join(';', code.Select(i => i.opcode.Name));
            neutral = Shape() switch
            {
                "ret" => "empty",
                "call;ret" when code[0].operand is MethodInfo { DeclaringType: { } t, Name: "get_CompletedTask" } && t == typeof(Task) => "completed-task",
                "ldarg.1;ret" or "ldarg.2;ret" or "ldarg.3;ret" or "ldarg.s;ret" => "identity",
                "ldc.i4.0;ret" or "ldc.i4.1;ret" or "ldnull;ret" => "constant",
                "ldsfld;ret" when code[0].operand is FieldInfo { DeclaringType: { } t, Name: "Zero" or "One" } && t == typeof(decimal) => "constant",
                "ldarg.2;ldnull;stind.ref;ldc.i4.0;ret" => "false-null-out",
                "ldarg.3;ldarg.2;stobj;ldc.i4.0;ret" or "ldarg.s;ldarg.3;stobj;ldc.i4.0;ret"
                    when code[2].operand as Type == typeof(decimal) => "false-identity-out",
                "ldc.i4.m1;ldc.i4.m1;ldc.i4.m1;ldc.i4.0;ldc.i4.0;newobj;ret"
                    when code[5].operand is ConstructorInfo { DeclaringType: { } t } && t == typeof(decimal) => "maximum-decimal",
                _ => null
            };
        }
        return new(method.Name + "(" + string.Join(',', method.GetParameters().Select(p => p.ParameterType.FullName)) + ")",
            method.DeclaringType!.FullName!, Convert.ToHexStringLower(SHA256.HashData(body)), method.GetParameters().Length, declaration, neutral);
    }
    // Harmony's __args injection participates in argument write-back, including out
    // slots. Generate value-only positional observer parameters instead. The copied
    // object array below is private to this tool and is never passed to Harmony.
    private static Type Bridge(MethodInfo[] targets)
    {
        var name = new AssemblyName("NormalEventBridge" + Interlocked.Increment(ref bridgeSequence));
        var type = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect).DefineDynamicModule(name.Name!)
            .DefineType("Observer", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var enterType = typeof(Func<MethodBase, object?, object?[], int>); var exitType = typeof(Action<object?[], object?, int>);
        var enter = type.DefineField("Enter", enterType, FieldAttributes.Public | FieldAttributes.Static);
        var exit = type.DefineField("Exit", exitType, FieldAttributes.Public | FieldAttributes.Static);
        foreach (var (target, index) in targets.Select((target, index) => (target, index)))
        {
            var original = target.GetParameters();
            void Array(ILGenerator il, Dictionary<int, int> bindings)
            {
                il.Emit(OpCodes.Ldc_I4, original.Length); il.Emit(OpCodes.Newarr, typeof(object));
                foreach (var (argument, position) in bindings)
                {
                    il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldc_I4, argument); il.Emit(OpCodes.Ldarg, position);
                    var t = original[argument].ParameterType; if (t.IsByRef) t = t.GetElementType()!;
                    if (t.IsValueType) il.Emit(OpCodes.Box, t); il.Emit(OpCodes.Stelem_Ref);
                }
            }
            var types = new List<Type> { typeof(MethodBase), typeof(object) }; var names = new List<string> { "__originalMethod", "__instance" };
            var input = new Dictionary<int, int>();
            for (var i = 0; i < original.Length; i++)
            {
                if (original[i].IsOut) continue;
                input.Add(i, types.Count); var t = original[i].ParameterType;
                types.Add(t.IsByRef ? t.GetElementType()! : t); names.Add("__" + i);
            }
            var state = types.Count; types.Add(typeof(int).MakeByRefType()); names.Add("__state");
            var prefix = type.DefineMethod("P" + index, MethodAttributes.Public | MethodAttributes.Static, typeof(void), types.ToArray());
            for (var i = 0; i < names.Count; i++) prefix.DefineParameter(i + 1, i == state ? ParameterAttributes.Out : ParameterAttributes.None, names[i]);
            var p = prefix.GetILGenerator(); p.Emit(OpCodes.Ldarg, state); p.Emit(OpCodes.Ldsfld, enter); p.Emit(OpCodes.Ldarg_0); p.Emit(OpCodes.Ldarg_1);
            Array(p, input); p.Emit(OpCodes.Callvirt, enterType.GetMethod("Invoke")!); p.Emit(OpCodes.Stind_I4); p.Emit(OpCodes.Ret);
            types.Clear(); names.Clear(); var output = new Dictionary<int, int>();
            for (var i = 0; i < original.Length; i++)
            {
                output.Add(i, types.Count); var t = original[i].ParameterType;
                types.Add(t.IsByRef ? t.GetElementType()! : t); names.Add("__" + i);
            }
            var result = types.Count;
            if (target.ReturnType != typeof(void)) { types.Add(target.ReturnType); names.Add("__result"); }
            state = types.Count; types.Add(typeof(int)); names.Add("__state");
            var postfix = type.DefineMethod("Q" + index, MethodAttributes.Public | MethodAttributes.Static, typeof(void), types.ToArray());
            for (var i = 0; i < names.Count; i++) postfix.DefineParameter(i + 1, ParameterAttributes.None, names[i]);
            var q = postfix.GetILGenerator(); q.Emit(OpCodes.Ldsfld, exit); Array(q, output);
            if (target.ReturnType == typeof(void)) q.Emit(OpCodes.Ldnull);
            else { q.Emit(OpCodes.Ldarg, result); if (target.ReturnType.IsValueType) q.Emit(OpCodes.Box, target.ReturnType); }
            q.Emit(OpCodes.Ldarg, state); q.Emit(OpCodes.Callvirt, exitType.GetMethod("Invoke")!); q.Emit(OpCodes.Ret);
        }
        var built = type.CreateType()!;
        built.GetField("Enter")!.SetValue(null, (Func<MethodBase, object?, object?[], int>)Enter);
        built.GetField("Exit")!.SetValue(null, (Action<object?[], object?, int>)Leave);
        return built;
    }
    public void Dispose()
    {
        if (!ReferenceEquals(active, this)) return;
        active = null;
        harmony.UnpatchAll(Owner);
    }
    private static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
}
#endif
