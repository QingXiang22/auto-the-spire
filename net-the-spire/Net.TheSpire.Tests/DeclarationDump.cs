using System.Text;
using BaseSpire.SinglePlayer;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;

namespace NetTheSpire.Tests;

/// <summary>
/// ENV-T03 诊断工具（不是检查）：把正式 dev 工厂的引擎声明原样打印出来，供"公开目录要从哪些声明派生"的
/// 逐条核对使用。它**只读**声明，不启动会话、不读状态、不参与 TOTAL 计数。
/// 用法：<c>dotnet run --project net-the-spire/Net.TheSpire.Tests -- --dump-declarations</c>
/// </summary>
internal static class DeclarationDump
{
    public static int Run()
    {
        var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad());
        var rules = factory.Rules;
        var build = factory.Describe();
        var text = new StringBuilder();
        text.AppendLine($"package={build.PackageId} scope={build.ScopeId} ruleset={build.RulesetId}");
        text.AppendLine($"publicBuild={build.PublicBuildId} projection={BaseSpireRuntimeFactory.ProjectionId}");
        text.AppendLine($"cards={build.CardCount} powers={build.PowerCount} enemies={build.EnemyCount}");

        text.AppendLine("== enums ==");
        foreach (var item in rules.Enums.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            text.AppendLine($"  {item.QualifiedId} [{string.Join(",", item.Members)}]");
        }

        text.AppendLine("== types ==");
        foreach (var type in rules.Types.OrderBy(type => type.QualifiedId, StringComparer.Ordinal))
        {
            text.AppendLine($"  {type.QualifiedId}");
            foreach (var field in type.Fields)
            {
                text.AppendLine(
                    $"    {field.Name}: {field.Kind} vis={field.Visibility} life={field.Lifetime} " +
                    $"optional={field.Optional} enum={Name(rules, field.EnumTypeId)} target={Name(rules, field.TargetTypeId)} " +
                    $"elem={Name(rules, field.ElementTargetTypeId)}");
            }
        }

        text.AppendLine("== projections ==");
        foreach (var projection in rules.Projections.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            text.AppendLine($"  {projection.QualifiedId} role={projection.ParticipantRole} " +
                            $"collections={projection.IncludeCollections} relations={projection.IncludeRelations}");
            foreach (var (id, item) in projection.Types.OrderBy(pair => pair.Key))
            {
                text.AppendLine($"    type#{id} {item.Type.QualifiedId} include={item.Include} " +
                                $"owner={item.OwnerFieldIndex} hidden={item.IncludeHiddenFields}");
            }
        }

        text.AppendLine("== collections ==");
        foreach (var collection in rules.Collections.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            text.AppendLine($"  {collection.QualifiedId} element={Name(rules, collection.ElementTypeId)} " +
                            $"ordered={collection.Ordered} group={collection.MembershipGroup} cap={collection.Capacity} " +
                            $"identity={collection.IdentityVisible} size={collection.SizeVisible} order={collection.OrderVisible}");
        }

        text.AppendLine("== relations ==");
        foreach (var relation in rules.Relations.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            text.AppendLine($"  {relation.QualifiedId} target={Name(rules, relation.TargetTypeId)} many={relation.Many} public={relation.Public}");
        }

        text.AppendLine("== actions ==");
        foreach (var action in rules.Actions.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            var parameters = string.Join(", ", action.Program.Parameters.Select(field =>
                $"{field.Name}:{field.Kind}{(field.Optional ? "?" : "")}"));
            text.AppendLine($"  {action.QualifiedId} role={action.ParticipantRole} " +
                            $"candidateSlot={action.CandidateParameterIndex} choices=[{string.Join(",", action.Choices.Keys)}] " +
                            $"params=[{parameters}]");
        }

        text.AppendLine("== terminals ==");
        foreach (var terminal in rules.TerminalResults.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            text.AppendLine($"  {terminal.QualifiedId} fields=[{string.Join(",", terminal.Fields.Select(f => f.Name))}]");
        }

        Console.Out.Write(text.ToString());
        return 0;
    }

    private static string Name(RuleSet rules, int id) => id < 0 ? "-" : rules.Type(id).QualifiedId;
}
