using System.Text;

namespace CoreTheSpire;

/// <summary>Compiles combat DSL declarations and effects once, before the combat core is created.</summary>
public static class SpireCombatDslCompiler
{
    private sealed class Compiler(DslPackageNode package, ContentVariantProfile profile = default)
    {
        private readonly string _packageName = package.Name;
        private readonly ContentVariantProfile _profile = profile;

        public IReadOnlyList<CardDefinition> CompileCards()
        {
            var cards = package.Members.OfType<DslDeclarationNode>().ToArray();
            if (cards.Length != package.Members.Count)
                throw Error(package.Location, "A card package can contain card declarations only.");
            foreach (var card in cards)
                if (card.Kind != "card") throw Error(card.Location, $"Expected a card declaration, found '{card.Kind}'.");
            var compiled = cards.Select(CompileCard).ToArray();
            if (compiled.Select(card => card.Id).Distinct().Count() != compiled.Length)
                throw Error(package.Location, "Card IDs must be unique within a package.");
            return Array.AsReadOnly(compiled);
        }

        public IReadOnlyList<PotionDefinition> CompilePotions()
        {
            var potions = package.Members.OfType<DslDeclarationNode>().ToArray();
            if (potions.Length != package.Members.Count)
                throw Error(package.Location, "A potion package can contain potion declarations only.");
            foreach (var potion in potions)
                if (potion.Kind != "potion") throw Error(potion.Location, $"Expected a potion declaration, found '{potion.Kind}'.");
            var compiled = potions.Select(CompilePotion).ToArray();
            if (compiled.Select(potion => potion.Id).Distinct().Count() != compiled.Length)
                throw Error(package.Location, "Potion IDs must be unique within a package.");
            return Array.AsReadOnly(compiled);
        }

        private PotionDefinition CompilePotion(DslDeclarationNode declaration)
        {
            var properties = ReadProperties(declaration);
            RequireOnlyProperties(declaration, properties, "rarity", "usage", "target", "use",
                "inSharedPool", "characterPools", "canGenerateInCombat");
            var usage = Required(properties, "usage", declaration.Location);
            if (Name(usage) != "combatOnly")
                throw Error(usage.Location, "Only combatOnly potion usage is supported.");
            var characterPools = properties.TryGetValue("characterPools", out var poolsNode)
                ? ParseIds(poolsNode, "characterPools") : null;
            var potion = new PotionDefinition(Id(declaration.Name),
                EnumValue<PotionRarity>(Required(properties, "rarity", declaration.Location)),
                CompileEffect(Required(properties, "use", declaration.Location)),
                ParseTarget(Required(properties, "target", declaration.Location)),
                properties.TryGetValue("inSharedPool", out var sharedNode) ? Boolean(sharedNode) : true,
                characterPools,
                properties.TryGetValue("canGenerateInCombat", out var generationNode) ? Boolean(generationNode) : true);
            try { potion.Validate(); }
            catch (ArgumentException exception) { throw Error(declaration.Location, exception.Message); }
            return potion;
        }

        private CardDefinition CompileCard(DslDeclarationNode declaration)
        {
            var properties = ReadProperties(declaration);
            RequireOnlyProperties(declaration, properties,
                "type", "rarity", "cost", "costKind", "upgradedCost", "target", "keywords", "play", "upgradedPlay",
                "canGenerateInCombat", "tags", "pools");
            var type = EnumValue<CardType>(Required(properties, "type", declaration.Location));
            var rarity = EnumValue<CardRarity>(Required(properties, "rarity", declaration.Location));
            var cost = Integer(Required(properties, "cost", declaration.Location));
            if (cost < 0) throw Error(declaration.Location, "Card cost cannot be negative.");
            var costKind = properties.TryGetValue("costKind", out var costKindNode)
                ? EnumValue<CardCostKind>(costKindNode)
                : CardCostKind.Fixed;
            var upgradedCost = properties.TryGetValue("upgradedCost", out var upgradedCostNode)
                ? Integer(upgradedCostNode)
                : (int?)null;
            if (upgradedCost is < 0) throw Error(upgradedCostNode!.Location, "Upgraded cost cannot be negative.");
            if (costKind == CardCostKind.X && cost != 0)
                throw Error(declaration.Location, "X-cost cards must declare cost = 0.");

            var target = ParseTarget(Required(properties, "target", declaration.Location));
            var keywords = properties.TryGetValue("keywords", out var keywordsNode)
                ? ParseKeywords(keywordsNode)
                : CardKeywords.None;
            var play = CompileEffect(Required(properties, "play", declaration.Location));
            var upgradedPlay = properties.TryGetValue("upgradedPlay", out var upgradedPlayNode)
                ? CompileEffect(upgradedPlayNode)
                : null;
            var tags = properties.TryGetValue("tags", out var tagsNode)
                ? ParseIds(tagsNode, "tags") : Array.Empty<ContentId>();
            var pools = properties.TryGetValue("pools", out var poolsNode)
                ? ParseIds(poolsNode, "pools") : Array.Empty<ContentId>();
            return new CardDefinition(Id(declaration.Name), cost, type, play, keywords, target, rarity,
                upgradedPlay, upgradedCost, costKind,
                properties.TryGetValue("canGenerateInCombat", out var canGenerateNode)
                    ? Boolean(canGenerateNode) : true) { Tags = tags, PoolIds = pools };
        }

        private EffectNode CompileEffect(DslValueNode node)
        {
            node = ResolveVariants(node);
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "sequence":
                    CheckShape(call, 0, int.MaxValue);
                    return new SequenceEffect(Positional(call).Select(argument => CompileEffect(argument.Value)).ToArray());
                case "if":
                    CheckShape(call, 2, 2, "else");
                    return new IfEffect(CompilePredicate(Argument(call, 0)), CompileEffect(Argument(call, 1)),
                        OptionalEffect(call, "else"));
                case "attack":
                    CheckShape(call, 1, 1, "target", "properties");
                    return new AttackEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget),
                        OptionalArgument(call, "properties") is { } damageProperties
                            ? ParseDamageProperties(damageProperties) : DamageProperties.Move);
                case "scaledAttack":
                    CheckShape(call, 2, 2, "count", "target");
                    return new ScaledAttackEffect(Integer(Argument(call, 0)), Integer(Argument(call, 1)),
                        CompileCombatCount(RequiredArgument(call, "count")),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget));
                case "attackUsingBlock":
                    CheckShape(call, 0, 0, "target");
                    return new AttackUsingBlockEffect(OptionalTarget(call, "target", TargetSelector.SelectedTarget));
                case "randomAttack":
                    CheckShape(call, 1, 1);
                    return new RandomAttackEffect(Integer(Argument(call, 0)));
                case "applyPower":
                    CheckShape(call, 2, 2, "target");
                    return new ApplyPowerEffect(ReferenceId(Argument(call, 0)), Integer(Argument(call, 1)),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget));
                case "applyScaledPower":
                    CheckShape(call, 3, 3, "count", "target");
                    return new ApplyScaledPowerEffect(Id(Name(Argument(call, 0))),
                        Integer(Argument(call, 1)), Integer(Argument(call, 2)),
                        CompileCombatCount(RequiredArgument(call, "count")),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "gainBlock":
                    CheckShape(call, 1, 1, "perPowerStack");
                    var perPower = OptionalArgument(call, "perPowerStack");
                    return new GainBlockEffect(Integer(Argument(call, 0)),
                        perPower is null ? null : Id(Name(perPower)));
                case "gainBlockFromCurrentBlock":
                    CheckShape(call, 0, 0, "multiplier");
                    return new GainBlockFromCurrentBlockEffect(OptionalInteger(call, "multiplier", 1));
                case "loseHp":
                    CheckShape(call, 1, 1, "target");
                    return new LoseHpEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "heal":
                    CheckShape(call, 1, 1, "target");
                    return new HealEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "draw":
                    CheckShape(call, 1, 1);
                    return new DrawCardsEffect(Integer(Argument(call, 0)));
                case "modifyEnergy":
                    CheckShape(call, 1, 1);
                    var energyAmount = Argument(call, 0);
                    return energyAmount is DslNumberNode ? new ModifyEnergyEffect(Integer(energyAmount))
                        : new ModifyEnergyEffect(0, CompileScaledAmount(energyAmount));
                case "repeatForCount":
                    CheckShape(call, 2, 2, "count", "effect");
                    return new RepeatForCountEffect(Integer(Argument(call, 0)), Integer(Argument(call, 1)),
                        CompileCombatCount(RequiredArgument(call, "count")),
                        CompileEffect(RequiredArgument(call, "effect")));
                case "addCards":
                    CheckShape(call, 1, 1, "pile", "count", "target", "inheritUpgradeFromPlayedCard");
                    return new AddCardsToPileEffect(Id(Name(Argument(call, 0))),
                        EnumValue<CardPile>(RequiredArgument(call, "pile")), OptionalInteger(call, "count", 1),
                        OptionalTarget(call, "target", TargetSelector.Self),
                        OptionalBoolean(call, "inheritUpgradeFromPlayedCard", false));
                case "chooseCard":
                    CheckShape(call, 0, 0, "from", "to", "prompt", "destinationPosition", "filter", "onSelected");
                    return new ChooseCardFromPileEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        OptionalPile(call, "to"), StringValue(RequiredArgument(call, "prompt")),
                        OptionalEnum(call, "destinationPosition", CardPilePosition.Bottom),
                        OptionalEnum(call, "filter", CardSelectionFilter.Any), OptionalEffect(call, "onSelected"));
                case "chooseCards":
                    CheckShape(call, 0, 0, "from", "prompt", "minChoices", "maxChoices", "filter", "onSelected");
                    return new ChooseCardsFromPileEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        StringValue(RequiredArgument(call, "prompt")),
                        Integer(RequiredArgument(call, "minChoices")),
                        Integer(RequiredArgument(call, "maxChoices")),
                        OptionalEnum(call, "filter", CardSelectionFilter.Any), OptionalEffect(call, "onSelected"));
                case "randomMoveCard":
                    CheckShape(call, 0, 0, "from", "to", "filter");
                    return new RandomlyMoveCardFromPileEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        EnumValue<CardPile>(RequiredArgument(call, "to")),
                        OptionalEnum(call, "filter", CardSelectionFilter.Any));
                case "forEachCard":
                    CheckShape(call, 0, 0, "from", "filter", "effect");
                    return new ForEachCardInPileEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        OptionalEnum(call, "filter", CardSelectionFilter.Any),
                        CompileEffect(RequiredArgument(call, "effect")));
                case "moveSelectedCard":
                    CheckShape(call, 0, 0, "from", "to", "destinationPosition");
                    return new MoveSelectedCardToPileEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        EnumValue<CardPile>(RequiredArgument(call, "to")),
                        OptionalEnum(call, "destinationPosition", CardPilePosition.Bottom));
                case "upgradeSelectedCard":
                    CheckShape(call, 0, 0);
                    return new UpgradeSelectedCardEffect();
                case "upgradeCards":
                    CheckShape(call, 0, 0, "pile");
                    return new UpgradeCardsInPileEffect(EnumValue<CardPile>(RequiredArgument(call, "pile")));
                case "repeat":
                    CheckShape(call, 2, 2);
                    return new RepeatEffect(Integer(Argument(call, 0)), CompileEffect(Argument(call, 1)));
                case "repeatForX":
                    CheckShape(call, 1, 1);
                    return new RepeatForXCostEffect(CompileEffect(Argument(call, 0)));
                case "retainSelectedCard":
                    CheckShape(call, 0, 0);
                    return new RetainSelectedCardEffect();
                default:
                    throw Error(call.Location, $"Unknown combat effect '{call.Name}'.");
            }
        }

        private EffectPredicate CompilePredicate(DslValueNode node)
        {
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "targetAlive":
                    CheckShape(call, 0, 0);
                    return new TargetAlivePredicate();
                case "hasPower":
                    CheckShape(call, 1, 1, "target");
                    return new HasPowerPredicate(Id(Name(Argument(call, 0))),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "energyAtLeast":
                    CheckShape(call, 1, 1);
                    return new EnergyAtLeastPredicate(Integer(Argument(call, 0)));
                case "cardExhaustedThisTurn":
                    CheckShape(call, 0, 0);
                    return new CardExhaustedThisTurnPredicate();
                case "playerLostHpThisTurn":
                    CheckShape(call, 0, 0);
                    return new PlayerLostHpThisTurnPredicate();
                case "pileCountAtLeast":
                    CheckShape(call, 2, 2);
                    return new PileCountAtLeastPredicate(EnumValue<CardPile>(Argument(call, 0)),
                        Integer(Argument(call, 1)));
                default:
                    throw Error(call.Location, $"Unknown combat predicate '{call.Name}'.");
            }
        }

        private ScaledCombatAmount CompileScaledAmount(DslValueNode node)
        {
            var call = RequireCall(node);
            if (call.Name != "scaled") throw Error(node.Location, "Calculated amounts require scaled(base, perCount, count=...).");
            CheckShape(call, 2, 2, "count");
            return new ScaledCombatAmount(Integer(Argument(call, 0)), Integer(Argument(call, 1)),
                CompileCombatCount(RequiredArgument(call, "count")));
        }

        private CombatCountSource CompileCombatCount(DslValueNode node)
        {
            var call = RequireCall(node);
            switch (call.Name)
            {
                case "pileCount":
                    CheckShape(call, 1, 1, "type");
                    return new PileCardCountSource(EnumValue<CardPile>(Argument(call, 0)),
                        OptionalArgument(call, "type") is { } type ? EnumValue<CardType>(type) : null);
                case "playerHpLossCount":
                    CheckShape(call, 0, 0);
                    return new PlayerHpLossCountSource();
                case "targetPowerAmount":
                    CheckShape(call, 1, 1);
                    return new TargetPowerCountSource(Id(Name(Argument(call, 0))));
                case "cardsWithTag":
                    CheckShape(call, 1, 1);
                    return new TaggedCardCountSource(ReferenceId(Argument(call, 0)));
                default:
                    throw Error(call.Location, $"Unknown attack count source '{call.Name}'.");
            }
        }

        private DslValueNode ResolveVariants(DslValueNode node)
        {
            if (node is DslCallNode { Name: "variants" } variants)
            {
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
                var preferred = _profile switch
                {
                    { DeadlyEnemies: true, ToughEnemies: true } => new[] { "deadlyTough", "deadly", "tough", "default" },
                    { DeadlyEnemies: true } => ["deadly", "default"],
                    { ToughEnemies: true } => ["tough", "default"],
                    _ => ["default"]
                };
                foreach (var key in preferred)
                    if (choices.TryGetValue(key, out var selected)) return ResolveVariants(selected);
                throw Error(variants.Location, "No difficulty variant matches the configured content profile.");
            }
            if (node is DslCallNode call)
                return call with { Arguments = call.Arguments.Select(argument =>
                    argument with { Value = ResolveVariants(argument.Value) }).ToArray() };
            if (node is DslListNode list)
                return list with { Items = list.Items.Select(ResolveVariants).ToArray() };
            return node;
        }

        public EffectNode CompileStandaloneEffect(DslValueNode node) => CompileEffect(node);

        private CardKeywords ParseKeywords(DslValueNode node)
        {
            if (node is not DslListNode list) throw Error(node.Location, "Card keywords must be a list.");
            var keywords = CardKeywords.None;
            foreach (var item in list.Items)
            {
                var keyword = Name(item) switch
                {
                    "exhaust" => CardKeywords.Exhaust,
                    "ethereal" => CardKeywords.Ethereal,
                    "unplayable" => CardKeywords.Unplayable,
                    "retain" => CardKeywords.Retain,
                    _ => throw Error(item.Location, "Unknown card keyword.")
                };
                if ((keywords & keyword) != 0) throw Error(item.Location, $"Duplicate card keyword '{keyword}'.");
                keywords |= keyword;
            }
            return keywords;
        }

        private IReadOnlyList<ContentId> ParseIds(DslValueNode node, string property)
        {
            if (node is not DslListNode list) throw Error(node.Location, $"{property} must be a list.");
            var ids = list.Items.Select(ReferenceId).ToArray();
            if (ids.Distinct().Count() != ids.Length)
                throw Error(node.Location, $"{property} must be unique.");
            return Array.AsReadOnly(ids);
        }

        private static DamageProperties ParseDamageProperties(DslValueNode node)
        {
            if (node is not DslListNode list) throw Error(node.Location, "Damage properties must be a list.");
            var properties = DamageProperties.None;
            foreach (var item in list.Items)
            {
                var property = EnumValue<DamageProperties>(item);
                if (property == DamageProperties.None || (properties & property) != 0)
                    throw Error(item.Location, "Damage properties must be unique nonzero flags.");
                properties |= property;
            }
            return properties;
        }

        private ContentId ReferenceId(DslValueNode node) => node switch
        {
            DslNameNode name => Id(name.Name),
            DslStringNode text => ContentId.Parse(text.Value),
            _ => throw Error(node.Location, "Content reference must be a local name or qualified ID string.")
        };

        private static TargetSelector ParseTarget(DslValueNode node) => Name(node) switch
        {
            "self" => TargetSelector.Self,
            "enemy" => TargetSelector.SelectedTarget,
            "firstEnemy" => TargetSelector.FirstEnemy,
            "allEnemies" => TargetSelector.AllOpponents,
            "randomEnemy" => TargetSelector.RandomEnemy,
            _ => throw Error(node.Location, "Content target must be self, enemy, firstEnemy, allEnemies, or randomEnemy.")
        };

        private static TargetSelector ParseEffectTarget(DslValueNode node) => Name(node) switch
        {
            "self" => TargetSelector.Self,
            "player" => TargetSelector.FirstEnemy,
            "target" => TargetSelector.SelectedTarget,
            "firstEnemy" => TargetSelector.FirstEnemy,
            "allEnemies" => TargetSelector.AllOpponents,
            "randomEnemy" => TargetSelector.RandomEnemy,
            _ => throw Error(node.Location, "Effect target must be self, target, firstEnemy, allEnemies, or randomEnemy.")
        };

        private TargetSelector OptionalTarget(DslCallNode call, string name, TargetSelector fallback) =>
            OptionalArgument(call, name) is { } node ? ParseEffectTarget(node) : fallback;

        private static CardPile? OptionalPile(DslCallNode call, string name) =>
            OptionalArgument(call, name) is not { } node || Name(node) == "none"
                ? null
                : EnumValue<CardPile>(node);

        private EffectNode? OptionalEffect(DslCallNode call, string name) =>
            OptionalArgument(call, name) is { } node ? CompileEffect(node) : null;

        private int OptionalInteger(DslCallNode call, string name, int fallback) =>
            OptionalArgument(call, name) is { } node ? Integer(node) : fallback;

        private static bool OptionalBoolean(DslCallNode call, string name, bool fallback) =>
            OptionalArgument(call, name) is { } node ? Boolean(node) : fallback;

        private static T OptionalEnum<T>(DslCallNode call, string name, T fallback) where T : struct, Enum =>
            OptionalArgument(call, name) is { } node ? EnumValue<T>(node) : fallback;

        private static IEnumerable<DslCallArgumentNode> Positional(DslCallNode call) =>
            call.Arguments.Where(argument => argument.Name is null);

        private static DslValueNode Argument(DslCallNode call, int index) => Positional(call).Skip(index)
            .Select(argument => argument.Value).FirstOrDefault()
            ?? throw Error(call.Location, $"'{call.Name}' is missing positional argument {index + 1}.");

        private static DslValueNode RequiredArgument(DslCallNode call, string name) =>
            OptionalArgument(call, name) ?? throw Error(call.Location, $"'{call.Name}' requires '{name}'.");

        private static DslValueNode? OptionalArgument(DslCallNode call, string name)
        {
            var matches = call.Arguments.Where(argument => argument.Name == name).ToArray();
            return matches.Length switch
            {
                0 => null,
                1 => matches[0].Value,
                _ => throw Error(call.Location, $"'{call.Name}' repeats argument '{name}'.")
            };
        }

        private static void CheckShape(DslCallNode call, int minimumPositionals, int maximumPositionals,
            params string[] allowedNamed)
        {
            var positionalCount = Positional(call).Count();
            if (positionalCount < minimumPositionals || positionalCount > maximumPositionals)
                throw Error(call.Location, $"'{call.Name}' has an invalid number of positional arguments.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in call.Arguments.Where(argument => argument.Name is not null))
            {
                if (!allowedNamed.Contains(argument.Name!, StringComparer.Ordinal))
                    throw Error(argument.Location, $"Unknown '{call.Name}' argument '{argument.Name}'.");
                if (!seen.Add(argument.Name!)) throw Error(argument.Location, $"Duplicate argument '{argument.Name}'.");
            }
        }

        private static Dictionary<string, DslValueNode> ReadProperties(DslDeclarationNode declaration)
        {
            var properties = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var member in declaration.Members)
            {
                if (member is not DslPropertyNode property)
                    throw Error(member.Location, $"{declaration.Kind} declarations can contain properties only.");
                if (!properties.TryAdd(property.Name, property.Value))
                    throw Error(property.Location, $"Duplicate {declaration.Kind} property '{property.Name}'.");
            }
            return properties;
        }

        private static void RequireOnlyProperties(DslDeclarationNode declaration,
            IReadOnlyDictionary<string, DslValueNode> properties, params string[] allowed)
        {
            var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
            foreach (var property in declaration.Members.OfType<DslPropertyNode>())
                if (!allowedSet.Contains(property.Name))
                    throw Error(property.Location, $"Unknown {declaration.Kind} property '{property.Name}'.");
        }

        private static DslValueNode Required(IReadOnlyDictionary<string, DslValueNode> properties,
            string name, DslSourceLocation location) => properties.TryGetValue(name, out var value)
            ? value
            : throw Error(location, $"Missing required property '{name}'.");

        private static DslCallNode RequireCall(DslValueNode node) => node is DslCallNode call
            ? call
            : throw Error(node.Location, "Expected an effect call.");

        private static int Integer(DslValueNode node) => node is DslNumberNode number && number.TryGetInt32(out var value)
            ? value
            : throw Error(node.Location, "Expected a 32-bit integer.");

        private static string StringValue(DslValueNode node) => node is DslStringNode text
            ? text.Value
            : throw Error(node.Location, "Expected a string.");

        private static bool Boolean(DslValueNode node) => node is DslBooleanNode boolean
            ? boolean.Value
            : throw Error(node.Location, "Expected a boolean.");

        private static string Name(DslValueNode node) => node is DslNameNode name
            ? name.Name
            : throw Error(node.Location, "Expected a name.");

        private static T EnumValue<T>(DslValueNode node) where T : struct, Enum
        {
            var name = Name(node);
            if (Enum.TryParse<T>(name, ignoreCase: true, out var value)
                && Enum.GetNames<T>().Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)))
                return value;
            throw Error(node.Location, $"Unknown {typeof(T).Name} value '{name}'.");
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

        private static DslSourceException Error(DslSourceLocation location, string message) => new(location, message);
    }

    public static IReadOnlyList<CardDefinition> CompileCards(DslPackageNode package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return new Compiler(package).CompileCards();
    }

    public static IReadOnlyList<PotionDefinition> CompilePotions(DslPackageNode package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return new Compiler(package).CompilePotions();
    }

    public static EffectNode CompileEffect(DslPackageNode package, DslValueNode expression,
        ContentVariantProfile profile = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(expression);
        return new Compiler(package, profile).CompileStandaloneEffect(expression);
    }
}
