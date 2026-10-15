using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal sealed record PackageBuildOptions(
    bool IncludeTestHeadbutt,
    bool FromAuthorText,
    SliceAuthorModel? AuthorModel,
    bool SwapEncounterOrder,
    bool SameDefinitionEnemyPair,
    string? PackageVersion,
    SliceEncounterPool? EncounterPool,
    bool ShuffleBattleDeck,
    bool TerminalOnConfiguredRoomCount,
    IReadOnlyList<SliceRelic>? RelicLibrary,
    IReadOnlyList<string>? StartingRelics,
    string? SpawnHpStream,
    bool ActMapEnabled,
    bool InitializeNewRun);
