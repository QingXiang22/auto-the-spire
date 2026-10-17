using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class EnemyCompilation
{
    internal static EnemySelectionBindings EnemySelection(ContentBindings contentBindings, EnemiesBindings enemiesBindings)
    {

        // -- BASE-02 �?1：敌人行动状态机（选取�?--------------------------------------------------------------
        //
        // 选取程序与源码同一套调用点�?*出生�?*（`CombatManager.AfterCreatureAdded` �?`MonsterModel.RollMove`�?
        // �?*每个玩家回合开�?*（`CombatManager` 玩家侧回合开�?�?`Creature.PrepareForNextTurn` �?`RollMove`）�?
        // `MonsterMoveStateMachine.FindNextMoveState` 的提前返回（`!_performedFirstMove && 当前是行动`�?
        // `MonsterMoveStateMachine.cs:60-63`）在这里�?`PerformedFirstMove` 字段表达，因此两次调�?*幂等**�?
        // 开局不掷点（初始状态是分支时才掷），第一次玩家回合开始不会再掷一次�?
        //
        // 每次落到分支状态的选取**恰好消费 1 次** `monster_ai`（`RunRngType.MonsterAi`，`RunRngSet.cs:77`）；
        // 行动→行动的固定转移与有序条件分支消耗 0 次。权重全为 0 时源仍然抽一次（`NextFloat(0)`，
        // `Rng.cs:156-164`）并命中**第一个**分支（`RandomBranchState.cs:117-126`）——ENV-T01 起这两条支路
        // 合并成同一次抽取（`SampleInt(monster_ai, 0, 2^53)`）：抽取**次数**与旧实现逐位相同（每只怪每次
        // 选取恰好 1 次），只有权重算术从"整数累积近似"换成源 float32，同一份 `bits` 不因门槛而改变。
        //
        // ENV-T01：加权选取的**源语义**（float32 求和 + 逐项 float 减法）不再是整数近似——一次
        // `SampleInt(stream, 0, 2^53)` 取到源 `NextDouble()` 的 53 位值，随后交给纯 Compute 资产
        // `SourceWeightedPick`（`SourceWeightedPickBackend`，源算法逐位复刻）。资产/后端身份进
        // `RuleSet.Identity`（→ `RulesetDigest` → `PublicBuildId`），因此"算法资产/backend/数值身份"
        // 自动进入构建身份。权重以 float32 **位型**传入（不是十进制近似）；资产元数固定为
        // "全部随机分支里最多的候选数"，每条分支只绑定自己的前 N 个权重并显式传 `Count`。
        ProcedureSymbol? selectEnemyMove = null;

        var machineSlot = new CompilationSlots();

        var monsterAiStream = VanillaRandomLayout.EngineStreamName("MonsterAi");

        var sourceWeightedPickArity = enemiesBindings.MachineEnemies
            .SelectMany(enemy => enemy.Behavior!.States.OfType<AuthorRandomState>())
            .Select(state => state.Candidates.Count)
            .DefaultIfEmpty(0)
            .Max();

        ComputeAssetSymbol? sourceWeightedPick = null;

        FieldSymbol? sourceWeightedPickBits = null;

        FieldSymbol? sourceWeightedPickCount = null;

        FieldSymbol? sourceWeightedPickPick = null;

        FieldSymbol[] sourceWeightedPickWeights = [];

        if (sourceWeightedPickArity > 0)
        {
            sourceWeightedPick = contentBindings.Package.ComputeAsset(SourceWeightedPick.AssetName);
            sourceWeightedPick.BackendId = SourceWeightedPickBackend.BackendId;
            sourceWeightedPick.SourcePath = "mod-the-spire/base-spire/BaseSpire/Randomness/SourceWeightedPick.cs";
            sourceWeightedPick.SourceHash = "sts2/RandomBranchState.cs#115-167+sts2/Random/Rng.cs#156-164@float32-v1";
            sourceWeightedPick.Entry = "pick";
            sourceWeightedPickBits = sourceWeightedPick.Input("Bits",
                FieldTypeSchema.Int(0, SourceWeightedPick.Span - 1));
            sourceWeightedPickCount = sourceWeightedPick.Input("Count",
                FieldTypeSchema.Int(1, sourceWeightedPickArity));
            sourceWeightedPickWeights = Enumerable.Range(0, sourceWeightedPickArity)
                .Select(index => sourceWeightedPick.Input($"W{index}",
                    FieldTypeSchema.Int(0, SourceWeightedPick.WeightBitsMax)))
                .ToArray();
            sourceWeightedPickPick = sourceWeightedPick.Output("Pick",
                FieldTypeSchema.Int(-1, sourceWeightedPickArity - 1));
        }

        return new EnemySelectionBindings(selectEnemyMove, machineSlot, monsterAiStream, sourceWeightedPickArity, sourceWeightedPick, sourceWeightedPickBits, sourceWeightedPickCount, sourceWeightedPickPick, sourceWeightedPickWeights);
    }

    internal static void EnemySelectionBody(
        ContentBindings contentBindings,
        EnemiesBindings enemiesBindings,
        EnemySelectionBindings enemySelectionBindings,
        SelectMachineCallback SelectMachine)
    {
        if (enemiesBindings.HasMachine)
        {
            var select = contentBindings.Package.Procedure("SelectEnemyMove");
            var selectSlot = select.Param("Monster", FieldTypeSchema.AnyRef());
            enemySelectionBindings.SelectEnemyMove = select.Symbol;
            select.Body(SelectDispatch(select, 0));

            Inst SelectDispatch(IrScope scope, int index)
            {
                var target = scope.Param(selectSlot);
                var enemy = enemiesBindings.MachineEnemies[index];
                var condition = scope.Equal(scope.Field(target, enemiesBindings.MonsterDefinition), scope.Const(enemy.Id));
                var body = SelectMachine(scope, target, enemy);
                return index == enemiesBindings.MachineEnemies.Count - 1
                    ? scope.If(condition, body)
                    : scope.IfElse(condition, body, [SelectDispatch(scope, index + 1)]);
            }
        }

    }

    internal static Inst HardcodedEnemyMove(
        EnemiesBindings enemiesBindings,
        PowerCommandsBindings powerCommandsBindings,
        PlayerDamageBindings playerDamageBindings,
        EnemyTurnBindings enemyTurnBindings,
        Expr monster) => enemyTurnBindings.StartEnemyTurn.IfElse(
                enemyTurnBindings.StartEnemyTurn.Equal(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterMoveIndex),
                    enemyTurnBindings.StartEnemyTurn.Const(0L)),
                new[]
                {
                enemyTurnBindings.StartEnemyTurn.Call(powerCommandsBindings.ApplyPower.Symbol, monster,
                    enemyTurnBindings.StartEnemyTurn.Const(SliceManifest.RitualId),
                    enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterFirstRitual), enemyTurnBindings.StartEnemyTurn.Const(false),
                    enemyTurnBindings.StartEnemyTurn.Const(true)),
                },
                new[]
                {
                enemyTurnBindings.StartEnemyTurn.Call(playerDamageBindings.DamagePlayer.Symbol, monster,
                    enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterStrikeDamage), enemyTurnBindings.StartEnemyTurn.Const(true)),
                });

    internal static Inst DeclaredEnemyMove(
        ContentBindings contentBindings,
        EnemiesBindings enemiesBindings,
        EnemyTurnBindings enemyTurnBindings,
        BuildCycleCallback BuildCycle,
        MachinePerformCallback MachinePerform,
        Expr monster)
    {
        var enemies = contentBindings.Author!.Enemies.ToList();
        return Branch(0);

        Inst Branch(int index)
        {
            var enemy = enemies[index];
            var condition = enemyTurnBindings.StartEnemyTurn.Equal(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterDefinition),
                enemyTurnBindings.StartEnemyTurn.Const(enemy.Id));
            var body = enemy.Behavior is null ? BuildCycle(monster, enemy) : MachinePerform(monster, enemy);
            return index == enemies.Count - 1
                ? enemyTurnBindings.StartEnemyTurn.If(condition, new[] { body })
                : enemyTurnBindings.StartEnemyTurn.IfElse(condition, new[] { body }, new[] { Branch(index + 1) });
        }
    }

    internal static Inst MachinePerform(
        EnemiesBindings enemiesBindings,
        EnemyTurnBindings enemyTurnBindings,
        MoveInstructionsCallback MoveInstructions,
        Expr monster,
        AuthorEnemy enemy)
    {
        var states = enemy.Behavior!.MoveStates.ToList();
        return Branch(0);

        Inst Branch(int index)
        {
            var move = enemy.Moves.Single(candidate =>
                string.Equals(candidate.Name, states[index].Move, StringComparison.Ordinal));
            var condition = enemyTurnBindings.StartEnemyTurn.Equal(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterNextMove),
                enemyTurnBindings.StartEnemyTurn.Const(move.Name));
            var body = new List<Inst>(MoveInstructions(monster, move))
                {
                    enemyTurnBindings.StartEnemyTurn.SetField(monster, enemiesBindings.PerformedFirstMove!,
                        enemyTurnBindings.StartEnemyTurn.Const(true)),
                };
            return index == states.Count - 1
                ? enemyTurnBindings.StartEnemyTurn.If(condition, body.ToArray())
                : enemyTurnBindings.StartEnemyTurn.IfElse(condition, body.ToArray(), new[] { Branch(index + 1) });
        }
    }

    internal static int EffectiveMove(AuthorEnemy enemy, int moveIndex)
    {
        var count = enemy.Cycle.Count;
        var from = enemy.RepeatFrom < 0 ? count - 1 : Math.Clamp(enemy.RepeatFrom, 0, count - 1);
        if (moveIndex < count)
        {
            return moveIndex;
        }

        if (from >= count - 1)
        {
            return count - 1;
        }

        var span = count - from;
        return from + ((moveIndex - from) % span);
    }

    internal static Inst BuildCycle(
        EnemiesBindings enemiesBindings,
        EnemyTurnBindings enemyTurnBindings,
        EffectiveMoveCallback EffectiveMove,
        MoveInstructionsCallback MoveInstructions,
        Expr monster,
        AuthorEnemy enemy)
    {
        var count = enemy.Cycle.Count;
        var from = enemy.RepeatFrom < 0 ? count - 1 : Math.Clamp(enemy.RepeatFrom, 0, count - 1);
        var effective = enemyTurnBindings.StartEnemyTurn.Local($"moveEffective{count}_{from}", FieldTypeSchema.Int(0, 999));
        var head = enemyTurnBindings.StartEnemyTurn.IfElse(
            enemyTurnBindings.StartEnemyTurn.Less(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterMoveIndex),
                enemyTurnBindings.StartEnemyTurn.Const((long)count)),
            new[] { enemyTurnBindings.StartEnemyTurn.SetLocal(effective, enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterMoveIndex)) },
            from >= count - 1
                ? new[] { enemyTurnBindings.StartEnemyTurn.SetLocal(effective, enemyTurnBindings.StartEnemyTurn.Const((long)count - 1)) }
                : new[]
                {
                        enemyTurnBindings.StartEnemyTurn.SetLocal(effective,
                            enemyTurnBindings.StartEnemyTurn.Add(enemyTurnBindings.StartEnemyTurn.Const((long)from),
                                enemyTurnBindings.StartEnemyTurn.Modulo(
                                    enemyTurnBindings.StartEnemyTurn.Subtract(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterMoveIndex),
                                        enemyTurnBindings.StartEnemyTurn.Const((long)from)),
                                    enemyTurnBindings.StartEnemyTurn.Const((long)(count - from))))),
                });
        Inst BranchAt(int index)
        {
            var move = enemy.Moves.Single(candidate =>
                string.Equals(candidate.Name, enemy.Cycle[index], StringComparison.Ordinal));
            var next = enemy.Cycle[EffectiveMove(enemy, index + 1)];
            var body = new List<Inst>
                {
                    enemyTurnBindings.StartEnemyTurn.SetField(monster, enemiesBindings.MonsterNextMove, enemyTurnBindings.StartEnemyTurn.Const(next)),
                };
            body.AddRange(MoveInstructions(monster, move));
            return index == count - 1
                ? enemyTurnBindings.StartEnemyTurn.If(enemyTurnBindings.StartEnemyTurn.Equal(enemyTurnBindings.StartEnemyTurn.LocalRef(effective),
                    enemyTurnBindings.StartEnemyTurn.Const((long)index)), body.ToArray())
                : enemyTurnBindings.StartEnemyTurn.IfElse(
                    enemyTurnBindings.StartEnemyTurn.Equal(enemyTurnBindings.StartEnemyTurn.LocalRef(effective), enemyTurnBindings.StartEnemyTurn.Const((long)index)),
                    body.ToArray(),
                    new[] { BranchAt(index + 1) });
        }

        return enemyTurnBindings.StartEnemyTurn.If(enemyTurnBindings.StartEnemyTurn.Const(true), new Inst[] { head, BranchAt(0) });
    }

    internal static Inst[] MoveInstructions(
        PowerCommandsBindings powerCommandsBindings,
        PlayerDamageBindings playerDamageBindings,
        EnemyTurnBindings enemyTurnBindings,
        MoveValueCallback MoveValue,
        EnemyBlockStepsCallback EnemyBlockSteps,
        ApplyMovePowerCallback ApplyMovePower,
        AddCardsInstructionCallback AddCardsInstruction,
        Expr monster,
        AuthorMove move)
    {
        var steps = new List<Inst>();
        foreach (var step in move.Steps)
        {
            steps.Add(step.Kind switch
            {
                "ritual" => enemyTurnBindings.StartEnemyTurn.Call(powerCommandsBindings.ApplyPower.Symbol, monster,
                    enemyTurnBindings.StartEnemyTurn.Const(SliceManifest.RitualId), MoveValue(monster, step),
                    enemyTurnBindings.StartEnemyTurn.Const(false), enemyTurnBindings.StartEnemyTurn.Const(true)),
                "damage" => enemyTurnBindings.StartEnemyTurn.Call(playerDamageBindings.DamagePlayer.Symbol, monster,
                    MoveValue(monster, step), enemyTurnBindings.StartEnemyTurn.Const(!move.Unpowered)),
                // `gain_block = <数�?`：本人获得格挡�?
                "gain_block" => enemyTurnBindings.StartEnemyTurn.If(enemyTurnBindings.StartEnemyTurn.Const(true),
                    [.. EnemyBlockSteps(monster, MoveValue(monster, step))]),
                // `apply_power_self = <能力 id>:<层数>` / `apply_power_player = <能力 id>:<层数>`�?
                "apply_power_self" => ApplyMovePower(monster, step.Value, self: true),
                "apply_power_player" => ApplyMovePower(monster, step.Value, self: false),
                // `add_cards:<�?id>:<张数>:<牌区>:<位置>`：往**玩家**牌区放状态牌�?
                "add_cards" => AddCardsInstruction(step.Value),
                _ => throw Unsupported($"enemy move step '{step.Kind}'",
                    "没有对应的通用 IR 降级"),
            });
        }

        return steps.ToArray();
    }

    internal static Inst ApplyMovePower(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PowerCommandsBindings powerCommandsBindings,
        EnemyTurnBindings enemyTurnBindings,
        Expr monster,
        string argument,
        bool self)
    {
        var lastColon = argument.LastIndexOf(':');
        if (lastColon <= 0 || !int.TryParse(argument[(lastColon + 1)..], out var stacks))
        {
            throw Unsupported($"enemy move power '{argument}'", "写法应为 <能力 id>:<层数>");
        }

        var powerId = argument[..lastColon];
        var owner = self ? monster : (Expr)enemyTurnBindings.StartEnemyTurn.First(enemyTurnBindings.StartEnemyTurn.Type(playerBindings.Player, battleBindings.BattleScope));
        return enemyTurnBindings.StartEnemyTurn.Call(powerCommandsBindings.ApplyPower.Symbol, owner, enemyTurnBindings.StartEnemyTurn.Const(powerId),
            enemyTurnBindings.StartEnemyTurn.Const((long)stacks), enemyTurnBindings.StartEnemyTurn.Const(!self), enemyTurnBindings.StartEnemyTurn.Const(true));
    }

    internal static Inst AddCardsInstruction(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        EnemyTurnBindings enemyTurnBindings,
        string argument)
    {
        // 卡 id 自身含冒号（`base-spire:slimed`），因此**从右往左**取三段：位置 / 牌区 / 张数。
        var parts = argument.Split(':');
        if (parts.Length < 4 || !int.TryParse(parts[^3], out var count) || count <= 0)
        {
            throw Unsupported($"enemy add_cards '{argument}'", "写法应为 <卡 id>:<张数>:<牌区>:<位置>");
        }

        var cardId = string.Join(':', parts[..^3]);
        var pileName = parts[^2];
        var pile = pileName switch
        {
            "hand" => pilesBindings.Hand,
            "draw" => pilesBindings.DrawPile,
            "discard" => pilesBindings.DiscardPile,
            _ => throw Unsupported($"enemy add_cards pile '{pileName}'", "只支持 hand / draw / discard"),
        };
        var definition = cardsBindings.CardDefinitions.Definition(cardId);
        var playerRef = enemyTurnBindings.StartEnemyTurn.First(enemyTurnBindings.StartEnemyTurn.Type(playerBindings.Player, battleBindings.BattleScope));
        var instructions = new List<Inst>();
        for (var index = 0; index < count; index++)
        {
            var cardSlot = enemyTurnBindings.StartEnemyTurn.Local($"addedCard{index}", FieldTypeSchema.Binding());
            var fields = cardsBindings.CardDefinitions.New(enemyTurnBindings.StartEnemyTurn, definition, false);
            if (parts[^1] == "bottom")
            {
                instructions.Add(enemyTurnBindings.StartEnemyTurn.CreateEntity(cardsBindings.Card, enemyTurnBindings.StartEnemyTurn.EntityScope(playerRef),
                    cardSlot, fields, pile));
                continue;
            }

            if (parts[^1] == "top")
            {
                instructions.Add(enemyTurnBindings.StartEnemyTurn.CreateEntity(cardsBindings.Card, enemyTurnBindings.StartEnemyTurn.EntityScope(playerRef),
                    cardSlot, fields, pile, InsertPosition.Start));
                continue;
            }

            if (parts[^1] != "random")
            {
                throw Unsupported($"enemy add_cards position '{parts[^1]}'",
                    "只支持 bottom / top / random");
            }

            var position = enemyTurnBindings.StartEnemyTurn.Local($"addedCardPosition{index}", FieldTypeSchema.Int(0, 999));
            // `Random` 的抽取：`NextInt(当前牌区张数 + 1)`，写入 `position` 槽（SampleInt 直接写结果槽）。
            instructions.Add(enemyTurnBindings.StartEnemyTurn.SampleInt(SliceManifest.DrawStream, enemyTurnBindings.StartEnemyTurn.Const(0L),
                enemyTurnBindings.StartEnemyTurn.Add(enemyTurnBindings.StartEnemyTurn.Count(enemyTurnBindings.StartEnemyTurn.Collection(pile)),
                    enemyTurnBindings.StartEnemyTurn.Const(1L)), position));
            instructions.Add(enemyTurnBindings.StartEnemyTurn.CreateEntity(cardsBindings.Card, enemyTurnBindings.StartEnemyTurn.EntityScope(playerRef),
                cardSlot, fields, pile));
            instructions.Add(enemyTurnBindings.StartEnemyTurn.MoveInCollection(pile, enemyTurnBindings.StartEnemyTurn.LocalRef(cardSlot),
                InsertPosition.AtIndex, enemyTurnBindings.StartEnemyTurn.LocalRef(position)));
        }

        return enemyTurnBindings.StartEnemyTurn.If(enemyTurnBindings.StartEnemyTurn.Const(true), instructions.ToArray());
    }

    internal static Expr MoveValue(
        EnemiesBindings enemiesBindings,
        EnemyTurnBindings enemyTurnBindings,
        Expr monster,
        AuthorMoveStep step) => step.Value switch
    {
        // 字段引用：数值仍只写在敌人的数据里（不重复声明）�?
        "first-ritual" => enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterFirstRitual),
        "strike" => enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterStrikeDamage),
        _ => long.TryParse(step.Value, out var literal)
            ? enemyTurnBindings.StartEnemyTurn.Const(literal)
            : throw Unsupported($"enemy move value '{step.Value}'",
                "只支持数字或字段引用 first-ritual / strike"),
    };

    internal static Inst[] SelectMachine(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        EnemySelectionBindings enemySelectionBindings,
        IrScope scope,
        Expr monster,
        AuthorEnemy enemy)
    {
        var behavior = enemy.Behavior!;
        var id = enemySelectionBindings.MachineSlot.Next();
        var current = scope.Local($"machineCurrent{id}", FieldTypeSchema.String(32));
        // `PerformedFirstMove || !IsMove(当前状�?`：当前是行动且还没执行过 �?不转换（首动不掷）�?
        var isMove = behavior.MoveStates
            .Select(state => (Expr)scope.Equal(scope.LocalRef(current), scope.Const(state.Move)))
            .Aggregate(scope.Or);
        return
        [
            scope.SetLocal(current, scope.Field(monster, enemiesBindings.MachineState!)),
                scope.If(
                    scope.Or(scope.Field(monster, enemiesBindings.PerformedFirstMove!), scope.Not(isMove)),
                    [.. ResolveState(), .. AppendMoveLog()]),
                // 公开意图：原版的 `NextMove` 就是**已经选好�?*那个行动（`NCreature.cs:289`），不另掷一次�?
                scope.SetField(monster, enemiesBindings.MachineState!, scope.LocalRef(current)),
                scope.SetField(monster, enemiesBindings.MonsterNextMove, scope.LocalRef(current)),
            ];

        /// <summary>按当前状态求后继：行�?�?固定后继；分�?�?当场求值（原版�?`do �?while (!IsMove)`）�?/summary>
        IEnumerable<Inst> ResolveState()
        {
            Inst StateAt(int index)
            {
                var state = behavior.States[index];
                var body = state is AuthorMoveState move ? Step(move.FollowUp) : Evaluate(state);
                var condition = scope.Equal(scope.LocalRef(current), scope.Const(state.Id));
                return index == behavior.States.Count - 1
                    ? scope.If(condition, body)
                    : scope.IfElse(condition, body, [StateAt(index + 1)]);
            }

            yield return StateAt(0);
        }

        /// <summary>后继是一个行动就记下来；是一个分支就当场求值（原版会在同一轮里继续走）�?/summary>
        Inst[] Step(string followUp)
        {
            var target = behavior.States.Single(state => state.Id == followUp);
            return target switch
            {
                AuthorMoveState => [scope.SetLocal(current, scope.Const(followUp))],
                _ => Evaluate(target),
            };
        }

        Inst[] Evaluate(AuthorEnemyState state) => state switch
        {
            AuthorRandomState random => EvaluateRandom(random),
            AuthorOrderedState ordered => EvaluateOrdered(ordered),
            _ => throw Unsupported($"enemy state '{state.Id}'", "没有对应的通用 IR 降级"),
        };

        /// <summary>
        /// `RandomBranchState`：每个候选先算**门槛后的权重**（`RandomBranchState.cs:130-167`），
        /// 再按源 `Σ weight` 与声明顺序的 float32 减法取首个 `num &lt;= 0` 的候选
        /// （`RandomBranchState.cs:115-128`）；求和与减法由 `SourceWeightedPick` 资产按源 float32 做。
        /// </summary>
        Inst[] EvaluateRandom(AuthorRandomState branch)
        {
            var bits = scope.Local($"moveBits{enemySelectionBindings.MachineSlot.Next()}", FieldTypeSchema.Int(0, SourceWeightedPick.Span - 1));
            var pick = scope.Local($"movePick{enemySelectionBindings.MachineSlot.Next()}", FieldTypeSchema.Int(-1, enemySelectionBindings.SourceWeightedPickArity - 1));
            var weights = branch.Candidates
                .Select(_ => scope.Local($"moveWeight{enemySelectionBindings.MachineSlot.Next()}",
                    FieldTypeSchema.Int(0, SourceWeightedPick.WeightBitsMax)))
                .ToArray();
            var steps = new List<Inst>
                {
                    // 恰好一次：源 `Rng.NextDouble()` 的 53 位值（`raw >> 11`）。旧实现里"权重全 0 时另抽
                    // `SampleInt(0,1)`"的那条支路在这里并了进来，因此抽取次数不变、选取也不依赖门槛后的总值。
                    scope.SampleInt(enemySelectionBindings.MonsterAiStream, scope.Const(0L), scope.Const(SourceWeightedPick.Span), bits),
                };
            for (var index = 0; index < branch.Candidates.Count; index++)
            {
                var candidate = branch.Candidates[index];
                // 权重按 **float32 位型**传：资产不做十进制近似，也不做整数累积。
                steps.Add(scope.SetLocal(weights[index],
                    scope.Const((long)BitConverter.SingleToInt32Bits(candidate.Weight))));
                if (Eligible(candidate) is { } gate)
                {
                    // 门槛不满足 ⇒ 源 `GetStateWeight` 返回 0f（加 0、减 0 都是精确 no-op）。
                    steps.Add(scope.If(scope.Not(gate), scope.SetLocal(weights[index], scope.Const(0L))));
                }
            }

            steps.Add(scope.Compute(
                enemySelectionBindings.SourceWeightedPick!,
                [
                    (enemySelectionBindings.SourceWeightedPickBits!, scope.LocalRef(bits)),
                        (enemySelectionBindings.SourceWeightedPickCount!, scope.Const((long)branch.Candidates.Count)),
                        .. weights.Select((slot, index) => (enemySelectionBindings.SourceWeightedPickWeights[index], scope.LocalRef(slot))),
                ],
                [(enemySelectionBindings.SourceWeightedPickPick!, pick)]));
            steps.Add(scope.If(
                scope.Less(scope.LocalRef(pick), scope.Const(0L)),
                // 源 `GetNextState` 找不到命中就抛 `InvalidOperationException`：这里显式失败，不静默退回某个行动。
                scope.Fail($"enemy move state machine: no valid state found in random branch '{branch.Id}'")));
            steps.Add(PickAt(0));
            return [.. steps];

            Inst PickAt(int index)
            {
                var body = new[] { scope.SetLocal(current, scope.Const(branch.Candidates[index].Move)) };
                if (index == branch.Candidates.Count - 1)
                {
                    // 资产只在"前面都没命中"时返回最后一个下标（源里此时 `num` 必然已 &lt;= 0），
                    // 因此这里只是显式兜底，不留空分支。
                    return scope.If(scope.Const(true), body);
                }

                return scope.IfElse(scope.Equal(scope.LocalRef(pick), scope.Const((long)index)), body,
                    [PickAt(index + 1)]);
            }
        }

        /// <summary>`ConditionalBranchState`：有序取值，不消耗随机数；全部不成立�?*显式失败**
        /// （源�?`ConditionalBranchState.cs:53` 抛错，不能静默退回某个行动）�?/summary>
        Inst[] EvaluateOrdered(AuthorOrderedState ordered)
        {
            Inst OrderedAt(int index)
            {
                var candidate = ordered.Candidates[index];
                var body = new[] { scope.SetLocal(current, scope.Const(candidate.Move)) };
                return index == ordered.Candidates.Count - 1
                    ? scope.If(Condition(candidate.When), body)
                    : scope.IfElse(Condition(candidate.When), body, [OrderedAt(index + 1)]);
            }

            return
            [
                scope.SetLocal(current, scope.Const(ordered.Id)),
                    OrderedAt(0),
                    scope.If(scope.Equal(scope.LocalRef(current), scope.Const(ordered.Id)),
                        scope.Fail($"enemy move state machine: no condition matched in '{ordered.Id}'")),
                ];
        }

        /// <summary>一个候选的可用性：重复限制 + 冷却窗口 + `when` 条件（全部满足才有权重）�?/summary>
        Expr? Eligible(AuthorMoveCandidate candidate)
        {
            var gates = new List<Expr>();
            if (candidate.Repeat.Kind == EnemyRepeatKinds.Once || candidate.MaxUsesPerCombat == 1)
            {
                // UseOnlyOnce：这个行�?*出现�?*（含出生时进日志的初始行动）就权�?0�?
                gates.Add(scope.Not(scope.Field(monster, enemiesBindings.OnceOnlyFields[candidate.Move])));
            }

            var streak = candidate.Repeat.StreakLimit;
            if (streak > 0)
            {
                // CannotRepeat / CanRepeatXTimes(n)：日�?*尾部连续**出现次数 �?n 就权�?0�?
                gates.Add(scope.Not(scope.And(
                    scope.Equal(scope.Field(monster, enemiesBindings.LastMove!), scope.Const(candidate.Move)),
                    scope.GreaterOrEqual(scope.Field(monster, enemiesBindings.MoveRunLength!), scope.Const((long)streak)))));
            }

            for (var index = 0; index < candidate.Cooldown && index < enemiesBindings.MoveHistory.Length; index++)
            {
                // cooldown = c：出现在日志最�?c 个行动里就权�?0（`RandomBranchState.cs:158-165`）�?
                gates.Add(scope.Not(scope.Equal(scope.Field(monster, enemiesBindings.MoveHistory[index]),
                    scope.Const(candidate.Move))));
            }

            if (candidate.When is { } when)
            {
                gates.Add(Condition(when));
            }

            return gates.Count == 0 ? null : gates.Aggregate(scope.And);
        }

        Expr Condition(AuthorEnemyCondition condition) => condition switch
        {
            EnemyHpBelowCondition hp => scope.Less(scope.Field(monster, enemiesBindings.MonsterHp), scope.Const((long)hp.Amount)),
            TurnAtLeastCondition turn => scope.GreaterOrEqual(
                scope.Field(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerTurn),
                scope.Const((long)turn.Turn)),
            AllCondition all => all.Parts.Select(Condition).Aggregate(scope.And),
            AnyCondition any => any.Parts.Select(Condition).Aggregate(scope.Or),
            NotCondition not => scope.Not(Condition(not.Inner)),
            _ => throw Unsupported($"enemy condition '{condition.Describe()}'", "没有对应的通用 IR 降级"),
        };

        /// <summary>把选中的行动追加进日志：历史位移、尾部连续次数、`UseOnlyOnce` 标记�?/summary>
        IEnumerable<Inst> AppendMoveLog()
        {
            for (var index = enemiesBindings.MoveHistory.Length - 1; index >= 1; index--)
            {
                yield return scope.SetField(monster, enemiesBindings.MoveHistory[index],
                    scope.Field(monster, enemiesBindings.MoveHistory[index - 1]));
            }

            if (enemiesBindings.MoveHistory.Length > 0)
            {
                yield return scope.SetField(monster, enemiesBindings.MoveHistory[0], scope.LocalRef(current));
            }

            yield return scope.IfElse(
                scope.Equal(scope.Field(monster, enemiesBindings.LastMove!), scope.LocalRef(current)),
                [scope.SetField(monster, enemiesBindings.MoveRunLength!,
                        scope.Add(scope.Field(monster, enemiesBindings.MoveRunLength!), scope.Const(1L)))],
                [
                    scope.SetField(monster, enemiesBindings.LastMove!, scope.LocalRef(current)),
                        scope.SetField(monster, enemiesBindings.MoveRunLength!, scope.Const(1L)),
                    ]);
            foreach (var (move, field) in enemiesBindings.OnceOnlyFields)
            {
                yield return scope.If(scope.Equal(scope.LocalRef(current), scope.Const(move)),
                    scope.SetField(monster, field, scope.Const(true)));
            }
        }
    }

    internal static string ShortMoveName(string declared) =>
                declared.Length is > 0 and <= 32 ? declared : SliceManifest.IncantationMove;

    internal static IEnumerable<Inst> EnemyBlockSteps(
        ContentBindings contentBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        PowerQueryBindings powerQueryBindings,
        EnemyTurnBindings enemyTurnBindings,
        Expr monster,
        Expr rawAmount)
    {
        var donors = contentBindings.Author?.Powers
            .Where(entry => entry.BlockGainMultiplier.Length > 0 && entry.BlockGainPriorEntriesPerStack <= 0)
            .ToList() ?? [];
        var additive = contentBindings.Author?.Powers.Where(entry => entry.BlockGainAdditivePerStack != 0).ToList() ?? [];
        if (donors.Count == 0 && additive.Count == 0)
        {
            yield return enemyTurnBindings.StartEnemyTurn.SetField(monster, enemiesBindings.MonsterBlock,
                enemyTurnBindings.StartEnemyTurn.Add(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterBlock), rawAmount));
            yield break;
        }

        var fixedSlot = enemyTurnBindings.StartEnemyTurn.Local($"enemyBlockGain{playerBindings.NextBlockGainSlot.Next()}",
            FieldTypeSchema.Fixed(2, 0m, 9999m));
        yield return enemyTurnBindings.StartEnemyTurn.SetLocal(fixedSlot, enemyTurnBindings.StartEnemyTurn.IntToFixed(rawAmount));
        foreach (var power in additive)
        {
            yield return enemyTurnBindings.StartEnemyTurn.SetLocal(fixedSlot, enemyTurnBindings.StartEnemyTurn.Add(
                enemyTurnBindings.StartEnemyTurn.LocalRef(fixedSlot),
                enemyTurnBindings.StartEnemyTurn.IntToFixed(enemyTurnBindings.StartEnemyTurn.Multiply(
                    enemyTurnBindings.StartEnemyTurn.CallPure(powerQueryBindings.PowerStacksOf.Symbol, monster, enemyTurnBindings.StartEnemyTurn.Const(power.Id)),
                    enemyTurnBindings.StartEnemyTurn.Const((long)power.BlockGainAdditivePerStack)))));
        }
        foreach (var power in donors)
        {
            yield return enemyTurnBindings.StartEnemyTurn.If(
                enemyTurnBindings.StartEnemyTurn.Greater(
                    enemyTurnBindings.StartEnemyTurn.CallPure(powerQueryBindings.PowerStacksOf.Symbol, monster, enemyTurnBindings.StartEnemyTurn.Const(power.Id)),
                    enemyTurnBindings.StartEnemyTurn.Const(0L)),
                enemyTurnBindings.StartEnemyTurn.SetLocal(fixedSlot, enemyTurnBindings.StartEnemyTurn.Multiply(
                enemyTurnBindings.StartEnemyTurn.LocalRef(fixedSlot),
                enemyTurnBindings.StartEnemyTurn.Const(decimal.Parse(power.BlockGainMultiplier,
                    System.Globalization.CultureInfo.InvariantCulture)))));
        }

        yield return enemyTurnBindings.StartEnemyTurn.SetLocal(fixedSlot,
            enemyTurnBindings.StartEnemyTurn.Maximum(enemyTurnBindings.StartEnemyTurn.LocalRef(fixedSlot), enemyTurnBindings.StartEnemyTurn.Const(0m)));
        yield return enemyTurnBindings.StartEnemyTurn.SetField(monster, enemiesBindings.MonsterBlock,
            enemyTurnBindings.StartEnemyTurn.Add(enemyTurnBindings.StartEnemyTurn.Field(monster, enemiesBindings.MonsterBlock),
                enemyTurnBindings.StartEnemyTurn.FixedToInt(enemyTurnBindings.StartEnemyTurn.LocalRef(fixedSlot), FixedRounding.Truncate)));
    }

}
