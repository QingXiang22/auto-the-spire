using System.Collections.ObjectModel;

namespace CoreTheSpire;

public abstract record SpireIntegerExpression;
public sealed record SpireIntegerLiteral(int Value) : SpireIntegerExpression;
public sealed record SpireIntegerRange(int Minimum, int Maximum) : SpireIntegerExpression;
public sealed record SpireProfiledInteger(IReadOnlyDictionary<string, SpireIntegerExpression> Values)
    : SpireIntegerExpression;

public enum SpireEffectTarget { Self, Player, Enemy }
public enum SpireCardPile { Draw, Hand, Discard, Exhaust }

public abstract record SpireEffectExpression;
public sealed record SpireAttackExpression(SpireEffectTarget Target, SpireIntegerExpression Damage) : SpireEffectExpression;
public sealed record SpireApplyPowerExpression(string Power, SpireIntegerExpression Amount, SpireEffectTarget Target)
    : SpireEffectExpression;
public sealed record SpireGainBlockExpression(SpireIntegerExpression Amount) : SpireEffectExpression;
public sealed record SpireAddCardsExpression(string Card, int Count, SpireCardPile Pile, SpireEffectTarget Target)
    : SpireEffectExpression;
public sealed record SpireDrawCardsExpression(int Count) : SpireEffectExpression;
public sealed record SpireSequenceExpression(IReadOnlyList<SpireEffectExpression> Effects) : SpireEffectExpression;

public sealed record SpireMoveChoice(string Move, int MaxConsecutiveUses = int.MaxValue);
public abstract record SpireMoveSelection;
public sealed record SpireSingleMoveSelection(string Move) : SpireMoveSelection;
public sealed record SpireChoiceSelection(IReadOnlyList<SpireMoveChoice> Choices, bool PreventImmediateRepeat)
    : SpireMoveSelection;

public abstract record SpireEnemyBehavior;
public sealed record SpireCycleBehavior(IReadOnlyList<string> Moves) : SpireEnemyBehavior;
public sealed record SpireLoopBehavior(SpireMoveSelection Selection) : SpireEnemyBehavior;
public sealed record SpireStartWithBehavior(string FirstMove, SpireEnemyBehavior Then) : SpireEnemyBehavior;
public sealed record SpireStartByRoleBehavior(IReadOnlyDictionary<string, string> StartingMoves,
    SpireEnemyBehavior Then) : SpireEnemyBehavior;

public sealed record SpireEnemyMove(string Name, SpireEffectExpression Effect, DslSourceLocation Location);
public sealed record SpireEnemyModel(string Name, SpireIntegerExpression Hp, IReadOnlyList<SpireEnemyMove> Moves,
    SpireEnemyBehavior Behavior, DslSourceLocation Location);
public sealed record SpireSemanticPackage(string Name, IReadOnlyList<SpireEnemyModel> Enemies);

/// <summary>Type-checks enemy values, effects, behaviors, and their references without lowering to runtime state.</summary>
public static class SpireSemanticCompiler
{
    private sealed class Compiler
    {
        private readonly DslPackageNode _syntax;
        private readonly IReadOnlyDictionary<string, string> _declarations;
        private readonly IReadOnlyList<DslDeclarationNode> _enemies;

        public Compiler(DslPackageNode syntax)
        {
            _syntax = syntax;
            var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
            var enemies = new List<DslDeclarationNode>();
            foreach (var member in syntax.Members)
            {
                if (member is not DslDeclarationNode declaration)
                    throw Error(member.Location, "A package body can contain content declarations only.");
                if (declaration.Kind is not ("card" or "power" or "enemy" or "encounter"))
                    throw Error(declaration.Location, $"Unknown declaration kind '{declaration.Kind}'.");
                if (!declarations.TryAdd(declaration.Name, declaration.Kind))
                    throw Error(declaration.Location, $"Duplicate content name '{declaration.Name}'.");
                if (declaration.Kind == "enemy") enemies.Add(declaration);
            }
            _declarations = declarations;
            _enemies = enemies;
        }

        public SpireSemanticPackage Compile()
        {
            var enemies = _enemies.Select(CompileEnemy).ToArray();
            return new SpireSemanticPackage(_syntax.Name, Array.AsReadOnly(enemies));
        }

        private SpireEnemyModel CompileEnemy(DslDeclarationNode declaration)
        {
            var properties = Properties(declaration);
            var allowedProperties = new HashSet<string>(["hp", "moves", "behavior"], StringComparer.Ordinal);
            foreach (var property in properties.Keys)
                if (!allowedProperties.Contains(property))
                    throw Error(declaration.Location, $"Unknown enemy property '{property}'.");
            foreach (var member in declaration.Members)
                if (member is not DslPropertyNode)
                    throw Error(member.Location, "Enemy declarations can contain properties only.");
            var hp = Required(properties, "hp", declaration.Location);
            var movesNode = Required(properties, "moves", declaration.Location);
            var behaviorNode = Required(properties, "behavior", declaration.Location);
            var moves = CompileMoves(movesNode);
            var moveNames = moves.Select(move => move.Name).ToHashSet(StringComparer.Ordinal);
            var behavior = CompileBehavior(behaviorNode, moveNames);
            var hitPoints = CompileInteger(hp);
            EnsurePositiveHp(hitPoints, hp.Location);
            return new SpireEnemyModel(declaration.Name, hitPoints, Array.AsReadOnly(moves), behavior,
                declaration.Location);
        }

        private static void EnsurePositiveHp(SpireIntegerExpression expression, DslSourceLocation location)
        {
            switch (expression)
            {
                case SpireIntegerLiteral { Value: > 0 }:
                    return;
                case SpireIntegerRange { Minimum: > 0 }:
                    return;
                case SpireProfiledInteger profile:
                    foreach (var value in profile.Values.Values) EnsurePositiveHp(value, location);
                    return;
                default:
                    throw Error(location, "Enemy HP values must be positive.");
            }
        }

        private SpireEnemyMove[] CompileMoves(DslValueNode node)
        {
            var actions = RequireCall(node, "actions");
            if (actions.Arguments.Count == 0) throw Error(actions.Location, "An enemy must define at least one move.");
            var moves = new List<SpireEnemyMove>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in actions.Arguments)
            {
                if (argument.Name is null) throw Error(argument.Location, "Move definitions need a name.");
                if (!names.Add(argument.Name)) throw Error(argument.Location, $"Duplicate move '{argument.Name}'.");
                moves.Add(new SpireEnemyMove(argument.Name, CompileEffect(argument.Value), argument.Location));
            }
            return moves.ToArray();
        }

        private SpireEffectExpression CompileEffect(DslValueNode node)
        {
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "attack":
                    if (call.Arguments.Count != 2 || call.Arguments[0].Name is not null
                        || call.Arguments[1].Name is not (null or "damage"))
                        throw Error(call.Location, "attack expects a target and a damage expression.");
                    return new SpireAttackExpression(CompileTarget(call.Arguments[0].Value),
                        CompileInteger(call.Arguments[1].Value));
                case "applyPower":
                    RequireArgumentCount(call, 3);
                    var power = Name(call.Arguments[0].Value);
                    RequireReference(power, "power", call.Arguments[0].Value.Location);
                    return new SpireApplyPowerExpression(power, CompileInteger(call.Arguments[1].Value),
                        CompileTarget(call.Arguments[2].Value));
                case "gainBlock":
                    RequireArgumentCount(call, 1);
                    return new SpireGainBlockExpression(CompileInteger(call.Arguments[0].Value));
                case "addCard":
                case "addCards":
                    var expectedCount = call.Name == "addCard" ? 3 : 4;
                    RequireArgumentCount(call, expectedCount);
                    var card = Name(call.Arguments[0].Value);
                    RequireReference(card, "card", call.Arguments[0].Value.Location);
                    var offset = call.Name == "addCards" ? 1 : 0;
                    var count = call.Name == "addCards" ? IntegerLiteral(call.Arguments[1].Value) : 1;
                    if (count <= 0) throw Error(call.Location, "Added card count must be positive.");
                    return new SpireAddCardsExpression(card, count,
                        CompilePile(call.Arguments[1 + offset].Value), CompileTarget(call.Arguments[2 + offset].Value));
                case "draw":
                    RequireArgumentCount(call, 1);
                    return new SpireDrawCardsExpression(IntegerLiteral(call.Arguments[0].Value));
                case "sequence":
                    return new SpireSequenceExpression(Array.AsReadOnly(call.Arguments
                        .Select(argument => CompileEffect(argument.Value)).ToArray()));
                default:
                    throw Error(call.Location, $"'{call.Name}' is not an effect expression.");
            }
        }

        private SpireEnemyBehavior CompileBehavior(DslValueNode node, IReadOnlySet<string> moves)
        {
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "cycle":
                    if (call.Arguments.Count == 0) throw Error(call.Location, "A cycle must contain at least one move.");
                    return new SpireCycleBehavior(Array.AsReadOnly(call.Arguments
                        .Select(argument => MoveName(argument.Value, moves)).ToArray()));
                case "loop":
                    RequireArgumentCount(call, 1);
                    return new SpireLoopBehavior(CompileSelection(call.Arguments[0].Value, moves));
                case "startWith":
                    RequireArgumentCount(call, 2);
                    return new SpireStartWithBehavior(MoveName(call.Arguments[0].Value, moves),
                        CompileBehavior(call.Arguments[1].Value, moves));
                case "startByRole":
                    var roleMoves = new Dictionary<string, string>(StringComparer.Ordinal);
                    DslValueNode? thenNode = null;
                    foreach (var argument in call.Arguments)
                    {
                        if (argument.Name == "then")
                        {
                            thenNode = argument.Value;
                            continue;
                        }
                        if (argument.Name is not ("alone" or "front" or "back"))
                            throw Error(argument.Location, "Starting roles must be alone, front, back, or then.");
                        if (!roleMoves.TryAdd(argument.Name, MoveName(argument.Value, moves)))
                            throw Error(argument.Location, $"Duplicate starting role '{argument.Name}'.");
                    }
                    if (roleMoves.Count == 0 || thenNode is null)
                        throw Error(call.Location, "startByRole needs at least one role and a then behavior.");
                    return new SpireStartByRoleBehavior(new ReadOnlyDictionary<string, string>(roleMoves),
                        CompileBehavior(thenNode, moves));
                default:
                    throw Error(call.Location, $"'{call.Name}' is not an enemy behavior expression.");
            }
        }

        private SpireMoveSelection CompileSelection(DslValueNode node, IReadOnlySet<string> moves)
        {
            if (node is DslNameNode name) return new SpireSingleMoveSelection(MoveName(name, moves));
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "chooseWithoutRepeat":
                    if (call.Arguments.Count < 2)
                        throw Error(call.Location, "chooseWithoutRepeat needs at least two moves.");
                    return new SpireChoiceSelection(Array.AsReadOnly(call.Arguments.Select(argument =>
                        new SpireMoveChoice(MoveName(argument.Value, moves))).ToArray()), true);
                case "chooseWithLimits":
                    if (call.Arguments.Count == 0)
                        throw Error(call.Location, "chooseWithLimits needs at least one candidate.");
                    var choices = new List<SpireMoveChoice>();
                    foreach (var argument in call.Arguments)
                    {
                        var candidate = RequireCall(argument.Value, "candidate");
                        if (candidate.Arguments.Count != 2 || candidate.Arguments[0].Name is not null
                            || candidate.Arguments[1].Name != "maxConsecutive")
                            throw Error(candidate.Location, "candidate expects a move and maxConsecutive.");
                        var move = MoveName(candidate.Arguments[0].Value, moves);
                        var max = IntegerLiteral(NamedArgument(candidate, "maxConsecutive"));
                        if (max <= 0) throw Error(candidate.Location, "maxConsecutive must be positive.");
                        choices.Add(new SpireMoveChoice(move, max));
                    }
                    return new SpireChoiceSelection(Array.AsReadOnly(choices.ToArray()), false);
                default:
                    throw Error(call.Location, $"'{call.Name}' is not a move selection expression.");
            }
        }

        private SpireIntegerExpression CompileInteger(DslValueNode node)
        {
            if (node is DslNumberNode) return new SpireIntegerLiteral(IntegerLiteral(node));
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "range":
                    RequireArgumentCount(call, 2);
                    var minimum = IntegerLiteral(call.Arguments[0].Value);
                    var maximum = IntegerLiteral(call.Arguments[1].Value);
                    if (minimum < 0 || maximum < minimum)
                        throw Error(call.Location, "Integer ranges must satisfy 0 <= minimum <= maximum.");
                    return new SpireIntegerRange(minimum, maximum);
                case "variants":
                    var values = new Dictionary<string, SpireIntegerExpression>(StringComparer.Ordinal);
                    foreach (var argument in call.Arguments)
                    {
                        if (argument.Name is null || argument.Name is not ("default" or "deadly" or "tough" or "deadlyTough"))
                            throw Error(argument.Location, "Difficulty variants use default, deadly, tough, or deadlyTough names.");
                        if (!values.TryAdd(argument.Name, CompileInteger(argument.Value)))
                            throw Error(argument.Location, $"Duplicate difficulty variant '{argument.Name}'.");
                    }
                    if (!values.ContainsKey("default"))
                        throw Error(call.Location, "Difficulty variants require a default value.");
                    return new SpireProfiledInteger(new ReadOnlyDictionary<string, SpireIntegerExpression>(values));
                default:
                    throw Error(call.Location, $"'{call.Name}' is not an integer value expression.");
            }
        }

        private string MoveName(DslValueNode node, IReadOnlySet<string> moves)
        {
            var name = Name(node);
            if (!moves.Contains(name)) throw Error(node.Location, $"Unknown enemy move '{name}'.");
            return name;
        }

        private static SpireEffectTarget CompileTarget(DslValueNode node) => Name(node) switch
        {
            "self" => SpireEffectTarget.Self,
            "player" => SpireEffectTarget.Player,
            "enemy" => SpireEffectTarget.Enemy,
            _ => throw Error(node.Location, "Target must be self, player, or enemy.")
        };

        private static SpireCardPile CompilePile(DslValueNode node) => Name(node) switch
        {
            "draw" => SpireCardPile.Draw,
            "hand" => SpireCardPile.Hand,
            "discard" => SpireCardPile.Discard,
            "exhaust" => SpireCardPile.Exhaust,
            _ => throw Error(node.Location, "Card pile must be draw, hand, discard, or exhaust.")
        };

        private void RequireReference(string name, string kind, DslSourceLocation location)
        {
            if (!_declarations.TryGetValue(name, out var actualKind))
                throw Error(location, $"Unknown content reference '{name}'.");
            if (actualKind != kind)
                throw Error(location, $"'{name}' is a {actualKind}; expected a {kind}.");
        }

        private static Dictionary<string, DslValueNode> Properties(DslDeclarationNode declaration)
        {
            var properties = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var property in declaration.Members.OfType<DslPropertyNode>())
                if (!properties.TryAdd(property.Name, property.Value))
                    throw Error(property.Location, $"Duplicate property '{property.Name}'.");
            return properties;
        }

        private static DslCallNode RequireCall(DslValueNode value, string? expectedName = null)
        {
            if (value is not DslCallNode call)
                throw Error(value.Location, "Expected a function expression.");
            if (expectedName is not null && call.Name != expectedName)
                throw Error(call.Location, $"Expected '{expectedName}'.");
            return call;
        }

        private static void RequireArgumentCount(DslCallNode call, int count)
        {
            if (call.Arguments.Count != count)
                throw Error(call.Location, $"'{call.Name}' expects {count} arguments.");
            if (call.Arguments.Any(argument => argument.Name is not null))
                throw Error(call.Location, $"'{call.Name}' uses positional arguments.");
        }

        private static DslValueNode NamedArgument(DslCallNode call, string name) =>
            call.Arguments.SingleOrDefault(argument => argument.Name == name)?.Value
            ?? throw Error(call.Location, $"'{call.Name}' requires argument '{name}'.");

        private static int IntegerLiteral(DslValueNode node)
        {
            if (node is DslNumberNode number && number.TryGetInt32(out var value)) return value;
            throw Error(node.Location, "Expected a 32-bit integer literal.");
        }

        private static string Name(DslValueNode node) => node is DslNameNode name
            ? name.Name
            : throw Error(node.Location, "Expected a name.");

        private static DslSourceException Error(DslSourceLocation location, string message) => new(location, message);

        private static DslValueNode Required(IReadOnlyDictionary<string, DslValueNode> properties, string name,
            DslSourceLocation location) => properties.TryGetValue(name, out var value)
            ? value
            : throw Error(location, $"Missing required property '{name}'.");
    }

    public static SpireSemanticPackage Compile(DslPackageNode syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        return new Compiler(syntax).Compile();
    }
}
