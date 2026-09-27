using CoreTheSpire;

namespace BaseSpire;

internal static class IroncladCardPool
{
    public static void Register(ContentRegistry registry) =>
        registry.Register(new CardPoolDefinition(BaseSpireIds.IroncladCardPool, []));
}
