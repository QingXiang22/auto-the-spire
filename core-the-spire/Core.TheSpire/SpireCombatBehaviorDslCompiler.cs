using System.Text;

namespace CoreTheSpire;

public sealed record SpireCombatBehaviorContent(
    IReadOnlyList<PowerDefinition> Powers,
    IReadOnlyList<EnemyDefinition> Enemies);

/// <summary>Compiles declarative Power and enemy behavior into the existing combat definitions.</summary>
public static class SpireCombatBehaviorDslCompiler
{
    private sealed class Compiler(DslPackageNode package, ContentVariantProfile profile)
    {
        private sealed record BehaviorPlan(string StartMove,
            IReadOnlyDictionary<string, string> NextMoves,
            IReadOnlyDictionary<string, EnemyActionTransition> Transitions,
            EnemyActionTransition? StartingTransition = null);

        private readonly string _packageName = package.Name;

        public SpireCombatBehaviorContent Compile()
        {
            var declarations = package.Members.OfType<DslDeclarationNode>().ToArray();
            if (declarations.Length != package.Members.Count)
                throw Error(package.Location, "A combat behavior package can contain declarations only.");

            var powers = declarations.Where(declaration => declaration.Kind == "power")
                .Select(CompilePower).ToArray();
            var enemies = declarations.Where(declaration => declaration.Kind == "enemy")
                .Select(CompileEnemy).ToArray();
            if (powers.Length + enemies.Length != declarations.Length)
                throw Error(package.Location, "Combat behavior packages support power and enemy declarations only.");
            EnsureUnique(powers.Select(power => power.Id), "Power");
            EnsureUnique(enemies.Select(enemy => enemy.Id), "Enemy");
            EnsureUnique(enemies.SelectMany(enemy => enemy.Actions).Select(action => action.Id), "Enemy action");
            return new SpireCombatBehaviorContent(Array.AsReadOnly(powers), Array.AsReadOnly(enemies));
        }

        private PowerDefinition CompilePower(DslDeclarationNode declaration)
        {
            var properties = Properties(declaration, "Power");
            Only(properties, "trigger", "effect", "skipFirstWhenAppliedToEnemy", "skipFirstWhenAppliedToPlayer", "role");
            var trigger = properties.TryGetValue("trigger", out var triggerNode)
                ? EnumValue<PowerTrigger>(triggerNode)
                : PowerTrigger.None;
            ContentId? triggerPower = null;
            if (properties.TryGetValue("effect", out var effectNode))
            {
                var call = RequireCall(effectNode);
                if (call.Name != "grantPower")
                    throw Error(call.Location, "A triggered Power effect must use grantPower(power, sourceStacks).");
                CheckShape(call, 2);
                if (Name(Argument(call, 1)) != "sourceStacks")
                    throw Error(Argument(call, 1).Location, "grantPower currently requires sourceStacks as its amount.");
                triggerPower = Id(Name(Argument(call, 0)));
            }
            var skipEnemy = OptionalBoolean(properties, "skipFirstWhenAppliedToEnemy");
            var skipPlayer = OptionalBoolean(properties, "skipFirstWhenAppliedToPlayer");
            var role = properties.TryGetValue("role", out var roleNode)
                ? EnumValue<PowerRole>(roleNode)
                : PowerRole.None;
            return new PowerDefinition(Id(declaration.Name), trigger, triggerPower,
                skipEnemy, role, SkipFirstTriggerWhenAppliedToPlayer: skipPlayer);
        }

        private EnemyDefinition CompileEnemy(DslDeclarationNode declaration)
        {
            var properties = Properties(declaration, "Enemy");
            Only(properties, "hp", "moves", "behavior");
            var (minHp, maxHp) = HpRange(Required(properties, "hp", declaration.Location));
            var moves = RequireCall(Required(properties, "moves", declaration.Location));
            if (moves.Name != "actions") throw Error(moves.Location, "Enemy moves must use actions(name = effect, ...).");
            if (moves.Arguments.Count == 0 || moves.Arguments.Any(argument => argument.Name is null))
                throw Error(moves.Location, "Enemy actions need one or more named move effects.");
            if (moves.Arguments.Select(argument => argument.Name).Distinct(StringComparer.Ordinal).Count() != moves.Arguments.Count)
                throw Error(moves.Location, "Enemy move names must be unique.");

            var behavior = RequireCall(Required(properties, "behavior", declaration.Location));
            var behaviorPlan = CompileBehavior(behavior);
            var moveByName = moves.Arguments.ToDictionary(argument => argument.Name!, argument => argument.Value,
                StringComparer.Ordinal);
            var usedMoves = behaviorPlan.NextMoves.Keys
                .Concat(behaviorPlan.NextMoves.Values)
                .Concat(behaviorPlan.Transitions.Keys)
                .Append(behaviorPlan.StartMove)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var moveName in usedMoves)
                if (!moveByName.ContainsKey(moveName))
                    throw Error(behavior.Location, $"Enemy behavior references unknown move '{moveName}'.");
            if (moveByName.Keys.Except(usedMoves, StringComparer.Ordinal).Any())
                throw Error(moves.Location, "Every declared enemy move must appear in its behavior.");

            var moveNames = moves.Arguments.Select(argument => argument.Name!).ToArray();
            var actionIds = moveNames.ToDictionary(name => name, Id, StringComparer.Ordinal);
            var actions = new EnemyActionDefinition[moveNames.Length];
            for (var i = 0; i < moveNames.Length; i++)
            {
                var moveName = moveNames[i];
                var sourceEffect = moveByName[moveName];
                var effect = SpireCombatDslCompiler.CompileEffect(package, sourceEffect, profile);
                var (intent, damage) = DescribeIntent(effect);
                var transition = behaviorPlan.Transitions.TryGetValue(moveName, out var moveTransition)
                    ? moveTransition
                    : null;
                var nextActionId = behaviorPlan.NextMoves.TryGetValue(moveName, out var nextMove)
                    ? actionIds[nextMove]
                    : (ContentId?)null;
                actions[i] = new EnemyActionDefinition(actionIds[moveName], effect, intent, damage,
                    NextActionId: nextActionId, Transition: transition);
            }
            return new EnemyDefinition(Id(declaration.Name), minHp, maxHp, actions,
                startingActionId: behaviorPlan.StartingTransition is null ? actionIds[behaviorPlan.StartMove] : null,
                startingTransition: behaviorPlan.StartingTransition);
        }

        private BehaviorPlan CompileBehavior(DslCallNode behavior)
        {
            if (behavior.Name == "cycle")
            {
                if (behavior.Arguments.Count == 0 || behavior.Arguments.Any(argument => argument.Name is not null))
                    throw Error(behavior.Location, "cycle requires one or more positional move names.");
                var moves = behavior.Arguments.Select(argument => Name(argument.Value)).ToArray();
                if (moves.Distinct(StringComparer.Ordinal).Count() != moves.Length)
                    throw Error(behavior.Location, "A cycle cannot repeat a move name.");
                var nextMoves = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < moves.Length; i++) nextMoves.Add(moves[i], moves[(i + 1) % moves.Length]);
                return new BehaviorPlan(moves[0], nextMoves,
                    new Dictionary<string, EnemyActionTransition>(StringComparer.Ordinal));
            }

            if (behavior.Name == "startWith")
            {
                if (behavior.Arguments.Count != 2 || behavior.Arguments[0].Name is not null
                    || behavior.Arguments[1].Name != "then")
                    throw Error(behavior.Location, "startWith requires startWith(move, then=behavior).");
                var first = Name(behavior.Arguments[0].Value);
                var after = CompileBehavior(RequireCall(behavior.Arguments[1].Value));
                var nextMoves = new Dictionary<string, string>(after.NextMoves, StringComparer.Ordinal);
                var transitions = new Dictionary<string, EnemyActionTransition>(after.Transitions, StringComparer.Ordinal);
                if (after.StartingTransition is null)
                    nextMoves[first] = after.StartMove;
                else
                    transitions[first] = after.StartingTransition;
                return new BehaviorPlan(first, nextMoves, transitions);
            }

            if (behavior.Name == "loop")
            {
                if (behavior.Arguments.Count != 1 || behavior.Arguments[0].Name is not null)
                    throw Error(behavior.Location, "loop requires one move or one choice expression.");
                if (behavior.Arguments[0].Value is DslNameNode repeated)
                    return new BehaviorPlan(repeated.Name,
                        new Dictionary<string, string>(StringComparer.Ordinal) { [repeated.Name] = repeated.Name },
                        new Dictionary<string, EnemyActionTransition>(StringComparer.Ordinal));
                return CompileBehavior(RequireCall(behavior.Arguments[0].Value));
            }

            if (behavior.Name is "chooseWithoutRepeat" or "chooseWithLimits")
            {
                var choiceNames = new List<(string Name, int MaxConsecutive)>();
                if (behavior.Name == "chooseWithoutRepeat")
                {
                    if (behavior.Arguments.Count == 0 || behavior.Arguments.Any(argument => argument.Name is not null))
                        throw Error(behavior.Location, "chooseWithoutRepeat requires positional move names.");
                    choiceNames.AddRange(behavior.Arguments.Select(argument => (Name(argument.Value), 0)));
                }
                else
                {
                    foreach (var argument in behavior.Arguments)
                    {
                        if (argument.Name is not null) throw Error(argument.Location, "chooseWithLimits takes candidate(...) expressions.");
                        var candidate = RequireCall(argument.Value);
                        if (candidate.Name != "candidate" || candidate.Arguments.Count != 2
                            || candidate.Arguments[0].Name is not null || candidate.Arguments[1].Name != "maxConsecutive")
                            throw Error(candidate.Location, "Each limited choice must be candidate(move, maxConsecutive=n).");
                        var name = Name(candidate.Arguments[0].Value);
                        var max = Integer(candidate.Arguments[1].Value);
                        if (max <= 0) throw Error(candidate.Arguments[1].Location, "maxConsecutive must be positive.");
                        choiceNames.Add((name, max));
                    }
                }
                if (choiceNames.Select(choice => choice.Name).Distinct(StringComparer.Ordinal).Count() != choiceNames.Count)
                    throw Error(behavior.Location, "Random behavior choices must be unique.");
                var choices = choiceNames.Select(choice => new EnemyActionChoice(Id(choice.Name),
                    MaxConsecutiveUses: choice.MaxConsecutive == 0 ? null : choice.MaxConsecutive)).ToArray();
                if (choices.Length < 2)
                    throw Error(behavior.Location, "A random behavior needs at least two choices.");
                var transition = new EnemyActionTransition(choices,
                    behavior.Name == "chooseWithoutRepeat"
                        ? EnemyActionRepeatPolicy.PreventImmediateRepeat
                        : EnemyActionRepeatPolicy.Allow);
                var transitions = choiceNames.Select(choice => choice.Name)
                    .ToDictionary(name => name, _ => transition, StringComparer.Ordinal);
                var startMove = choiceNames[0].Name;
                return new BehaviorPlan(startMove,
                    new Dictionary<string, string>(StringComparer.Ordinal), transitions, transition);
            }

            throw Error(behavior.Location, $"Unsupported enemy behavior '{behavior.Name}'.");
        }


        private (int Minimum, int Maximum) HpRange(DslValueNode node)
        {
            node = SelectVariant(node);
            var call = RequireCall(node);
            if (call.Name != "range") throw Error(call.Location, "Enemy HP must be range(minimum, maximum).");
            CheckShape(call, 2);
            var minimum = Integer(Argument(call, 0));
            var maximum = Integer(Argument(call, 1));
            if (minimum <= 0 || maximum < minimum) throw Error(call.Location, "Enemy HP range is invalid.");
            return (minimum, maximum);
        }

        private DslValueNode SelectVariant(DslValueNode node)
        {
            if (node is not DslCallNode { Name: "variants" } variants) return node;
            if (variants.Arguments.Any(argument => argument.Name is null))
                throw Error(variants.Location, "variants requires named alternatives.");
            if (variants.Arguments.Select(argument => argument.Name).Distinct(StringComparer.Ordinal).Count()
                != variants.Arguments.Count)
                throw Error(variants.Location, "Variant alternatives must be unique.");
            var choices = variants.Arguments.ToDictionary(argument => argument.Name!, argument => argument.Value,
                StringComparer.Ordinal);
            var allowed = new HashSet<string>(["default", "deadly", "tough", "deadlyTough"], StringComparer.Ordinal);
            if (choices.Keys.Any(key => !allowed.Contains(key)))
                throw Error(variants.Location, "Unknown difficulty variant.");
            var preferred = profile switch
            {
                { DeadlyEnemies: true, ToughEnemies: true } => new[] { "deadlyTough", "deadly", "tough", "default" },
                { DeadlyEnemies: true } => ["deadly", "default"],
                { ToughEnemies: true } => ["tough", "default"],
                _ => ["default"]
            };
            foreach (var key in preferred)
                if (choices.TryGetValue(key, out var selected)) return SelectVariant(selected);
            throw Error(variants.Location, "No difficulty variant matches the configured content profile.");
        }

        private static (IntentKind Intent, int IntentDamage) DescribeIntent(EffectNode effect)
        {
            if (effect is SequenceEffect sequence)
            {
                foreach (var child in sequence.Effects)
                {
                    var description = DescribeIntent(child);
                    if (description.Intent != IntentKind.None) return description;
                }
            }
            return effect switch
            {
                AttackEffect attack => (IntentKind.Attack, attack.Amount),
                RandomAttackEffect attack => (IntentKind.Attack, attack.Amount),
                ApplyPowerEffect => (IntentKind.Buff, 0),
                AddCardsToPileEffect => (IntentKind.Debuff, 0),
                _ => (IntentKind.None, 0)
            };
        }

        private Dictionary<string, DslValueNode> Properties(DslDeclarationNode declaration, string kind)
        {
            var properties = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var member in declaration.Members)
            {
                if (member is not DslPropertyNode property)
                    throw Error(member.Location, $"{kind} declarations can contain properties only.");
                if (!properties.TryAdd(property.Name, property.Value))
                    throw Error(property.Location, $"Duplicate {kind.ToLowerInvariant()} property '{property.Name}'.");
            }
            return properties;
        }

        private static void Only(IReadOnlyDictionary<string, DslValueNode> properties, params string[] allowed)
        {
            var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
            var unknown = properties.Keys.FirstOrDefault(key => !allowedSet.Contains(key));
            if (unknown is not null) throw Error(properties[unknown].Location, $"Unknown property '{unknown}'.");
        }

        private static DslValueNode Required(IReadOnlyDictionary<string, DslValueNode> properties,
            string name, DslSourceLocation location) => properties.TryGetValue(name, out var value)
            ? value
            : throw Error(location, $"Missing required property '{name}'.");

        private static bool OptionalBoolean(IReadOnlyDictionary<string, DslValueNode> properties, string name) =>
            !properties.TryGetValue(name, out var node) || node is DslBooleanNode { Value: false }
                ? false
                : node is DslBooleanNode { Value: true } ? true : throw Error(node.Location, $"'{name}' must be a boolean.");

        private static void CheckShape(DslCallNode call, int positionals)
        {
            if (call.Arguments.Count != positionals || call.Arguments.Any(argument => argument.Name is not null))
                throw Error(call.Location, $"'{call.Name}' requires exactly {positionals} positional arguments.");
        }

        private static DslCallNode RequireCall(DslValueNode node) => node is DslCallNode call
            ? call
            : throw Error(node.Location, "Expected a DSL call.");

        private static DslValueNode Argument(DslCallNode call, int index) => call.Arguments[index].Value;

        private static int Integer(DslValueNode node) => node is DslNumberNode number && number.TryGetInt32(out var value)
            ? value
            : throw Error(node.Location, "Expected a 32-bit integer.");

        private static string Name(DslValueNode node) => node is DslNameNode name
            ? name.Name
            : throw Error(node.Location, "Expected a name.");

        private static T EnumValue<T>(DslValueNode node) where T : struct, Enum
        {
            var name = Name(node);
            if (Enum.TryParse<T>(name, true, out var value)
                && Enum.GetNames<T>().Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)))
                return value;
            throw Error(node.Location, $"Unknown {typeof(T).Name} '{name}'.");
        }

        private ContentId Id(string name) => ContentId.Parse($"{_packageName}:{Slug(name)}");

        private static string Slug(string name)
        {
            var slug = new StringBuilder(name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                var current = name[i];
                if (char.IsUpper(current) && i > 0
                    && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                        || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    slug.Append('-');
                slug.Append(char.ToLowerInvariant(current));
            }
            return slug.ToString();
        }

        private static void EnsureUnique(IEnumerable<ContentId> ids, string kind)
        {
            if (ids.Distinct().Count() != ids.Count())
                throw new DslSourceException(default, $"{kind} IDs must be unique within a package.");
        }

        private static DslSourceException Error(DslSourceLocation location, string message) => new(location, message);
    }

    public static SpireCombatBehaviorContent Compile(DslPackageNode package, ContentVariantProfile profile = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        return new Compiler(package, profile).Compile();
    }
}
