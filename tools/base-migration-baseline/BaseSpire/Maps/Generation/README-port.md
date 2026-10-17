# SourceMap — faithful port of the StS2 act-map generator (word-supply driven)

Namespace: `BaseSpire.Slice.SourceMap`. Plain C#, no external packages, no `System.Random`, no time, no
environment-dependent ordering. Target: `net9.0` / `LangVersion 13` (same settings as
`mod-the-spire/base-spire/BaseSpire/BaseSpire.csproj`).

Source of truth (game code, read-only): `sts2/src/Core/Map/{ActMap,StandardActMap,MapPoint,MapPointType,MapPointState,MapCoord,MapPointTypeCounts,MapPathPruning,MapPostProcessing}.cs`,
`sts2/src/Core/Extensions/ListExtensions.cs`, `sts2/src/Core/Random/{Rng,MegaRandom}.cs`.
Structural template (same-shaped port that documents its own deviations as `偏离 #46` / `#51`):
`reference/sts2-sim/src/Sts2Sim.Core/Map/*.cs`, `.../Random/Rng.cs`.

## Files

| File | Ports | Notes |
|---|---|---|
| `SourceMapRandom.cs` | `Random/Rng.cs` + `Random/MegaRandom.cs` draw semantics | 53-bit payload replay; `SourceMapWordsExhaustedException` |
| `ShuffleExtensions.cs` | `Extensions/ListExtensions.cs` | `StableShuffle` / `UnstableShuffle` |
| `MapCoord.cs` | `Map/MapCoord.cs` | serialization surface dropped |
| `MapPointType.cs` | `Map/MapPointType.cs` | numeric values are output contract |
| `MapPointState.cs` | `Map/MapPointState.cs` | unused by generation |
| `MapPoint.cs` | `Map/MapPoint.cs` | quest surface dropped |
| `ActMap.cs` | `Map/ActMap.cs` | unchanged logic |
| `StandardActMap.cs` | `Map/StandardActMap.cs` | `ActModel`-free constructor |
| `MapPointTypeCounts.cs` | `Map/MapPointTypeCounts.cs` | ascension lookup → A0 constant |
| `MapPathPruning.cs` | `Map/MapPathPruning.cs` | verbatim draw order |
| `MapPostProcessing.cs` | `Map/MapPostProcessing.cs` | zero RNG, rewrites `coord.col` |
| `SourceActMapGenerator.cs` | *(new)* | public entry point + flat cell projection |

`MapTravel.cs` is deliberately **not** ported: it is travel/UI state, not generation, and it is not part of
the 13-file deliverable.

---

## A. Word supply, RNG surface, draw ledger

### A.1 The supply

`SourceMapRandom(IReadOnlyList<ulong> payloads, long startIndex = 0)`.

One element of `payloads` = **one 53-bit payload** = `raw64 >> 11` of a single source-generator word
(`[0, 2^53)`; the test side produces it with `SampleInt(stream, 0, 2^53)`). Mapping back to the game:

| Port | Game (`MegaRandom`) | Payload math |
|---|---|---|
| `NextDouble()` = `(double)payload * 1.1102230246251565E-16` | `(double)(raw >> 11) * 1.1102230246251565E-16` | `payload = raw >> 11`, **no second `>> 11`** |
| `NextFloat()` = `(float)(payload >> 29) * 5.9604645E-08f` | `(float)(raw >> 40) * 5.9604645E-08f` | `payload >> 29 == raw >> 40` |
| `NextInt(maxExclusive)` = `(int)(NextDouble() * maxExclusive)` | `MegaRandom.Next(int)` | 1 payload |
| `NextInt(min, max)` = `(int)(NextDouble() * (max - min)) + min` (long-range branch mirrored) | `Rng.NextInt(min, max)` → `MegaRandom.Next(min, max)` → `NextInner((int)range) + min` | 1 payload |
| `NextGaussianInt(mean, sd, min, max)` | `Rng.NextGaussianInt` (sine branch) | 2 payloads per iteration, retried out of range |
| `NextGaussianDouble(mean, sd, min, max)` | `Rng.NextGaussianDouble` (cosine branch) | 2 payloads per iteration, retried while outside `[0,1]` |
| `NextItem<T>(items)` = `items[NextInt(0, items.Count)]` | `Rng.NextItem` (materializes `IEnumerable`) | 1 payload |

Semantics notes that matter if the port is reused outside the generator:

* `NextFloat()` here is `MegaRandom.NextFloat()` = `(float)(raw >> 40) * 2^-24`. The game's *`Rng`* wrapper
  `Rng.NextFloat(float max = 1f)` is a different value: `(float)(NextDouble() * (double)max)` (one
  `NextDouble`, not one `>> 40`), and `Rng.NextFloat(min, max)` likewise. Both consume one word. The map
  algorithm calls neither, so the port exposes the `MegaRandom` form only.
* `NextInt(min, max)` mirrors `MegaRandom.Next(minValue, maxValue)` including its `long`-range branch
  (`range <= int.MaxValue` → int path, else `(int)((long)(NextDouble() * range) + min)`); the map path only
  passes small ranges, where the two branches agree.
* `NextItem` mirrors `Rng.NextItem`'s draw (`NextInt(0, count)`), but the source additionally returns
  `default(T)` for an empty sequence, while this port's `IReadOnlyList` form would throw from
  `NextInt(0, 0)`. Nothing in the map algorithm calls `NextItem`.
* `DrawsUsed` corresponds exactly to the source's `Rng.Counter`: the source increments `Counter` once per
  `NextInt`/`NextDouble`/`NextFloat` (and the Gaussian loops go through `NextDouble`), and each of those
  consumes exactly one generator word, so `Counter` and the payload count advance together.


`Exhausted` is true when no payload is left. A draw past the end throws
`SourceMapWordsExhaustedException` — the port never substitutes zero and continues.

### A.2 Which RNG methods the map algorithm actually calls

* `NextInt(0, 7)` (two-argument form) — start columns in `GenerateMap`.
* `NextInt(int maxExclusive)` (one-argument form) — `NextInt(i + 1)` inside `UnstableShuffle`.
* `NextGaussianInt` — **not** called by `StandardActMap` at all; it is called by the *caller* through
  `MapPointTypeCounts.StandardRandomUnknownCount` / the act's `GetMapPointTypes` (see §D). It is in the
  public surface because the counts phase must consume from the same supply.
* `NextDouble` — only as the internal step of the two above.
* Provided for interface parity, **not reached** by this algorithm: `NextInt(min, max)`, `NextFloat()`,
  `NextGaussianDouble`, `NextItem`.

Because every draw is one payload, `DrawsUsed` is simultaneously "payloads consumed".

### A.3 Exact draw order per phase

For `numberOfRooms = N` (act 1: N = 15), `_mapLength = RowCount = N + 1`, paths run rows `1 … N`
(so `N` coordinate steps per path), `hasSecondBoss = false`, `enablePruning = true`:

0. **Caller / counts phase** (source: `actModel.GetMapPointTypes(mapRng)` before the map is built):
   `NextGaussianInt(7,1,6,7)` (rest) → `NextGaussianInt(12,1,10,14)` (unknown, via
   `StandardRandomUnknownCount`). Variable: `2 × iterations` payloads each. Order matters — rest first.
1. **`GenerateMap`** — 7 start points, in this exact order:
   * 1 × `NextInt(0, 7)` per iteration (`i = 0 … 6`) = 7 payloads;
   * when `i == 1`, extra `NextInt(0, 7)` draws while the drawn point is already in `startMapPoints`
     (only the already-created col-0 point can collide, but the loop is unbounded in theory);
   * then `PathGenerate` for that start point: `while (row < _mapLength - 1)` from row 1 → `N` steps.
     Each step = `GenerateNextCoord` = 1 `StableShuffle` of a 3-element list = **2** payloads
     (`NextInt(3)`, `NextInt(2)`). ⇒ `2N` payloads per path ⇒ `14N` total (act 1: 210).
   * then `BossMapPoint` wiring and `StartingMapPoint` wiring: 0 draws.
2. **`AssignPointTypes`**:
   * fixed rows (RestSite, Treasure, Monster) and the RestSite→Shop→Elite→Unknown queue: 0 draws;
   * `AssignRemainingTypesToRandomPoints`, at most 3 rounds. Per round: collect the still-`Unassigned`
     points in **column-major** order (`GetAllMapPoints`), then `StableShuffle` ⇒ sort (0 draws) plus
     `count - 1` payloads; then one `GetNextValidPointType` per point (0 draws). A point can come back
     `Unassigned`, so a later round can see a smaller list. The loop breaks early when the queue empties.
   * final sweep `Unassigned → Monster`: 0 draws.
3. **`PruneAndRepair`** (3 iterations, variable):
   * `PruneDuplicateSegments` → `FindMatchingSegments` = 0 draws (pure traversal), then
     `while (PrunePaths(...))`: each pass shuffles every duplicate-segment group
     (`UnstableShuffle` ⇒ `groupSize - 1` payloads per group, 0 for singleton groups), then re-runs
     `FindMatchingSegments`. Capped at 50 iterations, else `InvalidOperationException`.
   * `RepairPrunedPointTypes`, in the order **Shop, Elite, RestSite, Unknown** (the last one is evaluated
     through `flag | …`, i.e. never short-circuited). Each `RepairPointType` returns immediately with
     **0 draws** when `targetCount - currentCount <= 0`; otherwise it `StableShuffle`s the
     `Monster && CanBeModified` list ⇒ `listSize - 1` payloads.
   * An iteration that repairs nothing breaks the loop.
4. **`CenterGrid` → `SpreadAdjacentMapPoints` → `StraightenPaths`**: **0 draws** (but they do rewrite
   `MapPoint.coord.col`; running them is mandatory for the emitted column numbers to be the game's).

---

## B. Deviations from `sts2/src/Core/Map/*`

Notation: *game does X / this port does Y / why*.

1. **Namespace / assembly.** `MegaCrit.Sts2.Core.Map` → `BaseSpire.Slice.SourceMap`. Same file/class/method
   names otherwise.
2. **`StandardActMap` constructor (`偏离 #46` equivalent).** Game (`sts2/src/Core/Map/StandardActMap.cs:87`):
   `StandardActMap(Rng mapRng, ActModel actModel, bool isMultiplayer, bool shouldReplaceTreasureWithElites,
   bool hasSecondBoss = false, MapPointTypeCounts? mapPointTypeCountsOverride = null, bool enablePruning = true)`,
   with `_mapLength = actModel.GetNumberOfRooms(isMultiplayer) + 1` and
   `_pointTypeCounts = mapPointTypeCountsOverride ?? actModel.GetMapPointTypes(mapRng)`. (The game's
   `CreateFor(RunState, bool)` builds the map rng as `new Rng(runState.Rng.Seed, $"act_{runState.CurrentActIndex + 1}_map")`
   — stream name `act_{n}_map`; not ported, the caller supplies the payload list instead.) Port:
   `StandardActMap(SourceMapRandom mapRng, int numberOfRooms, MapPointTypeCounts pointTypeCounts, bool
   hasSecondBoss = false, bool enablePruning = true)`. Why: no `ActModel` in this workspace; the caller
   must draw the act's Gaussians (A.3 step 0) before constructing, so the draw order is preserved.
   `numberOfRooms` is the value *before* the `+ 1`.
3. **`isMultiplayer` dropped (`偏离 #51` equivalent).** Game: `GetNumberOfRooms` subtracts one room for
   multiplayer. Port: single-player only; a caller that wants multiplayer passes `numberOfRooms - 1`.
4. **`ShouldReplaceTreasureWithElites` dropped (`偏离 #51` equivalent).** Game: when the Warden flag is set,
   row `GetRowCount() - 7` becomes `Elite` instead of `Treasure`. Port: always `Treasure`. Why: the flag
   lives on the run/ascension state; nothing in this scratch can model it. Callers that need it must
   post-process or supply a modified `MapPointTypeCounts`.
5. **`Rng` → `SourceMapRandom`** at every call site; `ListExtensions.StableShuffle/UnstableShuffle` →
   `ShuffleExtensions` (same namespace as the map files, so no extra usings). Draw sequence is identical.
6. **`MapCoord`:** dropped `IPacketSerializable`, `Serialize`, `Deserialize` and the `[JsonInclude]`
   attributes (multiplayer packets). `[Serializable]` kept. The source's primary-constructor struct with
   `public int col = col;` field initializers is expressed as an explicit constructor plus plain fields —
   same fields, same values, same `==`/`CompareTo`/`GetHashCode` semantics, and unambiguous
   `new MapCoord { … }` / `default(MapCoord)` support.
7. **`MapPoint`:** dropped the quest surface (`_quests`, `Quests`, `AddQuest`, `RemoveQuest`,
   `NodeMarkedChanged`) because it is typed on the game's `AbstractModel`, and generation/pruning never
   touches it. **Kept** everything else, including `LeftChild`, `RightChild`, `IsAdjacentLeft/Right`,
   `IsToTheLeft/Right`, `IsInTheSameRow`, `GetFirstCommonDescendant`, `GetCommonAncestor`,
   `GetLastJunctionLength`, `GetAllAncestors`, `GetAllDescendants`, `BFS_FindPath`, `BuildPath`,
   `IsDescendantPathSame` — note the reference port dropped these (`偏离 #53`); this port follows the
   **game source** instead. The private `Equals(MapPoint)` (coordinate equality) is kept verbatim; it is a
   signature overload, not an `object.Equals` override, so `object.Equals(a, b)`, `HashSet` membership and
   `segment.Contains(c)` all stay **reference-based** exactly as in the game.
8. **`MapPointTypeCounts.NumOfElites`:** game: `(int)Math.Round(5f * (AscensionHelper.HasAscension(
   AscensionLevel.SwarmingElites) ? 1.6f : 1f))`. Port: `= 5` (the A0 value), `init`-settable so a caller
   can pass `8` for SwarmingElites. Why: the helper needs the live run's ascension level.
9. **`MapPointTypeCounts.StandardRandomUnknownCount`:** parameter type `Rng` → `SourceMapRandom`. Body
   unchanged: `NextGaussianInt(12, 1, 10, 14)`.
10. **No extra argument pre-checks in the Gaussians.** The **source** `Rng.cs` has no
    `mean/stdDev` pre-checks; the reference port added `if (mean < 0.0 || mean > 1.0) throw` to
    `NextGaussianDouble` and `if (min > max) throw` / `if (mean < min || mean > max) throw` to
    `NextGaussianInt` (`偏离`-style additions). This port follows the **game**: it keeps only
    `if (min > max) throw` in `NextGaussianDouble` (which *does* exist in the source) and adds the
    `NextInt` argument guards that merely mirror the exceptions `MegaRandom`/`Rng` would surface
    (`maxExclusive < 1`, `minInclusive >= maxExclusive`). Those guards are outside the map draw path.
11. **`NextItem<T>(IReadOnlyList<T>)`** instead of `NextItem<T>(IEnumerable<T>)`, per the task book's
    signature. The source materializes `(items as T[]) ?? items.ToArray()` and indexes; the port indexes
    the list directly. Same draw, same element. One behavioural difference at the edge: the source returns
    `default(T)` for an empty sequence, whereas the port's `NextInt(0, 0)` throws — unreachable, since the
    map algorithm never calls `NextItem`.
12. **Dead code kept on purpose**: `_iterations`, `_mapWidth`, `public const int maxElites`,
    `ForEachInRow(..., bool canBeModified = false)`, `EnsureRowsContainsPointType`,
    `RowsContainPointType`, `GetRows`, `AssignPointTypesToRandomRows` are dead in the game too (nothing
    calls them; the loop bound and the start-column draw use the literal `7`). The parent design doc
    (`.audit/dsh-env-t05-map-room-2026-10-08/design-map-room.md` §4) said "do not implement dead code";
    this port keeps them, marked `Unreachable from the generator in the source too`, because the task book
    asked for a faithful port. Remove them if the parent prefers the design doc's rule.
13. **Equivalent rewrites (no semantic change).** `CollectionsMarshal.SetCount` + span fills →
    `new List<int> { -1, 0, 1 }`, `new List<MapPoint>(1) { x }`, `new List<MapPoint>(1 + n) { x }` + `AddRange`;
    `(num == 0 || num == 7) ? true : false` → plain `if`; the `SelectMany((MapPoint x) => x.Children)`
    lambda parameter renamed to `parent` (it shadowed the query range variable name); Godot's
    `Mathf.Max(0, col - 1)` / `Mathf.Min(col + 1, 6)` → BCL `Math.Max` / `Math.Min` (identical integer
    semantics; the port has no Godot reference).
17. **`GenerateNextCoord` failure message.** Game (`StandardActMap.cs:181`):
    `throw new InvalidOperationException($"Cannot find next node: seed={_rng.Seed}")`. The word supply has no
    seed, so the port reports its position instead:
    `throw new InvalidOperationException($"Cannot find next node: drawsUsed={_rng.DrawsUsed}")`. Exception
    type and control flow are unchanged; only the message text differs (a test matching the message must
    expect the port's text).
18. **`SourceActMapGenerator.cs` is new.** The game has no equivalent: it is the adapter from
    `Rng`-driven `StandardActMap` to a compact, serializable grid. Projection rules below (§C.0).
19. **Additive overloads.** `Generate` and `TryGenerate` each have an extra `long startIndex` overload so a
    caller can keep one payload list and continue after the counts phase. The task book's exact signatures
    are present and behave as specified (start index 0).
20. **`MapTravel.cs` not ported** (travel/UI state; not requested).


---

## C. Determinism: `HashSet` enumeration order

`MapPoint.parents` and `MapPoint.Children` are `HashSet<MapPoint>` and `MapPoint` does not override
`GetHashCode`, so bucket order follows the runtime's reference hash codes and is not a *contract*. The port
preserves every source enumeration site verbatim. Ranked by risk:

1. **HIGH — `MapPathPruning.FindAllPaths`**: `foreach (MapPoint child in currentMapPoint.Children)` decides
   the *order* of the path list. That order feeds `AddSegmentsToDictionary`, whose greedy
   `!AnyOverlappingSegments(...)` check keeps the **first** arriving segment of a key and drops later
   overlapping ones. Different child order ⇒ possibly different duplicate-segment groups ⇒ different
   `PrunePaths` shuffles (the shuffle draws happen per group, so the number/assignment of groups changes
   which segment list gets shuffled). This is the one place where a different enumeration order can change
   the map *and* the payload count. **Preserved as-is** (fidelity first). A deterministic variant would
   order children by `(coord.col, coord.row)` before iterating; that is an unverified deviation and was
   deliberately not applied. Flagged as the top cross-process risk.
2. **MEDIUM — `MapPostProcessing.SpreadAdjacentMapPoints`**: `foreach (int candidate in allowedPositions)`
   over a `HashSet<int>` built as `new HashSet<int>(Enumerable.Range(0, totalColumns))` then
   `IntersectWith`. Only a **strictly larger** gap wins, so ties are resolved by enumeration order. For
   `int` the hash is the value itself and the bucket layout is stable for a fixed insert sequence on a
   given runtime, so this is usually reproducible — but it is not guaranteed across runtime versions.
   **Preserved as-is.**
3. **LOW — `MapPathPruning.RemovePoint`**: `.ToList()` over `Children`/`parents` only determines the order in
   which edges are removed; the resulting graph is the same.
4. **LOW — `StandardActMap.GetSiblings` / `IsValidWithParents`**: `parents.SelectMany(p => p.Children)` and
   `parents.Concat(Children)` are consumed by `.Any(...)` only ⇒ order-independent.
5. **LOW — `MapPostProcessing.StraightenPaths` / `GetAllowedPositions`**: `...First()` is only reached when
   `Count == 1`; the rest is `IntersectWith` (set semantics) ⇒ order-independent.
6. **NONE — the type-assignment phase**: `GetAllMapPoints()` is column-major and `StableShuffle` sorts
   before shuffling, so the *set* of points entering the shuffle is order-independent.
7. **NONE — the emitted masks**: `MakeCell` ORs column bits over `parents`/`Children`; an OR cannot depend
   on enumeration order.

Unused by the generator path but order-sensitive if a caller calls them: `MapPoint.LeftChild`,
`RightChild`, `GetFirstCommonDescendant`, `BFS_FindPath`, `IsDescendantPathSame` (all kept verbatim).

### C.0 Cell projection (new API)

`SourceActMapResult.Cells` is produced after `StraightenPaths`:

* column-major: `for col 0..6 { for row 0..RowCount-1 }`, null grid cells skipped;
* `map.GetPoint(col, row)` is used, which resolves `StartingMapPoint` at (3, 0) — the grid array itself has
  no cell there — so the row-0/col-3 cell is emitted with its `ChildColsMask` covering the row-1 columns;
* `BossMapPoint` (3, `RowCount`) and, when present, `SecondBossMapPoint` (3, `RowCount + 1`) are appended as
  extra cells (they live outside the `7 × RowCount` array);
* `Type` is `(int)MapPointType`; `ParentColsMask`/`ChildColsMask` set bit `c` when a parent/child sits at
  column `c` in row `Row - 1` / `Row + 1`. Every edge the generator builds joins adjacent rows
  (`row → row+1` for paths, `row N → Boss`, `Boss → SecondBoss`), so the column mask is a complete
  description of the incident edges.
* No cell is fabricated or aggregated: one cell per real `MapPoint`.

---

## D. Inputs the caller must supply for act 1 (Overgrowth)

Single-player, no map-affecting modifiers:

| Input | Value | Source |
|---|---|---|
| `numberOfRooms` | `15` | `sts2/src/Core/Models/Acts/Overgrowth.cs:47` `BaseNumberOfRooms => 15`; `ActModel.GetNumberOfRooms(false)` returns it unchanged (`ActModel.cs:261-269`); `_mapLength = 16`, `RowCount = 16` |
| `NumOfRests` | `mapRng.NextGaussianInt(7, 1, 6, 7)` | `Overgrowth.cs:134-139`, drawn **first** |
| `NumOfUnknowns` | `MapPointTypeCounts.StandardRandomUnknownCount(mapRng)` = `NextGaussianInt(12, 1, 10, 14)` | same call site, drawn **second** |
| `NumOfShops` | `3` | `MapPointTypeCounts` |
| `NumOfElites` | `5` at A0; `8` when SwarmingElites is active (`Math.Round(5f * 1.6f)`) | `MapPointTypeCounts` |
| `PointTypesThatIgnoreRules` | empty; `{ Elite }` only with the BigGameHunter modifier | `sts2/src/Core/Models/Modifiers/BigGameHunter.cs:20` |
| `hasSecondBoss` | `false` (A10+ Double Boss makes it true) | caller/ascension state |

Caller flow (this is why `SourceMapRandom` takes a `startIndex` and why the generator has the additive
`startIndex` overload):

```csharp
var rng = new SourceMapRandom(payloads);                       // payloads = raw64 >> 11
int rests   = rng.NextGaussianInt(7, 1, 6, 7);                 // Overgrowth order: rest first
int unknown = MapPointTypeCounts.StandardRandomUnknownCount(rng); // then unknown
var counts  = new MapPointTypeCounts(unknownCount: unknown, restCount: rests); // (unknown, rest) order!
var result  = SourceActMapGenerator.Generate(payloads, rng.DrawsUsed, numberOfRooms: 15, counts);
// result.WordsConsumed == map-phase payloads only; add rng.DrawsUsed for the whole act-map phase.
```

Without the `startIndex` overload, pass a supply that already begins at the map phase (`payloads.Skip(
rng.DrawsUsed).ToList()`); `Generate(payloads, 15, counts)` starts at index 0 and would otherwise replay
the count draws.

Other acts, for reference: Underdocks `BaseNumberOfRooms = 15` with the same two Gaussians
(`Underdocks.cs:42`); Hive `14` with `NextGaussianInt(6, 1, 6, 7)` and `StandardRandomUnknownCount - 1`
(`Hive.cs:47`); Glory `13` with no rest Gaussian and `StandardRandomUnknownCount - 1` (`Glory.cs:43`).

---

## E. Open items / flagged for review

1. `README.md` §B.12 keeps dead code the parent's design doc said to skip — decide which rule wins.
2. `README.md` §B.19 adds `startIndex` overloads beyond the task book's API — additive only.
3. §C.1 (child enumeration order in `FindAllPaths`) is the only realistic cross-process divergence; it was
   left faithful, not sorted.
4. Nothing was compiled (`dotnet build` is forbidden here); the port was reviewed by hand against the
   sources. Riskiest syntax: struct/`record` declarations, nullable annotations, and the two generic
   shuffle extensions.
5. **Expected nullable warnings (not errors).** `BaseSpire.csproj` sets `Nullable enable` and does **not** set
   `TreatWarningsAsErrors`, so the source's non-annotated locals produce warnings in
   `ActMap.GetAllMapPoints` / `GetPointsInRow` (`MapPoint mapPoint = Grid[c, r];` on a `MapPoint?[,]` →
   CS8600/CS8602) and in `ActMap.GetPoint`. They are kept verbatim on purpose; annotate the locals as
   `MapPoint?` if a warning-free build is required (no behavioural change).
6. **Unverified by execution only.** Nothing here has been run; the fidelity claim rests on the line-by-line
   comparison recorded in §F.

---

## F. Verification log (line-by-line against the game source)

Each row is a full read of the game file compared statement-by-statement against the port. Findings were
fixed in the port.

| Game source | Port | Result |
|---|---|---|
| `sts2/src/Core/Map/StandardActMap.cs` (486 lines) | `StandardActMap.cs` | 2 deltas, fixed: failure message `seed=` → `drawsUsed=` (§B.17); `Mathf.Max/Min` → `Math.Max/Min` (§B.13). Draw sites, all five restriction sets, row rules and every pruning call verified identical. |
| `sts2/src/Core/Map/MapPathPruning.cs` (377 lines) | `MapPathPruning.cs` | faithful — `GenerateSegmentKey` (incl. the `row == 0` branch appending the **start row**), `PruneAllButLast`, `PruneSegment`, `RepairPrunedPointTypes` (non-short-circuited `flag |`), `RepairPointType` early return, `PruneDuplicateSegments` 50-iteration cap, `object.Equals` reference comparisons. |
| `sts2/src/Core/Map/MapPostProcessing.cs` (273 lines) | `MapPostProcessing.cs` | faithful — zero deltas. |
| `sts2/src/Core/Map/MapPoint.cs` (305 lines) | `MapPoint.cs` | faithful except the documented quest-surface drop. Verified `mapPoint.Equals(target)` in `BFS_FindPath` and `while (!Equals(mapPoint))` in `BuildPath` resolve to the private `Equals(MapPoint)` (coordinate equality), and `GetFirstCommonDescendant`'s `Equals(b)` likewise. |
| `sts2/src/Core/Map/ActMap.cs` (130 lines) | `ActMap.cs` | faithful — `GetPoint` lookup order (Boss → SecondBoss → Starting → grid bounds), `GetAllMapPoints`/`GetPointsInRow` column-major, `IsInMap`, `HasPoint`. |
| `sts2/src/Core/Map/MapCoord.cs`, `MapPointType.cs`, `MapPointState.cs`, `MapPointTypeCounts.cs` | same names | faithful except §B.6 (serialization surface) and §B.8 (`NumOfElites` A0 constant). Enum order confirmed: `MapPointType` = Unassigned 0, Unknown 1, Shop 2, Treasure 3, RestSite 4, Monster 5, Elite 6, Boss 7, Ancient 8; `MapPointState` = None, Travelable, Traveled, Untravelable. |
| `sts2/src/Core/Extensions/ListExtensions.cs` (106 lines) | `ShuffleExtensions.cs` | faithful — `list2.Sort()` == `Comparer<T>.Default`; descending Fibonacci–Yates with `NextInt(num + 1)` for `num = Count-1 … 1`. |
| `sts2/src/Core/Random/Rng.cs` (gaussians, `NextInt`, `NextItem`) + `MegaRandom.cs` (raw draws) | `SourceMapRandom.cs` | faithful; `NextGaussianInt` sine branch and `NextGaussianDouble` cosine branch match literally (including the `[0,1]` retry on the raw value before scaling to `[min,max]`), and `NextInt(min,max)` was changed to mirror `MegaRandom`'s `long`-range branch. |
