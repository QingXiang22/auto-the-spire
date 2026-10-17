using BaseSpire.Slice;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// I-71/I-84 判别：`describe` 的 `supported_structures[].declaration_ids` 必须**逐条**落在同一运行时的
/// `get_catalog` 里（`docs/contracts/net/0.1/invariants.md` §2.6 I-71，闭合要求见 I-84 行；0.2 沿用同一规则）。
///
/// 判别的是**真实 wire 结果**，不是 Profile 的内部清单：`describe` 和 `get_catalog` 各发一次请求，
/// 用同一个 DTO 解析器读回来再比。修复前的错误形态是把映射表的**引擎 qualified id**
/// （`base-spire:Player`）当公开声明 ID 发出去，而目录发布的是 `net:kind:base-spire:Player` 这类公开 ID ——
/// 那种输入必须被判开，因此本组同时验证"正例闭合"和"反例可区分"。
/// </summary>
internal static class DeclarationClosure
{
    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("I-71/I-84 0.1 切片 describe 的声明 ID 与同一 get_catalog 闭合；引擎键反例可区分 [真实]",
            SliceDescriptionsCloseAgainstCatalog);
    }

    /// <summary>目录声明的全部 ID：九张声明表的 `id`（与共享 L4 校验器同口径）。</summary>
    public static HashSet<string> DeclaredIds(DeclarationsDto declarations)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in Tables(declarations))
        {
            foreach (var id in table)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>返回 `describe` 引用了、但同一 `get_catalog` 未声明的 `structure:declaration_id`。</summary>
    public static List<string> Unclosed(DescribeResultDto describe, PublicCatalogDto catalog)
    {
        var declared = DeclaredIds(catalog.Declarations);
        var unclosed = new List<string>();
        foreach (var structure in describe.Capabilities.SupportedStructures)
        {
            foreach (var id in structure.DeclarationIds)
            {
                if (!declared.Contains(id))
                {
                    unclosed.Add($"{structure.Structure}:{id}");
                }
            }
        }

        return unclosed;
    }

    /// <summary>
    /// 返回被当成公开声明 ID 发出去的**引擎映射键**。这些键是 <see cref="PublicSnapshotProfile"/> 里
    /// "引擎 qualified id → 公开声明"的映射键，本身不在目录里；把它们写进 `declaration_ids` 就是
    /// 用引擎/机制名冒充公开声明（修复前的实际缺陷）。
    /// </summary>
    public static List<string> EngineKeysUsedAsDeclarationIds(DescribeResultDto describe, PublicSnapshotProfile profile)
    {
        var offenders = new List<string>();
        foreach (var structure in describe.Capabilities.SupportedStructures)
        {
            var engineKeys = structure.Structure switch
            {
                "Entity" => profile.Kinds.Keys,
                "Relation" => profile.Relations.Keys,
                "LegalAction" => profile.Actions.Keys,
                "TerminalResult" => profile.Terminals.Keys,
                _ => [],
            };
            foreach (var id in structure.DeclarationIds)
            {
                if (engineKeys.Contains(id, StringComparer.Ordinal))
                {
                    offenders.Add($"{structure.Structure}:{id}");
                }
            }
        }

        return offenders;
    }

    /// <summary>某个结构声明的 ID 列表（结构名不存在时显式失败，不用空表冒充"没声明"）。</summary>
    public static IReadOnlyList<string> StructureIds(DescribeResultDto describe, string structure) =>
        describe.Capabilities.SupportedStructures.Single(item => item.Structure == structure).DeclarationIds;

    private static IEnumerable<IEnumerable<string>> Tables(DeclarationsDto declarations)
    {
        yield return declarations.EntityKinds.Select(item => item.Id);
        yield return declarations.ContentTypes.Select(item => item.Id);
        yield return declarations.Fields.Select(item => item.Id);
        yield return declarations.RelationTypes.Select(item => item.Id);
        yield return declarations.PileTypes.Select(item => item.Id);
        yield return declarations.SequenceTypes.Select(item => item.Id);
        yield return declarations.GroupTypes.Select(item => item.Id);
        yield return declarations.ActionTypes.Select(item => item.Id);
        yield return declarations.TerminalReasons.Select(item => item.Id);
    }

    // -- 0.1 切片真实宿主 -------------------------------------------------------------------------------------

    private static void SliceDescriptionsCloseAgainstCatalog()
    {
        var profile = SlicePublicProfile.Create();
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build().Build());
        // 只构造宿主、不开局：describe/get_catalog 不推进游戏（protocol.md:174-175）。
        var host = new ControlHost(builder.Build(), profile, SliceManifest.Player, SliceManifest.PlayerRole,
            "base-spire:PlayerView", 20261001UL, computeBackend: BaseSpireComputeBackend.Instance);

        var describeResponse = host.Dispatch(Req("dc-describe", Operation.Describe));
        Check.True(describeResponse.Ok, $"describe 必须成功：{describeResponse.Error?.Code}");
        var describe = DescribeResultDto.FromWire(describeResponse.Result!);

        var catalogResponse = host.Dispatch(Req("dc-catalog", Operation.GetCatalog,
            ("catalog_id", WireValue.Str(profile.CatalogId))));
        Check.True(catalogResponse.Ok, $"get_catalog 必须成功：{catalogResponse.Error?.Code}");
        var catalog = PublicCatalogDto.FromWire(catalogResponse.Result!);

        // 正例：真实宿主的声明必须逐条闭合。
        var unclosed = Unclosed(describe, catalog);
        Check.Equal(0, unclosed.Count,
            $"describe 的 declaration_ids 必须全部在本目录声明（I-84）：{string.Join(",", unclosed)}");
        var engineKeys = EngineKeysUsedAsDeclarationIds(describe, profile);
        Check.Equal(0, engineKeys.Count,
            $"describe 不得把引擎映射键当公开声明 ID：{string.Join(",", engineKeys)}");

        // 声明 ID 必须是**已发布的公开形态**（不是"恰好闭合"的空表）：实体/动作/终局/关系各有实际条目。
        Check.True(StructureIds(describe, "Entity").Contains("net:kind:base-spire:Player", StringComparer.Ordinal),
            "Entity 必须声明已发布的 kind id net:kind:base-spire:Player");
        Check.True(StructureIds(describe, "LegalAction").Contains("net:action:base-spire:PlayCard", StringComparer.Ordinal),
            "LegalAction 必须声明 PlayCard 的公开动作类型");
        Check.True(StructureIds(describe, "LegalAction").Contains("net:action:net.answer_decision", StringComparer.Ordinal),
            "LegalAction 必须登记净自声明的决策应答类型（wire 候选里会出现它）");
        Check.True(StructureIds(describe, "TerminalResult").Count > 0, "必须声明终局原因");

        // 反例（可区分性自证）：把 Entity 的声明 ID 换成**引擎键**（修复前的错误形态），同一判据必须报出来。
        // 否则"0 违规"只说明判据空转。
        var perturbed = new DescribeResultDto(describe.Identity, describe.Capabilities with
        {
            SupportedStructures =
            [
                .. describe.Capabilities.SupportedStructures.Select(structure =>
                    structure.Structure == "Entity"
                        ? structure with { DeclarationIds = ["base-spire:Player", "base-spire:Card"] }
                        : structure),
            ],
        });
        var reopened = Unclosed(perturbed, catalog);
        Check.Equal(2, reopened.Count, $"反例必须被判开（实际 {string.Join(",", reopened)}）");
        Check.True(reopened.Contains("Entity:base-spire:Player", StringComparer.Ordinal),
            "反例必须定位到 Entity:base-spire:Player");
        Check.Equal(2, EngineKeysUsedAsDeclarationIds(perturbed, profile).Count,
            "引擎键判别必须能识别反例（不是恒空）");
    }

    private static RequestDto Req(string id, Operation operation, params (string Key, WireValue Value)[] members) =>
        new(id, operation, new WireObject(members.Select(member =>
            new KeyValuePair<string, WireValue>(member.Key, member.Value))));
}
