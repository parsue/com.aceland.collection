# Changelog

All notable changes to this project will be documented in this file.

---

## [1.0.1] - 2026-10-15
### Fixed
- Benchmark Window cannot open script issue

## [1.0.0] - 2026-10-14
### Added
- **DOTS layer bundled in** (`AceLand.Collection.Dots` assembly): Burst-compilable, job-safe `NativeContainer`-backed variants — `NativeBinaryHeap<T, TComparer>`, `NativeSpatialHashGrid2D/3D<T>`, `NativeKdTree2D/3D<T>`.
- **Conversion bridges** (`ToNative` / `ToManaged` extension methods) between managed and native variants, implemented via `internal` access (`InternalsVisibleTo` + snapshot / backing accessors) so the public API surface is unchanged.
- **Inspector editing for Search collections** (Editor tool): `Trie<TValue>` and `RadixTree<TValue>` serialized fields can now add / edit / remove entries directly in the Inspector via a custom property drawer. Keys are staged, validated (non-empty, no duplicates) and committed to avoid the serialization round-trip wiping in-progress edits. The drawer is part of the paid Editor tooling and shows a read-only guidance page until a license is active; Runtime behaviour and the public API are unchanged.
- **Inspector editing for Spatial collections** (Editor tool): `SpatialHashGrid2D/3D<T>`, `QuadTree<T>`, `Octree<T>`, `KdTree2D/3D<T>` and `RTree2D/3D<T>` serialized fields can now add / edit / remove entries directly in the Inspector. Structural settings (`cellSize`, `bounds`, `capacity`, `maxDepth`, `maxEntries`, `minEntries`) are surfaced as a header; entries expose their position/bounds and item for in-place editing. Same paid Editor tooling + read-only guidance gate; Runtime behaviour and the public API are unchanged.
- **`BinaryHeap<T>` capacity control:** new `Create(HeapMode, int capacity)` / `Create(HeapMode, IComparer<T>, int capacity)` overloads and a public `EnsureCapacity(int)` method. Pre-sizing the backing store before a large burst of `Push` calls avoids the incremental array-doubling reallocations, sharply cutting transient GC allocations.
- **`Deque<T>` pre-sized growth path:** new `CreateWithCapacity(int initialCapacity)` factory that keeps the unbounded auto-grow semantics of `Create()` but allocates the ring buffer up front. Supplying the expected element count skips the early geometric-doubling reallocations (which otherwise discard each superseded array as garbage) — cutting insert-time transient GC by ~62% (1.03 MB → 392 KB for 100,000 ints) at the same throughput. `Create()` (unbounded, grow) and `Create(int capacity)` (fixed, no grow) are unchanged.
- **Zero-allocation query overloads for all Spatial collections.** Every spatial query method — `SpatialHashGrid2D/3D<T>.QueryRect` / `QueryBox` / `QueryRadius`, `QuadTree<T>.QueryRect` / `QueryRadius`, `Octree<T>.QueryBox` / `QueryRadius`, `KdTree2D/3D<T>.FindKNearest` / `QueryRadius`, and `RTree2D/3D<T>.Query` / `QueryPoint` — now offers a caller-supplied buffer overload (e.g. `QueryRadius(center, radius, List<T> results)`) that clears and refills the passed list instead of allocating a fresh one. Repeated queries against a reused buffer now allocate **zero GC** (e.g. `SpatialHashGrid2D` radius query: 180 KB → **0 B** across 2,000 calls). The existing `out List<T>` overloads are retained unchanged for backward compatibility and simply delegate to the new ones after allocating a list.

### Fixed
- **Spatial collections no longer throw on serialization when never built via `Create(...)`.** A field declared `[SerializeField] SpatialHashGrid2D/3D<T>`, `KdTree2D/3D<T>`, `QuadTree<T>`, `Octree<T>` or `RTree2D/3D<T>` is materialized by Unity through deserialization (bypassing the private constructor), leaving the backing stores `null`; `OnBeforeSerialize` → `WriteSerializedItems` then threw a `NullReferenceException`. `WriteSerializedItems` now early-returns when its backing store is null, so an uninitialized field serializes as empty instead of throwing.

### Changed
- **`BiDictionary<TKey,TValue>` now stores each pair once.** The previous implementation kept two full `Dictionary` instances (forward *and* reverse), duplicating every key/value payload and roughly doubling build-time GC versus a single `Dictionary`. It is now a purpose-built open-chaining hash table: one entry array holds each `(key, value)` exactly once, with two independent hash-bucket chains (by key hash and by value hash) layered over that single store. Entries do not cache their hash codes either — hashes are recomputed on the rare unlink/resize paths — so each slot is just two link ints plus the payload. Together these cut the build allocation from **4.16 MB down to 2.50 MB** for 100,000 pairs (from ~3.5× a single `Dictionary` to ~1.23×, the ~0.4 MB residual being the irreducible second bucket/link array for the reverse chain), while `TryGetByKey` / `TryGetByValue` stay O(1) and fully allocation-free (~21× faster than a linear reverse scan). Removed slots are recycled through an internal free list. Public API, bijection semantics, and Unity serialization round-tripping are unchanged.
- Merged the former standalone `com.aceland.collection.dots` package into `com.aceland.collection` (single package, dual runtime assembly). The managed `AceLand.Collection` assembly stays zero-dependency; the new `AceLand.Collection.Dots` assembly carries the Unity DOTS dependencies.
- `package.json` now declares hard dependencies on `com.unity.burst`, `com.unity.collections`, and `com.unity.mathematics`.

## Note
First public release. If you have an older version, please update or re-install.
For detail please visit and bookmark our [GitBook](https://aceland-workshop.gitbook.io/aceland-unity-packages/)