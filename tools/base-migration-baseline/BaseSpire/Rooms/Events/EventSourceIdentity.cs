namespace BaseSpire.Slice.Rooms;

/// <summary>
/// ENV-T09-unknown-room-modules-20261012：**源事件身份 ↔ 已注册模块 ID** 的唯一转换点。
///
/// 两种身份各自的来源：
/// <list type="bullet">
/// <item>源事件身份 = `Models/Events/&lt;Name&gt;.cs` 的类名（`SunkenStatue`），它由
///       <see cref="RunSourcePoolManifest.Events"/> 按源 `Overgrowth.AllEvents + ModelDb.AllSharedEvents`
///       的枚举顺序给出，也是 `RunEventQueue` 成员里 `SourceId` 的取值 —— 队列身份与源池顺序都不改写。</item>
/// <item>已注册模块 ID = <see cref="IEventRoomModule.Id"/>，也是 `EventCompileContext.CurrentEventId`
///       的取值。</item>
/// </list>
///
/// 为什么映射本身是恒等：`EventRoomCatalog.Enter` 的运行时分派只有"IR 字符串相等"这一种原语，
/// 队列里的源名字必须能与模块 ID **逐字**相等，否则分派永远落到"未注册"分支。因此这里不做任何
/// 重命名（不加前缀、不转短横线、不改大小写）—— 它的价值在**建包期校验**：不在源池里的名字
/// 立即失败，于是"模块 ID 拼错或改写源名"不会以运行期静默失配的形式出现。
///
/// 未实现身份的处理：源池里没有注册模块的身份**不在这里被替换**，由 `EventRoomCatalog.Enter`
/// 的未注册分支显式失败（不伪造离开选项、不静默替代）。
/// </summary>
public static class EventSourceIdentity
{
    /// <summary>源池里的全部事件身份（源顺序）。</summary>
    public static IReadOnlyList<string> SourcePool => RunSourcePoolManifest.Events;

    /// <summary>源身份是否属于本构建登记的源事件池。</summary>
    public static bool IsKnownSource(string sourceName) =>
        !string.IsNullOrWhiteSpace(sourceName)
        && RunSourcePoolManifest.Events.Contains(sourceName, StringComparer.Ordinal);

    /// <summary>
    /// 源身份 → 注册模块 ID。越界（不在源池里的名字）在**建包期**显式失败。
    /// </summary>
    public static string ModuleId(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        if (!IsKnownSource(sourceName))
        {
            throw new ArgumentException(
                $"'{sourceName}' 不在源事件池 RunSourcePoolManifest.Events 里："
                + "事件模块 ID 必须逐字等于源事件身份（不重命名、不缩池）", nameof(sourceName));
        }

        return sourceName;
    }

    /// <summary>注册模块 ID → 源身份（同一个转换的逆方向；同样校验源池成员）。</summary>
    public static string SourceName(string moduleId) => ModuleId(moduleId);
}
