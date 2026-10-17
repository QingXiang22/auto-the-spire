using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Single-draw authority for regular draw and draw-until; queries are evaluated in the Engine.</summary>
public sealed class CardDrawProcedures(CardPileSymbols piles, FieldSymbol kind, EnumTypeSymbol kinds,
    ProcedureSymbol clearKnown, ProcedureSymbol revokeKnown, Func<IrScope, Expr> prevented,
    SourceStableShuffleSeam sourceOrder,
    Func<IrScope, Expr, Inst[]>? afterDraw = null,
    Func<IrScope, Inst[]>? afterShuffle = null)
{
    public ProcedureSymbol Declare(EnginePackageBuilder package)
    {
        var draw = package.Procedure("DrawCards");
        var count = draw.Param("Count", FieldTypeSchema.Int(0, 9999));
        draw.Body(draw.Repeat(draw.Param(count), One(draw)));
        return draw.Symbol;
    }

    private Inst[] One(IrScope scope, int? result = null)
    {
        var drawn = result ?? scope.Local("drawn", FieldTypeSchema.Binding());
        return [
            scope.SetLocal(drawn, scope.Const(Value.None)),
            scope.If(scope.And(scope.Not(prevented(scope)),
                scope.Less(scope.Count(scope.Collection(piles.Hand)), scope.Const((long)SliceManifest.MaxHandSize))),
                Reshuffle(scope),
                scope.SetLocal(drawn, scope.First(scope.Collection(piles.Draw))),
                scope.If(scope.Not(scope.IsNone(scope.LocalRef(drawn))),
                    scope.RemoveFromCollection(piles.Draw, scope.LocalRef(drawn)),
                    scope.Call(revokeKnown, scope.LocalRef(drawn)),
                    scope.AddToCollection(piles.Hand, scope.LocalRef(drawn)),
                    scope.If(scope.Const(true), afterDraw?.Invoke(scope, scope.LocalRef(drawn)) ?? [])))];
    }

    /// <summary>
    /// 源 `CardPileCmd.ShuffleIfNecessary → Shuffle`（<c>sts2/src/Core/Commands/CardPileCmd.cs:864-876</c>）：
    /// 合并后的牌堆（弃牌堆 ++ 抽牌堆余牌）**先按源 `CardModel.CompareTo` 排序，再 Fisher–Yates**。
    /// 触发条件与源一致（抽牌堆空且弃牌堆非空），因此这里的合并顺序就是弃牌堆顺序。
    ///
    /// 排序本身在本方法里按"源排序键快照 → 纯计算给出每一步的当前下标 → 移到末尾"实现
    /// （见 <see cref="OrderLikeSource"/> 与 `Randomness/SourceStableShuffle.cs`）；随机数仍然只由
    /// `ShuffleCollection` 消费一次，条数不变。
    /// </summary>
    public Inst Reshuffle(IrScope scope) => scope.If(scope.And(
        scope.IsNone(scope.First(scope.Collection(piles.Draw))), scope.Exists(scope.Collection(piles.Discard))),
        [
            scope.Call(clearKnown),
            scope.ForEach(scope.Collection(piles.Discard), card => [
                scope.RemoveFromCollection(piles.Discard, card), scope.AddToCollection(piles.Draw, card)]),
            .. OrderLikeSource(scope),
            scope.ShuffleCollection(piles.Draw, SliceManifest.DrawStream),
            .. afterShuffle?.Invoke(scope) ?? [],
        ]);

    /// <summary>
    /// 源 `StableShuffle` 的预排序，按牌区当前顺序执行：
    /// <list type="number">
    /// <item>把每张牌的**源排序键**（`ENTRY@升级等级`，见 `CardSchema.SourceOrderKey`）拼成快照 blob；</item>
    /// <item>每一步问纯计算"目标顺序第 step 张现在在哪个下标"，把那张牌移到末尾。</item>
    /// </list>
    /// 不变量：集合 = [尚未放置的（保持原相对次序）] ++ [已放置的（源目标顺序）]，所以每一步的下标就是
    /// 该牌在未放置前缀里的名次。不新增运行态、不消耗随机数、不改公开输入。
    /// </summary>
    private Inst[] OrderLikeSource(IrScope scope)
    {
        var keys = scope.Local("sourceShuffleKeys", FieldTypeSchema.String(SourceStableShuffle.MaxBlobLength));
        var count = scope.Local("sourceShuffleCount", FieldTypeSchema.Int(0, 9999));
        var step = scope.Local("sourceShuffleStep", FieldTypeSchema.Int(0, 9999));
        var index = scope.Local("sourceShuffleIndex", FieldTypeSchema.Int(0, 9999));
        return
        [
            scope.SetLocal(count, scope.Count(scope.Collection(piles.Draw))),
            scope.SetLocal(keys, scope.Const(string.Empty)),
            scope.ForEach(scope.Collection(piles.Draw), card => [
                scope.SetLocal(keys, scope.Concat(scope.LocalRef(keys),
                    scope.Field(card, sourceOrder.CardKey), scope.Const(SourceStableShuffle.KeySeparator)))]),
            scope.SetLocal(step, scope.Const(0L)),
            scope.Repeat(scope.LocalRef(count),
                scope.Compute(sourceOrder.Asset,
                    [(sourceOrder.Keys, scope.LocalRef(keys)), (sourceOrder.Step, scope.LocalRef(step))],
                    [(sourceOrder.Index, index)]),
                scope.MoveInCollection(piles.Draw,
                    scope.First(scope.Skip(scope.Collection(piles.Draw), scope.LocalRef(index))),
                    InsertPosition.End),
                scope.SetLocal(step, scope.Add(scope.LocalRef(step), scope.Const(1L)))),
        ];
    }

    public Inst[] Until(IrScope scope, string continueKind)
    {
        var active = scope.Local("continueDrawing", FieldTypeSchema.Bool());
        var drawn = scope.Local("drawUntilCard", FieldTypeSchema.Binding());
        // At most MaxHandSize successful draws; failure ends immediately, including NoDraw/full hand/empty piles.
        return [scope.SetLocal(active, scope.Const(true)),
            scope.Repeat(scope.Const((long)SliceManifest.MaxHandSize),
                scope.If(scope.LocalRef(active), [.. One(scope, drawn),
                    scope.SetLocal(active, scope.And(scope.Not(scope.IsNone(scope.LocalRef(drawn))),
                        scope.Equal(scope.Field(scope.LocalRef(drawn), kind), scope.EnumConst(kinds, continueKind))))]))];
    }
}
