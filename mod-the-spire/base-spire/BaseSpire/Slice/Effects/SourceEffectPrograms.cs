using System.Collections.Immutable;
using CoreTheSpire;
using BaseSpire.Slice.Effects;

namespace BaseSpire.Slice;

public static partial class SpireCardSource
{
    private static CardEffectProgram ReadEffectProgram(DslValueNode value, string where, string defaultTarget,
        ScaledValue? cardDamage, bool xCost, ref List<string> problems, out List<Primitive> summary)
    {
        summary = [];
        if (value is not DslBlockNode block)
        {
            problems.Add($"{where}: effect program must be a block");
            return CardEffectProgram.Empty;
        }
        var effects = ImmutableArray.CreateBuilder<CardEffect>();
        foreach (var member in block.Members)
        {
            if (member is not DslPropertyNode property)
            {
                problems.Add($"{member.Location}: {where}: expected a named effect");
                continue;
            }
            if (property.Name == "chooseCard" && property.Value is DslBlockNode choice)
            {
                var fields = choice.Members.OfType<DslPropertyNode>().ToArray();
                foreach (var f in fields.Where(f => f.Name is not ("from" or "to" or "count" or "filter" or "destinationPosition" or "autoSelect" or "prompt" or "onSelected")))
                    problems.Add($"{f.Location}: unsupported chooseCard field '{f.Name}'");
                foreach (var repeated in fields.GroupBy(f => f.Name).Where(g => g.Count() > 1))
                    problems.Add($"{property.Location}: duplicate chooseCard field '{repeated.Key}'");
                var from = PileOf(fields.LastOrDefault(f => f.Name == "from"), where, ref problems);
                var selectedBody = fields.LastOrDefault(f => f.Name == "onSelected");
                var toField = fields.LastOrDefault(f => f.Name == "to");
                var to = selectedBody is null ? PileOf(toField, where, ref problems) : null;
                if (selectedBody is not null && toField is not null && NameOf(toField.Value) != "none")
                    problems.Add($"{property.Location}: use onSelected/moveSelectedCard to compose movement; to must be none or omitted");
                var filterField = fields.LastOrDefault(f => f.Name == "filter");
                var filter = FilterOf(filterField, where, ref problems);
                if (filterField is not null && NameOf(filterField.Value) is not ("attack" or "nonAttack" or "upgradeable" or "nonSlySkill"))
                    problems.Add($"{filterField.Location}: chooseCard filter must be attack, nonAttack, upgradeable or nonSlySkill");
                var count = IntegerFieldOf(fields, "count", where, 1, ref problems);
                var position = fields.LastOrDefault(f => f.Name == "destinationPosition");
                var top = position is not null;
                if (top && (to != CardEffectPile.Draw || NameOf(position!.Value) != "top"))
                    problems.Add($"{property.Location}: destinationPosition supports draw/top only");
                var automatic = fields.LastOrDefault(f => f.Name == "autoSelect");
                if (automatic is not null && automatic.Value is not DslBooleanNode)
                    problems.Add($"{automatic.Location}: autoSelect must be boolean");
                if (from is not (CardEffectPile.Hand or CardEffectPile.Discard)
                    || selectedBody is null && (to is not (CardEffectPile.Hand or CardEffectPile.Discard or CardEffectPile.Exhaust or CardEffectPile.Draw) || from == to)
                    || count is < 0 or > 9999)
                    problems.Add($"{property.Location}: chooseCard requires distinct hand/discard source and combat destination, count=0..9999");
                if (choice.Members.Count != fields.Length)
                    problems.Add($"{property.Location}: chooseCard expects named fields");
                if (selectedBody is not null && position is not null)
                    problems.Add($"{position.Location}: put destinationPosition on moveSelectedCard inside onSelected");
                if (from is not null && filter is not null && count is >= 0 and <= 9999)
                {
                    SelectCardsEffect? select = null;
                    if (selectedBody is not null)
                    {
                        var program = ReadEffectProgram(selectedBody.Value, where + ".onSelected", defaultTarget, cardDamage, xCost,
                            ref problems, out var selectedSummary);
                        summary.AddRange(selectedSummary);
                        if (program.Effects.IsEmpty) problems.Add($"{selectedBody.Location}: onSelected must contain an effect");
                        select = new(from.Value, filter.Value, (int)count, program, property.Location)
                        {
                            AutoSelect = automatic?.Value is DslBooleanNode flag && flag.Value,
                            Prompt = filter == CardEffectCardFilter.Upgradeable ? "Choose a card to upgrade." : "Choose cards.",
                        };
                    }
                    else if (to is not null)
                        select = CardEffectPrimitives.ChooseMove(from.Value, to.Value, (int)count, filter.Value, top,
                            automatic?.Value is DslBooleanNode flag && flag.Value, property.Location);
                    var prompt = fields.LastOrDefault(f => f.Name == "prompt");
                    if (prompt is not null && prompt.Value is not DslStringNode) problems.Add($"{prompt.Location}: prompt must be a string");
                    if (select is not null) effects.Add(prompt?.Value is DslStringNode text ? select with { Prompt = text.Value } : select);
                }
                continue;
            }
            if (property.Name == "upgradeSelectedCard")
            {
                if (property.Value is not DslBlockNode { Members.Count: 0 }) problems.Add($"{property.Location}: upgradeSelectedCard requires an empty block");
                else effects.Add(new UpgradeSelectedCardEffect(property.Location));
                continue;
            }
            if (property.Name == "grantSelectedSly")
            {
                if (property.Value is not DslBlockNode { Members.Count: 0 }) problems.Add($"{property.Location}: grantSelectedSly requires an empty block");
                else effects.Add(new GrantSelectedSlyEffect(property.Location));
                continue;
            }
            if (property.Name == "cloneSelectedCard")
            {
                if (CloneCardStep(property.Value, $"{property.Location}: {where}.cloneSelectedCard", ref problems) is not null)
                    effects.Add(new CloneCardEffect(CardEffectPile.Discard, true, property.Location));
                continue;
            }
            if (property.Name == "upgradeCards")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                if (fields.Length != 1 || fields[0].Name != "pile" || NameOf(fields[0].Value) != "hand")
                    problems.Add($"{property.Location}: upgradeCards requires pile=hand");
                else effects.Add(new ForEachCardEffect(CardEffectPile.Hand, CardEffectCardFilter.Any,
                    new([new UpgradeSelectedCardEffect(property.Location)]), property.Location));
                continue;
            }
            if (property.Name == "cloneCard")
            {
                if (CloneCardStep(property.Value, $"{property.Location}: {where}.cloneCard", ref problems) is not null)
                    effects.Add(new CloneCardEffect(CardEffectPile.Discard, false, property.Location));
                continue;
            }
            if (property.Name == "randomMoveCard" && property.Value is DslBlockNode randomChoice
                && !randomChoice.Members.OfType<DslPropertyNode>().Any(f => f.Name == "after"))
            {
                var fields = randomChoice.Members.OfType<DslPropertyNode>().ToArray();
                foreach (var field in fields.Where(f => f.Name is not ("from" or "to" or "filter" or "onSelected")))
                    problems.Add($"{field.Location}: unsupported randomMoveCard field '{field.Name}'");
                foreach (var duplicate in fields.GroupBy(f => f.Name).Where(g => g.Count() > 1))
                    problems.Add($"{property.Location}: duplicate randomMoveCard field '{duplicate.Key}'");
                if (fields.Length != randomChoice.Members.Count) problems.Add($"{property.Location}: randomMoveCard expects named fields");
                var from = PileOf(fields.LastOrDefault(f => f.Name == "from"), where, ref problems);
                var filter = FilterOf(fields.LastOrDefault(f => f.Name == "filter"), where, ref problems);
                var selected = fields.LastOrDefault(f => f.Name == "onSelected");
                CardEffectProgram body = CardEffectProgram.Empty;
                if (selected is not null)
                {
                    if (fields.Any(f => f.Name == "to")) problems.Add($"{property.Location}: randomMoveCard uses either to or onSelected");
                    body = ReadEffectProgram(selected.Value, where + ".randomSelected", defaultTarget, cardDamage, xCost,
                        ref problems, out var randomSummary);
                    summary.AddRange(randomSummary);
                    if (body.Effects.IsEmpty) problems.Add($"{selected.Location}: onSelected must contain an effect");
                }
                else
                {
                    var to = PileOf(fields.LastOrDefault(f => f.Name == "to"), where, ref problems);
                    if (from is not null && to is not null && from != to)
                        body = new([new MoveSelectedCardEffect(from.Value, to.Value, property.Location) { Manual = true }]);
                    else problems.Add($"{property.Location}: randomMoveCard requires distinct source and destination");
                }
                if (from is not (CardEffectPile.Hand or CardEffectPile.Discard)) problems.Add($"{property.Location}: random source must be hand or discard");
                if (from is not null && filter is not null) effects.Add(new RandomCardEffect(from.Value, filter.Value, body, property.Location));
                continue;
            }
            if (property.Name == "modifyPowerVariable")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                foreach (var f in fields.Where(f => f.Name is not ("power" or "variable" or "amount"))) problems.Add($"{f.Location}: unsupported power variable field");
                var power = fields.LastOrDefault(f => f.Name == "power"); var variable = fields.LastOrDefault(f => f.Name == "variable"); var amount = fields.LastOrDefault(f => f.Name == "amount");
                if (power is null || variable is null || ToId(NameOf(variable.Value)) != "self-damage" || amount?.Value is not DslNumberNode n || !long.TryParse(n.Literal, out var delta))
                    problems.Add($"{property.Location}: modifyPowerVariable requires power, variable=SelfDamage and integer amount");
                else effects.Add(new ModifyPowerVariableEffect(NameOf(power.Value), "self-damage", delta, property.Location));
                continue;
            }
            if (property.Name == "resolveDoom")
            {
                if (property.Value is not DslBlockNode doomBlock || doomBlock.Members.Count != 0)
                    problems.Add($"{property.Location}: resolveDoom requires an empty block");
                else effects.Add(new ResolveDoomCardEffect(property.Location));
                continue;
            }
            if (property.Name is "summonOsty" or "healOsty" or "loseOstyHp" or "ostyAttack")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                if (property.Value is not DslBlockNode || fields.Length != 1 || fields[0].Name != "amount")
                    problems.Add($"{property.Location}: {property.Name} requires exactly amount");
                else effects.Add(new OstyCardEffect(property.Name, ReadNumericAmount(fields[0].Value, ref problems), property.Location));
                continue;
            }
            if (property.Name is "channelOrb" or "evokeOrb" or "addOrbSlots" or "removeOrbSlots")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                var kind = fields.LastOrDefault(field => field.Name == "kind");
                var amount = fields.LastOrDefault(field => field.Name == "amount");
                var dequeue = fields.LastOrDefault(field => field.Name == "dequeue");
                if (property.Value is not DslBlockNode || fields.GroupBy(field => field.Name).Any(group => group.Count() > 1)
                    || fields.Any(field => field.Name != "amount" && !(property.Name == "channelOrb" && field.Name == "kind") && !(property.Name == "evokeOrb" && field.Name == "dequeue"))
                    || property.Name == "channelOrb" && (kind is null || NameOf(kind.Value) is not ("Lightning" or "Frost" or "Dark" or "Plasma" or "Glass"))
                    || dequeue is not null && NameOf(dequeue.Value) is not ("true" or "false"))
                    problems.Add($"{property.Location}: invalid {property.Name} fields or orb kind");
                else effects.Add(new OrbCardEffect(property.Name, kind is null ? "" : NameOf(kind.Value),
                    amount is null ? CardEffectAmount.Literal(1) : ReadNumericAmount(amount.Value, ref problems), dequeue is null || NameOf(dequeue.Value) == "true", property.Location));
                continue;
            }
            if (property.Name == "forge")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                var card = fields.LastOrDefault(field => field.Name == "card");
                var variable = fields.LastOrDefault(field => field.Name == "variable");
                var amount = fields.LastOrDefault(field => field.Name == "amount");
                if (card is null || variable is null || amount is null || fields.Any(field => field.Name is not ("card" or "variable" or "amount"))
                    || fields.GroupBy(field => field.Name).Any(group => group.Count() > 1))
                    problems.Add($"{property.Location}: forge requires card, variable and amount");
                else effects.Add(new ForgeCardEffect(NameOf(card.Value).Contains(':') ? NameOf(card.Value) : ToId(NameOf(card.Value)), ToId(NameOf(variable.Value)), ReadNumericAmount(amount.Value, ref problems), property.Location));
                continue;
            }
            if (property.Name is "gainMaxHp" or "gainStars" or "loseStars" or "setStars")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                var amount = fields.LastOrDefault(f => f.Name == "amount");
                var target = fields.LastOrDefault(f => f.Name == "target");
                if (amount is null || fields.Any(f => f.Name is not ("amount" or "target"))
                    || fields.GroupBy(f => f.Name).Any(g => g.Count() > 1)
                    || target is not null && NameOf(target.Value) != "context.owner")
                    problems.Add($"{property.Location}: {property.Name} requires amount and optional target=context.owner");
                else effects.Add(new PrimitiveCardEffect(property.Name switch
                    {
                        "gainStars" => CardEffectOperation.GainStars,
                        "loseStars" => CardEffectOperation.LoseStars,
                        "setStars" => CardEffectOperation.SetStars,
                        _ => CardEffectOperation.GainMaxHp,
                    }, CardEffectTarget.Owner,
                    ReadNumericAmount(amount.Value, ref problems), "", property.Location));
                continue;
            }
            if (property.Name == "attackVariable")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                var variable = fields.SingleOrDefault(f => f.Name == "variable");
                if (fields.Length != 1 || variable is null) problems.Add($"{property.Location}: attackVariable requires variable");
                else effects.Add(new PrimitiveCardEffect(CardEffectOperation.Damage, CardEffectTarget.Selected,
                    new(CardAmountSource.Variable,0,1,ToId(NameOf(variable.Value))),"",property.Location));
                continue;
            }
            if (property.Name == "randomMoveCard" && property.Value is DslBlockNode randomBlock && randomBlock.Members.OfType<DslPropertyNode>().Any(f => f.Name == "after"))
            {
                var fields = randomBlock.Members.OfType<DslPropertyNode>().ToArray();
                string Name(string key) => fields.LastOrDefault(f => f.Name == key) is { } p ? NameOf(p.Value) : "";
                var after = fields.LastOrDefault(f => f.Name == "after")?.Value as DslBlockNode;
                var grow = after?.Members.OfType<DslPropertyNode>().SingleOrDefault();
                var variable = (grow?.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().SingleOrDefault();
                if (fields.Any(f => f.Name is not ("from" or "to" or "filter" or "after")) || Name("from")!="hand" || Name("to")!="exhaust" || Name("filter")!="attack"
                    || grow?.Name!="accumulateCardDamage" || variable?.Name!="variable") problems.Add($"{property.Location}: unsupported random move growth shape");
                else effects.Add(new ExhaustAttackAndGrowEffect(ToId(NameOf(variable.Value)),property.Location));
                continue;
            }
            if (property.Name == "autoPlaySelectedCard")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                if(fields.Length!=1 || fields[0].Name!="from" || NameOf(fields[0].Value)!="exhaust") problems.Add($"{property.Location}: automatic self play supports from=exhaust only");
                else effects.Add(new AutoPlaySelfEffect(CardEffectPile.Exhaust,property.Location));
                continue;
            }
            if (property.Name == "let")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                var name = fields.LastOrDefault(f => f.Name == "name");
                var valueField = fields.LastOrDefault(f => f.Name == "value");
                var bodyField = fields.LastOrDefault(f => f.Name == "effect");
                if (fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != 3 || fields.Length != 3 || name is null || valueField is null || bodyField is null || NameOf(name.Value).Length == 0)
                    problems.Add($"{property.Location}: let requires name, value and effect");
                else
                {
                    var amount = ReadNumericAmount(valueField.Value, ref problems);
                    var body = ReadEffectProgram(bodyField.Value, where + ".let", defaultTarget, cardDamage, xCost,
                        ref problems, out var bodySummary);
                    summary.AddRange(bodySummary);
                    effects.Add(new WithCountCardEffect(NameOf(name.Value), amount, body, property.Location));
                }
                continue;
            }
            if (property.Name == "if")
            {
                if (property.Value is not DslBlockNode branch)
                {
                    problems.Add($"{property.Location}: if must be a block");
                    continue;
                }
                var fields = branch.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields.Where(f => f.Name is not ("condition" or "then" or "else")))
                    problems.Add($"{field.Location}: unsupported if field '{field.Name}'");
                var condition = fields.LastOrDefault(f => f.Name == "condition");
                var then = fields.LastOrDefault(f => f.Name == "then");
                var otherwise = fields.LastOrDefault(f => f.Name == "else");
                if (condition is null || then is null)
                {
                    problems.Add($"{property.Location}: if requires condition and then");
                    continue;
                }
                var predicate = ConditionOf(condition.Value, where, defaultTarget, ref problems);
                var thenProgram = ReadEffectProgram(then.Value, where + ".then", defaultTarget, cardDamage, xCost, ref problems, out var thenSummary);
                var elseProgram = otherwise is null ? CardEffectProgram.Empty
                    : ReadEffectProgram(otherwise.Value, where + ".else", defaultTarget, cardDamage, xCost, ref problems, out _);
                // Conditional summaries are represented by the legacy conditional metadata, not fixed steps.
                if (predicate.Length > 0)
                    effects.Add(new ConditionalCardEffect(CardEffectCondition.FromAuthor(predicate), thenProgram, elseProgram, property.Location));
                continue;
            }
            if (property.Name == "repeat")
            {
                if (property.Value is not DslBlockNode repeat)
                {
                    problems.Add($"{property.Location}: repeat must be a block");
                    continue;
                }
                var fields = repeat.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields.Where(f => f.Name is not ("count" or "effect")))
                    problems.Add($"{field.Location}: unsupported repeat field '{field.Name}'");
                var count = fields.LastOrDefault(f => f.Name == "count");
                var body = fields.LastOrDefault(f => f.Name == "effect");
                if (count?.Value is DslCallNode or DslNameNode && body is not null)
                {
                    var dynamicCount = ReadNumericAmount(count.Value, ref problems);
                    var dynamicBody = ReadEffectProgram(body.Value, where + ".repeat", defaultTarget, cardDamage, xCost, ref problems, out var dynamicSummary);
                    effects.Add(new DynamicRepeatCardEffect(dynamicCount, dynamicBody, property.Location));
                    summary.AddRange(dynamicSummary);
                    continue;
                }
                if (count?.Value is not DslNumberNode number || !int.TryParse(number.Literal, out var repetitions)
                    || repetitions < 0 || body is null)
                {
                    problems.Add($"{property.Location}: repeat requires a non-negative integer count and an effect block");
                    continue;
                }
                var program = ReadEffectProgram(body.Value, where + ".repeat", defaultTarget, cardDamage, xCost, ref problems, out var repeatedSummary);
                effects.Add(new RepeatCardEffect(repetitions, program, property.Location));
                summary.AddRange(repeatedSummary.Select(p => p with { Count = p.Count * repetitions }));
                continue;
            }
            if (property.Name is "repeatForX" or "repeatForCount")
            {
                if (property.Value is not DslBlockNode loop)
                {
                    problems.Add($"{property.Location}: {property.Name} must be a block");
                    continue;
                }
                var fields = loop.Members.OfType<DslPropertyNode>().ToList();
                var allowed = property.Name == "repeatForX"
                    ? (IReadOnlyList<string>)["effect"]
                    : ["base", "perCount", "count", "effect"];
                foreach (var field in fields.Where(f => !allowed.Contains(f.Name)))
                    problems.Add($"{field.Location}: unsupported {property.Name} field '{field.Name}'");
                var body = fields.LastOrDefault(f => f.Name == "effect");
                if (body is null)
                {
                    problems.Add($"{property.Location}: {property.Name} requires an effect block");
                    continue;
                }
                // `repeatForX` reads the payment boundary captured by the shared play procedure;
                // `repeatForCount` takes `base + perCount * count` from live state once, on entry.
                var count = property.Name == "repeatForX"
                    ? new CardEffectAmount(CardAmountSource.PaidX, 0, 1)
                    : AmountFromCountFields(fields, property.Location.ToString(), ref problems);
                var program = ReadEffectProgram(body.Value, where + "." + property.Name, defaultTarget,
                    cardDamage, xCost, ref problems, out var loopSummary);
                effects.Add(new DynamicRepeatCardEffect(count, program, property.Location));
                // A dynamic count cannot be folded into fixed counts; keep the body's shapes for the
                // legacy inspection summary only.
                summary.AddRange(loopSummary);
                continue;
            }
            if (property.Name == "modifyCardVariable")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields?.Where(f => f.Name is not ("variable" or "amount")) ?? [])
                    problems.Add($"{field.Location}: unsupported modifyCardVariable field '{field.Name}'");
                var name = fields?.LastOrDefault(f => f.Name == "variable");
                var delta = fields?.LastOrDefault(f => f.Name == "amount");
                if (name is null || delta?.Value is not DslNumberNode number || !long.TryParse(number.Literal, out var amount))
                    problems.Add($"{property.Location}: modifyCardVariable requires variable and integer amount");
                else
                    effects.Add(new ModifyCardVariableEffect(ToId(NameOf(name.Value)), amount, property.Location));
                continue;
            }
            // ENV-T05 `withCount { name count effect }`：**词法快照**。进入时把 `count` 求值一次并绑定
            // 到 `name`；body 里的 `savedCount { name }` 读这个绑定。源语义（Stoke / FiendFire）是
            // 「先量手牌 → 再消耗手牌 → 最后按量到的张数结算」，所以快照必须早于消耗，且不随后续
            // 牌区变化重算。绑定与 body 的词法关系由 WithCountCardEffect 表达，编译期解析名字。
            if (property.Name == "withCount")
            {
                if (property.Value is not DslBlockNode counted)
                {
                    problems.Add($"{property.Location}: withCount must be a block");
                    continue;
                }
                var fields = counted.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields.Where(f => f.Name is not ("name" or "count" or "effect")))
                    problems.Add($"{field.Location}: unsupported withCount field '{field.Name}'");
                var nameField = fields.LastOrDefault(f => f.Name == "name");
                var countField = fields.LastOrDefault(f => f.Name == "count");
                var countedBody = fields.LastOrDefault(f => f.Name == "effect");
                if (nameField is null || countField is null || countedBody is null)
                {
                    problems.Add($"{property.Location}: withCount requires name, count and effect");
                    continue;
                }
                var savedName = NameOf(nameField.Value);
                if (savedName.Length == 0)
                {
                    problems.Add($"{property.Location}: withCount requires a name");
                    continue;
                }
                var source = CountSourceOf(countField.Value, where, ref problems);
                var countedProgram = ReadEffectProgram(countedBody.Value, where + ".withCount", defaultTarget,
                    cardDamage, xCost, ref problems, out var countedSummary);
                // 动态数量的 body 形状不能折进固定步骤；只留给旧摘要做检查。
                summary.AddRange(countedSummary);
                if (source.Length > 0)
                    effects.Add(new WithCountCardEffect(savedName, CardEffectAmount.FromAuthor(source, 0, 1),
                        countedProgram, property.Location));
                continue;
            }
            // ENV-T05 `forEachCard { from filter effect }`：按牌区（可带 `filter = attack|nonAttack`）逐张
            // 绑定"当前牌"，对每张跑一次 body。迭代集合在引擎 `ForEachInst` 进入时快照，所以 body 里
            // 把牌移出牌区不会打乱遍历（SecondWind / Stoke / PrimalForce / FiendFire 依赖这一点）。
            if (property.Name == "forEachCard")
            {
                if (property.Value is not DslBlockNode loop)
                {
                    problems.Add($"{property.Location}: forEachCard must be a block");
                    continue;
                }
                var fields = loop.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields.Where(f => f.Name is not ("from" or "filter" or "effect")))
                    problems.Add($"{field.Location}: unsupported forEachCard field '{field.Name}'");
                var fromField = fields.LastOrDefault(f => f.Name == "from");
                var filterField = fields.LastOrDefault(f => f.Name == "filter");
                var loopBody = fields.LastOrDefault(f => f.Name == "effect");
                if (fromField is null || loopBody is null)
                {
                    problems.Add($"{property.Location}: forEachCard requires from and effect");
                    continue;
                }
                var from = PileOf(fromField, property.Location.ToString(), ref problems);
                var filter = FilterOf(filterField, property.Location.ToString(), ref problems);
                var loopProgram = ReadEffectProgram(loopBody.Value, where + ".forEachCard", defaultTarget,
                    cardDamage, xCost, ref problems, out var loopSummary);
                summary.AddRange(loopSummary);
                if (from is not null && filter is not null)
                    effects.Add(new ForEachCardEffect(from.Value, filter.Value, loopProgram, property.Location));
                continue;
            }
            // ENV-T05 `moveSelectedCard { from to }`：把 forEachCard 绑定的当前牌移到另一张牌区。
            // 目标为消耗区时由编译器补「记一次消耗 + 触发 afterExhaust」（与既有 `exhaust_from_hand` 同口径）。
            if (property.Name == "moveSelectedCard")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields?.Where(f => f.Name is not ("from" or "to" or "destinationPosition")) ?? [])
                    problems.Add($"{field.Location}: unsupported moveSelectedCard field '{field.Name}'");
                var from = PileOf(fields?.LastOrDefault(f => f.Name == "from"), property.Location.ToString(), ref problems);
                var to = PileOf(fields?.LastOrDefault(f => f.Name == "to"), property.Location.ToString(), ref problems);
                if (from is null || to is null)
                {
                    problems.Add($"{property.Location}: moveSelectedCard requires from and to");
                    continue;
                }
                var position = fields?.LastOrDefault(f => f.Name == "destinationPosition");
                var top = position is not null;
                if (top && (to != CardEffectPile.Draw || NameOf(position!.Value) != "top"))
                    problems.Add($"{property.Location}: destinationPosition supports draw/top only");
                effects.Add(new MoveSelectedCardEffect(from.Value, to.Value, property.Location) { Manual = true, Top = top });
                continue;
            }
            // ENV-T05 `transformSelectedCard { card from upgraded }`：把当前牌变成另一个**定义**
            // （PrimalForce 把手牌里的攻击牌变成 GiantRock；升级形态由 `upgraded = true` 指定）。
            // 卡 id 在这里只带出**局部名**，包前缀与存在性由链接/运行时解析。
            if (property.Name == "transformSelectedCard")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToList();
                foreach (var field in fields?.Where(f => f.Name is not ("card" or "from" or "upgraded")) ?? [])
                    problems.Add($"{field.Location}: unsupported transformSelectedCard field '{field.Name}'");
                var cardField = fields?.LastOrDefault(f => f.Name == "card");
                var from = PileOf(fields?.LastOrDefault(f => f.Name == "from"), property.Location.ToString(), ref problems);
                var upgraded = fields?.LastOrDefault(f => f.Name == "upgraded")?.Value is DslBooleanNode flag
                    && flag.Value;
                var cardName = cardField is null ? string.Empty : NameOf(cardField.Value);
                if (cardName.Length == 0 || from is null)
                {
                    problems.Add($"{property.Location}: transformSelectedCard requires card and from");
                    continue;
                }
                effects.Add(new TransformSelectedCardEffect(ToId(cardName), from.Value, upgraded, property.Location));
                continue;
            }
            if (property.Name == "drawUntil")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                foreach (var field in fields.Where(f => f.Name != "type")) problems.Add($"{field.Location}: unsupported drawUntil field '{field.Name}'");
                var kindField = fields.LastOrDefault(f => f.Name == "type");
                var kind = kindField is null ? "" : NameOf(kindField.Value);
                if (kind is not ("attack" or "skill" or "power" or "status")) problems.Add($"{property.Location}: drawUntil requires type=attack|skill|power|status");
                else effects.Add(new DrawUntilCardEffect(char.ToUpperInvariant(kind[0]) + kind[1..], property.Location));
                continue;
            }
            if (property.Name == "autoPlay")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                foreach (var field in fields.Where(f => f.Name is not ("from" or "count" or "forceExhaust")))
                    problems.Add($"{field.Location}: unsupported autoPlay field '{field.Name}'");
                var fromField = fields.LastOrDefault(f => f.Name == "from");
                if (fromField is null || NameOf(fromField.Value) != "draw")
                    problems.Add($"{property.Location}: autoPlay supports from=draw (top) only");
                var exhaust = fields.LastOrDefault(f => f.Name == "forceExhaust");
                if (exhaust is not null && exhaust.Value is not DslBooleanNode) problems.Add($"{exhaust.Location}: forceExhaust must be boolean");
                effects.Add(new AutoPlayCardEffect(ReadCommandCount(fields.LastOrDefault(f => f.Name == "count"), ref problems),
                    exhaust?.Value is DslBooleanNode flag && flag.Value, property.Location));
                continue;
            }
            if (property.Name == "generateCards")
            {
                var fields = (property.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>().ToArray() ?? [];
                foreach (var field in fields.Where(f => f.Name is not ("count" or "pool" or "type" or "upgraded" or "distinct" or "cost" or "choose" or "allowCancel")))
                    problems.Add($"{field.Location}: unsupported generateCards field '{field.Name}'");
                var pool = fields.LastOrDefault(f => f.Name == "pool")?.Value as DslBlockNode;
                if (pool is null) problems.Add($"{property.Location}: generateCards requires an explicit pool; the complete character generation pool is not registered yet");
                var characterPool = pool?.Members.OfType<DslPropertyNode>().SingleOrDefault(f => f.Name == "character");
                var ids = pool?.Members.OfType<DslPropertyNode>().Where(f => f.Name == "card").Select(f => ToId(NameOf(f.Value))).ToImmutableArray() ?? [];
                if (pool is not null && characterPool is null && (ids.Length == 0 || pool.Members.Any(f => f is not DslPropertyNode p || p.Name != "card")))
                    problems.Add($"{property.Location}: pool requires one or more card = Definition entries");
                var count = ReadCommandCount(fields.LastOrDefault(f => f.Name == "count"), ref problems);
                var filter = FilterOf(fields.LastOrDefault(f => f.Name == "type"), property.Location.ToString(), ref problems);
                if (filter is CardEffectCardFilter.Upgradeable or CardEffectCardFilter.NonSlySkill) problems.Add($"{property.Location}: generation type supports attack or nonAttack only");
                bool Flag(string name) => fields.LastOrDefault(f => f.Name == name)?.Value is DslBooleanNode flag && flag.Value;
                foreach (var field in fields.Where(f => f.Name is "upgraded" or "distinct" or "choose" or "allowCancel"))
                    if (field.Value is not DslBooleanNode) problems.Add($"{field.Location}: {field.Name} must be boolean");
                if (Flag("choose") && (!Flag("distinct") || count.Source != CardAmountSource.Literal || count.Constant is < 1 or > 999 || count.Operator is not null))
                    problems.Add($"{property.Location}: choose generation requires distinct=true and literal count=1..999");
                if (Flag("allowCancel") && !Flag("choose")) problems.Add($"{property.Location}: allowCancel requires choose=true");
                if (Flag("distinct") && !Flag("choose") && count != CardEffectAmount.Literal(1)) problems.Add($"{property.Location}: direct distinct generation currently supports literal count=1 only");
                var cost = fields.LastOrDefault(f => f.Name == "cost");
                if (cost is not null && (cost.Value is not DslNumberNode n || n.Literal != "0"))
                    problems.Add($"{cost.Location}: explicit generation supports cost=0 (free through first play or turn end) only");
                effects.Add(new GenerateCardsEffect(count, filter ?? CardEffectCardFilter.Any, Flag("upgraded"), Flag("distinct"),
                    cost is not null, property.Location) { Pool = ids, CharacterPool = characterPool is null ? "" : NameOf(characterPool.Value), Choose = Flag("choose"), AllowCancel = Flag("allowCancel") });
                continue;
            }
            var numericProperties = property.Value is DslBlockNode numericBlock
                ? numericBlock.Members.OfType<DslPropertyNode>().Where(p => p.Name is "damage" or "amount" or "stacks" or "count")
                    .Where(p => p.Value is DslCallNode or DslNameNode
                        || p.Name != "count" && p.Value is DslBlockNode { Members.Count: 1 }
                            && property.Name != "modifyEnergy").ToArray() : [];
            var numericExpressions = new Dictionary<string, CardEffectAmount>(StringComparer.Ordinal);
            foreach (var numeric in numericProperties)
                if (!numericExpressions.TryAdd(numeric.Name, ReadNumericAmount(numeric.Value, ref problems)))
                    problems.Add($"{numeric.Location}: duplicate numeric field '{numeric.Name}'");
            var primitiveProperty = numericProperties.Length == 0 ? property : property with
            {
                Value = ((DslBlockNode)property.Value) with
                {
                    Members = ((DslBlockNode)property.Value).Members.Select(m => m is DslPropertyNode p && numericProperties.Contains(p)
                        ? p with { Value = new DslNumberNode(p.Name == "count" ? "1" : "0", p.Location) } : m).ToArray(),
                },
            };
            // Existing leaf readers remain the syntax authority; numeric expressions bypass only their scalar summary.
            var problemsBefore = problems.Count;
            var primitives = PrimitivesOf(new DslBlockNode([primitiveProperty], block.Location), where, ref problems);
            for (var i = problemsBefore; i < problems.Count; i++) problems[i] = $"{property.Location}: {problems[i]}";
            foreach (var raw in primitives)
            {
                var primitive = raw.Target.Length == 0
                    ? raw with { Target = defaultTarget == "self" ? "owner" : "target" } : raw;
                summary.Add(raw);
                var operation = primitive.Name switch
                {
                    "attack" or "scaledAttack" or "attackUsingBlock" or "randomAttack" => CardEffectOperation.Damage,
                    "gainBlock" or "gainBlockFromCurrentBlock" => CardEffectOperation.GainBlock,
                    "modifyEnergy" => CardEffectOperation.GainEnergy,
                    "draw" => CardEffectOperation.Draw,
                    "loseHp" => CardEffectOperation.LoseHp,
                    "heal" => CardEffectOperation.Heal,
                    "applyPower" => CardEffectOperation.ApplyPower,
                    "on-fatal-gain-max-hp" => CardEffectOperation.GainMaxHp,
                    _ => throw new InvalidOperationException($"{property.Location}: unmapped primitive '{primitive.Name}'"),
                };
                var target = operation is CardEffectOperation.GainBlock or CardEffectOperation.GainEnergy or CardEffectOperation.Draw
                    or CardEffectOperation.Heal or CardEffectOperation.LoseHp
                    ? CardEffectTarget.Owner
                    : primitive.Name == "randomAttack" ? CardEffectTarget.RandomEnemy
                    : primitive.Target switch
                    {
                        "owner" => CardEffectTarget.Owner,
                        "target" => CardEffectTarget.Selected,
                        "allEnemies" => CardEffectTarget.AllEnemies,
                        "randomEnemy" => CardEffectTarget.RandomEnemy,
                        _ => CardEffectTarget.Selected,
                    };
                if (operation == CardEffectOperation.LoseHp && raw.Target is not ("" or "owner"))
                    problems.Add($"{property.Location}: loseHp only supports the owner");
                if (operation == CardEffectOperation.Damage && target == CardEffectTarget.Owner)
                    problems.Add($"{property.Location}: attack against the owner is not supported by the enemy damage procedure");
                if (raw.Target.StartsWith("unsupported:", StringComparison.Ordinal))
                    problems.Add($"{property.Location}: unsupported target '{raw.Target}'");
                var amount = CardEffectAmount.Literal(operation == CardEffectOperation.ApplyPower ? primitive.Stacks : primitive.Amount);
                if (primitive.AmountSource.Length > 0)
                    amount = CardEffectAmount.FromAuthor(primitive.AmountSource, primitive.AmountBase, primitive.AmountPerCount);
                else if (primitive.Name == "attackUsingBlock" && cardDamage is { } damage)
                    amount = CardEffectAmount.FromAuthor(damage.Source, damage.Base, damage.IsScaled ? damage.PerCount : 0);
                else if (operation == CardEffectOperation.Damage && primitive.Name == "attackUsingBlock")
                    amount = new(CardAmountSource.Block, 0, 1);
                // ENV-T05：X 费不再**隐式**改写整张卡的数值叶。源语义里 X 的位置是逐卡声明的：
                // Whirlwind 是「每次 5/8 点、命中 X 次」（X 只出现在**重复次数**上），把它读成
                // 「伤害 = X」会算成 3 次 × 3 点而不是 3 次 × 5 点。因此 X 只由作者显式写出的两条
                // 路径进入：`repeatForX`（次数 = 已付 X）与 `energyX` 数值来源。
                // 正式内容里 `cost = X` 的卡只有 Whirlwind 与 Cascade，后者的 play 里没有数值叶。
                // `xCost` 仍留在签名里，留给后续需要「叶级 X」的卡显式声明时使用（那时也要写成
                // 显式字段，不能再靠整卡通配）。
                var amountKey = operation == CardEffectOperation.ApplyPower ? "stacks"
                    : operation == CardEffectOperation.Damage ? "damage" : "amount";
                if (numericExpressions.TryGetValue(amountKey, out var numericExpression)) amount = numericExpression;
                CardEffect leaf = new PrimitiveCardEffect(operation, target, amount, primitive.PowerId, property.Location);
                if (primitive.Name == "on-fatal-gain-max-hp") leaf = CardEffectPrimitives.OnFatal(amount, property.Location);
                if (numericExpressions.TryGetValue("count", out var dynamicCount))
                    leaf = new DynamicRepeatCardEffect(dynamicCount, new([leaf]), property.Location);
                else if (primitive.Count != 1)
                {
                    if (primitive.Count < 0)
                        problems.Add($"{property.Location}: effect repeat count must be non-negative");
                    else
                        leaf = new RepeatCardEffect(primitive.Count, new([leaf]), property.Location);
                }
                effects.Add(leaf);
            }
        }
        return new(effects.ToImmutable());
    }

    /// <summary>
    /// Reads the inline `base` / `perCount` / `count` trio that `repeatForCount` and `autoPlay` carry,
    /// so a repetition/生成 count uses the same typed amount sources as a damage amount.
    /// </summary>
    private static CardEffectAmount AmountFromCountFields(IReadOnlyList<DslPropertyNode> fields, string where,
        ref List<string> problems)
    {
        var count = fields.LastOrDefault(f => f.Name == "count")?.Value;
        var baseValue = IntegerFieldOf(fields, "base", where, 0, ref problems);
        var perCount = IntegerFieldOf(fields, "perCount", where, 1, ref problems);
        if (count is null)
        {
            problems.Add($"{where}: repeatForCount requires a count source");
            return CardEffectAmount.Literal(baseValue);
        }
        var source = CountSourceOf(count, where, ref problems);
        return source.Length == 0
            ? CardEffectAmount.Literal(baseValue)
            : CardEffectAmount.FromAuthor(source, baseValue, perCount);
    }

    private static CardEffectAmount ReadNumericAmount(DslValueNode node, ref List<string> problems)
    {
        if (node is DslNumberNode number && long.TryParse(number.Literal, out var literal))
            return CardEffectAmount.Literal(literal);
        if (node is DslNameNode name)
        {
            if (name.Name == "PaidX") return new(CardAmountSource.PaidX, 0, 1);
            if (name.Name == "PaidStarX") return new(CardAmountSource.PaidStarX, 0, 1);
            if (name.Name == "context.owner.block") return new(CardAmountSource.Block, 0, 1);
            if (name.Name.StartsWith("variables.", StringComparison.Ordinal))
                return new(CardAmountSource.Variable, 0, 1, ToId(name.Name[10..]));
            if (name.Name.StartsWith("locals.", StringComparison.Ordinal))
                return new(CardAmountSource.SavedCount, 0, 1, name.Name[7..]);
        }
        if (node is DslCallNode call)
        {
            if (call.Name == "enemyPowerSum" && call.Arguments.Count == 1 && call.Arguments[0].Name == "power")
                return new(CardAmountSource.EnemyPowerSum,0,1,NameOf(call.Arguments[0].Value));
            if (call.Name is "add" or "subtract" or "multiply" or "divide" or "minimum" or "maximum")
            {
                if (call.Arguments.Count != 2 || call.Arguments.Any(a => a.Name is not null))
                    problems.Add($"{call.Location}: {call.Name} requires two positional numeric operands");
                else
                {
                    var operands = ImmutableArray.CreateBuilder<CardEffectAmount>();
                    foreach (var argument in call.Arguments) operands.Add(ReadNumericAmount(argument.Value, ref problems));
                    return CardEffectAmount.Literal(0) with { Operator = call.Name, Operands = operands.ToImmutable() };
                }
            }
            else if (call.Name == "pileCount" && call.Arguments.Count == 1 && call.Arguments[0].Name == "pile")
            {
                var source = NameOf(call.Arguments[0].Value) switch
                {
                    "hand" => CardAmountSource.Hand, "draw" => CardAmountSource.Draw,
                    "discard" => CardAmountSource.Discard, "exhaust" => CardAmountSource.Exhaust,
                    _ => (CardAmountSource?)null,
                };
                if (source is not null) return new(source.Value, 0, 1);
            }
            else if (call.Name == "powerAmount" && call.Arguments.Count == 2
                && call.Arguments.Count(a => a.Name == "target") == 1 && call.Arguments.Count(a => a.Name == "power") == 1
                && NameOf(call.Arguments.Single(a => a.Name == "target").Value) == "context.target")
                return new(CardAmountSource.TargetPower, 0, 1, NameOf(call.Arguments.Single(a => a.Name == "power").Value));
        }
        if (node is DslBlockNode { Members.Count: 1 } block)
        {
            var source = CountSourceOf(block, node.Location.ToString(), ref problems);
            if (source.Length > 0) return CardEffectAmount.FromAuthor(source, 0, 1);
        }
        problems.Add($"{node.Location}: invalid numeric expression or missing explicit context");
        return CardEffectAmount.Literal(0);
    }

    private static CardEffectAmount ReadCommandCount(DslPropertyNode? count, ref List<string> problems)
    {
        if (count?.Value is DslCallNode or DslNameNode) return ReadNumericAmount(count.Value, ref problems);
        if (count?.Value is DslNumberNode number && long.TryParse(number.Literal, out var value) && value >= 0) return CardEffectAmount.Literal(value);
        if (count?.Value is DslBlockNode block)
        {
            if (block.Members.OfType<DslPropertyNode>().FirstOrDefault(f => f.Name == "scaled")?.Value is DslBlockNode scaled)
                return AmountFromCountFields(scaled.Members.OfType<DslPropertyNode>().ToArray(), count.Location.ToString(), ref problems);
            var source = CountSourceOf(block, count.Location.ToString(), ref problems);
            if (source.Length > 0) return CardEffectAmount.FromAuthor(source, 0, 1);
        }
        problems.Add($"{count?.Location}: command requires a non-negative count or a typed count source");
        return CardEffectAmount.Literal(0);
    }

    private static long IntegerFieldOf(IReadOnlyList<DslPropertyNode> fields, string name, string where, long fallback,
        ref List<string> problems)
    {
        var field = fields.LastOrDefault(f => f.Name == name);
        if (field is null) return fallback;
        if (field.Value is DslNumberNode number && long.TryParse(number.Literal, out var value)) return value;
        problems.Add($"{field.Location}: {where} field '{name}' must be an integer literal");
        return fallback;
    }

    /// <summary>
    /// ENV-T05：`from` / `to` 的牌区名 → 类型化牌区。作者写的是源里的短名
    /// （`hand` / `draw` / `discard` / `exhaust`），写错必须显式拒绝，不能猜一个默认牌区。
    /// </summary>
    private static CardEffectPile? PileOf(DslPropertyNode? field, string where, ref List<string> problems)
    {
        if (field is null)
        {
            problems.Add($"{where}: missing pile");
            return null;
        }
        var name = NameOf(field.Value);
        var pile = name switch
        {
            "hand" or "Hand" => (CardEffectPile?)CardEffectPile.Hand,
            "draw" or "drawPile" or "Draw" => CardEffectPile.Draw,
            "discard" or "discardPile" or "Discard" => CardEffectPile.Discard,
            "exhaust" or "exhaustPile" or "Exhaust" => CardEffectPile.Exhaust,
            "inPlay" or "play" or "InPlay" => CardEffectPile.InPlay,
            _ => null,
        };
        if (pile is null) problems.Add($"{field.Location}: unsupported pile '{name}'");
        return pile;
    }

    /// <summary>
    /// ENV-T05：`forEachCard` 的 `filter`（缺省 `Any`）。当前内容只用 `attack` / `nonAttack`；
    /// 其他谓词显式拒绝，不用"匹配一切"悄悄放过。
    /// </summary>
    private static CardEffectCardFilter? FilterOf(DslPropertyNode? field, string where, ref List<string> problems)
    {
        if (field is null) return CardEffectCardFilter.Any;
        var name = NameOf(field.Value);
        var filter = name switch
        {
            "attack" or "Attack" => (CardEffectCardFilter?)CardEffectCardFilter.Attack,
            "nonAttack" or "NonAttack" or "skill" => CardEffectCardFilter.NonAttack,
            "upgradeable" => CardEffectCardFilter.Upgradeable,
            "nonSlySkill" => CardEffectCardFilter.NonSlySkill,
            _ => null,
        };
        if (filter is null) problems.Add($"{field.Location}: unsupported forEachCard filter '{name}'");
        return filter;
    }

    private static void ValidateEffectVariables(CardEffectProgram program, IReadOnlyDictionary<string, int> variables,
        ref List<string> problems)
    {
        var amountProblems = new List<string>();
        void CheckAmount(CardEffectAmount amount, DslSourceLocation source)
        {
            if (amount.Source == CardAmountSource.Variable && !variables.ContainsKey(amount.Reference))
                amountProblems.Add($"{source}: undeclared card variable '{amount.Reference}'");
            foreach (var operand in amount.Operands) CheckAmount(operand, source);
        }
        foreach (var effect in program.Effects)
        {
            switch (effect)
            {
                case PrimitiveCardEffect leaf: CheckAmount(leaf.Amount, leaf.Source); break;
                case ForgeCardEffect forge: CheckAmount(forge.Amount, forge.Source); break;
                case OrbCardEffect orb: CheckAmount(orb.Amount, orb.Source); break;
                case OstyCardEffect osty: CheckAmount(osty.Amount, osty.Source); break;
                case DynamicRepeatCardEffect dynamicAmount: CheckAmount(dynamicAmount.Count, dynamicAmount.Source); break;
                case WithCountCardEffect count: CheckAmount(count.Count, count.Source); break;
                case GenerateCardsEffect generate: CheckAmount(generate.Count, generate.Source); break;
                case AutoPlayCardEffect auto: CheckAmount(auto.Count, auto.Source); break;
            }
            var name = effect switch
            {
                ModifyCardVariableEffect change => change.Variable,
                ExhaustAttackAndGrowEffect grow => grow.Variable,
                PrimitiveCardEffect { Amount.Source: CardAmountSource.Variable } leaf => leaf.Amount.Reference,
                _ => "",
            };
            if (name.Length > 0 && !variables.ContainsKey(name))
                problems.Add($"{effect.Source}: undeclared card variable '{name}'");
            if (effect is ConditionalCardEffect branch)
            {
                ValidateEffectVariables(branch.Then, variables, ref problems);
                ValidateEffectVariables(branch.Else, variables, ref problems);
            }
            if (effect is RepeatCardEffect repeat) ValidateEffectVariables(repeat.Body, variables, ref problems);
            if (effect is DynamicRepeatCardEffect dynamic)
                ValidateEffectVariables(dynamic.Body, variables, ref problems);
            if (effect is WithCountCardEffect withCount)
                ValidateEffectVariables(withCount.Body, variables, ref problems);
            if (effect is ForEachCardEffect each)
                ValidateEffectVariables(each.Body, variables, ref problems);
            if (effect is SelectCardsEffect select)
                ValidateEffectVariables(select.OnSelected, variables, ref problems);
            if (effect is RandomCardEffect random)
                ValidateEffectVariables(random.OnSelected, variables, ref problems);
        }
        problems.AddRange(amountProblems);
    }

    private static void ValidateHookProgram(CardEffectProgram program, ref List<string> problems)
    {
        foreach (var effect in program.Effects)
        {
            if (effect is SelectCardsEffect or RandomCardEffect or UpgradeSelectedCardEffect or GrantSelectedSlyEffect or CloneCardEffect
                or ModifyPlayedCardCostEffect or CreateDefinedCardEffect or RewritePlayedCardEffect)
                problems.Add($"{effect.Source}: hook cannot initiate card selection, upgrade or cloning");
            if (effect is AutoPlayCardEffect or GenerateCardsEffect or ForgeCardEffect)
                problems.Add($"{effect.Source}: hook cannot initiate automatic play or card generation");
            if (effect is ConditionalCardEffect branch)
            {
                ValidateHookProgram(branch.Then, ref problems);
                ValidateHookProgram(branch.Else, ref problems);
            }
            if (effect is RepeatCardEffect repeat) ValidateHookProgram(repeat.Body, ref problems);
            if (effect is DynamicRepeatCardEffect dynamic) ValidateHookProgram(dynamic.Body, ref problems);
            if (effect is WithCountCardEffect withCount) ValidateHookProgram(withCount.Body, ref problems);
            if (effect is ForEachCardEffect each) ValidateHookProgram(each.Body, ref problems);
            if (effect is SelectCardsEffect select) ValidateHookProgram(select.OnSelected, ref problems);
            if (effect is RandomCardEffect random) ValidateHookProgram(random.OnSelected, ref problems);
        }
    }

    private static AuthorHooks LinkHookPrograms(AuthorHooks hooks, Func<string, DslSourceLocation, string> resolve,
        Func<string, DslSourceLocation, string> resolveTag, Func<string, DslSourceLocation, string> resolveCard)
        => hooks.Programs is null ? hooks : hooks with
        {
            Programs = hooks.Programs.ToImmutableDictionary(entry => entry.Key,
                entry => LinkEffectProgram(entry.Value, resolve, resolveTag, resolveCard)),
        };

    private static CardEffectAmount LinkAmount(CardEffectAmount amount, DslSourceLocation source,
        Func<string, DslSourceLocation, string> resolve, Func<string, DslSourceLocation, string> resolveTag)
        => amount with
        {
            Reference = amount.Source switch
            {
                CardAmountSource.TargetPower or CardAmountSource.EnemyPowerSum => resolve(amount.Reference, source),
                CardAmountSource.CardsWithTag => resolveTag(amount.Reference, source),
                _ => amount.Reference,
            },
            Operands = [.. amount.Operands.Select(a => LinkAmount(a, source, resolve, resolveTag))],
        };

    private static CardEffectProgram LinkEffectProgram(CardEffectProgram program,
        Func<string, DslSourceLocation, string> resolve, Func<string, DslSourceLocation, string> resolveTag,
        Func<string, DslSourceLocation, string> resolveCard)
        => new([.. program.Effects.Select(effect => effect switch
        {
            CreateDefinedCardEffect create => create with { CardId = resolveCard(create.CardId, create.Source) },
            RewritePlayedCardEffect rewrite => rewrite with { CardId = resolveCard(rewrite.CardId, rewrite.Source) },
            ForgeCardEffect forge => forge with { CardId = resolveCard(forge.CardId, forge.Source), Amount = LinkAmount(forge.Amount, forge.Source, resolve, resolveTag) },
            OrbCardEffect orb => orb with { Amount = LinkAmount(orb.Amount, orb.Source, resolve, resolveTag) },
            OstyCardEffect osty => osty with { Amount = LinkAmount(osty.Amount, osty.Source, resolve, resolveTag) },
            ModifyPowerVariableEffect change => change with { PowerId = resolve(change.PowerId, change.Source) },
            PrimitiveCardEffect leaf => leaf with
            {
                PowerId = leaf.Operation == CardEffectOperation.ApplyPower ? resolve(leaf.PowerId, leaf.Source) : leaf.PowerId,
                Amount = LinkAmount(leaf.Amount, leaf.Source, resolve, resolveTag),
            },
            ConditionalCardEffect branch => branch with
            {
                Condition = branch.Condition.Kind == CardConditionKind.TargetHasPower
                    ? branch.Condition with { Reference = resolve(branch.Condition.Reference, branch.Source) } : branch.Condition,
                Then = LinkEffectProgram(branch.Then, resolve, resolveTag, resolveCard),
                Else = LinkEffectProgram(branch.Else, resolve, resolveTag, resolveCard),
            },
            RepeatCardEffect repeat => repeat with { Body = LinkEffectProgram(repeat.Body, resolve, resolveTag, resolveCard) },
            DynamicRepeatCardEffect dynamicRepeat => dynamicRepeat with
            {
                Count = LinkAmount(dynamicRepeat.Count, dynamicRepeat.Source, resolve, resolveTag),
                Body = LinkEffectProgram(dynamicRepeat.Body, resolve, resolveTag, resolveCard),
            },
            // ENV-T05：`withCount` 的名字是**编译期词法作用域**，不需要跨包链接；只要递归 body。
            WithCountCardEffect withCount => withCount with
            {
                Count = LinkAmount(withCount.Count, withCount.Source, resolve, resolveTag),
                Body = LinkEffectProgram(withCount.Body, resolve, resolveTag, resolveCard),
            },
            ForEachCardEffect each => each with
            {
                Body = LinkEffectProgram(each.Body, resolve, resolveTag, resolveCard),
            },
            SelectCardsEffect select => select with { OnSelected = LinkEffectProgram(select.OnSelected, resolve, resolveTag, resolveCard) },
            RandomCardEffect random => random with { OnSelected = LinkEffectProgram(random.OnSelected, resolve, resolveTag, resolveCard) },
            // ENV-T05：`transformSelectedCard` 的卡引用按卡 id 同一口径补包前缀，缺了就显式报错。
            TransformSelectedCardEffect transform => transform with
            {
                CardId = resolveCard(transform.CardId, transform.Source),
            },
            AutoPlayCardEffect auto => auto with { Count = LinkAmount(auto.Count, auto.Source, resolve, resolveTag) },
            GenerateCardsEffect generate => generate with
            {
                Count = LinkAmount(generate.Count, generate.Source, resolve, resolveTag),
                Pool = [.. generate.Pool.Select(id => resolveCard(id, generate.Source))],
            },
            _ => effect,
        })]);
}
