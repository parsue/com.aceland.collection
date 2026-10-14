using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AceLand.Collection.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace AceLand.Collection.Dots.Benchmark.Editor
{
    /// <summary>
    /// Editor window that measures the Burst-compiled <c>Native*</c> containers
    /// against their managed <c>AceLand.Collection</c> counterparts. The UI mirrors
    /// the core <c>Collection Benchmark</c> window (toolbar suite selector, parameter
    /// box, structured result table with GC allocation and speed-up columns) so both
    /// tools present a consistent interface. Results are indicative (single machine,
    /// single run) and intended for verifying the DOTS layer delivers the expected
    /// speed-up rather than as precise micro-benchmarks.
    /// </summary>
    public sealed class NativeCollectionsBenchmarkWindow : EditorWindow
    {
        private const string ToolName = "Dots Benchmark";
        private const string MenuPath = "Tools/AceLand/Collection/Dots Benchmark";
        private const string PlayModeTestScriptName = "NativeCollectionsBurstJobTests";

        private enum Suite
        {
            BinaryHeap,
            SpatialHashGrid,
            KdTree,
        }

        [Serializable]
        private struct Result
        {
            public string Label;
            public double Milliseconds;
            public long AllocatedBytes;
            public int Operations;
            public bool IsBaseline;
        }

        [SerializeField] private Suite _suite = Suite.BinaryHeap;
        [SerializeField] private int _sampleCount = 20_000;
        [SerializeField] private int _queryCount = 2_000;
        [SerializeField] private float _worldExtent = 500f;
        [SerializeField] private float _cellSize = 5f;
        [SerializeField] private float _queryRadius = 5f;
        [SerializeField] private int _seed = 12345;
        [SerializeField] private Vector2 _scroll;

        [NonSerialized] private readonly List<Result> _results = new();
        [NonSerialized] private string _status;
        [NonSerialized] private MessageType _statusType = MessageType.None;
        [NonSerialized] private bool _running;

        [MenuItem(MenuPath, priority = 21)]
        private static void Open()
        {
            var window = GetWindow<NativeCollectionsBenchmarkWindow>();
            window.titleContent = new GUIContent(ToolName);
            window.minSize = new Vector2(560f, 460f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawParameters();
            DrawActions();
            DrawStatus();
            DrawResults();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(ToolName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_running))
                {
                    _suite = (Suite)EditorGUILayout.EnumPopup(
                        _suite, EditorStyles.toolbarPopup, GUILayout.Width(160f));
                }
            }
        }

        private void DrawParameters()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.DisabledScope(_running))
            {
                EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);

                _sampleCount = Mathf.Clamp(
                    EditorGUILayout.IntField("Sample Count", _sampleCount), 1, 5_000_000);
                _seed = EditorGUILayout.IntField("Random Seed", _seed);

                if (_suite != Suite.BinaryHeap)
                {
                    _queryCount = Mathf.Clamp(
                        EditorGUILayout.IntField("Query Count", _queryCount), 1, 1_000_000);
                    _worldExtent = Mathf.Max(
                        1f, EditorGUILayout.FloatField("World Extent (± units)", _worldExtent));
                }

                if (_suite == Suite.SpatialHashGrid)
                {
                    _cellSize = Mathf.Max(
                        0.01f, EditorGUILayout.FloatField("Grid Cell Size", _cellSize));
                    _queryRadius = Mathf.Max(
                        0.01f, EditorGUILayout.FloatField("Query Radius", _queryRadius));
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(_running))
            {
                if (GUILayout.Button("Run Benchmark", GUILayout.Height(26f)))
                    RunBenchmark();

                if (GUILayout.Button("Open Benchmark Script", GUILayout.Height(26f), GUILayout.Width(170f)))
                    OpenBenchmarkScript();

                if (GUILayout.Button("Open Test Script", GUILayout.Height(26f), GUILayout.Width(140f)))
                    OpenPlayModeTestScript();

                using (new EditorGUI.DisabledScope(_results.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Height(26f), GUILayout.Width(70f)))
                    {
                        _results.Clear();
                        SetStatus(null, MessageType.None);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("What this measures", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(DescribeSuite(_suite), EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static string DescribeSuite(Suite suite) => suite switch
        {
            Suite.BinaryHeap =>
                "BinaryHeap: performs \"push all + pop all\" over Sample Count random integers (2x ops total).\n"
                + "The baseline is the managed IndexedPriorityQueue<int,int> (Min); the item under test "
                + "is a NativeBinaryHeap executed as a Burst Job.",
            Suite.SpatialHashGrid =>
                "SpatialHashGrid2D: inserts Sample Count random points, then performs Query Count radius queries.\n"
                + "The baseline is the managed SpatialHashGrid2D; the item under test is a "
                + "NativeSpatialHashGrid2D executed as a Burst Job.",
            Suite.KdTree =>
                "KdTree2D: builds a tree from Sample Count random points, then performs Query Count "
                + "nearest-neighbour queries.\n"
                + "The baseline is the managed KdTree2D; the item under test is a NativeKdTree2D "
                + "executed as a Burst Job.",
            _ => string.Empty,
        };

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusType);
        }

        private void DrawResults()
        {
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No benchmark has run yet. Set the parameters, then press Run Benchmark.\n"
                    + "Before measuring, a forced GC runs and Stopwatch + GC.GetTotalMemory capture "
                    + "elapsed time and allocation. Native items under test are warmed up (Burst "
                    + "compilation) before being measured.",
                    MessageType.Info);
                return;
            }

            var baseline = FindBaselineMs();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                DrawResultHeaderRow();
                foreach (var result in _results)
                    DrawResultRow(result, baseline);
            }
        }

        private void DrawResultHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Structure", EditorStyles.boldLabel, GUILayout.Width(240f));
                GUILayout.Label("Time (ms)", EditorStyles.boldLabel, GUILayout.Width(90f));
                GUILayout.Label("Ops", EditorStyles.boldLabel, GUILayout.Width(80f));
                GUILayout.Label("GC Alloc", EditorStyles.boldLabel, GUILayout.Width(100f));
                GUILayout.Label("vs Baseline", EditorStyles.boldLabel);
            }
        }

        private void DrawResultRow(Result result, double baselineMs)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var label = result.IsBaseline ? result.Label + "  (baseline)" : result.Label;
                GUILayout.Label(label, GUILayout.Width(240f));
                GUILayout.Label(result.Milliseconds.ToString("F2", CultureInfo.InvariantCulture),
                    GUILayout.Width(90f));
                GUILayout.Label(result.Operations.ToString("N0", CultureInfo.InvariantCulture),
                    GUILayout.Width(80f));
                GUILayout.Label(FormatBytes(result.AllocatedBytes), GUILayout.Width(100f));
                GUILayout.Label(FormatSpeedup(result.Milliseconds, baselineMs));
            }
        }

        // ---- Benchmark orchestration -------------------------------------

        private void RunBenchmark()
        {
            _running = true;
            _results.Clear();
            SetStatus("Running…", MessageType.Info);

            try
            {
                switch (_suite)
                {
                    case Suite.BinaryHeap:
                        RunHeapSuite();
                        break;
                    case Suite.SpatialHashGrid:
                        RunSpatialSuite();
                        break;
                    case Suite.KdTree:
                        RunKdTreeSuite();
                        break;
                }

                SetStatus(
                    $"Completed {_results.Count} run(s) on {_suite}. "
                    + "Lower time is better; the baseline is the managed reference. Native (Burst) "
                    + "produces almost no managed GC.",
                    MessageType.Info);
            }
            catch (Exception ex)
            {
                SetStatus($"Benchmark failed: {ex.Message}", MessageType.Error);
                Debug.LogError($"[Dots Benchmark] failed: {ex}");
            }
            finally
            {
                _running = false;
                Repaint();
            }
        }

        // ---- BinaryHeap -----------------------------------------------------

        private struct AscendingInt : IComparer<int>
        {
            public int Compare(int a, int b) => a.CompareTo(b);
        }

        [BurstCompile]
        private struct NativeHeapJob : IJob
        {
            [ReadOnly] public NativeArray<int> Values;
            [WriteOnly] public NativeArray<int> Sink;

            public void Execute()
            {
                var heap = new NativeBinaryHeap<int, AscendingInt>(Values.Length, Allocator.Temp, default);
                for (var i = 0; i < Values.Length; i++)
                    heap.Push(Values[i]);

                var last = 0;
                while (heap.TryPop(out var v))
                    last = v;

                Sink[0] = last;
                heap.Dispose();
            }
        }

        private void RunHeapSuite()
        {
            var ops = _sampleCount * 2; // push + pop
            var rng = new Random((uint)_seed);
            var values = new NativeArray<int>(_sampleCount, Allocator.TempJob);
            for (var i = 0; i < _sampleCount; i++)
                values[i] = rng.NextInt();

            try
            {
                // Managed baseline: AceLand IndexedPriorityQueue (BCL PriorityQueue is
                // unavailable pre-.NET 6 / Unity 2022.3). Key by index, value as priority.
                _results.Add(Measure("Managed IndexedPriorityQueue", ops, true, () =>
                {
                    var pq = IndexedPriorityQueue<int, int>.Create(HeapMode.Min);
                    for (var i = 0; i < _sampleCount; i++)
                        pq.Enqueue(i, values[i]);
                    while (pq.TryDequeue(out _, out _)) { }
                }));

                using var sink = new NativeArray<int>(1, Allocator.TempJob);
                // Warm-up so the Burst/JIT compile cost is excluded from the measurement.
                new NativeHeapJob { Values = values, Sink = sink }.Schedule().Complete();

                _results.Add(Measure("NativeBinaryHeap (Burst job)", ops, false, () =>
                {
                    new NativeHeapJob { Values = values, Sink = sink }.Schedule().Complete();
                }));
            }
            finally
            {
                values.Dispose();
            }
        }

        // ---- SpatialHashGrid2D ---------------------------------------------

        [BurstCompile]
        private struct NativeGridBuildQueryJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Points;
            [ReadOnly] public NativeArray<float2> Queries;
            public float Radius;
            public float CellSize;
            [WriteOnly] public NativeArray<int> TotalHits;

            public void Execute()
            {
                var grid = new NativeSpatialHashGrid2D<int>(CellSize, Points.Length, Allocator.Temp);
                for (var i = 0; i < Points.Length; i++)
                    grid.Insert(Points[i], i);

                var results = new NativeList<int>(64, Allocator.Temp);
                var total = 0;
                for (var q = 0; q < Queries.Length; q++)
                {
                    grid.QueryRadius(Queries[q], Radius, results);
                    total += results.Length;
                }

                TotalHits[0] = total;
                results.Dispose();
                grid.Dispose();
            }
        }

        private void RunSpatialSuite()
        {
            var ops = _sampleCount + _queryCount; // inserts + radius queries
            var rng = new Random((uint)_seed + 1u);

            var points = new NativeArray<float2>(_sampleCount, Allocator.TempJob);
            var queries = new NativeArray<float2>(_queryCount, Allocator.TempJob);
            for (var i = 0; i < _sampleCount; i++)
                points[i] = rng.NextFloat2(new float2(-_worldExtent), new float2(_worldExtent));
            for (var i = 0; i < _queryCount; i++)
                queries[i] = rng.NextFloat2(new float2(-_worldExtent), new float2(_worldExtent));

            try
            {
                _results.Add(Measure("Managed SpatialHashGrid2D", ops, true, () =>
                {
                    var grid = SpatialHashGrid2D<int>.Create(_cellSize);
                    for (var i = 0; i < _sampleCount; i++)
                        grid.Insert(points[i], i);
                    for (var q = 0; q < _queryCount; q++)
                        grid.QueryRadius(queries[q], _queryRadius, out _);
                }));

                using var hits = new NativeArray<int>(1, Allocator.TempJob);
                var job = new NativeGridBuildQueryJob
                {
                    Points = points, Queries = queries,
                    Radius = _queryRadius, CellSize = _cellSize, TotalHits = hits,
                };
                job.Schedule().Complete(); // warm-up

                _results.Add(Measure("NativeSpatialHashGrid2D (Burst)", ops, false, () =>
                {
                    job.Schedule().Complete();
                }));
            }
            finally
            {
                points.Dispose();
                queries.Dispose();
            }
        }

        // ---- KdTree2D -------------------------------------------------------

        [BurstCompile]
        private struct NativeKdBuildQueryJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Points;
            [ReadOnly] public NativeArray<int> Items;
            [ReadOnly] public NativeArray<float2> Queries;
            [WriteOnly] public NativeArray<int> Checksum;

            public void Execute()
            {
                var tree = new NativeKdTree2D<int>(Points, Items, Allocator.Temp);
                var sum = 0;
                for (var q = 0; q < Queries.Length; q++)
                    if (tree.TryFindNearest(Queries[q], out var nearest))
                        sum += nearest;

                Checksum[0] = sum;
                tree.Dispose();
            }
        }

        private void RunKdTreeSuite()
        {
            var ops = _sampleCount + _queryCount; // build + nearest queries
            var rng = new Random((uint)_seed + 2u);

            var points = new NativeArray<float2>(_sampleCount, Allocator.TempJob);
            var items = new NativeArray<int>(_sampleCount, Allocator.TempJob);
            var queries = new NativeArray<float2>(_queryCount, Allocator.TempJob);
            for (var i = 0; i < _sampleCount; i++)
            {
                points[i] = rng.NextFloat2(new float2(-_worldExtent), new float2(_worldExtent));
                items[i] = i;
            }
            for (var i = 0; i < _queryCount; i++)
                queries[i] = rng.NextFloat2(new float2(-_worldExtent), new float2(_worldExtent));

            try
            {
                _results.Add(Measure("Managed KdTree2D", ops, true, () =>
                {
                    var tree = KdTree2D<int>.Create();
                    for (var i = 0; i < _sampleCount; i++)
                        tree.Insert(points[i], i);
                    var sum = 0;
                    for (var q = 0; q < _queryCount; q++)
                        if (tree.TryFindNearest(queries[q], out var nearest))
                            sum += nearest;
                    _ = sum;
                }));

                using var checksum = new NativeArray<int>(1, Allocator.TempJob);
                var job = new NativeKdBuildQueryJob
                {
                    Points = points, Items = items, Queries = queries, Checksum = checksum,
                };
                job.Schedule().Complete(); // warm-up

                _results.Add(Measure("NativeKdTree2D (Burst)", ops, false, () =>
                {
                    job.Schedule().Complete();
                }));
            }
            finally
            {
                points.Dispose();
                items.Dispose();
                queries.Dispose();
            }
        }

        // ---- Measurement primitive ---------------------------------------

        private static Result Measure(string label, int operations, bool isBaseline, Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            var after = GC.GetTotalMemory(false);

            return new Result
            {
                Label = label,
                Milliseconds = sw.Elapsed.TotalMilliseconds,
                AllocatedBytes = Math.Max(0, after - before),
                Operations = operations,
                IsBaseline = isBaseline,
            };
        }

        // ---- Source tracing ----------------------------------------------

        // Opens this benchmark window's own source file so the exact code producing
        // the numbers can be inspected.
        private void OpenBenchmarkScript()
        {
            var script = MonoScript.FromScriptableObject(this);
            if (script != null)
            {
                SetStatus("Opening benchmark source…", MessageType.Info);
                AssetDatabase.OpenAsset(script);
                return;
            }

            SetStatus("Could not locate the benchmark script asset.", MessageType.Warning);
        }

        // Locates and opens the Burst-in-Job PlayMode test script, which contains the
        // correctness assertions for the same Native* containers.
        private void OpenPlayModeTestScript()
        {
            var guids = AssetDatabase.FindAssets($"{PlayModeTestScriptName} t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileNameWithoutExtension(path)
                        .Equals(PlayModeTestScriptName, StringComparison.Ordinal))
                    continue;

                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                    continue;

                SetStatus("Opening PlayMode test source…", MessageType.Info);
                AssetDatabase.OpenAsset(script);
                return;
            }

            SetStatus($"Could not locate the test script \"{PlayModeTestScriptName}\".", MessageType.Warning);
        }

        // ---- Formatting helpers ------------------------------------------

        private double FindBaselineMs()
        {
            foreach (var result in _results)
            {
                if (result.IsBaseline)
                    return result.Milliseconds;
            }
            return 0.0;
        }

        private static string FormatSpeedup(double ms, double baselineMs)
        {
            if (baselineMs <= 0.0 || ms <= 0.0)
                return "—";

            var ratio = baselineMs / ms;
            if (ratio >= 1.0)
                return $"{ratio.ToString("F2", CultureInfo.InvariantCulture)}× faster";
            return $"{(1.0 / ratio).ToString("F2", CultureInfo.InvariantCulture)}× slower";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
                return "~0 B";
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{(bytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture)} KB";
            return $"{(bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture)} MB";
        }

        private void SetStatus(string message, MessageType type)
        {
            _status = message;
            _statusType = type;
        }
    }
}
