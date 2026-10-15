using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Card instance schema shared by all builders. No runtime state lives in this object.</summary>
public sealed record CardSchema(EntityTypeSymbol Type, FieldSymbol Definition, FieldSymbol Kind,
    FieldSymbol Upgraded, FieldSymbol Unplayable, FieldSymbol Ethereal, FieldSymbol Cost,
    FieldSymbol CostModifier, FieldSymbol Amount, FieldSymbol PowerId, FieldSymbol PowerStacks,
    FieldSymbol LongTermSource, FieldSymbol CloneOf, FieldSymbol FreeUntilPlayed,
    FieldSymbol SourceOrderKey)
{
    public static CardSchema Declare(EnginePackageBuilder package, EnumTypeSymbol kind)
    {
        var type = package.EntityType("Card");
        return new(type,
            type.AddField("DefinitionId", FieldTypeSchema.String(64)),
            type.AddField("Kind", FieldTypeSchema.EnumOf(kind)),
            type.AddField("Upgraded", FieldTypeSchema.Bool()),
            type.AddField("Unplayable", FieldTypeSchema.Bool()),
            type.AddField("Ethereal", FieldTypeSchema.Bool()),
            type.AddField("Cost", FieldTypeSchema.Int(0, 9)),
            type.AddField("CostModifier", FieldTypeSchema.Int(-999, 999)),
            type.AddField("Amount", FieldTypeSchema.Int(0, 999)),
            type.AddField("PowerId", FieldTypeSchema.String(64)),
            type.AddField("PowerStacks", FieldTypeSchema.Int(0, 99)),
            type.AddField("LongTermSource", FieldTypeSchema.RefTo(type), visibility: FieldVisibility.Hidden, optional: true),
            type.AddField("CloneOf", FieldTypeSchema.RefTo(type), visibility: FieldVisibility.Hidden, optional: true),
            type.AddField("FreeUntilPlayed", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden),
            // ENV-T06-source-stable-shuffle-20261012：**源排序键**（源 `CardModel.CompareTo`：
            // `ModelId.Entry` 序数 + `CurrentUpgradeLevel`；见 `Slice/SourceStableShuffle.cs`）。
            // 它是**编译辅助元数据**（定义不变量 + 当前升级形态），隐藏、不进公开输入；
            // 创建/复制/升级（普通、升级、克隆、变形）都必须写出它，否则重洗时读空值会直接报错。
            type.AddField("SourceOrderKey", FieldTypeSchema.String(SourceStableShuffle.MaxKeyLength),
                visibility: FieldVisibility.Hidden));
    }
}
