using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class ContentCompilation
{
    internal static ContentBindings Content(PackageBuildOptions options)
    {
        var package = new EnginePackageBuilder(SliceManifest.PackageId,
            options.PackageVersion ?? SliceManifest.Version);

        var author = options.AuthorModel ?? (options.FromAuthorText ? SliceAuthorFrontend.Load() : null);

        return new ContentBindings(package, author);
    }

    internal static SliceCard CardOf(ContentBindings contentBindings, string contentId) => contentBindings.Author is null
                ? SliceManifest.Card(contentId)
                : SliceAuthorFrontend.ToSliceCard(contentBindings.Author.Cards.First(c => c.Id == contentId));

    internal static SliceEnemy EnemyOf(ContentBindings contentBindings, string contentId) => contentBindings.Author is null
                ? SliceManifest.Enemy(contentId)
                : SliceAuthorFrontend.ToSliceEnemy(contentBindings.Author.Enemies.First(e => e.Id == contentId));
}
