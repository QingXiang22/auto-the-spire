using CoreTheSpire;

namespace BaseSpire;

internal static class IroncladCardPool
{
    public static void Register(ContentRegistry registry)
    {
        foreach (var pool in BaseSpireContent.LoadCardPools()) registry.Register(pool);
    }
}
