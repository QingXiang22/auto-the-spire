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
            var declarations = package.Members.OfType<DslDeclarationNode>().ToArray();
            if (declarations.Length != package.Members.Count)
                throw Error(package.Location, "A card package can contain card declarations only.");
            foreach (var declaration in declarations)
                if (declaration.Kind is not ("card" or "cardPool"))
                    throw Error(declaration.Location, $"Expected a card or cardPool declaration, found '{declaration.Kind}'.");
            var cards = declarations.Where(declaration => declaration.Kind == "card").ToArray();
            var compiled = cards.Select(CompileCard).ToArray();
            if (compiled.Select(card => card.Id).Distinct().Count() != compiled.Length)
                throw Error(package.Location, "Card IDs must be unique within a package.");
            return Array.AsReadOnly(compiled);
        }

        public IReadOnlyList<CardPoolDefinition> CompileCardPools()
        {
            var declarations = package.Members.OfType<DslDeclarationNode>().ToArray();
            if (declarations.Length != package.Members.Count)
                throw Error(package.Location, "A card pool package can contain declarations only.");
            foreach (var declaration in declarations)
                if (declaration.Kind is not ("card" or "cardPool"))
                    throw Error(declaration.Location, $"Expected a card or cardPool declaration, found '{declaration.Kind}'.");
            var pools = declarations.Where(declaration => declaration.Kind == "cardPool").Select(declaration =>
            {
                var properties = ReadProperties(declaration);
                RequireOnlyProperties(declaration, properties, "cards");
                return new CardPoolDefinition(Id(declaration.Name),
                    ParseIds(Required(properties, "cards", declaration.Location), "cards"));
            }).ToArray();
            if (pools.Select(pool => pool.Id).Distinct().Count() != pools.Length)
                throw Error(package.Location, "Card pool IDs must be unique within a package.");
            return Array.AsReadOnly(pools);
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
            var properties = ReadProperties(declaration, lastWins: true);
            if (properties.TryGetValue("normal", out var normalNode))
                return CompileVersionedCard(declaration, properties, normalNode);
            RequireOnlyProperties(declaration, properties,
                "type", "rarity", "cost", "costKind", "upgradedCost", "target", "keywords", "upgradedKeywords", "play", "upgradedPlay",
                "canGenerateInCombat", "tags", "pools", "onExhaust", "upgradedOnExhaust",
                "onEnterCombat", "upgradedOnEnterCombat", "beforeCardPlayed", "upgradedBeforeCardPlayed",
                "afterCardPlayed", "upgradedAfterCardPlayed", "afterAutoPostPlay",
                "upgradedAfterAutoPostPlay", "turnEndInHand", "variables", "damageVariable", "damageValue",
                "upgradedDamageValue", "removedUpgradedKeywords", "multiplayerOnly");
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
            if (properties.ContainsKey("damageVariable") && (properties.ContainsKey("damageValue")
                || properties.ContainsKey("upgradedDamageValue")))
                throw Error(declaration.Location, "A card cannot define damageVariable together with damageValue.");
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
                    ? Boolean(canGenerateNode) : true)
            {
                Tags = tags, PoolIds = pools,
                DamageValueVariable = properties.TryGetValue("damageVariable", out var damageVariableNode)
                    ? ReferenceId(damageVariableNode) : null,
                DamageValue = properties.TryGetValue("damageValue", out var damageValueNode)
                    && damageValueNode is DslNumberNode ? DecimalValue(damageValueNode) : null,
                UpgradedDamageValue = properties.TryGetValue("upgradedDamageValue", out var upgradedDamageValueNode)
                    && upgradedDamageValueNode is DslNumberNode ? DecimalValue(upgradedDamageValueNode) : null,
                CalculatedDamageValue = properties.TryGetValue("damageValue", out damageValueNode)
                    && damageValueNode is not DslNumberNode ? CompileScaledAmount(damageValueNode) : null,
                UpgradedCalculatedDamageValue = properties.TryGetValue("upgradedDamageValue", out upgradedDamageValueNode)
                    && upgradedDamageValueNode is not DslNumberNode ? CompileScaledAmount(upgradedDamageValueNode) : null,
                UpgradedKeywords = properties.TryGetValue("upgradedKeywords", out var upgradedKeywordsNode)
                    ? ParseKeywords(upgradedKeywordsNode) : CardKeywords.None,
                RemovedUpgradedKeywords = properties.TryGetValue("removedUpgradedKeywords", out var removedUpgradedKeywordsNode)
                    ? ParseKeywords(removedUpgradedKeywordsNode) : CardKeywords.None,
                MultiplayerOnly = properties.TryGetValue("multiplayerOnly", out var multiplayerOnlyNode)
                    && Boolean(multiplayerOnlyNode),
                Variables = properties.TryGetValue("variables", out var variablesNode)
                    ? CompileCardVariables(variablesNode) : Array.Empty<CardVariableDefinition>(),
                OnExhaust = properties.TryGetValue("onExhaust", out var exhaustNode) ? CompileEffect(exhaustNode) : null,
                UpgradedOnExhaust = properties.TryGetValue("upgradedOnExhaust", out var upgradedExhaustNode)
                    ? CompileEffect(upgradedExhaustNode) : null,
                TurnEndInHand = properties.TryGetValue("turnEndInHand", out var turnEndInHandNode)
                    ? CompileEffect(turnEndInHandNode) : null,
                OnEnterCombat = properties.TryGetValue("onEnterCombat", out var entryNode) ? CompileEffect(entryNode) : null,
                UpgradedOnEnterCombat = properties.TryGetValue("upgradedOnEnterCombat", out var upgradedEntryNode)
                    ? CompileEffect(upgradedEntryNode) : null,
                BeforeCardPlayed = CompilePlayListener(properties, "beforeCardPlayed"),
                UpgradedBeforeCardPlayed = CompilePlayListener(properties, "upgradedBeforeCardPlayed"),
                AfterCardPlayed = CompilePlayListener(properties, "afterCardPlayed"),
                UpgradedAfterCardPlayed = CompilePlayListener(properties, "upgradedAfterCardPlayed"),
                AfterAutoPostPlay = properties.TryGetValue("afterAutoPostPlay", out var postPlayNode)
                    ? CompileEffect(postPlayNode) : null,
                UpgradedAfterAutoPostPlay = properties.TryGetValue("upgradedAfterAutoPostPlay", out var upgradedPostPlayNode)
                    ? CompileEffect(upgradedPostPlayNode) : null
            };
        }

        private CardDefinition CompileVersionedCard(DslDeclarationNode declaration,
            IReadOnlyDictionary<string, DslValueNode> cardProperties, DslValueNode normalNode)
        {
            RequireOnlyProperties(declaration, cardProperties,
                "pools", "tags", "canGenerateInCombat", "multiplayerOnly", "variables", "damageVariable",
                "damageValue", "upgradedDamageValue", "upgradeable", "normal", "upgraded");
            if (normalNode is not DslBlockNode normalBlock)
                throw Error(normalNode.Location, "Card normal version must be a block.");
            if (!cardProperties.TryGetValue("upgraded", out var upgradedNode)
                || upgradedNode is not DslBlockNode upgradedBlock)
                throw Error(declaration.Location, "Versioned cards require a complete upgraded block.");

            var normal = ReadLastWinsProperties(normalBlock);
            var upgraded = ReadLastWinsProperties(upgradedBlock);
            RequireOnlyProperties(normalBlock, normal, "type", "rarity", "cost", "target", "keywords", "play", "hooks");
            RequireOnlyProperties(upgradedBlock, upgraded, "type", "rarity", "cost", "target", "keywords", "play", "hooks");

            foreach (var required in new[] { "type", "rarity", "cost", "target", "keywords", "play", "hooks" })
            {
                if (!normal.ContainsKey(required)) throw Error(normalBlock.Location, $"Normal version requires '{required}'.");
                if (!upgraded.ContainsKey(required)) throw Error(upgradedBlock.Location, $"Upgraded version requires '{required}'.");
            }

            EnsureVersionIdentity(normal, upgraded, "type");
            EnsureVersionIdentity(normal, upgraded, "rarity");
            var normalCost = ParseVersionCost(normal["cost"]);
            var upgradedCost = ParseVersionCost(upgraded["cost"]);
            if (normalCost.IsX != upgradedCost.IsX)
                throw Error(upgraded["cost"].Location, "Upgraded card cost kind must match the normal version.");
            var type = EnumValue<CardType>(normal["type"]);
            var rarity = EnumValue<CardRarity>(normal["rarity"]);
            var normalTarget = ParseVersionTarget(normal["target"]);
            var upgradedTarget = ParseVersionTarget(upgraded["target"]);
            if (normalTarget != upgradedTarget)
                throw Error(upgraded["target"].Location, "Upgraded card target must match the normal target.");

            var normalKeywords = ParseBraceNames(normal["keywords"], "keywords").Aggregate(CardKeywords.None,
                (current, item) => current | ParseKeyword(item));
            var upgradedKeywords = ParseBraceNames(upgraded["keywords"], "keywords").Aggregate(CardKeywords.None,
                (current, item) => current | ParseKeyword(item));
            var normalHooks = ReadLastWinsProperties(RequireBlock(normal["hooks"], "hooks"));
            var upgradedHooks = ReadLastWinsProperties(RequireBlock(upgraded["hooks"], "hooks"));
            RequireOnlyProperties(RequireBlock(normal["hooks"], "hooks"), normalHooks,
                "onExhaust", "turnEndInHand", "onEnterCombat", "beforeCardPlayed", "afterCardPlayed",
                "afterAutoPostPlay");
            RequireOnlyProperties(RequireBlock(upgraded["hooks"], "hooks"), upgradedHooks,
                "onExhaust", "turnEndInHand", "onEnterCombat", "beforeCardPlayed", "afterCardPlayed",
                "afterAutoPostPlay");

            var normalEffect = CompileVersionEffect(normal["play"]);
            var upgradedEffect = CompileVersionEffect(upgraded["play"]);
            var normalOnExhaust = OptionalVersionEffect(normalHooks, "onExhaust");
            var upgradedOnExhaust = OptionalVersionEffect(upgradedHooks, "onExhaust");
            var normalOnEnter = OptionalVersionEffect(normalHooks, "onEnterCombat");
            var upgradedOnEnter = OptionalVersionEffect(upgradedHooks, "onEnterCombat");
            var normalBefore = OptionalVersionListener(normalHooks, "beforeCardPlayed");
            var upgradedBefore = OptionalVersionListener(upgradedHooks, "beforeCardPlayed");
            var normalAfter = OptionalVersionListener(normalHooks, "afterCardPlayed");
            var upgradedAfter = OptionalVersionListener(upgradedHooks, "afterCardPlayed");
            var normalTurnEnd = OptionalVersionEffect(normalHooks, "turnEndInHand");
            var upgradedTurnEnd = OptionalVersionEffect(upgradedHooks, "turnEndInHand");
            var upgradeable = !cardProperties.TryGetValue("upgradeable", out var upgradeableNode)
                || Boolean(upgradeableNode);
            var card = new CardDefinition(Id(declaration.Name), normalCost.Value, type, normalEffect,
                normalKeywords, normalTarget, rarity, upgradeable ? upgradedEffect : null,
                upgradeable && !upgradedCost.IsX ? upgradedCost.Value : null,
                normalCost.IsX ? CardCostKind.X : CardCostKind.Fixed,
                cardProperties.TryGetValue("canGenerateInCombat", out var generationNode)
                    ? Boolean(generationNode) : true)
            {
                PoolIds = cardProperties.TryGetValue("pools", out var poolsNode)
                    ? ParseBraceIds(poolsNode, "pools") : Array.Empty<ContentId>(),
                Tags = cardProperties.TryGetValue("tags", out var tagsNode)
                    ? ParseBraceIds(tagsNode, "tags") : Array.Empty<ContentId>(),
                UpgradedKeywords = upgradeable ? upgradedKeywords & ~normalKeywords : CardKeywords.None,
                RemovedUpgradedKeywords = upgradeable ? normalKeywords & ~upgradedKeywords : CardKeywords.None,
                MultiplayerOnly = cardProperties.TryGetValue("multiplayerOnly", out var multiplayerNode)
                    && Boolean(multiplayerNode),
                OnExhaust = normalOnExhaust,
                UpgradedOnExhaust = upgradeable ? upgradedOnExhaust : null,
                TurnEndInHand = normalTurnEnd,
                UpgradedTurnEndInHand = upgradeable ? upgradedTurnEnd : null,
                OnEnterCombat = normalOnEnter,
                UpgradedOnEnterCombat = upgradeable ? upgradedOnEnter : null,
                BeforeCardPlayed = normalBefore,
                UpgradedBeforeCardPlayed = upgradeable ? upgradedBefore : null,
                AfterCardPlayed = normalAfter,
                UpgradedAfterCardPlayed = upgradeable ? upgradedAfter : null,
                AfterAutoPostPlay = normalHooks.TryGetValue("afterAutoPostPlay", out var normalPostPlay)
                    ? CompileVersionEffect(normalPostPlay) : null,
                UpgradedAfterAutoPostPlay = upgradeable && upgradedHooks.TryGetValue("afterAutoPostPlay", out var upgradedPostPlay)
                    ? CompileVersionEffect(upgradedPostPlay) : null,
                Variables = cardProperties.TryGetValue("variables", out var variablesNode)
                    ? CompileCardVariables(LowerNamedCollection(variablesNode, "variable")) : Array.Empty<CardVariableDefinition>(),
                DamageValueVariable = cardProperties.TryGetValue("damageVariable", out var damageVariableNode)
                    ? ReferenceId(damageVariableNode) : null,
                DamageValue = cardProperties.TryGetValue("damageValue", out var damageValueNode)
                    && damageValueNode is DslNumberNode ? DecimalValue(damageValueNode) : null,
                UpgradedDamageValue = cardProperties.TryGetValue("upgradedDamageValue", out var upgradedDamageNode)
                    && upgradedDamageNode is DslNumberNode ? DecimalValue(upgradedDamageNode) : null,
                CalculatedDamageValue = cardProperties.TryGetValue("damageValue", out damageValueNode)
                    && damageValueNode is not DslNumberNode ? CompileScaledAmount(LowerStructuredValue(damageValueNode)) : null,
                UpgradedCalculatedDamageValue = cardProperties.TryGetValue("upgradedDamageValue", out upgradedDamageNode)
                    && upgradedDamageNode is not DslNumberNode ? CompileScaledAmount(LowerStructuredValue(upgradedDamageNode)) : null
            };
            return card;
        }

        private void EnsureVersionIdentity(IReadOnlyDictionary<string, DslValueNode> normal,
            IReadOnlyDictionary<string, DslValueNode> upgraded, string field)
        {
            if (Name(normal[field]) != Name(upgraded[field]))
                throw Error(upgraded[field].Location, $"Upgraded card {field} must match the normal version.");
        }

        private static (bool IsX, int Value) ParseVersionCost(DslValueNode node)
        {
            if (node is DslNameNode { Name: "X" }) return (true, 0);
            var amount = Integer(node);
            if (amount < 0) throw Error(node.Location, "Card cost cannot be negative.");
            return (false, amount);
        }

        private TargetSelector ParseVersionTarget(DslValueNode node)
        {
            if (node is not DslBlockNode block)
                throw Error(node.Location, "Card target must be a result block.");
            var entries = block.Members.OfType<DslPropertyNode>().ToArray();
            if (entries.Length != 1)
                throw Error(node.Location, "Target blocks currently require one selection expression.");
            if (entries[0].Name == "selectSelf") return TargetSelector.Self;
            if (entries[0].Name == "selectNone") return TargetSelector.None;
            if (entries[0].Name == "selectAll") return TargetSelector.AllOpponents;
            if (entries[0].Name == "selectFirst") return TargetSelector.FirstEnemy;
            if (entries[0].Name == "selectRandom") return TargetSelector.RandomEnemy;
            if (entries[0].Name != "selectOne" || entries[0].Value is not DslBlockNode query)
                throw Error(node.Location, "Unknown target selection expression.");
            var queryProperties = ReadLastWinsProperties(query);
            if (!queryProperties.TryGetValue("from", out var source)
                || Name(source) is not ("context.opponents" or "context.allies")
                || !queryProperties.TryGetValue("where", out var filter) || Name(filter) != "alive")
                throw Error(node.Location, "selectOne currently accepts living context.opponents or context.allies.");
            return TargetSelector.SelectedTarget;
        }

        private EffectNode CompileVersionEffect(DslValueNode node)
        {
            var block = RequireBlock(node, "effect");
            var effects = new List<EffectNode>();
            foreach (var member in block.Members)
            {
                if (member is not DslPropertyNode property)
                    throw Error(member.Location, "Effect blocks contain named effect statements.");
                effects.Add(CompileEffect(LowerEffectStatement(property)));
            }
            return effects.Count == 1 ? effects[0] : new SequenceEffect(effects);
        }

        private DslValueNode LowerEffectStatement(DslPropertyNode property)
        {
            if (property.Value is not DslBlockNode block)
                throw Error(property.Location, $"Effect '{property.Name}' requires a named block.");
            return LowerNamedCall(property.Name, block, property.Location, effectStatement: true);
        }

        private DslValueNode LowerStructuredValue(DslValueNode node)
        {
            if (node is DslNameNode { Name: "context.target" } targetContext)
                return new DslNameNode("target", targetContext.Location);
            if (node is DslNameNode { Name: "context.owner" } ownerContext)
                return new DslNameNode("self", ownerContext.Location);
            if (node is DslNameNode { Name: "context.player" } playerContext)
                return new DslNameNode("player", playerContext.Location);
            if (node is not DslBlockNode block) return node;
            if (block.Members.Count == 1 && block.Members[0] is DslPropertyNode property
                && property.Value is DslBlockNode expressionBlock)
                return LowerNamedCall(property.Name, expressionBlock, property.Location, effectStatement: false);
            if (block.Members.Count > 0 && block.Members.All(member => member is DslPropertyNode
                    { Value: DslBlockNode }))
                return LowerNamedCall("sequence", block, block.Location, effectStatement: true);
            if (block.Members.All(member => member is DslPropertyNode
                    { Value: DslBooleanNode { Value: true } }))
                return new DslListNode(Array.AsReadOnly(block.Members.OfType<DslPropertyNode>()
                    .Select(property => (DslValueNode)new DslNameNode(property.Name, property.Location)).ToArray()),
                    block.Location);
            return new DslListNode(Array.AsReadOnly(block.Members.Select(member => member switch
            {
                DslPropertyNode property => LowerStructuredValue(property.Value),
                _ => throw Error(member.Location, "Expected a value in this collection.")
            }).ToArray()), block.Location);
        }

        private DslValueNode LowerNamedCall(string name, DslBlockNode block, DslSourceLocation location,
            bool effectStatement)
        {
            if (name == "sequence")
            {
                var effects = block.Members.Select(member => member is DslPropertyNode property
                    ? LowerEffectStatement(property)
                    : throw Error(member.Location, "Sequence blocks contain effect statements.")).ToArray();
                return new DslCallNode(name, Array.AsReadOnly(effects.Select(effect =>
                    new DslCallArgumentNode(null, effect, effect.Location)).ToArray()), location);
            }

            if (name == "if")
            {
                var fields = ReadLastWinsProperties(block);
                var arguments = new List<DslCallArgumentNode>();
                if (fields.TryGetValue("condition", out var condition))
                    arguments.Add(new(null, LowerStructuredValue(condition), condition.Location));
                if (fields.TryGetValue("then", out var then))
                    arguments.Add(new(null, LowerEffectValue(then), then.Location));
                foreach (var field in new[] { "else" })
                    if (fields.TryGetValue(field, out var value))
                        arguments.Add(new(field, LowerEffectValue(value), value.Location));
                return new DslCallNode(name, Array.AsReadOnly(arguments.ToArray()), location);
            }

            var positionalNames = PositionalFields.TryGetValue(name, out var names)
                ? names : Array.Empty<string>();
            var args = new List<DslCallArgumentNode>();
            foreach (var field in ReadLastWinsProperties(block).Select(pair =>
                         block.Members.OfType<DslPropertyNode>().Last(property => property.Name == pair.Key)))
            {
                var lowered = field.Name is "effect" or "onSelected" or "after" or "onFatal" or "then" or "else"
                    ? LowerEffectValue(field.Value)
                    : LowerStructuredValue(field.Value);
                var positionalIndex = Array.IndexOf(positionalNames, field.Name);
                args.Add(new(positionalIndex >= 0 ? null : field.Name, lowered, field.Location));
            }
            args = args.OrderBy(argument => argument.Name is null
                ? Array.IndexOf(positionalNames, block.Members.OfType<DslPropertyNode>()
                    .First(property => property.Location == argument.Location).Name)
                : int.MaxValue).ToList();
            return new DslCallNode(name, Array.AsReadOnly(args.ToArray()), location);
        }

        private DslValueNode LowerEffectValue(DslValueNode node)
        {
            if (node is not DslBlockNode block) return LowerStructuredValue(node);
            if (block.Members.Count == 1 && block.Members[0] is DslPropertyNode property
                && property.Value is DslBlockNode expressionBlock)
                return LowerNamedCall(property.Name, expressionBlock, property.Location, effectStatement: true);
            return LowerNamedCall("sequence", block, block.Location, effectStatement: true);
        }

        private static readonly IReadOnlyDictionary<string, string[]> PositionalFields =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["attack"] = ["damage"], ["attackVariable"] = ["variable"],
                ["scaledAttack"] = ["base", "perCount"], ["randomAttack"] = ["damage"],
                ["applyPower"] = ["power", "stacks"], ["applyScaledPower"] = ["power", "base", "perCount"],
                ["gainBlock"] = ["amount"], ["gainBlockFromCurrentBlock"] = [], ["loseHp"] = ["amount"],
                ["heal"] = ["amount"], ["draw"] = ["amount"], ["modifyEnergy"] = ["amount"],
                ["addCards"] = ["card"], ["chooseCard"] = [], ["chooseCards"] = [], ["cloneCard"] = [],
                ["moveSelectedCard"] = [], ["upgradeSelectedCard"] = [], ["upgradeCards"] = [],
                ["transformSelectedCard"] = ["card"], ["randomMoveCard"] = [],
                ["forEachCard"] = [], ["withCount"] = ["name"], ["repeatForCount"] = ["base", "perCount"],
                ["repeat"] = ["count", "effect"], ["repeatForX"] = ["effect"], ["sequence"] = [], ["if"] = [],
                ["generateCards"] = [], ["autoPlay"] = [], ["autoPlaySelectedCard"] = [],
                ["drawUntil"] = [], ["gainMaxHp"] = ["amount"], ["modifyPowerVariable"] = ["power", "variable", "amount"],
                ["modifyCombatState"] = ["variable", "amount"], ["modifyEnemyVariable"] = ["variable", "amount"],
                ["modifyCardVariable"] = ["variable", "amount"], ["accumulateCardDamage"] = ["variable"],
                ["decrementPower"] = ["power"], ["removePower"] = ["power"], ["retainSelectedCard"] = [],
                ["randomMoveCards"] = [], ["modifySelectedCardCost"] = ["modifier"], ["choose"] = [],
                ["cardEvent"] = [], ["scaled"] = ["base", "perCount"],
                ["cardPlaysThisTurn"] = ["count"], ["pileCountAtLeast"] = ["pile", "count"],
                ["powerAmountAtLeast"] = ["power", "amount"], ["hasPower"] = ["power"],
                ["energyAtLeast"] = ["amount"], ["playerHpLossCount"] = [],
                ["playerLostHpThisTurn"] = [], ["cardExhaustedThisTurn"] = [],
                ["selectedCardIsClone"] = [], ["targetAlive"] = [],
                ["targetPowerAmount"] = ["power"], ["cardsWithTag"] = ["tag"],
                ["pileCount"] = ["pile"], ["savedCount"] = ["name"], ["cardVariable"] = ["variable"],
                ["variable"] = ["name", "default"], ["costModifier"] = ["amount"]
            };

        private EffectNode? OptionalVersionEffect(IReadOnlyDictionary<string, DslValueNode> properties, string name) =>
            properties.TryGetValue(name, out var node) ? CompileVersionEffect(node) : null;

        private CardPlayListener? OptionalVersionListener(IReadOnlyDictionary<string, DslValueNode> properties,
            string name)
        {
            if (!properties.TryGetValue(name, out var node)) return null;
            var fields = ReadLastWinsProperties(RequireBlock(node, name));
            var whenType = fields.TryGetValue("whenCardType", out var typeNode)
                ? EnumValue<CardType>(typeNode) : (CardType?)null;
            var effect = fields.TryGetValue("effect", out var effectNode)
                ? CompileVersionEffect(effectNode) : new SequenceEffect(Array.Empty<EffectNode>());
            return new CardPlayListener(effect, whenType);
        }

        private static DslBlockNode RequireBlock(DslValueNode node, string description) => node as DslBlockNode
            ?? throw Error(node.Location, $"{description} must be a block.");

        private static IReadOnlyDictionary<string, DslValueNode> ReadLastWinsProperties(DslBlockNode block)
        {
            var result = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var member in block.Members)
            {
                if (member is not DslPropertyNode property)
                    throw Error(member.Location, "Expected a property in this block.");
                result[property.Name] = property.Value;
            }
            return result;
        }

        private ContentId[] ParseBraceIds(DslValueNode node, string description)
        {
            if (node is not DslBlockNode block)
                throw Error(node.Location, $"{description} must use a brace block.");
            var ids = block.Members.Select(member => member switch
            {
                DslPropertyNode property when property.Value is DslBooleanNode { Value: true } =>
                    ReferenceId(new DslNameNode(property.Name, property.Location)),
                DslPropertyNode property when property.Value is DslStringNode => ReferenceId(property.Value),
                _ => throw Error(member.Location, $"Invalid {description} entry.")
            }).ToArray();
            if (ids.Distinct().Count() != ids.Length)
                throw Error(node.Location, $"{description} entries must be unique.");
            return ids;
        }

        private DslValueNode LowerNamedCollection(DslValueNode node, string entryName)
        {
            if (node is not DslBlockNode block)
                return LowerStructuredValue(node);
            var entries = block.Members.Select(member => member switch
            {
                DslPropertyNode property when property.Name == entryName && property.Value is DslBlockNode entry =>
                    LowerNamedCall(entryName, entry, property.Location, effectStatement: false),
                _ => throw Error(member.Location, $"Expected {entryName} entries.")
            }).ToArray();
            return new DslListNode(Array.AsReadOnly(entries), block.Location);
        }

        private static string[] ParseBraceNames(DslValueNode node, string description)
        {
            if (node is not DslBlockNode block)
                throw Error(node.Location, $"{description} must use a brace block.");
            return block.Members.Select(member => member switch
            {
                DslPropertyNode property when property.Value is DslBooleanNode { Value: true } => property.Name,
                DslPropertyNode property when property.Value is DslNameNode => property.Name,
                _ => throw Error(member.Location, $"Invalid {description} entry.")
            }).ToArray();
        }

        private static CardKeywords ParseKeyword(string name) => name switch
        {
            "exhaust" => CardKeywords.Exhaust, "ethereal" => CardKeywords.Ethereal,
            "unplayable" => CardKeywords.Unplayable, "retain" => CardKeywords.Retain,
            "innate" => CardKeywords.Innate,
            _ => throw new FormatException($"Unknown card keyword '{name}'.")
        };

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
                    CheckShape(call, 1, 1, "target", "properties", "onFatal");
                    return new AttackEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget),
                        OptionalArgument(call, "properties") is { } damageProperties
                            ? ParseDamageProperties(damageProperties) : DamageProperties.Move,
                        OptionalEffect(call, "onFatal"));
                case "attackVariable":
                    CheckShape(call, 1, 1, "target", "hits", "properties");
                    return new AttackCardVariableEffect(ReferenceId(Argument(call, 0)),
                        OptionalInteger(call, "hits", 1),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget),
                        OptionalArgument(call, "properties") is { } variableAttackProperties
                            ? ParseDamageProperties(variableAttackProperties) : DamageProperties.Move);
                case "scaledAttack":
                    CheckShape(call, 2, 2, "count", "target", "properties", "onFatal");
                    return new ScaledAttackEffect(Integer(Argument(call, 0)), Integer(Argument(call, 1)),
                        CompileCombatCount(RequiredArgument(call, "count")),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget),
                        OptionalArgument(call, "properties") is { } scaledProperties
                            ? ParseDamageProperties(scaledProperties) : DamageProperties.Move,
                        OptionalEffect(call, "onFatal"));
                case "attackUsingBlock":
                    CheckShape(call, 0, 0, "target");
                    return new AttackUsingBlockEffect(OptionalTarget(call, "target", TargetSelector.SelectedTarget));
                case "randomAttack":
                    CheckShape(call, 1, 1, "properties");
                    var randomDamage = Argument(call, 0);
                    return new RandomAttackEffect(randomDamage is DslNumberNode ? Integer(randomDamage) : 0,
                        randomDamage is DslNumberNode ? null : CompileScaledAmount(randomDamage),
                        OptionalArgument(call, "properties") is { } randomProperties
                            ? ParseDamageProperties(randomProperties) : DamageProperties.Move);
                case "applyPower":
                    CheckShape(call, 2, 2, "target");
                    return new ApplyPowerEffect(ReferenceId(Argument(call, 0)), Integer(Argument(call, 1)),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget));
                case "applyScaledPower":
                    CheckShape(call, 3, 3, "count", "target", "onlyEnemy");
                    return new ApplyScaledPowerEffect(Id(Name(Argument(call, 0))),
                        Integer(Argument(call, 1)), Integer(Argument(call, 2)),
                        CompileCombatCount(RequiredArgument(call, "count")),
                        OptionalTarget(call, "target", TargetSelector.Self),
                        OptionalArgument(call, "onlyEnemy") is { } onlyEnemyPower ? Id(Name(onlyEnemyPower)) : null);
                case "stealGold":
                    CheckShape(call, 3, 3, "target");
                    return new StealGoldEffect(Integer(Argument(call, 0)), Id(Name(Argument(call, 1))),
                        Id(Name(Argument(call, 2))), OptionalTarget(call, "target", TargetSelector.FirstEnemy));
                case "addGoldReward":
                    CheckShape(call, 2, 2, "count");
                    return new AddGoldRewardEffect(Integer(Argument(call, 0)), Integer(Argument(call, 1)),
                        CompileCombatCount(RequiredArgument(call, "count")));
                case "setGoldRewardPercent":
                    CheckShape(call, 1, 1);
                    return new SetGoldRewardPercentEffect(Integer(Argument(call, 0)));
                case "stealRunCard":
                    CheckShape(call, 1, 1, "from", "priorities");
                    var sourcePilesNode = RequiredArgument(call, "from");
                    if (sourcePilesNode is not DslListNode sourcePiles || sourcePiles.Items.Count == 0)
                        throw Error(sourcePilesNode.Location, "stealRunCard requires at least one source pile.");
                    var sourcePileIds = sourcePiles.Items.Select(item => EnumValue<CardPile>(item)).ToArray();
                    if (sourcePileIds.Any(pile => pile is not (CardPile.Draw or CardPile.Discard))
                        || sourcePileIds.Distinct().Count() != sourcePileIds.Length)
                        throw Error(sourcePiles.Location, "Run-card theft can search unique draw/discard piles only.");
                    var priorityNode = RequiredArgument(call, "priorities");
                    if (priorityNode is not DslListNode priorities || priorities.Items.Count == 0)
                        throw Error(priorityNode.Location, "stealRunCard requires an ordered priority list.");
                    return new StealRunCardEffect(ReferenceId(Argument(call, 0)), sourcePileIds,
                        priorities.Items.Select(CompileRunCardPriority).ToArray());
                case "returnHeldRunCardReward":
                    CheckShape(call, 1, 1, "sourceEncounter");
                    return new ReturnHeldRunCardRewardEffect(ReferenceId(Argument(call, 0)),
                        ReferenceId(RequiredArgument(call, "sourceEncounter")));
                case "gainBlock":
                    CheckShape(call, 1, 1, "perPowerStack", "properties", "target", "onlyEnemy");
                    var perPower = OptionalArgument(call, "perPowerStack");
                    var onlyEnemy = OptionalArgument(call, "onlyEnemy");
                    return new GainBlockEffect(Integer(Argument(call, 0)),
                        perPower is null ? null : Id(Name(perPower)),
                        OptionalArgument(call, "properties") is { } blockPropertiesNode
                            ? ParseDamageProperties(blockPropertiesNode) : null,
                        OptionalTarget(call, "target", TargetSelector.Self),
                        onlyEnemy is null ? null : Id(Name(onlyEnemy)));
                case "gainBlockFromCurrentBlock":
                    CheckShape(call, 0, 0, "multiplier", "target");
                    return new GainBlockFromCurrentBlockEffect(OptionalInteger(call, "multiplier", 1),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "loseHp":
                    CheckShape(call, 1, 1, "target", "properties");
                    return new LoseHpEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self),
                        OptionalArgument(call, "properties") is { } hpLossPropertiesNode
                            ? ParseDamageProperties(hpLossPropertiesNode) : DamageProperties.None);
                case "heal":
                    CheckShape(call, 1, 1, "target");
                    return new HealEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "draw":
                    CheckShape(call, 1, 1);
                    var drawAmount = Argument(call, 0);
                    return drawAmount is DslNumberNode ? new DrawCardsEffect(Integer(drawAmount))
                        : new DrawCardsEffect(0, CompileScaledAmount(drawAmount));
                case "drawUntil":
                    CheckShape(call, 0, 0, "type");
                    return new DrawUntilCardTypeEffect(
                        EnumValue<CardType>(RequiredArgument(call, "type")));
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
                case "withCount":
                    CheckShape(call, 1, 1, "count", "effect");
                    return new WithCountEffect(StringValue(Argument(call, 0)),
                        CompileCombatCount(RequiredArgument(call, "count")),
                        CompileEffect(RequiredArgument(call, "effect")));
                case "autoPlay":
                    CheckShape(call, 0, 0, "from", "count", "position", "forceExhaust");
                    var autoCount = RequiredArgument(call, "count");
                    var autoSource = EnumValue<CardPile>(RequiredArgument(call, "from"));
                    if (autoSource != CardPile.Draw) throw Error(call.Location, "Automatic play currently requires the draw pile.");
                    return new AutoPlayCardsEffect(autoSource,
                        autoCount is DslNumberNode ? Integer(autoCount) : 0,
                        OptionalEnum(call, "position", CardPilePosition.Top), OptionalBoolean(call, "forceExhaust", false),
                        autoCount is DslNumberNode ? null : CompileScaledAmount(autoCount));
                case "autoPlayRandomCards":
                    CheckShape(call, 0, 0, "from", "count", "type", "forceExhaust");
                    var randomAutoCount = RequiredArgument(call, "count");
                    return new AutoPlayRandomCardsEffect(EnumValue<CardPile>(RequiredArgument(call, "from")),
                        randomAutoCount is DslNumberNode ? Integer(randomAutoCount) : 0,
                        OptionalArgument(call, "type") is { } randomAutoType ? EnumValue<CardType>(randomAutoType) : null,
                        OptionalBoolean(call, "forceExhaust", false),
                        randomAutoCount is DslNumberNode ? null : CompileScaledAmount(randomAutoCount));
                case "autoPlaySelectedCard":
                    CheckShape(call, 0, 0, "from", "forceExhaust");
                    return new AutoPlaySelectedCardEffect(OptionalEnum(call, "from", CardPile.Exhaust),
                        OptionalBoolean(call, "forceExhaust", false));
                case "addCards":
                    CheckShape(call, 1, 1, "pile", "count", "target", "inheritUpgradeFromPlayedCard", "position");
                    return new AddCardsToPileEffect(ReferenceId(Argument(call, 0)),
                        EnumValue<CardPile>(RequiredArgument(call, "pile")), OptionalInteger(call, "count", 1),
                        OptionalTarget(call, "target", TargetSelector.Self),
                        OptionalBoolean(call, "inheritUpgradeFromPlayedCard", false),
                        OptionalEnum(call, "position", CardPilePosition.Bottom));
                case "summonEnemy":
                    CheckShape(call, 1, 1, "count", "startingAction", "startingPowers", "additionalPowers", "maxLivingEnemies", "maxEnemies", "slot");
                    var summonCount = OptionalInteger(call, "count", 1);
                    if (summonCount <= 0) throw Error(call.Location, "Summoned enemy count must be positive.");
                    var startingActionNode = OptionalArgument(call, "startingAction");
                    var summonStartingPowers = OptionalArgument(call, "startingPowers") is { } summonPowersNode
                        ? CompileStartingPowers(summonPowersNode) : null;
                    var summonAdditionalPowers = OptionalArgument(call, "additionalPowers") is { } summonAdditionalPowersNode
                        ? CompileStartingPowers(summonAdditionalPowersNode) : null;
                    var maxLivingEnemiesNode = OptionalArgument(call, "maxLivingEnemies");
                    var maxLivingEnemies = maxLivingEnemiesNode is null ? (int?)null
                        : Integer(maxLivingEnemiesNode);
                    if (maxLivingEnemies is <= 0)
                        throw Error(call.Location, "maxLivingEnemies must be positive when specified.");
                    var maxEnemiesNode = OptionalArgument(call, "maxEnemies");
                    var maxEnemies = maxEnemiesNode is null ? (int?)null : Integer(maxEnemiesNode);
                    if (maxEnemies is <= 0)
                        throw Error(call.Location, "maxEnemies must be positive when specified.");
                    var slotPlacement = OptionalEnum(call, "slot", SummonSlotPlacement.NextAvailable);
                    return new SummonEnemyEffect(ReferenceId(Argument(call, 0)), summonCount,
                        startingActionNode is null ? null : ReferenceId(startingActionNode),
                        summonStartingPowers, maxLivingEnemies, maxEnemies, slotPlacement, summonAdditionalPowers);
                case "summonRandomEnemy":
                    CheckShape(call, 1, 1, "startingPowers", "additionalPowers", "maxLivingEnemies", "avoidPrevious", "slot");
                    if (Argument(call, 0) is not DslListNode enemyChoices || enemyChoices.Items.Count < 2)
                        throw Error(Argument(call, 0).Location, "summonRandomEnemy requires a list of at least two enemy IDs.");
                    var randomEnemyIds = enemyChoices.Items.Select(ReferenceId).ToArray();
                    if (randomEnemyIds.Distinct().Count() != randomEnemyIds.Length)
                        throw Error(enemyChoices.Location, "summonRandomEnemy choices must be unique.");
                    var randomStartingPowers = OptionalArgument(call, "startingPowers") is { } randomPowersNode
                        ? CompileStartingPowers(randomPowersNode) : null;
                    var randomAdditionalPowers = OptionalArgument(call, "additionalPowers") is { } randomAdditionalPowersNode
                        ? CompileStartingPowers(randomAdditionalPowersNode) : null;
                    var randomMaxLivingNode = OptionalArgument(call, "maxLivingEnemies");
                    var randomMaxLiving = randomMaxLivingNode is null ? (int?)null : Integer(randomMaxLivingNode);
                    if (randomMaxLiving is <= 0)
                        throw Error(call.Location, "maxLivingEnemies must be positive when specified.");
                    return new SummonRandomEnemyEffect(randomEnemyIds, randomStartingPowers, randomMaxLiving,
                        OptionalBoolean(call, "avoidPrevious", false),
                        OptionalEnum(call, "slot", SummonSlotPlacement.NextAvailable), randomAdditionalPowers);
                case "respawnSelf":
                    CheckShape(call, 1, 1, "startingAction");
                    var respawnStartingAction = OptionalArgument(call, "startingAction");
                    return new RespawnSelfEffect(ReferenceId(Argument(call, 0)),
                        respawnStartingAction is null ? null : ReferenceId(respawnStartingAction));
                case "stun":
                    CheckShape(call, 0, 0, "target");
                    return new StunEnemyEffect(OptionalTarget(call, "target", TargetSelector.Self));
                case "kill":
                    CheckShape(call, 0, 0, "target");
                    return new KillEffect(OptionalTarget(call, "target", TargetSelector.Self));
                case "escape":
                    CheckShape(call, 0, 0, "target");
                    return new EscapeEffect(OptionalTarget(call, "target", TargetSelector.Self));
                case "queueRevival":
                    CheckShape(call, 0, 0, "intent");
                    var revivalIntent = OptionalArgument(call, "intent") is { } intentNode
                        ? EnumValue<IntentKind>(intentNode) : IntentKind.Heal;
                    if (revivalIntent is IntentKind.None or IntentKind.Attack or IntentKind.DeathBlow)
                        throw Error(call.Location, "A queued enemy revival must use a non-damaging intent.");
                    return new QueueEnemyRevivalEffect(revivalIntent);
                case "temporarilyDowngradeCards":
                    CheckShape(call, 1, 1, "target");
                    return new TemporarilyDowngradeCardsEffect(ReferenceId(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "restoreTemporarilyDowngradedCards":
                    CheckShape(call, 1, 1, "target");
                    return new RestoreTemporarilyDowngradedCardsEffect(ReferenceId(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "registerPowerApplier":
                    CheckShape(call, 1, 1, "target");
                    return new RegisterPowerApplierEffect(ReferenceId(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "afflictCards":
                    CheckShape(call, 1, 1, "type", "target", "onlyIfUnafflicted");
                    return new AfflictCardsEffect(ReferenceId(Argument(call, 0)),
                        OptionalArgument(call, "type") is { } afflictType ? EnumValue<CardType>(afflictType) : null,
                        OptionalEnum(call, "target", TargetSelector.Self),
                        OptionalBoolean(call, "onlyIfUnafflicted", false));
                case "clearAffliction":
                    CheckShape(call, 1, 1, "type", "target");
                    return new ClearAfflictionEffect(ReferenceId(Argument(call, 0)),
                        OptionalArgument(call, "type") is { } clearType ? EnumValue<CardType>(clearType) : null,
                        OptionalEnum(call, "target", TargetSelector.Self));
                case "setEnemyHealth":
                    CheckShape(call, 1, 1);
                    var healthRange = RequireCall(Argument(call, 0));
                    if (healthRange.Name != "range")
                        throw Error(healthRange.Location, "setEnemyHealth uses range(minimum, maximum).");
                    CheckShape(healthRange, 2, 2);
                    var minimumHealth = Integer(Argument(healthRange, 0));
                    var maximumHealth = Integer(Argument(healthRange, 1));
                    if (minimumHealth <= 0 || maximumHealth < minimumHealth)
                        throw Error(healthRange.Location, "Enemy health range is invalid.");
                    return new SetEnemyHealthEffect(minimumHealth, maximumHealth);
                case "generateCards":
                    CheckShape(call, 0, 0, "count", "pile", "upgraded", "type", "distinct", "cost");
                    var generationCount = RequiredArgument(call, "count");
                    return new GenerateCombatCardsEffect(
                        generationCount is DslNumberNode ? Integer(generationCount) : 0,
                        OptionalEnum(call, "pile", CardPile.Hand), OptionalBoolean(call, "upgraded", false),
                        generationCount is DslNumberNode ? null : CompileScaledAmount(generationCount),
                        OptionalArgument(call, "type") is { } generatedType ? EnumValue<CardType>(generatedType) : null,
                        OptionalBoolean(call, "distinct", false),
                        OptionalArgument(call, "cost") is { } generatedCost ? CompileCostModifier(generatedCost).Modifier : null);
                case "choose":
                    CheckShape(call, 0, 0, "prompt", "options");
                    if (RequiredArgument(call, "options") is not DslListNode choiceOptions)
                        throw Error(call.Location, "Choice options must be a list.");
                    var compiledOptions = choiceOptions.Items.Select(node =>
                    {
                        var option = RequireCall(node);
                        if (option.Name != "option") throw Error(option.Location, "Expected option(label, effect=...).");
                        CheckShape(option, 1, 2, "effect");
                        var positional = Positional(option).ToArray();
                        if (positional.Length == 2 && OptionalArgument(option, "effect") is not null)
                            throw Error(option.Location, "An option effect must be positional or named, not both.");
                        return new ChoiceOption(StringValue(positional[0].Value), positional.Length == 2
                            ? CompileEffect(positional[1].Value) : OptionalEffect(option, "effect"));
                    }).ToArray();
                    if (compiledOptions.Length == 0) throw Error(call.Location, "A choice requires at least one option.");
                    return new ChooseEffect(StringValue(RequiredArgument(call, "prompt")), compiledOptions);
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
                        OptionalArgument(call, "maxChoices") is DslNumberNode maxChoicesNode
                            ? Integer(maxChoicesNode) : 0,
                        OptionalEnum(call, "filter", CardSelectionFilter.Any), OptionalEffect(call, "onSelected"),
                        OptionalArgument(call, "maxChoices") is { } calculatedMaxChoices
                            and not DslNumberNode ? CompileScaledAmount(calculatedMaxChoices) : null);
                case "randomMoveCard":
                    CheckShape(call, 0, 0, "from", "to", "filter", "after");
                    return new RandomlyMoveCardFromPileEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        EnumValue<CardPile>(RequiredArgument(call, "to")),
                        OptionalEnum(call, "filter", CardSelectionFilter.Any),
                        OptionalEffect(call, "after"));
                case "randomMoveCards":
                    CheckShape(call, 0, 0, "from", "to", "count", "filter", "after");
                    var randomMoveCountNode = OptionalArgument(call, "count");
                    return new RandomlyMoveCardsEffect(
                        EnumValue<CardPile>(RequiredArgument(call, "from")),
                        EnumValue<CardPile>(RequiredArgument(call, "to")),
                        randomMoveCountNode is DslNumberNode ? Integer(randomMoveCountNode) : 1,
                        randomMoveCountNode is not null and not DslNumberNode
                            ? CompileScaledAmount(randomMoveCountNode) : null,
                        OptionalEnum(call, "filter", CardSelectionFilter.Any),
                        OptionalEffect(call, "after"));
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
                case "modifySelectedCardCost":
                    CheckShape(call, 1, 1);
                    var localCost = CompileCostModifier(Argument(call, 0), allowCalculated: true);
                    return new ModifySelectedCardCostEffect(localCost.Modifier, localCost.CalculatedAmount);
                case "modifyCardVariable":
                    CheckShape(call, 2, 2, "card", "operation");
                    var variableAmount = Argument(call, 1);
                    return new ModifyCardVariableEffect(ReferenceId(Argument(call, 0)),
                        variableAmount is DslNumberNode ? DecimalValue(variableAmount) : 0,
                        OptionalEnum(call, "card", CardContext.Played),
                        OptionalEnum(call, "operation", CardVariableOperation.Add),
                        variableAmount is DslNumberNode ? null : CompileScaledAmount(variableAmount));
                case "addCardEnchantment":
                    CheckShape(call, 1, 1, "card");
                    return new AddCardEnchantmentEffect(ReferenceId(Argument(call, 0)),
                        OptionalEnum(call, "card", CardContext.Selected));
                case "removeCardEnchantment":
                    CheckShape(call, 1, 1, "card");
                    return new RemoveCardEnchantmentEffect(ReferenceId(Argument(call, 0)),
                        OptionalEnum(call, "card", CardContext.Selected));
                case "accumulateCardDamage":
                    CheckShape(call, 1, 1, "target", "source");
                    return new AccumulateCardDamageEffect(ReferenceId(Argument(call, 0)),
                        OptionalEnum(call, "target", CardContext.Played),
                        OptionalEnum(call, "source", CardContext.Selected));
                case "modifyPowerVariable":
                    CheckShape(call, 3, 3, "target", "operation");
                    return new ModifyPowerVariableEffect(Id(Name(Argument(call, 0))),
                        Id(Name(Argument(call, 1))), Integer(Argument(call, 2)),
                        OptionalTarget(call, "target", TargetSelector.Self),
                        OptionalEnum(call, "operation", PowerVariableOperation.Add));
                case "modifyEnemyVariable":
                    CheckShape(call, 2, 2, "operation");
                    return new ModifyEnemyVariableEffect(Id(Name(Argument(call, 0))),
                        Integer(Argument(call, 1)),
                        OptionalEnum(call, "operation", PowerVariableOperation.Add));
                case "modifyCombatState":
                    CheckShape(call, 2, 2, "operation");
                    return new ModifyCombatStateEffect(Id(Name(Argument(call, 0))),
                        Integer(Argument(call, 1)),
                        OptionalEnum(call, "operation", PowerVariableOperation.Add));
                case "gainMaxHp":
                    CheckShape(call, 1, 1);
                    return new GainMaxHpEffect(Integer(Argument(call, 0)));
                case "loseMaxHp":
                    CheckShape(call, 1, 1, "target");
                    return new LoseMaxHpEffect(Integer(Argument(call, 0)),
                        OptionalTarget(call, "target", TargetSelector.Self));
                case "downgradeSelectedCard":
                    CheckShape(call, 0, 0);
                    return new DowngradeSelectedCardEffect();
                case "cloneCard":
                    CheckShape(call, 0, 0, "card", "pile", "count");
                    var cloneCountNode = OptionalArgument(call, "count");
                    return new CloneCardEffect(OptionalEnum(call, "card", CardContext.Played),
                        OptionalEnum(call, "pile", CardPile.Hand),
                        cloneCountNode is DslNumberNode ? Integer(cloneCountNode) : 1,
                        cloneCountNode is not null and not DslNumberNode ? CompileScaledAmount(cloneCountNode) : null);
                case "transformSelectedCard":
                    CheckShape(call, 1, 1, "from", "upgraded");
                    return new TransformSelectedCardEffect(ReferenceId(Argument(call, 0)),
                        EnumValue<CardPile>(RequiredArgument(call, "from")), OptionalBoolean(call, "upgraded", false));
                case "upgradeCards":
                    CheckShape(call, 0, 0, "pile");
                    return new UpgradeCardsInPileEffect(EnumValue<CardPile>(RequiredArgument(call, "pile")));
                case "decrementPower":
                    CheckShape(call, 1, 1);
                    return new DecrementPowerEffect(ReferenceId(Argument(call, 0)));
                case "removePower":
                    CheckShape(call, 1, 1);
                    return new RemovePowerEffect(ReferenceId(Argument(call, 0)));
                case "removePowers":
                    CheckShape(call, 0, 0, "debuffsOnly", "includeTemporary");
                    return new RemovePowersEffect(OptionalBoolean(call, "debuffsOnly", true),
                        OptionalBoolean(call, "includeTemporary", false));
                case "setEnemyAction":
                    CheckShape(call, 1, 1);
                    return new SetEnemyActionEffect(ReferenceId(Argument(call, 0)));
                case "expirePower":
                    CheckShape(call, 1, 1);
                    return new ExpirePowerEffect(ReferenceId(Argument(call, 0)));
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
                case "powerAmountAtLeast":
                    CheckShape(call, 2, 2, "target");
                    return new PowerAmountAtLeastPredicate(Id(Name(Argument(call, 0))),
                        Integer(Argument(call, 1)), OptionalTarget(call, "target", TargetSelector.Self));
                case "energyAtLeast":
                    CheckShape(call, 1, 1);
                    return new EnergyAtLeastPredicate(Integer(Argument(call, 0)));
                case "cardExhaustedThisTurn":
                    CheckShape(call, 0, 0);
                    return new CardExhaustedThisTurnPredicate();
                case "playerLostHpThisTurn":
                    CheckShape(call, 0, 0);
                    return new PlayerLostHpThisTurnPredicate();
                case "selectedCardIsClone":
                    CheckShape(call, 0, 0);
                    return new SelectedCardIsClonePredicate();
                case "cardHasEnchantment":
                    CheckShape(call, 1, 1, "card");
                    return new CardHasEnchantmentPredicate(ReferenceId(Argument(call, 0)),
                        OptionalEnum(call, "card", CardContext.Selected));
                case "pileCountAtLeast":
                    CheckShape(call, 2, 2);
                    return new PileCountAtLeastPredicate(EnumValue<CardPile>(Argument(call, 0)),
                        Integer(Argument(call, 1)));
                case "cardPlaysThisTurn":
                    CheckShape(call, 1, 1, "type", "phase");
                    var historyType = OptionalArgument(call, "type");
                    return new CardPlayCountEqualsPredicate(
                        historyType is null ? null : EnumValue<CardType>(historyType),
                        OptionalEnum(call, "phase", CardPlayHistoryPhase.Finished), Integer(Argument(call, 0)));
                case "cardPlaysThisTurnAtLeast":
                    CheckShape(call, 1, 1, "type", "phase");
                    var minimumHistoryType = OptionalArgument(call, "type");
                    return new CardPlayCountAtLeastPredicate(
                        minimumHistoryType is null ? null : EnumValue<CardType>(minimumHistoryType),
                        OptionalEnum(call, "phase", CardPlayHistoryPhase.Finished), Integer(Argument(call, 0)));
                default:
                    throw Error(call.Location, $"Unknown combat predicate '{call.Name}'.");
            }
        }

        private IReadOnlyList<CardVariableDefinition> CompileCardVariables(DslValueNode node)
        {
            if (node is not DslListNode list) throw Error(node.Location, "Card variables require a list.");
            var variables = list.Items.Select(item =>
            {
                var call = RequireCall(item);
                if (call.Name != "variable") throw Error(call.Location, "Expected variable(Name, initialValue).");
                CheckShape(call, 2, 2, "upgraded", "preserveOnDowngrade");
                return new CardVariableDefinition(ReferenceId(Argument(call, 0)), DecimalValue(Argument(call, 1)),
                    OptionalArgument(call, "upgraded") is { } upgraded ? DecimalValue(upgraded) : null,
                    OptionalBoolean(call, "preserveOnDowngrade", false));
            }).ToArray();
            if (variables.Select(variable => variable.Id).Distinct().Count() != variables.Length)
                throw Error(node.Location, "A card cannot declare duplicate variables.");
            return Array.AsReadOnly(variables);
        }

        private RunCardSelectionPriority CompileRunCardPriority(DslValueNode node)
        {
            var call = RequireCall(node);
            if (call.Name != "priority") throw Error(call.Location, "Expected priority(...) in run-card priorities.");
            CheckShape(call, 0, 0, "rarities", "requireEnchantments", "excludeEnchantments", "matchAny");
            var rarities = OptionalArgument(call, "rarities") is { } rarityNode
                ? CompileEnumList<CardRarity>(rarityNode, "Card rarity filters") : Array.Empty<CardRarity>();
            var required = OptionalArgument(call, "requireEnchantments") is { } requiredNode
                ? CompileReferenceList(requiredNode, "Required enchantments") : Array.Empty<ContentId>();
            var excluded = OptionalArgument(call, "excludeEnchantments") is { } excludedNode
                ? CompileReferenceList(excludedNode, "Excluded enchantments") : Array.Empty<ContentId>();
            if (rarities.Length == 0 && required.Length == 0)
                throw Error(call.Location, "A card priority needs a rarity or required-enchantment filter.");
            return new RunCardSelectionPriority(rarities, required, excluded,
                OptionalBoolean(call, "matchAny", false));
        }

        private T[] CompileEnumList<T>(DslValueNode node, string description) where T : struct, Enum
        {
            if (node is not DslListNode list || list.Items.Count == 0)
                throw Error(node.Location, $"{description} require a non-empty list.");
            var values = list.Items.Select(item => EnumValue<T>(item)).ToArray();
            if (values.Distinct().Count() != values.Length)
                throw Error(node.Location, $"{description} cannot contain duplicates.");
            return values;
        }

        private ContentId[] CompileReferenceList(DslValueNode node, string description)
        {
            if (node is not DslListNode list)
                throw Error(node.Location, $"{description} require a list.");
            var values = list.Items.Select(ReferenceId).ToArray();
            if (values.Distinct().Count() != values.Length)
                throw Error(node.Location, $"{description} cannot contain duplicates.");
            return values;
        }

        private CardPlayListener? CompilePlayListener(IReadOnlyDictionary<string, DslValueNode> properties, string name)
        {
            if (!properties.TryGetValue(name, out var node)) return null;
            var call = RequireCall(node);
            if (call.Name != "cardEvent") throw Error(call.Location, "Expected cardEvent(effect=..., whenCardType=...).");
            CheckShape(call, 0, 0, "effect", "whenCardType");
            return new CardPlayListener(CompileEffect(RequiredArgument(call, "effect")),
                OptionalArgument(call, "whenCardType") is { } type ? EnumValue<CardType>(type) : null);
        }

        private (CardCostModifier Modifier, ScaledCombatAmount? CalculatedAmount) CompileCostModifier(DslValueNode node,
            bool allowCalculated = false)
        {
            var call = RequireCall(node);
            if (call.Name != "costModifier") throw Error(call.Location, "Expected costModifier(amount, expires=[...]).");
            CheckShape(call, 1, 1, "operation", "expires", "reduceOnly");
            if (RequiredArgument(call, "expires") is not DslListNode expirations || expirations.Items.Count == 0)
                throw Error(call.Location, "Cost modifier expirations must be a nonempty list.");
            var boundary = expirations.Items.Aggregate((CardCostExpiration)0,
                (flags, item) => flags | EnumValue<CardCostExpiration>(item));
            var amountNode = Argument(call, 0);
            if (!allowCalculated && amountNode is not DslNumberNode)
                throw Error(amountNode.Location, "Calculated cost modifiers require modifySelectedCardCost.");
            var calculated = amountNode is DslNumberNode ? null : CompileScaledAmount(amountNode);
            var modifier = new CardCostModifier(amountNode is DslNumberNode ? Integer(amountNode) : 0,
                OptionalEnum(call, "operation", CardCostOperation.Set), boundary,
                OptionalBoolean(call, "reduceOnly", false));
            modifier.Validate();
            return (modifier, calculated);
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
                case "energyX":
                    CheckShape(call, 0, 0);
                    return new EnergyXCountSource();
                case "block":
                    CheckShape(call, 0, 0, "target");
                    return new BlockCountSource(OptionalTarget(call, "target", TargetSelector.Self));
                case "savedCount":
                    CheckShape(call, 1, 1);
                    return new SavedCountSource(StringValue(Argument(call, 0)));
                case "cardPlaysThisTurn":
                    CheckShape(call, 0, 0, "type", "phase");
                    return new CardPlaysThisTurnCountSource(
                        OptionalArgument(call, "type") is { } playedType ? EnumValue<CardType>(playedType) : null,
                        OptionalEnum(call, "phase", CardPlayHistoryPhase.Finished));
                case "cardVariable":
                    CheckShape(call, 1, 1, "card");
                    return new CardVariableCountSource(ReferenceId(Argument(call, 0)),
                        OptionalEnum(call, "card", CardContext.Played));
                case "powerVariable":
                    CheckShape(call, 2, 2, "target");
                    return new PowerVariableCountSource(Id(Name(Argument(call, 0))),
                        Id(Name(Argument(call, 1))), OptionalTarget(call, "target", TargetSelector.Self));
                case "takePowerEventAmount":
                    CheckShape(call, 1, 1, "card");
                    return new TakePowerEventAmountCountSource(Id(Name(Argument(call, 0))),
                        OptionalEnum(call, "card", CardContext.Played));
                case "takePowerTurnEventAmount":
                    CheckShape(call, 1, 1);
                    return new TakePowerTurnEventAmountCountSource(Id(Name(Argument(call, 0))));
                case "product":
                    CheckShape(call, 2, 2);
                    return new ProductCombatCountSource(CompileCombatCount(Argument(call, 0)),
                        CompileCombatCount(Argument(call, 1)));
                case "targetPowerAmount":
                    CheckShape(call, 1, 1, "target");
                    return new TargetPowerCountSource(Id(Name(Argument(call, 0))),
                        OptionalTarget(call, "target", TargetSelector.SelectedTarget));
                case "cardsWithTag":
                    CheckShape(call, 1, 1);
                    return new TaggedCardCountSource(ReferenceId(Argument(call, 0)));
                default:
                    throw Error(call.Location, $"Unknown attack count source '{call.Name}'.");
            }
        }

        private IReadOnlyList<EnemyStartingPower> CompileStartingPowers(DslValueNode node)
        {
            if (node is not DslListNode list)
                throw Error(node.Location, "Summoned enemy starting Powers must be a list.");
            var powers = list.Items.Select(item =>
            {
                var call = RequireCall(item);
                if (call.Name != "power")
                    throw Error(call.Location, "Summoned enemy starting Powers use power(Name, amount).");
                CheckShape(call, 2, 2);
                var amount = Integer(ResolveVariants(Argument(call, 1)));
                if (amount <= 0)
                    throw Error(call.Location, "Summoned enemy starting Power amounts must be positive.");
                return new EnemyStartingPower(ReferenceId(Argument(call, 0)), amount);
            }).ToArray();
            if (powers.Select(power => power.Power).Distinct().Count() != powers.Length)
                throw Error(node.Location, "Summoned enemy starting Powers cannot be duplicated.");
            return Array.AsReadOnly(powers);
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
                    "innate" => CardKeywords.Innate,
                    _ => throw Error(item.Location, "Unknown card keyword.")
                };
                if ((keywords & keyword) != 0) throw Error(item.Location, $"Duplicate card keyword '{keyword}'.");
                keywords |= keyword;
            }
            return keywords;
        }

        private IReadOnlyList<ContentId> ParseIds(DslValueNode node, string property)
        {
            var items = node switch
            {
                DslListNode list => list.Items,
                DslBlockNode block => block.Members.Select(member => member switch
                {
                    DslPropertyNode entry => (DslValueNode)new DslNameNode(entry.Name, entry.Location),
                    _ => throw Error(member.Location, $"Invalid {property} entry.")
                }).ToArray(),
                _ => throw Error(node.Location, $"{property} must be a collection.")
            };
            var ids = items.Select(ReferenceId).ToArray();
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
            "none" => TargetSelector.None,
            "self" => TargetSelector.Self,
            "enemy" or "anyAlly" => TargetSelector.SelectedTarget,
            "firstEnemy" => TargetSelector.FirstEnemy,
            "allEnemies" => TargetSelector.AllOpponents,
            "randomEnemy" => TargetSelector.RandomEnemy,
            _ => throw Error(node.Location, "Content target must be none, self, enemy, firstEnemy, allEnemies, or randomEnemy.")
        };

        private static TargetSelector ParseEffectTarget(DslValueNode node) => Name(node) switch
        {
            "self" => TargetSelector.Self,
            "player" => TargetSelector.FirstEnemy,
            "target" => TargetSelector.SelectedTarget,
            "firstEnemy" => TargetSelector.FirstEnemy,
            "allEnemies" => TargetSelector.AllOpponents,
            "randomEnemy" => TargetSelector.RandomEnemy,
            "otherAllies" => TargetSelector.OtherAllies,
            _ => throw Error(node.Location, "Effect target must be self, target, firstEnemy, allEnemies, randomEnemy, or otherAllies.")
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

        private static Dictionary<string, DslValueNode> ReadProperties(DslDeclarationNode declaration,
            bool lastWins = false)
        {
            var properties = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var member in declaration.Members)
            {
                if (member is not DslPropertyNode property)
                    throw Error(member.Location, $"{declaration.Kind} declarations can contain properties only.");
                if (!lastWins && !properties.TryAdd(property.Name, property.Value))
                    throw Error(property.Location, $"Duplicate {declaration.Kind} property '{property.Name}'.");
                if (lastWins) properties[property.Name] = property.Value;
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

        private static void RequireOnlyProperties(DslBlockNode declaration,
            IReadOnlyDictionary<string, DslValueNode> properties, params string[] allowed)
        {
            var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
            foreach (var property in declaration.Members.OfType<DslPropertyNode>())
                if (!allowedSet.Contains(property.Name))
                    throw Error(property.Location, $"Unknown property '{property.Name}'.");
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

        private static decimal DecimalValue(DslValueNode node) => node is DslNumberNode number && number.TryGetDecimal(out var value)
            ? value
            : throw Error(node.Location, "Expected a decimal number.");

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

    public static IReadOnlyList<CardPoolDefinition> CompileCardPools(DslPackageNode package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return new Compiler(package).CompileCardPools();
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
