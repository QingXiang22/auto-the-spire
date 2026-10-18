using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>文本规则场景的组合结果：Host、用于 ruleset_id 的公开构建身份、编译出的规则集。</summary>
internal sealed record TextRuleScenarioSetup(ControlHost Host, string PublicBuildIdentity, RuleSet Rules);

/// <summary>
/// 场景 <c>text-rules</c>：把任意 core DSL 文本规则包接到 Host 的唯一组合点（design.md §7.3 第 4 项）。
/// </summary>
/// <remarks>
/// 目录、观察、候选、身份全部走 <see cref="DerivedPublicProfile"/> 派生；本类不注册任何逐项字段/
/// 动作，也不含具体游戏名。规则包新增公开字段或合法动作只改规则包与（必要时）四类不可推导元数据，
/// 这里的生产代码一行不动。baseline 与 extended 两个规则包走同一份代码。
/// </remarks>
internal static class TextRuleScenario
{
    /// <summary>场景名，与 <c>--scenario</c> 取值一致。</summary>
    public const string Scenario = "text-rules";

    public static TextRuleScenarioSetup CreatePackage(string manifestPath, string? scope = null,
        int ledgerCapacity = 4096, string? defaultGameSeed = null)
    {
        var package = DslPackage.Load(manifestPath, scope);
        var spec = TextRuleProviderSpec.Load(package.PublicMetadataPath);
        if (spec.ScenarioProfile != package.Scope) throw new FormatException("Package scope and public metadata disagree.");
        spec = spec with { Metadata = spec.Metadata with { StaticBuildIdentity = spec.Metadata.StaticBuildIdentity + "|package=" + package.Digest } };
        return Create([package.EntryPath], spec, ledgerCapacity, defaultGameSeed, package.Rules, package);
    }

    public static TextRuleScenarioSetup Create(
        IReadOnlyList<string> rulePaths,
        TextRuleProviderSpec spec,
        int ledgerCapacity = 4096,
        string? defaultGameSeed = null,
        RuleSet? compiledRules = null, DslPackage? package = null)
    {
        ArgumentNullException.ThrowIfNull(rulePaths);
        ArgumentNullException.ThrowIfNull(spec);
        if (rulePaths.Count == 0)
        {
            throw new ArgumentException("文本规则场景至少要一个规则包入口文件", nameof(rulePaths));
        }

        // 通用规则提供方：core 的 DSL 编译器加载入口文件与其递归 imports，按依赖序编译。
        var rules = compiledRules ?? EngineDslCompiler.LoadFiles([.. rulePaths]);

        // 公开构建身份必须反映规则包本身：把它折进 BuilderSet 的语义身份 RuleSet.Identity，
        // 这样「只在规则包里新增公开字段/合法动作」也一定会换 ruleset_id/catalog_id；
        // 每局 seed 永不参与（DerivedPublicProfile.Build 不收 seed）。
        var metadata = spec.Metadata with
        {
            StaticBuildIdentity = spec.Metadata.StaticBuildIdentity + "|rules=" + rules.Identity,
        };

        var profile = DerivedPublicProfile.Build(
            rules,
            spec.ProjectionId,
            spec.ParticipantRole,
            metadata,
            NetProtocol.V02);

        var source = new TextRuleSource(
            rules,
            profile,
            spec.ScenarioProfile,
            spec.ProjectionId,
            spec.ParticipantRole,
            defaultGameSeed ?? spec.DefaultGameSeed, package);

        return new TextRuleScenarioSetup(new ControlHost(source, ledgerCapacity), metadata.StaticBuildIdentity, rules);
    }
}
