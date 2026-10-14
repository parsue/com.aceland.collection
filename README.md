## AceLand Collection

[![Open Core](https://img.shields.io/badge/Open%20Core-2A8B22)]()
[![Burst](https://img.shields.io/badge/Burst-Ready-FF6C37)]()
[![CoreCLR Ready](https://img.shields.io/badge/CoreCLR-Ready-4A5BC2)]()    
[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-db61a2?logo=githubsponsors&logoColor=white)](https://github.com/sponsors/parsue)
[![Discord](https://img.shields.io/badge/Discord-Join-5865F2?logo=discord&logoColor=white)](https://discord.gg/XsCYGnYzuc)
[![Docs](https://img.shields.io/badge/Docs-GitBook-3884FF?logo=gitbook&logoColor=white)](https://docs.parsue.io/aceland-unity-packages/packages/collection)

AceLand Collection is a high-performance, Unity-serializable data-structure library for Unity. Every managed collection supports Unity serialization (`[SerializeField]` / Inspector), is written in modern C#, and is designed with CoreCLR as well as ECS / Jobs / DOTS directions in mind. The package also ships a Burst-compilable, job-safe **DOTS layer** (`Native*` containers) in the same package.

This package follows an **Open Core** business model:
- **The Runtime data-structure layer is fully open source (MIT)** — it can be installed and used on its own, with zero dependency on any Editor tooling.
- **The Editor visualization tooling layer is a paid Pro feature**, published on the Unity Asset Store.

---

## Free (Open Source · MIT) — Runtime Data-Structure Layer

The free tier is the complete Runtime library. Once installed it can be used directly in your project and **does not require, nor depend on, any paid Editor tooling**.

### Priority / Heap
- `BinaryHeap<T>` — binary heap (Min / Max)
- `MinMaxHeap<T>` — double-ended heap (retrieve both min and max)
- `IndexedPriorityQueue<TKey, TPriority>` — indexed priority queue with updatable priorities

### Sequence / Buffer
- `RingBuffer<T>` — ring buffer (fixed capacity, overwrite semantics)
- `Deque<T>` — double-ended queue
- `LruCache<TKey, TValue>` — LRU cache (with eviction policy)

### Mapping
- `MultiMap<TKey, TValue>` — one-key-to-many-values mapping
- `BiDictionary<TKey, TValue>` — bidirectional mapping

### Search
- `Trie<TValue>` — prefix tree
- `RadixTree<TValue>` — compressed prefix tree (Radix / Patricia)

### Relational
- `Graph<TNode, TEdge>` — directed / undirected graph (BFS / DFS)
- `HierarchyTree<T>` — hierarchy tree (ancestors / descendants / re-parenting)
- `DisjointSet<T>` — union-find (disjoint set)

### Spatial
- `SpatialHashGrid2D<T>` / `SpatialHashGrid3D<T>` — spatial hash grids
- `QuadTree<T>` / `Octree<T>` — quadtree / octree
- `KdTree2D<T>` / `KdTree3D<T>` — k-d tree (nearest neighbour / k-nearest / radius queries)
- `RTree2D<T>` / `RTree3D<T>` — R-Tree (range / point queries)

> All collections are created through `Create(...)` factory methods or builders; constructors are not exposed. They also support `RestoreComparer(...)` to restore comparers after deserialization.

---

## DOTS Layer (Open Source · MIT) — `Native*` Containers

For Burst / Jobs / DOTS workloads, this package **also ships** `NativeContainer`-backed
equivalents in a second runtime assembly (`AceLand.Collection.Dots`), so no extra package
is required. They are Burst-compilable and job-safe:

- `NativeBinaryHeap<T, TComparer>` — corresponds to `BinaryHeap<T>` (uses a `struct` comparer so Burst can inline it)
- `NativeSpatialHashGrid2D<T>` / `NativeSpatialHashGrid3D<T>` — corresponds to `SpatialHashGrid2D/3D<T>`
- `NativeKdTree2D<T>` / `NativeKdTree3D<T>` — corresponds to `KdTree2D/3D<T>` (one-shot balanced build)

These types use `where T : unmanaged`, take an explicit `Allocator`, and must be `Dispose()`d
by the caller (no GC allocation, no Inspector serialization).

### Unity dependencies

Because the DOTS layer is bundled in, this package declares hard dependencies on
`com.unity.burst`, `com.unity.collections`, and `com.unity.mathematics`. On Unity 6+
these come pre-installed; on Unity 2022.3 LTS they are resolved automatically by the
Package Manager. The managed `AceLand.Collection` assembly itself remains zero-dependency;
only the `AceLand.Collection.Dots` assembly references the Unity DOTS packages.

### Conversion bridges (optional)

`ToNative` / `ToManaged` extension methods bridge between the managed and native variants:

```csharp
// managed -> native (caller disposes)
using var nativeHeap = managedHeap.ToNative(default(MyComparer), Allocator.TempJob);

// native -> managed (re-enables Inspector serialization)
var managedGrid = nativeGrid.ToManaged();
```

Native variants can also be used directly inside Jobs / ECS without any conversion.

---

## Pro (Paid) — Editor Visualization Tooling Layer

The Pro tier is an optional set of Editor tools published on the Unity Asset Store (one-time purchase). **Whether or not it is installed has no effect on the Runtime layer**; when unlicensed, the tool windows display a licensing prompt page.

- **Hierarchy Tree Builder** — build / edit `HierarchyTree` through a visual interface, with JSON import / export.
- **Collection Whiteboard** — a node-based visual monitoring panel for collection state.
- **Search Collection Inspector** — add / edit / remove `Trie<TValue>` and `RadixTree<TValue>` entries directly in the Inspector, with staged, validated key commits (non-empty, no duplicates).

> **Collection Benchmark** — built-in performance benchmarks (e.g. `BinaryHeap` vs `SortedSet`, `SpatialHashGrid` vs brute-force radius queries) — is **free** and requires no license.

---

## Benchmarks

Measured with the built-in **Collection Benchmark** tool (Editor, free). Lower time is better. Numbers below are representative of a single machine — treat them as relative comparisons, not absolute guarantees.

### Managed layer — priority ordering (200,000 ops)

> **What this measures:** an apples-to-apples priority-queue workload. Both structures perform the same interleaved insert + extract-minimum drain over 100,000 elements (200,000 ops total): every element is inserted, then the current minimum is repeatedly read and removed until empty. `BinaryHeap<int>` is pre-sized via `Create(HeapMode.Min, capacity)` so its backing array does not reallocate mid-run. `SortedSet<int>` uses `Add` + `Min` / `Remove(Min)` as its equivalent incremental drain.
>
> Parameters: Sample Count `100,000`, Random Seed `12345`. The `SortedSet<int>` (BCL) row is the managed baseline.

| Structure | Time (ms) | Ops | GC Alloc | vs Baseline |
| --- | ---: | ---: | ---: | --- |
| `BinaryHeap<int>` (AceLand) | 25.00 | 200,000 | 392.0 KB | 1.75× faster |
| `SortedSet<int>` (BCL, baseline) | 43.81 | 200,000 | 3.92 MB | 1.00× |

On the same incremental priority-queue workload, `BinaryHeap<int>` is about **1.75× faster** than `SortedSet<int>` (25.00 ms vs 43.81 ms) while allocating **roughly one tenth of the GC** (392 KB vs 3.92 MB). The low allocation comes from pre-sizing the heap so the backing array never grows during the run; `SortedSet<int>` allocates a red-black tree node per element, which dominates its GC cost. Prefer `BinaryHeap<int>` whenever you need true incremental push / pop of the current extreme.

### Managed layer — spatial radius queries

> **What this measures:** `SpatialHashGrid2D<T>` insert throughput, followed by radius queries against the same grid, compared to a brute-force linear scan (the BCL baseline) that tests every point per query. The grid trades a one-time insert cost for dramatically cheaper repeated neighbour lookups. The radius query uses the caller-supplied buffer overload (`QueryRadius(center, radius, List<T> results)`), which clears and refills a reused list instead of allocating a fresh one per call.
>
> Parameters: Sample Count `100,000`, Random Seed `12345`, Query Count `2,000`, World Extent `400`, Grid Cell Size `4`, Query Radius `6`.

| Operation | Time (ms) | Ops | GC Alloc | vs Brute-force |
| --- | ---: | ---: | ---: | --- |
| `SpatialHashGrid2D` — insert | 65.29 | 100,000 | 12.88 MB | 21.03× faster |
| `SpatialHashGrid2D` — radius query | 29.93 | 2,000 | 0 B | 45.88× faster |
| Brute-force radius query (BCL, baseline) | 1373.23 | 2,000 | 0 B | 1.00× |

For repeated neighbour lookups the spatial hash grid is **~46× faster** than a brute-force scan (29.93 ms vs 1373.23 ms over 2,000 queries), because each query only visits the handful of cells overlapping the radius instead of all 100,000 points. Even the one-time insert phase amortises well against the brute-force cost. The radius query allocates **zero GC** across all 2,000 calls: every spatial `Query*` / `FindKNearest` method now offers a caller-supplied buffer overload that clears and refills a reused `List<T>` (the older `out List<T>` overloads remain for compatibility but allocate one list per call). The grid still allocates to store its buckets (12.88 MB across 100,000 inserts), so reuse a grid — and a single query buffer — across many queries to get the full benefit; the brute-force scan allocates nothing but pays a linear cost on every query.

### Managed layer — indexed priority queue re-prioritise (150,000 ops)

> **What this measures:** a decrease-key / re-prioritise workload. `IndexedPriorityQueue<int, int>` updates an existing element's priority in place via its position index, while the BCL baseline must remove the stale `(priority, key)` entry from a `SortedSet<(int, int)>` and re-insert it with the new priority — the standard workaround when the container has no keyed update.
>
> Parameters: Sample Count `150,000`, Random Seed `12345`. The `SortedSet` re-insert (BCL) row is the managed baseline.

| Structure | Time (ms) | Ops | GC Alloc | vs Baseline |
| --- | ---: | ---: | ---: | --- |
| `IndexedPriorityQueue<int,int>` (AceLand) | 19.70 | 150,000 | 4.54 MB | 4.69× faster |
| `SortedSet` re-insert (BCL, baseline) | 92.48 | 150,000 | 8.32 MB | 1.00× |

On a re-prioritise workload `IndexedPriorityQueue<int,int>` is about **4.69× faster** than the `SortedSet` remove-then-reinsert pattern (19.70 ms vs 92.48 ms). The indexed queue reaches the target element in O(1) via its position map and sifts it into place, whereas the `SortedSet` pays two O(log n) tree operations plus tuple boxing for every update. Pre-sizing via `Create(capacity)` now also **roughly halves its GC** (4.54 MB vs 8.32 MB). Prefer `IndexedPriorityQueue` for Dijkstra / A* style algorithms where node priorities change constantly.

### Managed layer — double-ended queue front operations

> **What this measures:** double-ended throughput split into two symmetric phases. `Deque<int>` performs O(1) push/pop at both ends via its ring buffer, compared to `List<int>.Insert(0, …)` (front-insert) and `List<int>.RemoveAt(0)` (front-remove) — both O(n) because they shift the entire backing array. Each phase compares against its own like-for-like `List` baseline. The insert phase is shown twice: the default `Create()` deque grows geometrically from capacity 8, while `CreateWithCapacity(n)` pre-sizes the ring buffer up front (same auto-grow semantics, minus the early doubling reallocations).
>
> Parameters: Sample Count `100,000`, Random Seed `12345`. Each `List` baseline is capped at `20,000` items to avoid an O(n²) freeze.

| Structure | Time (ms) | Ops | GC Alloc | vs Baseline |
| --- | ---: | ---: | ---: | --- |
| `Deque<int>` — insert (AceLand) | 0.59 | 100,000 | 1.03 MB | 7.81× faster |
| `Deque<int>` — insert pre-sized (AceLand) | 0.69 | 100,000 | 392.0 KB | 6.71× faster |
| `List` front-insert (BCL, baseline) | 4.63 | 20,000 | 280.0 KB | 1.00× |
| `Deque<int>` — drain (AceLand) | 0.24 | 100,000 | 0 B | 693.45× faster |
| `List` front-remove (BCL, baseline) | 167.05 | 20,000 | 0 B | 1.00× |

Splitting the workload shows each side on its own baseline. On **insertion** `Deque<int>` is about **7.81× faster** (0.59 ms for 100,000 ring-buffer inserts vs 4.63 ms for 20,000 `List` front inserts). The default `Create()` deque's 1.03 MB GC is the genuine cost of amortised geometric growth (the ring buffer doubles from 8 past 131,072 slots, discarding each superseded array); pre-sizing with `CreateWithCapacity(n)` collapses it to a **single 392 KB backing block** — a **~62% GC reduction** at the same throughput. On the **drain** the gap widens dramatically: `Deque<int>` empties 100,000 elements in **0.24 ms** with zero allocation, while `List<int>.RemoveAt(0)` needs 167.05 ms for only 20,000 removals — about **693× slower**, because every front removal shifts the whole backing array. Use `Deque<T>` for queues, sliding windows, and any workload that pushes and pops at both ends; prefer `CreateWithCapacity(n)` when the element count is known up front.

### Managed layer — LRU cache access (50,000 ops)

> **What this measures:** least-recently-used cache access with eviction. `LruCache` combines the hash lookup and recency list into one integrated O(1) structure, compared to a hand-rolled `Dictionary` + `LinkedList` LRU (the BCL baseline) that keeps the two containers in sync manually.
>
> Parameters: Sample Count `50,000`, Random Seed `12345`. The `Dictionary` + `LinkedList` LRU (BCL) row is the managed baseline.

| Structure | Time (ms) | Ops | GC Alloc | vs Baseline |
| --- | ---: | ---: | ---: | --- |
| `LruCache` (AceLand) | 4.60 | 50,000 | 776.0 KB | 1.75× faster |
| `Dictionary` + `LinkedList` LRU (BCL, baseline) | 8.07 | 50,000 | 880.0 KB | 1.00× |

`LruCache` is about **1.75× faster** than a manual `Dictionary` + `LinkedList` LRU (4.60 ms vs 8.07 ms) while packaging the lookup, recency reorder, and eviction into a single tested type. Pre-sizing its internal map to capacity means it now also allocates **less** GC than the hand-rolled baseline (776 KB vs 880 KB) — so you get both the speed and the memory edge in a correct, ready-to-use O(1) cache.

### Managed layer — bidirectional dictionary reverse lookup

> **What this measures:** the cost of a dual index, split into two symmetric phases. In the **build** phase `BiDictionary` populates both forward and reverse indexes over a single shared entry table (the reverse chain adds index work without duplicating the payload); in the **reverse-lookup** phase `TryGetByValue` is O(1) against the maintained reverse index. A plain `Dictionary` (the BCL baseline) builds one index but must linearly scan every entry for a reverse query.
>
> Parameters: Sample Count `100,000`, Reverse-Lookup Count `50,000`, Random Seed `12345`. The **build** baseline populates a single `Dictionary` over the *same* `100,000` entries (an honest per-entry comparison); the **reverse-scan** baseline is capped at `5,000` items to avoid an O(n²) freeze.

| Structure | Time (ms) | Ops | GC Alloc | vs Baseline |
| --- | ---: | ---: | ---: | --- |
| `BiDictionary` — build (AceLand) | 1.44 | 100,000 | 2.50 MB | 1.23× slower |
| `Dictionary` build (BCL, baseline) | 0.73 | 100,000 | 2.08 MB | 1.00× |
| `BiDictionary` — reverse-lookup (AceLand) | 1.85 | 50,000 | 0 B | 21.41× faster |
| `Dictionary` linear reverse-scan (BCL, baseline) | 39.61 | 5,000 | 0 B | 1.00× |

Splitting the suite separates the one-time cost of a dual index from its query payoff. **Building** both indexes is only about **1.23× slower** than building a single `Dictionary` over the *same* 100,000 entries (1.44 ms / 2.50 MB vs 0.73 ms / 2.08 MB). The single-storage design keeps this near parity: each `(key, value)` pair lives once in one entry table with two hash-bucket chains layered over it, and entries no longer cache their hash codes, so the ~0.4 MB delta is just the second bucket/link array for the reverse chain — the irreducible floor for an O(1) reverse index (kept to a single allocation by pre-sizing via `Create(capacity)`). That cost is repaid on the **reverse-lookup**: `BiDictionary` answers 50,000 value→key queries in **1.85 ms** with zero allocation, whereas the plain `Dictionary` needs 39.61 ms for only 5,000 linear scans — roughly **21.41× faster** on ten times the queries. Use `BiDictionary` when both lookup directions are hot; it trades an almost-free build for reverse lookups that never scan.

### DOTS layer — Burst-compiled native structures

Measured with the built-in **Dots Benchmark** tool (Editor). Each native structure is compared against its managed counterpart running the same workload; lower time is better, and native containers allocate zero managed GC.

> Parameters — **Binary Heap:** Sample Count `20,000`, Random Seed `12345` (push all + pop all, 40,000 ops). **Spatial Hash Grid:** Sample Count `20,000`, Random Seed `12345`, Query Count `2,000`, World Extent `500`, Grid Cell Size `5`, Query Radius `5` (build + radius queries, 22,000 ops). **Kd Tree:** Sample Count `20,000`, Random Seed `12345`, Query Count `2,000`, World Extent `500` (build + nearest queries, 22,000 ops). Numbers are representative of a single machine — treat them as relative comparisons.

| Scenario | Managed | Managed GC | Native (Burst) | Native GC | Speedup |
| --- | ---: | ---: | ---: | ---: | ---: |
| `BinaryHeap` — push all + pop all (40,000 ops) | 15.59 ms | 2.98 MB | 2.71 ms | 0 B | ~5.76× |
| `SpatialHashGrid2D` — build + radius queries (22,000 ops) | 17.53 ms | 2.60 MB | 4.56 ms | 0 B | ~3.85× |
| `KdTree2D` — build + nearest queries (22,000 ops) | 16.30 ms | 2.46 MB | 5.37 ms | 0 B | ~3.03× |

The Burst-compiled native structures deliver a **~3.0–5.8× speedup** over the managed equivalents while producing identical query results (e.g. matching hit counts on the spatial grid). Crucially, they run inside Jobs / ECS with **zero managed GC allocation** (vs 2.4–3.0 MB on the managed side), making them the recommended choice for hot paths and large data sets.

---

## Documents
We use GitBook as the public documentation of our packages.

> Visit our [GitBook](https://docs.parsue.io/aceland-unity-packages/packages/collection)

Please visit our GitBook for details.
