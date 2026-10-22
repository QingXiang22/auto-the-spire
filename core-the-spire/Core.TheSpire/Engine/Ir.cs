namespace CoreTheSpire.Engine;

/// <summary>
/// Canonical rule IR. Expressions and queries are pure: they read state but never write it and never consume
/// randomness. Instructions either change state or request interaction. Every reference to a declaration is a
/// package-local reference slot; <see cref="RuleSetBuilder"/> fills the resolution table.
/// </summary>
public abstract class IrNode
{
    /// <summary>Diagnostics metadata; deliberately excluded from semantic rule identity.</summary>
    public DslSourceLocation? SourceLocation { get; internal set; }

    public abstract string Primitive { get; }

    public virtual IReadOnlyList<IrNode> Children => [];

    public PrimitiveSignature Signature => PrimitiveSignatures.Get(Primitive);
}

/// <summary>Compile-time source attribution. It never contributes to semantic identity.</summary>
public static class IrSource
{
    public static T At<T>(this T node, DslSourceLocation source) where T : IrNode
    {
        // Attribute only unassigned descendants: a called/expanded body may have a more precise source.
        node.SourceLocation ??= source;
        foreach (var child in node.Children) child.At(source);
        return node;
    }
}

public abstract class Expr : IrNode
{
}

public abstract class Query : IrNode
{
}

public abstract class Inst : IrNode
{
}

public sealed class Block : IrNode
{
    public static readonly Block Empty = new(Array.Empty<Inst>());

    private readonly Inst[] _instructions;

    public Block(IEnumerable<Inst> instructions)
    {
        _instructions = instructions?.ToArray() ?? throw new ArgumentNullException(nameof(instructions));
    }

    public IReadOnlyList<Inst> Instructions => Array.AsReadOnly(_instructions);

    public int Count => _instructions.Length;

    public Inst this[int index] => _instructions[index];

    public override string Primitive => "block";

    public override IReadOnlyList<IrNode> Children => Array.AsReadOnly(_instructions);

    public override string ToString() => $"block[{Count}]";
}

// ---------------------------------------------------------------------------------------------------------------
// Expressions
// ---------------------------------------------------------------------------------------------------------------

public sealed class ConstExpr(Value value) : Expr
{
    public Value Value { get; } = value;

    public override string Primitive => "expr.const";

    public override string ToString() => $"const({Value})";
}

/// <summary>Reads a frame slot: parameters occupy the leading slots, declared locals follow.</summary>
public sealed class LocalExpr(int slot) : Expr
{
    public int Slot { get; } = slot;

    public override string Primitive => "expr.local";

    public override string ToString() => $"local[{Slot}]";
}

public sealed class ParamExpr(int slot) : Expr
{
    public int Slot { get; } = slot;

    public override string Primitive => "expr.param";

    public override string ToString() => $"param[{Slot}]";
}

/// <summary>The scope the executing frame runs in. Used to create entities and child scopes.</summary>
public sealed class FrameScopeExpr : Expr
{
    public override string Primitive => "expr.scope";

    public override string ToString() => "scope";
}

/// <summary>
/// The scope an instance lives in. Needed to attach a created entity to its owner's scope (for example a power
/// instance next to the creature that owns it) without the author having to thread a scope handle through by hand.
/// </summary>
public sealed class EntityScopeExpr(Expr entity) : Expr
{
    public Expr Entity { get; } = entity;

    public override string Primitive => "expr.entityScope";

    public override IReadOnlyList<IrNode> Children => [Entity];

    public override string ToString() => $"scopeOf({Entity})";
}

/// <summary>Reads a declared field of a referenced instance. <see cref="FieldRef"/> is a package-local ref slot.</summary>
public sealed class FieldExpr(Expr target, int fieldRef) : Expr
{
    public Expr Target { get; } = target;

    public int FieldRef { get; } = fieldRef;

    public override string Primitive => "expr.field";

    public override IReadOnlyList<IrNode> Children => [Target];

    public override string ToString() => $"field({Target}, @{FieldRef})";
}

public enum ArithOp
{
    Add = 0,
    Subtract = 1,
    Multiply = 2,
    Divide = 3,
    Modulo = 4,
    Minimum = 5,
    Maximum = 6,
}

public sealed class ArithExpr(ArithOp op, Expr left, Expr right) : Expr
{
    public ArithOp Op { get; } = op;

    public Expr Left { get; } = left;

    public Expr Right { get; } = right;

    public override string Primitive => "expr.arith";

    public override IReadOnlyList<IrNode> Children => [Left, Right];

    public override string ToString() => $"{Op}({Left}, {Right})";
}

public enum CompareOp
{
    Equal = 0,
    NotEqual = 1,
    Less = 2,
    LessOrEqual = 3,
    Greater = 4,
    GreaterOrEqual = 5,
}

public sealed class CompareExpr(CompareOp op, Expr left, Expr right) : Expr
{
    public CompareOp Op { get; } = op;

    public Expr Left { get; } = left;

    public Expr Right { get; } = right;

    public override string Primitive => "expr.compare";

    public override IReadOnlyList<IrNode> Children => [Left, Right];

    public override string ToString() => $"{Left} {Op} {Right}";
}

public sealed class LogicExpr(bool isAnd, Expr left, Expr right) : Expr
{
    public bool IsAnd { get; } = isAnd;

    public Expr Left { get; } = left;

    public Expr Right { get; } = right;

    public override string Primitive => "expr.logic";

    public override IReadOnlyList<IrNode> Children => [Left, Right];

    public override string ToString() => $"({Left} {(IsAnd ? "and" : "or")} {Right})";
}

public sealed class NotExpr(Expr operand) : Expr
{
    public Expr Operand { get; } = operand;

    public override string Primitive => "expr.not";

    public override IReadOnlyList<IrNode> Children => [Operand];

    public override string ToString() => $"not({Operand})";
}

public enum ConvertOp
{
    IntToFixed = 0,
    FixedToIntTruncate = 1,
    FixedToIntFloor = 2,
    FixedToIntCeiling = 3,
    FixedToIntRound = 4,
}

public sealed class ConvertExpr(ConvertOp op, Expr operand) : Expr
{
    public ConvertOp Op { get; } = op;

    public Expr Operand { get; } = operand;

    public override string Primitive => "expr.convert";

    public override IReadOnlyList<IrNode> Children => [Operand];

    public override string ToString() => $"{Op}({Operand})";
}

public sealed class EnumConstExpr(int enumRef, string member) : Expr
{
    public int EnumRef { get; } = enumRef;

    public string Member { get; } = member;

    public override string Primitive => "expr.enum";

    public override string ToString() => $"enum@{EnumRef}.{Member}";
}

public sealed class CountExpr(Query source) : Expr
{
    public Query Source { get; } = source;

    public override string Primitive => "expr.count";

    public override IReadOnlyList<IrNode> Children => [Source];

    public override string ToString() => $"count({Source})";
}

/// <summary>
/// Absolute index of an instance inside a collection, or -1 when it is not a member. This is how a package states a
/// position it knows (for example "the card this effect put on top is at position 0") without the engine having to
/// guess what the player knows.
/// </summary>
public sealed class CollectionIndexExpr(int collectionRef, Expr item) : Expr
{
    public int CollectionRef { get; } = collectionRef;

    public Expr Item { get; } = item;

    public override string Primitive => "expr.collectionIndex";

    public override IReadOnlyList<IrNode> Children => [Item];

    public override string ToString() => $"index(collection@{CollectionRef}, {Item})";
}

public sealed class ExistsExpr(Query source) : Expr
{
    public Query Source { get; } = source;

    public override string Primitive => "expr.exists";

    public override IReadOnlyList<IrNode> Children => [Source];

    public override string ToString() => $"exists({Source})";
}

/// <summary>First candidate of a query, or <see cref="Value.None"/> when the query is empty.</summary>
public sealed class FirstExpr(Query source) : Expr
{
    public Query Source { get; } = source;

    public override string Primitive => "expr.first";

    public override IReadOnlyList<IrNode> Children => [Source];

    public override string ToString() => $"first({Source})";
}

public sealed class IsNoneExpr(Expr operand) : Expr
{
    public Expr Operand { get; } = operand;

    public override string Primitive => "expr.none";

    public override IReadOnlyList<IrNode> Children => [Operand];

    public override string ToString() => $"isNone({Operand})";
}

/// <summary>Calls a declared pure procedure. Purity is enforced at link time and again while executing.</summary>
public sealed class CallPureExpr(int procedureRef, IReadOnlyList<Expr> arguments) : Expr
{
    public int ProcedureRef { get; } = procedureRef;

    public IReadOnlyList<Expr> Arguments { get; } = Array.AsReadOnly(arguments.ToArray());

    public override string Primitive => "expr.call";

    public override IReadOnlyList<IrNode> Children => Arguments;

    public override string ToString() => $"call@{ProcedureRef}({string.Join(", ", Arguments)})";
}

/// <summary>Read-only ordered reduction through pure event listeners; the first payload is the accumulator.</summary>
public sealed class FoldEventExpr(int eventRef, IReadOnlyList<Expr> arguments) : Expr
{
    public int EventRef { get; } = eventRef;
    public IReadOnlyList<Expr> Arguments { get; } = Array.AsReadOnly(arguments.ToArray());
    public override string Primitive => "expr.foldEvent";
    public override IReadOnlyList<IrNode> Children => Arguments;
}

public enum AggregateOp
{
    Sum = 0,
    Minimum = 1,
    Maximum = 2,
    Count = 3,
    SumFloat32 = 4,
}

/// <summary>Reduces a query to one value. The item slot binds each candidate while the value expression is evaluated.</summary>
public sealed class AggregateExpr(Query source, AggregateOp op, int itemSlot, Expr? itemValue) : Expr
{
    public Query Source { get; } = source;

    public AggregateOp Op { get; } = op;

    public int ItemSlot { get; } = itemSlot;

    public Expr? ItemValue { get; } = itemValue;

    public override string Primitive => "expr.aggregate";

    public override IReadOnlyList<IrNode> Children => ItemValue is null ? [Source] : [Source, ItemValue];

    public override string ToString() => $"{Op}({Source})";
}

public sealed class StringConcatExpr(IReadOnlyList<Expr> parts) : Expr
{
    public IReadOnlyList<Expr> Parts { get; } = Array.AsReadOnly(parts.ToArray());

    public override string Primitive => "expr.concat";

    public override IReadOnlyList<IrNode> Children => Parts;

    public override string ToString() => $"concat({string.Join(" + ", Parts)})";
}

// ---------------------------------------------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------------------------------------------

/// <summary>All live instances of a declared entity type, optionally restricted to one scope type.</summary>
/// <summary>
/// 遍历一个**引用列表结果槽**里的引用（例如 <see cref="DecisionResponseKind.EntityRefList"/> 的决策结果）。
/// 元素类型是引用本身；它让"多选之后逐一消费"成为 IR 内可表达的事，而不需要宿主帮忙展开。
/// </summary>
public sealed class RefListQuery(int listSlot) : Query
{
    public int ListSlot { get; } = listSlot;

    public override string Primitive => "query.refList";
}

public sealed class TypeQuery(int typeRef, int scopeTypeRef = -1) : Query
{
    public int TypeRef { get; } = typeRef;

    public int ScopeTypeRef { get; } = scopeTypeRef;

    public override string Primitive => "query.type";

    public override string ToString() => ScopeTypeRef < 0 ? $"type@{TypeRef}" : $"type@{TypeRef} in scope@{ScopeTypeRef}";
}

/// <summary>Members of a declared collection in its declared order (ordered) or ascending instance id (unordered).</summary>
public sealed class CollectionQuery(int collectionRef, int scopeTypeRef = -1) : Query
{
    public int CollectionRef { get; } = collectionRef;

    public int ScopeTypeRef { get; } = scopeTypeRef;

    public override string Primitive => "query.collection";

    public override string ToString() => $"collection@{CollectionRef}";
}

public sealed class RelationQuery(int relationRef, Expr owner) : Query
{
    public int RelationRef { get; } = relationRef;

    public Expr Owner { get; } = owner;

    public override string Primitive => "query.relation";

    public override IReadOnlyList<IrNode> Children => [Owner];

    public override string ToString() => $"relation@{RelationRef}({Owner})";
}

public sealed class FilterQuery(Query source, Expr predicate, int itemSlot) : Query
{
    public Query Source { get; } = source;

    public Expr Predicate { get; } = predicate;

    public int ItemSlot { get; } = itemSlot;

    public override string Primitive => "query.filter";

    public override IReadOnlyList<IrNode> Children => [Source, Predicate];

    public override string ToString() => $"filter({Source})";
}

public sealed class SortQuery(Query source, Expr key, bool descending, int itemSlot, bool stable = true) : Query
{
    public bool Stable { get; } = stable;
    public Query Source { get; } = source;

    public Expr Key { get; } = key;

    public bool Descending { get; } = descending;

    public int ItemSlot { get; } = itemSlot;

    public override string Primitive => "query.sort";

    public override IReadOnlyList<IrNode> Children => [Source, Key];

    public override string ToString() => $"sort({Source}, {(Descending ? "desc" : "asc")})";
}

public sealed class TakeQuery(Query source, Expr count) : Query
{
    public Query Source { get; } = source;

    public Expr Count { get; } = count;

    public override string Primitive => "query.take";

    public override IReadOnlyList<IrNode> Children => [Source, Count];

    public override string ToString() => $"take({Source}, {Count})";
}

public sealed class SkipQuery(Query source, Expr count) : Query
{
    public Query Source { get; } = source;

    public Expr Count { get; } = count;

    public override string Primitive => "query.skip";

    public override IReadOnlyList<IrNode> Children => [Source, Count];

    public override string ToString() => $"skip({Source}, {Count})";
}

// ---------------------------------------------------------------------------------------------------------------
// Instructions
// ---------------------------------------------------------------------------------------------------------------

public sealed record FieldInit(int FieldRef, Expr Value);

public sealed record NamedExpr(string Name, Expr Value);

public sealed record NamedSlot(string Name, int Slot);

public enum InsertPosition
{
    Start = 0,
    End = 1,
    AtIndex = 2,
}

public enum DecisionResponseKind
{
    EntityRef = 0,
    EntityRefList = 1,
    Int = 2,
    Bool = 3,
    Enum = 4,
    Participant = 5,
    Confirm = 6,
}

public sealed class SetLocalInst(int slot, Expr value) : Inst
{
    public int Slot { get; } = slot;

    public Expr Value { get; } = value;

    public override string Primitive => "inst.setLocal";

    public override IReadOnlyList<IrNode> Children => [Value];

    public override string ToString() => $"local[{Slot}] = {Value}";
}

public sealed class SetFieldInst(Expr target, int fieldRef, Expr value) : Inst
{
    public Expr Target { get; } = target;

    public int FieldRef { get; } = fieldRef;

    public Expr Value { get; } = value;

    public override string Primitive => "inst.setField";

    public override IReadOnlyList<IrNode> Children => [Target, Value];

    public override string ToString() => $"set {Target}.@{FieldRef} = {Value}";
}

public sealed class CreateEntityInst(
    int typeRef,
    Expr scope,
    IReadOnlyList<FieldInit> fields,
    int resultSlot,
    int addToCollectionRef = -1,
    InsertPosition position = InsertPosition.End,
    Expr? positionIndex = null) : Inst
{
    public int TypeRef { get; } = typeRef;

    public Expr Scope { get; } = scope;

    public IReadOnlyList<FieldInit> Fields { get; } = Array.AsReadOnly(fields.ToArray());

    public int ResultSlot { get; } = resultSlot;

    public int AddToCollectionRef { get; } = addToCollectionRef;

    public InsertPosition Position { get; } = position;

    public Expr? PositionIndex { get; } = positionIndex;

    public override string Primitive => "inst.createEntity";

    public override IReadOnlyList<IrNode> Children =>
        PositionIndex is null ? [Scope, .. Fields.Select(f => f.Value)] : [Scope, PositionIndex, .. Fields.Select(f => f.Value)];

    public override string ToString() => $"create@{TypeRef} -> local[{ResultSlot}]";
}

public sealed class CreateScopeInst(int scopeTypeRef, Expr? parent, int resultSlot) : Inst
{
    public int ScopeTypeRef { get; } = scopeTypeRef;

    public Expr? Parent { get; } = parent;

    public int ResultSlot { get; } = resultSlot;

    public override string Primitive => "inst.createScope";

    public override IReadOnlyList<IrNode> Children => Parent is null ? [] : [Parent];

    public override string ToString() => $"createScope@{ScopeTypeRef} -> local[{ResultSlot}]";
}

/// <summary>Destroys instances: the entity itself, or its scope subtree when <see cref="ScopeSubtree"/> is set.</summary>
public sealed class DestroyEntityInst(Expr target, bool scopeSubtree = false) : Inst
{
    public Expr Target { get; } = target;

    public bool ScopeSubtree { get; } = scopeSubtree;

    public override string Primitive => "inst.destroyEntity";

    public override IReadOnlyList<IrNode> Children => [Target];

    public override string ToString() => ScopeSubtree ? $"destroyScope({Target})" : $"destroy({Target})";
}

public sealed class AddToCollectionInst(
    int collectionRef,
    Expr item,
    InsertPosition position = InsertPosition.End,
    Expr? positionIndex = null) : Inst
{
    public int CollectionRef { get; } = collectionRef;

    public Expr Item { get; } = item;

    public InsertPosition Position { get; } = position;

    public Expr? PositionIndex { get; } = positionIndex;

    public override string Primitive => "inst.addToCollection";

    public override IReadOnlyList<IrNode> Children =>
        PositionIndex is null ? [Item] : [Item, PositionIndex];

    public override string ToString() => $"add collection@{CollectionRef} <- {Item}";
}

public sealed class RemoveFromCollectionInst(int collectionRef, Expr item) : Inst
{
    public int CollectionRef { get; } = collectionRef;

    public Expr Item { get; } = item;

    public override string Primitive => "inst.removeFromCollection";

    public override IReadOnlyList<IrNode> Children => [Item];

    public override string ToString() => $"remove collection@{CollectionRef} <- {Item}";
}

public sealed class MoveInCollectionInst(
    int collectionRef,
    Expr item,
    InsertPosition position,
    Expr? positionIndex = null) : Inst
{
    public int CollectionRef { get; } = collectionRef;

    public Expr Item { get; } = item;

    public InsertPosition Position { get; } = position;

    public Expr? PositionIndex { get; } = positionIndex;

    public override string Primitive => "inst.moveInCollection";

    public override IReadOnlyList<IrNode> Children =>
        PositionIndex is null ? [Item] : [Item, PositionIndex];

    public override string ToString() => $"move collection@{CollectionRef} <- {Item}";
}

public sealed class ShuffleCollectionInst(int collectionRef, Expr stream) : Inst
{
    public int CollectionRef { get; } = collectionRef;

    public Expr Stream { get; } = stream;

    public override string Primitive => "inst.shuffleCollection";

    public override IReadOnlyList<IrNode> Children => [Stream];

    public override string ToString() => $"shuffle collection@{CollectionRef} with {Stream}";
}

public sealed class SetRelationInst(int relationRef, Expr owner, Expr target, bool add) : Inst
{
    public int RelationRef { get; } = relationRef;

    public Expr Owner { get; } = owner;

    public Expr Target { get; } = target;

    public bool Add { get; } = add;

    public override string Primitive => "inst.setRelation";

    public override IReadOnlyList<IrNode> Children => [Owner, Target];

    public override string ToString() => $"{(Add ? "add" : "remove")} relation@{RelationRef}({Owner}, {Target})";
}

public sealed class ClearRelationInst(int relationRef, Expr owner) : Inst
{
    public int RelationRef { get; } = relationRef;

    public Expr Owner { get; } = owner;

    public override string Primitive => "inst.clearRelation";

    public override IReadOnlyList<IrNode> Children => [Owner];

    public override string ToString() => $"clear relation@{RelationRef}({Owner})";
}

public sealed class SampleIntInst(Expr stream, Expr min, Expr max, int resultSlot) : Inst
{
    public Expr Stream { get; } = stream;

    public Expr Min { get; } = min;

    public Expr Max { get; } = max;

    public int ResultSlot { get; } = resultSlot;

    public override string Primitive => "inst.sampleInt";

    public override IReadOnlyList<IrNode> Children => [Stream, Min, Max];

    public override string ToString() => $"sample {Stream} in [{Min}, {Max}) -> local[{ResultSlot}]";
}

public sealed class SamplePickInst(Expr stream, Query candidates, int resultSlot) : Inst
{
    public Expr Stream { get; } = stream;

    public Query Candidates { get; } = candidates;

    public int ResultSlot { get; } = resultSlot;

    public override string Primitive => "inst.samplePick";

    public override IReadOnlyList<IrNode> Children => [Stream, Candidates];

    public override string ToString() => $"pick {Stream} <- {Candidates} -> local[{ResultSlot}]";
}

public sealed class EmitEventInst(int eventRef, IReadOnlyList<FieldInit> payload) : Inst
{
    public int EventRef { get; } = eventRef;

    public IReadOnlyList<FieldInit> Payload { get; } = Array.AsReadOnly(payload.ToArray());

    public override string Primitive => "inst.emitEvent";

    public override IReadOnlyList<IrNode> Children => Payload.Select(p => p.Value).ToArray();

    public override string ToString() => $"emit@{EventRef}({string.Join(", ", Payload.Select(p => p.Value))})";
}

public sealed class RequestDecisionInst(
    Expr participant,
    DecisionResponseKind kind,
    Query? candidates,
    Expr? minimum,
    Expr? maximum,
    string prompt,
    int resultSlot) : Inst
{
    public Expr Participant { get; } = participant;

    public DecisionResponseKind Kind { get; } = kind;

    public Query? Candidates { get; } = candidates;

    public Expr? Minimum { get; } = minimum;

    public Expr? Maximum { get; } = maximum;

    public string Prompt { get; } = prompt;

    public int ResultSlot { get; } = resultSlot;

    public override string Primitive => "inst.requestDecision";

    public override IReadOnlyList<IrNode> Children
    {
        get
        {
            var children = new List<IrNode> { Participant };
            if (Candidates is not null) children.Add(Candidates);
            if (Minimum is not null) children.Add(Minimum);
            if (Maximum is not null) children.Add(Maximum);
            return children;
        }
    }

    public override string ToString() => $"decide {Kind} for {Participant}: {Prompt}";
}

public sealed class ComputeInst(int computeRef, IReadOnlyList<NamedExpr> inputs, IReadOnlyList<NamedSlot> outputs) : Inst
{
    public int ComputeRef { get; } = computeRef;

    public IReadOnlyList<NamedExpr> Inputs { get; } = Array.AsReadOnly(inputs.ToArray());

    public IReadOnlyList<NamedSlot> Outputs { get; } = Array.AsReadOnly(outputs.ToArray());

    public override string Primitive => "inst.compute";

    public override IReadOnlyList<IrNode> Children => Inputs.Select(i => i.Value).ToArray();

    public override string ToString() => $"compute@{ComputeRef}({string.Join(", ", Inputs.Select(i => i.Name))})";
}

public sealed class CallInst(int procedureRef, IReadOnlyList<Expr> arguments, int resultSlot = -1) : Inst
{
    public int ProcedureRef { get; } = procedureRef;

    public IReadOnlyList<Expr> Arguments { get; } = Array.AsReadOnly(arguments.ToArray());

    public int ResultSlot { get; } = resultSlot;

    public override string Primitive => "inst.call";

    public override IReadOnlyList<IrNode> Children => Arguments;

    public override string ToString() => $"call@{ProcedureRef}({string.Join(", ", Arguments)})";
}

public sealed class IfInst(Expr condition, Block then, Block? otherwise = null) : Inst
{
    public Expr Condition { get; } = condition;

    public Block Then { get; } = then;

    public Block? Else { get; } = otherwise;

    public override string Primitive => "inst.if";

    public override IReadOnlyList<IrNode> Children => Else is null ? [Condition, Then] : [Condition, Then, Else];

    public override string ToString() => $"if {Condition}";
}

public sealed class RepeatInst(Expr count, Block body) : Inst
{
    public Expr Count { get; } = count;

    public Block Body { get; } = body;

    public override string Primitive => "inst.repeat";

    public override IReadOnlyList<IrNode> Children => [Count, Body];

    public override string ToString() => $"repeat {Count}";
}

/// <summary>Iterates a query snapshot. The candidate list and order are fixed when the loop starts.</summary>
public sealed class ForEachInst(Query query, int itemSlot, Block body) : Inst
{
    public Query Query { get; } = query;

    public int ItemSlot { get; } = itemSlot;

    public Block Body { get; } = body;

    public override string Primitive => "inst.forEach";

    public override IReadOnlyList<IrNode> Children => [Query, Body];

    public override string ToString() => $"forEach {Query}";
}

public sealed class ReturnInst(Expr? value = null) : Inst
{
    public Expr? Value { get; } = value;

    public override string Primitive => "inst.return";

    public override IReadOnlyList<IrNode> Children => Value is null ? [] : [Value];

    public override string ToString() => Value is null ? "return" : $"return {Value}";
}

public sealed class SetTerminalInst(int terminalRef, IReadOnlyList<FieldInit> fields) : Inst
{
    public int TerminalRef { get; } = terminalRef;

    public IReadOnlyList<FieldInit> Fields { get; } = Array.AsReadOnly(fields.ToArray());

    public override string Primitive => "inst.setTerminal";

    public override IReadOnlyList<IrNode> Children => Fields.Select(f => f.Value).ToArray();

    public override string ToString() => $"terminal@{TerminalRef}";
}

public sealed class FailInst(string message) : Inst
{
    public string Message { get; } = message;

    public override string Primitive => "inst.fail";

    public override string ToString() => $"fail(\"{Message}\")";
}

/// <summary>
/// Central primitive catalogue. One signature serves front-end validation, link-time checks, execution and docs.
/// </summary>
public static class PrimitiveSignatures
{
    private static readonly Dictionary<string, PrimitiveSignature> Table = Build();

    public static PrimitiveSignature Get(string primitive) =>
        Table.TryGetValue(primitive, out var signature)
            ? signature
            : throw Faults.Schema($"unknown primitive '{primitive}'");

    public static IReadOnlyCollection<PrimitiveSignature> All => Table.Values;

    private static Dictionary<string, PrimitiveSignature> Build()
    {
        var readPure = new[] { EngineEffect.PureExpression, EngineEffect.ReadsState };
        var table = new List<PrimitiveSignature>
        {
            new("expr.const", [EngineEffect.PureExpression]),
            new("expr.local", [EngineEffect.PureExpression]),
            new("expr.param", [EngineEffect.PureExpression]),
            new("expr.scope", [EngineEffect.PureExpression]),
            new("expr.entityScope", readPure),
            new("expr.field", readPure),
            new("expr.arith", [EngineEffect.PureExpression]),
            new("expr.compare", [EngineEffect.PureExpression]),
            new("expr.logic", [EngineEffect.PureExpression]),
            new("expr.not", [EngineEffect.PureExpression]),
            new("expr.convert", [EngineEffect.PureExpression]),
            new("expr.randomValue", [EngineEffect.PureExpression]),
            new("expr.binary64", [EngineEffect.PureExpression]),
            new("expr.word64", [EngineEffect.PureExpression]),
            new("expr.text", [EngineEffect.PureExpression]),
            new("expr.enum", [EngineEffect.PureExpression]),
            new("expr.count", readPure),
            new("expr.collectionIndex", readPure),
            new("expr.exists", readPure),
            new("expr.first", readPure),
            new("expr.none", [EngineEffect.PureExpression]),
            new("expr.call", [EngineEffect.PureExpression, EngineEffect.ReadsState, EngineEffect.CallsProcedure]),
            new("expr.foldEvent", [EngineEffect.PureExpression, EngineEffect.ReadsState, EngineEffect.CallsProcedure]),
            new("expr.aggregate", readPure),
            new("expr.concat", [EngineEffect.PureExpression]),
            new("block", []),
            new("query.type", readPure),
            new("query.collection", readPure),
            new("query.relation", readPure),
            new("query.filter", readPure),
            new("query.sort", readPure),
            new("query.take", readPure),
            new("query.skip", readPure),
            new("inst.setLocal", []),
            new("inst.setField", [EngineEffect.WritesState]),
            new("inst.createEntity", [EngineEffect.WritesState]),
            new("inst.createScope", [EngineEffect.WritesState]),
            new("inst.destroyEntity", [EngineEffect.WritesState]),
            new("inst.addToCollection", [EngineEffect.WritesState]),
            new("inst.removeFromCollection", [EngineEffect.WritesState]),
            new("inst.moveInCollection", [EngineEffect.WritesState]),
            new("inst.shuffleCollection", [EngineEffect.WritesState, EngineEffect.ConsumesRandom]),
            new("inst.setRelation", [EngineEffect.WritesState]),
            new("inst.clearRelation", [EngineEffect.WritesState]),
            new("inst.sampleInt", [EngineEffect.ConsumesRandom]),
            new("inst.seedStream", [EngineEffect.WritesState]),
            new("inst.seedStream64", [EngineEffect.WritesState]),
            new("inst.sampleFloat32", [EngineEffect.ConsumesRandom]),
            new("inst.samplePick", [EngineEffect.ConsumesRandom]),
            new("inst.emitEvent", [EngineEffect.EmitsEvent]),
            new("inst.requestDecision", [EngineEffect.RequestsDecision]),
            new("inst.compute", [EngineEffect.CallsCompute]),
            new("inst.call", [EngineEffect.CallsProcedure]),
            new("inst.if", []),
            new("inst.repeat", []),
            new("inst.forEach", []),
            new("inst.return", []),
            new("inst.setTerminal", [EngineEffect.WritesState, EngineEffect.EndsSession]),
            new("inst.fail", [EngineEffect.EndsSession]),
        };

        var dictionary = new Dictionary<string, PrimitiveSignature>(table.Count, StringComparer.Ordinal);
        foreach (var signature in table) dictionary.Add(signature.Name, signature);
        return dictionary;
    }
}
