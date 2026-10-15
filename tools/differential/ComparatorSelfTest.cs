using System.Globalization;

namespace Differential;

/// <summary>
/// 比较器自检：**受控错误记录**必须被抓到（ENV §9/§6.4）。
///
/// 它证明"比较器真的会报差异"，而不是只证明"两份相同记录不报差异"。严格入口
/// （<see cref="TestCommand"/>）与探索性入口（<see cref="CompareCommand"/>）共用同一份自检，
/// 避免两条入口各自维护一套"我以为会比较"的断言。
/// </summary>
internal static class ComparatorSelfTest
{
    internal sealed record Item(string Name, string ExpectedKind, bool Detected, string? Detail);

    /// <summary>对整批 base 记录构造受控错误并逐个断言被抓到；返回每一项的实测结果。</summary>
    public static List<Item> Run(IReadOnlyList<CaseRecord> records, string reproduction)
    {
        var items = new List<Item>();
        if (records.Count == 0)
        {
            items.Add(new Item("suite/non-empty", "no_divergence", false, "没有可用的记录"));
            return items;
        }

        var baseline = records.OrderByDescending(item => item.Boundaries.Count).First();
        var identical = Comparator.Compare(baseline, baseline, reproduction);
        items.Add(new Item($"identical/{baseline.CaseId}", "no_divergence",
            identical.Divergences.Count == 0, $"verdict={identical.Verdict}"));

        // 每种受控错误都在**整批记录**里找第一个能改的目标（不同 case 覆盖的能力不同：
        // 例如只有触发洗牌的 case 才会创建随机流）。
        items.Add(Detect("mutant/field", "field", record =>
            MutateFact(record, "player.hp") is { } mutant ? (mutant, record) : null, records, reproduction));
        items.Add(Detect("mutant/action", "action", record =>
            MutateAction(record) is { } mutant ? (mutant, record) : null, records, reproduction));
        items.Add(Detect("mutant/collection", "collection", record =>
            MutateFact(record, "pile.hand.order", truncate: true) is { } mutant ? (mutant, record) : null,
            records, reproduction));
        items.Add(Detect("mutant/decision", "decision", record =>
            MutateDecision(record) is { } mutant ? (mutant, record) : null, records, reproduction));

        // 随机流只在"声明为原版兼容"时才参与比较；两侧都标成兼容，证明那条代码路径真的会报差异。
        items.Add(Detect("mutant/stream", "stream", record =>
        {
            var compatible = record with { RngProfile = RngProfiles.VanillaCompatible, RngConsumedByScript = false };
            return MutateStream(compatible) is { } mutant ? (mutant, compatible) : null;
        }, records, reproduction));
        // ENV-T04 vulnerable 定向负例：仅变异真实执行记录的回合末持续时间；
        // 不伪称独立 reference 执行，也不移除现有 stream/collection 自检。
        var vulnerable = records.Where(record => record.CaseId == "vulnerable-decay-via-bash").ToList();
        if (vulnerable.Count > 0)
        {
            items.Add(Detect("mutant/vulnerable-duration", "field", record =>
                MutateFact(record, "power.enemy.base-spire:calcified-cultist.0.stacks") is { } mutant
                    ? (mutant, record) : null, vulnerable, reproduction));
        }
        foreach (var record in records.Where(record => record.InitialState is not null).Take(1))
        {
            var proof = record.InitialState!;
            var init = proof.Setup.InitialPiles!;
            void Rejected(string name, InitialPiles mutant)
            {
                try { ControlledInit.Validate(proof.Setup with { InitialPiles = mutant }); items.Add(new Item(name, "invalid_fixture", false, "未拒绝")); }
                catch (CheckFailed failure) { items.Add(new Item(name, "invalid_fixture", true, failure.Message)); }
            }
            Rejected("init/duplicate-slot", init with { Draw = init.Draw!.Select((card, index) => index == 1 ? card with { Slot = init.Draw![0].Slot } : card).ToArray() });
            Rejected("init/missing-card", init with { Draw = init.Draw!.Skip(1).ToArray() });
            Rejected("init/incomplete", init with { Exhaust = null });
            var zero = record with { RngProfile = RngProfiles.ControlledStateNoRandom, RngConsumedByScript = false,
                ScriptRandom = new RandomMeasurement("synthetic-gate-negative", "self-test only", 0, 0, [], []) };
            foreach (var (name, mutant) in new[] {
                ("gate/random-consumption", zero with { ScriptRandom = zero.ScriptRandom! with { After = 1 } }),
                ("gate/unknown", zero with { ScriptRandom = zero.ScriptRandom! with { After = null } }),
                ("gate/missing-declaration", zero with { InitialState = null }) })
            {
                var verdict = Comparator.Compare(mutant, zero, reproduction).Verdict;
                items.Add(new Item(name, "unsupported_rng_profile", verdict == "unsupported_rng_profile", verdict));
            }
            items.Add(Detect("mutant/ordered-initial-slots", "collection", candidate =>
            {
                var boundary = candidate.Boundaries[0];
                var facts = boundary.Facts.Select(fact => fact.Path == "pile.draw.initial-slots"
                    ? fact with { Value = string.Join(",", fact.Value.Split(',').Reverse()) } : fact).ToArray();
                return (candidate with { Boundaries = candidate.Boundaries.Select((b, i) => i == 0 ? b with { Facts = facts } : b).ToArray() }, candidate);
            }, [record], reproduction));
        }
        if (records.Any(record => record.CaseId == "power-turn-end-decay"))
        {
            var fixture = FixtureSuites.Load(FixtureSuites.ResolvePath(Repo.Root(), "regression"))
                .Cases.Single(item => item.Id == "power-turn-end-decay");
            var correct = records.Single(record => record.CaseId == fixture.Id);
            foreach (var (field, value) in new[] { ("Ticks", false), ("SkipNextTick", true) })
            {
                var mutant = BaseBackend.RunPowerInitializationFault(fixture, field, value);
                var comparison = Comparator.Compare(mutant, correct, reproduction);
                var hit = comparison.Divergences.FirstOrDefault(diff => diff.Kind == "field"
                    && diff.Path.Contains("power.", StringComparison.Ordinal));
                items.Add(new Item($"execution/power-{field}", "field", hit is not null,
                    hit is null ? comparison.Verdict : $"{hit.Path}: {hit.Left} ≠ {hit.Right}"));
            }
#if REFERENCE_SIM
            // Both directions: already-held duration debuffs tick immediately; Strength remains a counter.
            foreach (var owner in new[] { "player", "enemy:0" })
            foreach (var power in new[] { "base-spire:strength", "base-spire:vulnerable", "base-spire:weak" })
            {
                var control = fixture with { Setup = fixture.Setup with { Powers = [new FixturePower(owner, power, 2)] } };
                var left = BaseBackend.Run(control);
                var right = ReferenceBackend.Run(control);
                var comparison = Comparator.Compare(left, right, reproduction);
                var ownerPath = owner == "player" ? "player" : "enemy.base-spire:calcified-cultist";
                var path = $"power.{ownerPath}.0.stacks";
                string? Stack(CaseRecord record, int boundary) => record.Boundaries[boundary].Facts
                    .FirstOrDefault(fact => fact.Path == path)?.Value;
                var expected = power == "base-spire:strength" ? "2" : "1";
                var detected = left.Boundaries.Count == 3 && right.Boundaries.Count == 3
                    && Stack(left, 0) == "2" && Stack(right, 0) == "2"
                    && Stack(left, 2) == expected && Stack(right, 2) == expected
                    && comparison.Verdict == "passed";
                items.Add(new Item($"execution/held-{owner}-{power}", "held-power-trajectory", detected,
                    $"base={Stack(left, 0)}→{Stack(left, 2)}; reference={Stack(right, 0)}→{Stack(right, 2)}; verdict={comparison.Verdict}"));
            }
#endif
        }
        return items;
    }

    /// <summary>构造受控错误并断言比较器报出期望种类的首差。</summary>
    public static Item Detect(string name, string expectedKind,
        Func<CaseRecord, (CaseRecord Mutant, CaseRecord Reference)?> mutate,
        IReadOnlyList<CaseRecord> records, string reproduction)
    {
        var skipped = 0;
        foreach (var candidate in records.OrderByDescending(item => item.Boundaries.Count))
        {
            if (mutate(candidate) is not { } mutation)
            {
                skipped++;
                continue;
            }

            var result = Comparator.Compare(mutation.Mutant, mutation.Reference, reproduction);
            var hit = result.Divergences.FirstOrDefault(item => item.Kind == expectedKind);
            return new Item($"{name}/{candidate.CaseId}", expectedKind, hit is not null,
                hit is null
                    ? $"没有报出 {expectedKind}；实际 verdict={result.Verdict}，差异 {result.Divergences.Count} 条"
                    : $"{hit.Path}: {hit.Left} ≠ {hit.Right}");
        }

        return new Item(name, expectedKind, false,
            $"整批 {records.Count} 个记录里都没有该种类的可改目标（跳过 {skipped}）——这本身就是比较器覆盖不足");
    }

    /// <summary>受控错误：改一条规范化事实的值（或截断它的列表值）。</summary>
    public static CaseRecord? MutateFact(CaseRecord record, string path, bool truncate = false)
    {
        for (var index = record.Boundaries.Count - 1; index >= 0; index--)
        {
            var boundary = record.Boundaries[index];
            var target = boundary.Facts.ToList().FindIndex(fact => fact.Path == path);
            if (target < 0)
            {
                continue;
            }

            var facts = boundary.Facts.ToList();
            var fact = facts[target];
            string value;
            if (truncate)
            {
                var parts = fact.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    continue;
                }

                value = string.Join(",", parts.Take(parts.Length - 1));
            }
            else if (long.TryParse(fact.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                value = (number + 1).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                continue;
            }

            facts[target] = fact with { Value = value };
            return Replace(record, index, boundary with { Facts = facts });
        }

        return null;
    }

    public static CaseRecord? MutateAction(CaseRecord record)
    {
        for (var index = record.Boundaries.Count - 1; index >= 0; index--)
        {
            var boundary = record.Boundaries[index];
            for (var i = 0; i < boundary.Actions.Count; i++)
            {
                var action = boundary.Actions[i];
                if (action.Candidates.Count == 0)
                {
                    continue;
                }

                var actions = boundary.Actions.ToList();
                actions[i] = action with { Candidates = action.Candidates.Take(action.Candidates.Count - 1).ToList() };
                return Replace(record, index, boundary with { Actions = actions });
            }
        }

        return null;
    }

    /// <summary>受控错误：决策点的候选少一个（"候选集合本身也要比"的那条路径）。</summary>
    public static CaseRecord? MutateDecision(CaseRecord record)
    {
        for (var index = record.Boundaries.Count - 1; index >= 0; index--)
        {
            var boundary = record.Boundaries[index];
            for (var i = 0; i < boundary.Decisions.Count; i++)
            {
                var decision = boundary.Decisions[i];
                if (decision.Candidates.Count == 0)
                {
                    continue;
                }

                var decisions = boundary.Decisions.ToList();
                decisions[i] = decision with
                {
                    Candidates = decision.Candidates.Take(decision.Candidates.Count - 1).ToList(),
                };
                return Replace(record, index, boundary with { Decisions = decisions });
            }
        }

        return null;
    }

    public static CaseRecord? MutateStream(CaseRecord record)
    {
        for (var index = record.Boundaries.Count - 1; index >= 0; index--)
        {
            var boundary = record.Boundaries[index];
            if (boundary.Streams.Count == 0)
            {
                continue;
            }

            var streams = boundary.Streams.ToList();
            streams[0] = streams[0] with { Draws = streams[0].Draws + 1 };
            return Replace(record, index, boundary with { Streams = streams });
        }

        return null;
    }

    private static CaseRecord Replace(CaseRecord record, int index, BoundaryRecord boundary)
    {
        var boundaries = record.Boundaries.ToList();
        boundaries[index] = boundary;
        return record with { Boundaries = boundaries };
    }
}
