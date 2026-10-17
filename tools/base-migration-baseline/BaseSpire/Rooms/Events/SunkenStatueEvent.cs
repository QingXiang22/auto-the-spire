using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// 源 <c>Models/Events/SunkenStatue.cs</c>（Overgrowth 事件 `SunkenStatue`）的独立事件模块。
///
/// 源行为（逐条对照）：
/// <list type="bullet">
/// <item>`CanonicalVars`：`Relic` = `SwordOfStone`、`GoldVar(111)`、`HpLoss = 7`。</item>
/// <item>`CalculateVars`：`DynamicVars.Gold.BaseValue += (decimal)base.Rng.NextInt(-10, 11)`
///       —— 进入事件时**无条件**掷一笔事件级随机数（见 <see cref="EventRandomAsset"/>），
///       结果就是本页的金币量，与玩家选哪个选项无关。</item>
/// <item>`GenerateInitialOptions`：`GRAB_SWORD`（获得 `SwordOfStone`）与 `DIVE_INTO_WATER`
///       （`PlayerCmd.GainGold(Gold)` + `CreatureCmd.Damage(7, Unblockable|Unpowered)`），
///       两个选项之后事件结束（`SetEventFinished`）。</item>
/// <item>`IsAllowed` **没有覆盖** ⇒ 本模块不实现 <see cref="IEventEligibility"/>（源默认 true）。</item>
/// </list>
///
/// 选项支持边界（明确失败，不静默省略、不用别的选项替代）：
/// <list type="bullet">
/// <item>`DIVE_INTO_WATER` 只需要"金币写回 + 无格挡/无能力修正的 7 点生命损失"，属已支持机制，
///       由共享RunResources生成IR；选择结束时生命归零则关闭访问并发布Defeat。</item>
/// <item>`GRAB_SWORD` 需要遗物 `SwordOfStone` 的**获得与效果**，二者都尚未实现：动作保留源选项身份，
///       但选择它**显式失败**并说明原因（源上没有别的选项可以替代，也不伪造离场）。</item>
/// </list>
/// </summary>
public sealed class SunkenStatueEvent : ChoiceEventModule
{
    /// <summary>源类名（= `RunSourcePoolManifest.Events` 的成员 = `RunEventQueue` 的 `SourceId`）。</summary>
    public const string SourceName = "SunkenStatue";

    /// <summary>源 `SUNKEN_STATUE.pages.INITIAL.options.GRAB_SWORD`。</summary>
    public const string GrabSwordAction = "SunkenStatueGrabSword";

    /// <summary>源 `SUNKEN_STATUE.pages.INITIAL.options.DIVE_INTO_WATER`。</summary>
    public const string DiveIntoWaterAction = "SunkenStatueDiveIntoWater";

    /// <summary>源 `new GoldVar(111)`。</summary>
    public const int BaseGold = 111;

    /// <summary>源 `base.Rng.NextInt(-10, 11)`。</summary>
    public const int GoldVarianceMin = -10;

    public const int GoldVarianceMax = 11;

    /// <summary>源 `_hpLossKey = "HpLoss"` = 7。</summary>
    public const int HpLoss = 7;

    private readonly EventVisitState state;

    public SunkenStatueEvent(EventVisitState state) =>
        this.state = state ?? throw new ArgumentNullException(nameof(state));

    public override string Id => EventSourceIdentity.ModuleId(SourceName);

    /// <summary>源 `EventModel.BeginEvent` → `CalculateVars`：掷出本页金币量（一次事件级抽取）。</summary>
    public override Inst[] Enter(IrScope scope, EventCompileContext context, Expr run)
    {
        var draw = state.Random.NextInt(scope, SourceName, GoldVarianceMin, GoldVarianceMax);
        return
        [
            .. draw.Steps,
            scope.SetField(run, state.EventPageGold,
                scope.Add(scope.Const((long)BaseGold), scope.LocalRef(draw.ValueSlot))),
        ];
    }

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context) =>
    [
        new(DiveIntoWaterAction, effects =>
        [
            .. effects.GainGold(effects.Read(state.EventPageGold)),
            .. effects.DamageOutsideCombat(HpLoss),
        ]),
        new(GrabSwordAction, effects =>
        [effects.Unsupported("base-spire: SunkenStatue 的 GrabSword 需要遗物 SwordOfStone 的获得与效果，尚未实现"
            + "（保留源选项身份，不静默省略、不用别的选项替代）")]),
    ];
}
