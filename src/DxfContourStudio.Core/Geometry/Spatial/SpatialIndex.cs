#nullable enable

using System;
using System.Collections.Generic;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Uniform-grid spatial index (docs/ADR-015-Spatial-Index.md) for fast
/// point-radius queries over many entities. Pure and deterministic:
/// - Build once from an entity list + cell size (world units);
/// - each entity is registered in every grid cell its bounds overlap;
/// - Query(p, radius) returns candidate entities whose bounds box may touch
///   the query circle, with exact per-entity DistanceToPoint filtering
///   applied by the caller/index (exact check included here);
/// - the index trades memory for O(1)-ish queries, which the interactive
///   picker turns into a handful of cell lookups instead of an O(n) scan.
///
/// Rebuilds are cheap: Clear/Build from any entity source. The index never
/// owns the entities — stale entries are the caller's responsibility.
/// </summary>
public sealed class SpatialIndex
{
    private readonly double _cellSize;
    private readonly Dictionary<(int, int), GridCell> _cells = new();
    private readonly List<IGeometryEntity> _all = [];

    /// <summary>Number of stored entities (for diagnostics).</summary>
    public int Count => _all.Count;

    public SpatialIndex(double cellSize)
    {
        _cellSize = cellSize > 0 ? cellSize : 1.0;
    }

    /// <summary>Cell buckets (world grid), keyed by cell coordinate.</summary>
    public void Build(IReadOnlyList<IGeometryEntity> entities)
    {
        _cells.Clear();
        _all.Clear();
        _all.AddRange(entities);
        foreach (var e in entities)
        {
            Add(e);
        }
    }

    /// <summary>Opens the index for building (also called by Build).</summary>
    public void Add(IGeometryEntity entity)
    {
        if (entity.Bounds.IsEmpty)
        {
            return;
        }

        Bounds b = entity.Bounds;
        int minKx = CellIndex(b.MinX);
        int minKy = CellIndex(b.MinY);
        int maxKx = CellIndex(b.MaxX);
        int maxKy = CellIndex(b.MaxY);
        for (int kx = minKx; kx <= maxKx; kx++)
        {
            for (int ky = minKy; ky <= maxKy; ky++)
            {
                GetCell(kx, ky).Add(entity);
            }
        }
    }

    private GridCell GetCell(int kx, int ky) => _cells.GetOrCreate((kx, ky));

    /// <summary>
    /// Entities whose bounds intersect the axis-aligned square centered at
    /// <paramref name="p"/> with half-extent <paramref name="radius"/> and
    /// whose exact distance to p is within the radius.
    /// </summary>
    public List<IGeometryEntity> Query(Point2 p, double radius)
    {
        var hits = new List<IGeometryEntity>();
        if (radius <= 0)
        {
            return hits;
        }

        int minKx = CellIndex(p.X - radius);
        int maxKx = CellIndex(p.X + radius);
        int minKy = CellIndex(p.Y - radius);
        int maxKy = CellIndex(p.Y + radius);
        var seen = new HashSet<IGeometryEntity>();
        for (int kx = minKx; kx <= maxKx; kx++)
        {
            for (int ky = minKy; ky <= maxKy; ky++)
            {
                if (!_cells.TryGetValue((kx, ky), out var cell))
                {
                    continue;
                }

                foreach (var e in cell.Items)
                {
                    if (seen.Add(e) && e.DistanceToPoint(p) <= radius)
                    {
                        hits.Add(e);
                    }
                }
            }
        }

        return hits;
    }

    /// <summary>Cell coordinate for a world coordinate (floor division, supports negatives).</summary>
    private int CellIndex(double value) => (int)Math.Floor(value / _cellSize);

    private sealed class GridCell
    {
        private readonly List<IGeometryEntity> _items = new();

        public void Add(IGeometryEntity entity) => _items.Add(entity);

        public IReadOnlyList<IGeometryEntity> Items => _items;
    }
}

internal static class SpatialIndexExt
{
    public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        where TKey : notnull
        where TValue : new()
    {
        if (!dict.TryGetValue(key, out var value))
        {
            value = new TValue();
            dict[key] = value;
        }

        return value;
    }
}