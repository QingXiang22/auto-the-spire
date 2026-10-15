namespace Differential;

/// <summary>随机 Profile 的稳定名字。不是受控无随机/原版兼容的 case 一律不计通过（ENV §5）。</summary>
internal static class RngProfiles
{
    /// <summary>原版兼容随机：同种子逐步一致。ENV-01 完成后才可能真的有 case 用这个值。</summary>
    public const string VanillaCompatible = "vanilla-compatible@1";

    /// <summary>
    /// 受控状态、且脚本执行期间**不消费新随机数**（ENV §5 明确允许的第一批比较基础）。
    /// 这条声明由两处把守：夹具自检**拒绝**含 `end_turn` 的受控无随机声明（回合切换走对端私有随机），
    /// 以及实测（脚本期间我方随机调用总数变化即降级为 <c>unsupported_rng_profile</c>）。
    /// </summary>
    public const string ControlledNoRandom = "controlled-no-random@1";
    public const string ControlledStateNoRandom = "controlled-state-no-random@1";

    /// <summary>路径依赖后端私有随机序列（ENV-01 未完成）：一律不计通过，差异只作证据。</summary>
    public const string UnsupportedRngProfile = "unsupported_rng_profile";
}

/// <summary>首差记录（ENV §6.3 要求的最小字段集）。</summary>
internal sealed record Divergence(
    string CaseId,
    int BoundaryIndex,
    string BoundaryKind,
    string Kind,
    string Path,
    string? Left,
    string? Right,
    string LastConsistentBoundary,
    string Reproduction);

internal sealed record ComparisonResult(
    string CaseId,
    string Verdict,
    string? Detail,
    IReadOnlyList<Divergence> Divergences);

/// <summary>
/// 逐步比较两个后端的记录，报**首差**。
///
/// 只比较有规则含义的可追踪状态：合法动作集合、有序牌区成员、语义槽位字段、随机流调用计数/状态指纹。
/// 原生实例 id 不参与比较（见 <see cref="EngineSnapshot"/>）。任一 case 出现差异立即停在该边界，
/// 不再继续比较后续边界（先确定首因，ENV §6.3）。
/// </summary>
internal static class Comparator
{
    private const int MaxDivergencesPerBoundary = 12;

    public static ComparisonResult Compare(CaseRecord left, CaseRecord right, string reproduction)
    {
        if (left.Status is "invalid_fixture" || right.Status is "invalid_fixture")
        {
            return new ComparisonResult(left.CaseId, "invalid_fixture",
                $"left={left.Status}:{left.Detail} right={right.Status}:{right.Detail}", []);
        }

        if (left.Status == "faulted" || right.Status == "faulted")
        {
            return new ComparisonResult(left.CaseId, "fault",
                $"left={left.Status}:{left.Detail} right={right.Status}:{right.Detail}", []);
        }

        if (left.Status is "invalid_action" or "unsupported_decision"
            || right.Status is "invalid_action" or "unsupported_decision")
        {
            return new ComparisonResult(left.CaseId, "diverged",
                $"脚本在一侧走不通：left={left.Status}:{left.Detail} right={right.Status}:{right.Detail}", []);
        }

        // 声明为原版兼容时才比较随机流。
        var profileCompatible = left.RngProfile == RngProfiles.VanillaCompatible
            && right.RngProfile == RngProfiles.VanillaCompatible;

        // 可计入通过的条件：原版兼容，**或**受控无随机且**实测**脚本期间没有消费新随机数。
        var countable = profileCompatible
            || (left.RngProfile == RngProfiles.ControlledNoRandom
                && right.RngProfile == RngProfiles.ControlledNoRandom
                && !left.RngConsumedByScript
                && !right.RngConsumedByScript)
            || (left.RngProfile == RngProfiles.ControlledStateNoRandom
                && right.RngProfile == RngProfiles.ControlledStateNoRandom
                && ControlledInit.Valid(left) && ControlledInit.Valid(right)
                && left.ScriptRandom?.MeasuredZero == true && right.ScriptRandom?.MeasuredZero == true
                && !left.RngConsumedByScript && !right.RngConsumedByScript);

        var divergences = new List<Divergence>();
        var lastConsistent = "（无：第 0 个边界之前就不同，或两侧边界数不同）";

        for (var index = 0; index < Math.Min(left.Boundaries.Count, right.Boundaries.Count); index++)
        {
            var boundaryDivergences = CompareBoundary(left, right, index, lastConsistent, reproduction,
                profileCompatible);
            if (boundaryDivergences.Count > 0)
            {
                divergences.AddRange(boundaryDivergences);
                return Verdict(left, right, countable, divergences);
            }

            lastConsistent = BoundaryLabel(left.Boundaries[index]);
        }

        if (left.Boundaries.Count != right.Boundaries.Count)
        {
            var shared = Math.Min(left.Boundaries.Count, right.Boundaries.Count);
            divergences.Add(new Divergence(left.CaseId, shared, "boundary-count", "boundary_count",
                $"boundaries[{shared}]",
                $"共 {left.Boundaries.Count} 个边界（{left.Backend}）",
                $"共 {right.Boundaries.Count} 个边界（{right.Backend}）",
                lastConsistent,
                reproduction));
            return Verdict(left, right, countable, divergences);
        }

        return Verdict(left, right, countable, divergences);
    }

    /// <summary>
    /// 判定：只有**可计入**的 case 才可能判 <c>passed</c>/<c>diverged</c>。
    /// 不可计入（随机路径各走各的）时一律 <c>unsupported_rng_profile</c>，差异作为**证据**附上，
    /// 并明确写出"这不是规则结论"——否则 RNG 差异会被读成规则差异（ENV §5/§6.3）。
    /// </summary>
    private static ComparisonResult Verdict(CaseRecord left, CaseRecord right, bool countable,
        IReadOnlyList<Divergence> divergences)
    {
        if (countable)
        {
            return divergences.Count > 0
                ? new ComparisonResult(left.CaseId, "diverged", null, divergences)
                : new ComparisonResult(left.CaseId, "passed", null, []);
        }

        var detail = $"rng_profile='{left.RngProfile}'（base 脚本消费随机={left.RngConsumedByScript}，"
            + $"reference 消费随机={right.RngConsumedByScript}）：该 Profile 下不可判定，不计通过。"
            + (divergences.Count > 0
                ? $"下方 {divergences.Count} 条差异是**证据**（随机调用序列不同导致的路径分叉），不是规则结论。"
                : string.Empty);
        return new ComparisonResult(left.CaseId, "unsupported_rng_profile", detail, divergences);
    }

    private static List<Divergence> CompareBoundary(CaseRecord left, CaseRecord right, int index,
        string lastConsistent, string reproduction, bool profileCompatible)
    {
        var divergences = new List<Divergence>();
        var a = left.Boundaries[index];
        var b = right.Boundaries[index];
        var at = $"boundary[{index}]";

        // 1. 合法动作集合（含候选与可绑定组合数）。
        foreach (var option in CompareActions(a.Actions, b.Actions))
        {
            divergences.Add(new Divergence(left.CaseId, index, a.Kind, "action",
                $"{at}.actions[{option.Name}]", option.Left, option.Right, lastConsistent, reproduction));
            if (divergences.Count >= MaxDivergencesPerBoundary)
            {
                return divergences;
            }
        }

        // 2. 决策点（候选集合与数量约束参与比较；Kind/Prompt 只入记录）。
        foreach (var difference in CompareDecisions(a.Decisions, b.Decisions))
        {
            divergences.Add(new Divergence(left.CaseId, index, a.Kind, difference.Kind,
                $"{at}.decisions{difference.Name}.{difference.Field}", difference.Left, difference.Right,
                lastConsistent, reproduction));
            if (divergences.Count >= MaxDivergencesPerBoundary)
            {
                return divergences;
            }
        }

        // 3. 规范化事实：牌区顺序/槽位字段/能力层数——两侧用同一套路径表达。
        foreach (var difference in CompareFacts(a.Facts, b.Facts))
        {
            divergences.Add(new Divergence(left.CaseId, index, a.Kind, KindOfFact(difference.Name),
                $"{at}.{difference.Name}", difference.Left, difference.Right,
                lastConsistent, reproduction));
            if (divergences.Count >= MaxDivergencesPerBoundary)
            {
                return divergences;
            }
        }

        // 4. 随机流：声明为原版兼容时比较。这里比的是**具名流**（显式映射到同一引擎流名、同一单位：
        //    自播种以来的生成器调用数）。动态/临时 RNG 的槽位不参与比较——它们没有稳定的跨后端键，
        //    而且是在比较单位的自然键之外；把不可比的槽位一起比只会制造假差异掩盖真正的规则差异。
        if (profileCompatible)
        {
            foreach (var stream in CompareStreams(NamedStreams(a.Streams), NamedStreams(b.Streams)))
            {
                divergences.Add(new Divergence(left.CaseId, index, a.Kind, stream.Kind,
                    $"{at}.streams[{stream.Name}].{stream.Field}",
                    stream.Left, stream.Right, lastConsistent, reproduction));
                if (divergences.Count >= MaxDivergencesPerBoundary)
                {
                    return divergences;
                }
            }
        }

        return divergences;
    }

    /// <summary>
    /// 可比流（显式映射的具名流）。base 侧只报具名引擎流；参考侧同一张快照里还带动态/临时 RNG
    /// （原始名、独立 <c>observed:</c> 槽位），那些没有稳定的跨后端键，不参与比较——
    /// 两侧的条目集合本身不同不是规则差异，只能当证据看。
    /// </summary>
    private static IReadOnlyList<StreamSnapshot> NamedStreams(IReadOnlyList<StreamSnapshot> streams) =>
        [.. streams.Where(Records.IsNamedStream)];

    /// <summary>事实路径 → 差异种类（供首差报告分类；不改变"只要值不同就是差异"这件事）。</summary>
    private static string KindOfFact(string path) => path switch
    {
        _ when path.StartsWith("pile.", StringComparison.Ordinal) => "collection",
        _ when path.StartsWith("rng.", StringComparison.Ordinal) => "stream",
        _ when path.StartsWith("action.", StringComparison.Ordinal) => "action",
        _ when path.StartsWith("decision.", StringComparison.Ordinal) => "decision",
        _ => "field",
    };

    private static IEnumerable<Difference> CompareFacts(
        IReadOnlyList<FactSnapshot> left, IReadOnlyList<FactSnapshot> right)
    {
        var leftMap = left.ToDictionary(fact => fact.Path, fact => fact.Value, StringComparer.Ordinal);
        var rightMap = right.ToDictionary(fact => fact.Path, fact => fact.Value, StringComparer.Ordinal);
        foreach (var path in leftMap.Keys.Concat(rightMap.Keys).Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var hasLeft = leftMap.TryGetValue(path, out var a);
            var hasRight = rightMap.TryGetValue(path, out var b);
            if (hasLeft != hasRight)
            {
                yield return new Difference(path, "fact", "present",
                    hasLeft ? "存在" : "缺失", hasRight ? "存在" : "缺失");
                continue;
            }

            if (a != b)
            {
                yield return new Difference(path, "fact", "value", a, b);
            }
        }
    }

    internal sealed record Difference(string Name, string Kind, string Field, string? Left, string? Right);

    private static IEnumerable<Difference> CompareActions(
        IReadOnlyList<ActionOption> left, IReadOnlyList<ActionOption> right)
    {
        foreach (var action in left.Select(item => item.Action)
                     .Concat(right.Select(item => item.Action)).Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var a = left.FirstOrDefault(item => item.Action == action);
            var b = right.FirstOrDefault(item => item.Action == action);
            if (a is null || b is null)
            {
                yield return new Difference(action, "action", "present",
                    a is null ? "缺失" : "存在", b is null ? "缺失" : "存在");
                continue;
            }

            if (!a.Candidates.SequenceEqual(b.Candidates, StringComparer.Ordinal))
            {
                yield return new Difference(action, "action", "candidates",
                    "[" + string.Join(",", a.Candidates) + "]",
                    "[" + string.Join(",", b.Candidates) + "]");
            }

            if (a.Bindings != b.Bindings)
            {
                yield return new Difference(action, "action", "bindings",
                    a.Bindings.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    b.Bindings.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    private static IEnumerable<Difference> CompareDecisions(
        IReadOnlyList<DecisionSnapshot> left, IReadOnlyList<DecisionSnapshot> right)
    {
        if (left.Count != right.Count)
        {
            yield return new Difference(string.Empty, "decision", "count",
                left.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                right.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            yield break;
        }

        for (var index = 0; index < left.Count; index++)
        {
            var a = left[index];
            var b = right[index];
            if (!a.Candidates.SequenceEqual(b.Candidates, StringComparer.Ordinal))
            {
                yield return new Difference($"[{index}]", "decision", "candidates",
                    "[" + string.Join(",", a.Candidates) + "]",
                    "[" + string.Join(",", b.Candidates) + "]");
            }

            if (a.Minimum != b.Minimum || a.Maximum != b.Maximum)
            {
                yield return new Difference($"[{index}]", "decision", "count",
                    $"{a.Minimum}..{a.Maximum}", $"{b.Minimum}..{b.Maximum}");
            }

            if (a.Chosen != b.Chosen)
            {
                yield return new Difference($"[{index}]", "decision", "chosen", a.Chosen, b.Chosen);
            }
        }
    }

    private static IEnumerable<Difference> CompareStreams(
        IReadOnlyList<StreamSnapshot> left, IReadOnlyList<StreamSnapshot> right)
    {
        foreach (var name in left.Select(item => item.Stream)
                     .Concat(right.Select(item => item.Stream)).Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var a = left.FirstOrDefault(item => item.Stream == name);
            var b = right.FirstOrDefault(item => item.Stream == name);
            if (a is null || b is null)
            {
                yield return new Difference(name, "stream", "present",
                    a is null ? "缺失" : "存在", b is null ? "缺失" : "存在");
                continue;
            }

            if (a.Draws != b.Draws)
            {
                yield return new Difference(name, "stream", "draws",
                    a.Draws.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    b.Draws.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (a.Fingerprint != b.Fingerprint)
            {
                yield return new Difference(name, "stream-state", "fingerprint", a.Fingerprint, b.Fingerprint);
            }
        }
    }

    private static string BoundaryLabel(BoundaryRecord boundary) =>
        $"{boundary.Index}:{boundary.Kind}（{boundary.Label}）";
}
