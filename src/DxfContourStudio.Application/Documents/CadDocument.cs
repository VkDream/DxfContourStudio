#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Documents;

/// <summary>
/// In-memory representation of an opened drawing. Holds the imported
/// geometry plus layer information and per-layer visibility (view state).
/// Does not know anything about DXF files or WPF rendering — both stay
/// outside this class.
///
/// Layer visibility is a view-level flag: hiding a layer never deletes
/// entities, it only removes them from rendering, picking and selection
/// (<see cref="VisibleEntities"/> / <see cref="IsVisibleForInteraction"/>).
///
/// Thread-safety: not thread-safe; the WPF layer must mutate it on the UI
/// thread (commands run on the dispatcher).
/// </summary>
public sealed class CadDocument
{
    private readonly List<IGeometryEntity> _entities = [];
    private readonly Dictionary<string, bool> _layerVisibility = new(StringComparer.OrdinalIgnoreCase);

    // Spatial index (ADR-015): lazily rebuilt whenever the entity list
    // changes. The revision counter is bumped in MutateAndNotify so pick
    // queries never scan the full list once the index is built.
    private int _revision;
    private SpatialIndex? _pickIndex;
    private int _pickRevision = -1;
    private const double PickCellSize = 8.0;

    /// <summary>
    /// Raised whenever the document's data changes (entities replaced, added,
    /// removed). View-level changes (layer visibility) do NOT raise this: a
    /// hidden layer is a view preference, not a document mutation (see
    /// ADR-009-Project-Format). Subscribers use it to mark the dirty state.
    /// </summary>
    public event Action? DataChanged;

    /// <summary>
    /// True when the document content differs from what was last saved /
    /// imported. Set by <see cref="MarkDataChanged"/>; cleared by the caller
    /// after a successful save or import (no event-driven auto-clear — the
    /// caller owns the save lifecycle).
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>Source file the document was imported from (null when empty/new).</summary>
    public string? SourceFilePath { get; private set; }

    /// <summary>
    /// The unit the document geometry is expressed in (the interpreted import
    /// unit, or the persisted project unit). Geometry itself is always stored
    /// in millimeters internally.
    /// </summary>
    public LengthUnit Units { get; set; } = LengthUnit.Millimeter;

    /// <summary>The layers declared by the source, in import order.</summary>
    public IReadOnlyList<LayerState> Layers { get; private set; } = [];

    /// <summary>
    /// Marks the document as modified and notifies listeners. Called by the
    /// mutation paths (import, commands, batch repair, project load).
    /// </summary>
    public void MarkDataChanged()
    {
        IsDirty = true;
        DataChanged?.Invoke();
    }

    /// <summary>
    /// Records a geometry mutation (replacement / insert / remove) and marks
    /// the document dirty. Kept separate so commands don't need to know the
    /// dirty bookkeeping.
    /// </summary>
    private void MutateAndNotify()
    {
        _revision++;
        MarkDataChanged();
    }

    /// <summary>All geometry entities currently in the document (in stable order).</summary>
    public IReadOnlyList<IGeometryEntity> Entities => _entities;

    /// <summary>Human-readable note about the import (units, warning count, ...).</summary>
    public string? ImportSummary { get; private set; }

    /// <summary>When the last import failed fatally, this holds the reason; otherwise null.</summary>
    public string? FatalImportError { get; private set; }

    /// <summary>Combined bounds of every visible entity; empty when none are visible.</summary>
    public Bounds? OverallBounds
    {
        get
        {
            var visible = _entities.Where(IsVisibleForInteraction).ToList();
            if (visible.Count == 0)
            {
                return null;
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var e in visible)
            {
                minX = Math.Min(minX, e.Bounds.MinX);
                minY = Math.Min(minY, e.Bounds.MinY);
                maxX = Math.Max(maxX, e.Bounds.MaxX);
                maxY = Math.Max(maxY, e.Bounds.MaxY);
            }

            return new Bounds(minX, minY, maxX, maxY);
        }
    }

    public CadDocument()
    {
    }

    /// <summary>
    /// Replaces the whole content with an import result. Used once when a
    /// file is opened; keeps ids stable across re-imports (ids are assigned
    /// by the reader so they already are unique per import). Layer
    /// visibility resets to "all visible".
    /// </summary>
    public void ReplaceContent(IReadOnlyList<IGeometryEntity> entities, IReadOnlyList<LayerState> layers, string? sourcePath, string? summary, string? fatalError)
    {
        _entities.Clear();
        _entities.AddRange(entities);
        Layers = layers;
        SourceFilePath = sourcePath;
        ImportSummary = summary;
        FatalImportError = fatalError;
        ShowAllLayers();
        MarkDataChanged();
    }

    /// <summary>Replaces the full entity list (used by undo/redo restore).</summary>
    public void ReplaceEntities(IReadOnlyList<IGeometryEntity> entities)
    {
        _entities.Clear();
        _entities.AddRange(entities);
        MutateAndNotify();
    }

    /// <summary>Appends one entity (used by drawing/editing commands).</summary>
    public void AddEntity(IGeometryEntity entity)
    {
        _entities.Add(entity);
        MutateAndNotify();
    }

    /// <summary>
    /// Inserts an entity at a specific position (used by undo of a delete so
    /// relative ordering is restored). Clamps the index into range.
    /// </summary>
    public void InsertEntity(int index, IGeometryEntity entity)
    {
        _entities.Insert(Math.Clamp(index, 0, _entities.Count), entity);
        MutateAndNotify();
    }

    /// <summary>Removes one entity by identity.</summary>
    public bool RemoveEntity(long id)
    {
        int idx = _entities.FindIndex(e => e.Id == id);
        if (idx < 0)
        {
            return false;
        }

        _entities.RemoveAt(idx);
        MutateAndNotify();
        return true;
    }

    /// <summary>Returns the entity with the given id, or null when absent.</summary>
    public IGeometryEntity? GetEntityById(long id) => _entities.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Monotonic revision of the geometry content, incremented by every
    /// mutation (replace / insert / remove / replace-all). View-level changes
    /// (layer visibility) do not touch it. UI layers use it to invalidate
    /// derived caches (snap grids, overlay buffers, ...).
    /// </summary>
    public int GeometryRevision => _revision;

    /// <summary>
    /// How many entities the most recent <see cref="Pick"/> /
    /// <see cref="QueryNear"/> took from the spatial index before the
    /// visibility filter. Test hook proving that picking does not scan the
    /// whole entity list (ADR-015).
    /// </summary>
    public int LastPickQueryCandidateCount { get; private set; }

    /// <summary>
    /// Replaces an existing entity in place (same list position), keeping
    /// document ordering stable. Used by move/transform commands.
    /// </summary>
    public bool ReplaceEntity(IGeometryEntity replacement)
    {
        int idx = _entities.FindIndex(e => e.Id == replacement.Id);
        if (idx < 0)
        {
            return false;
        }

        _entities[idx] = replacement;
        MutateAndNotify();
        return true;
    }

    /// <summary>
    /// Whether the entity participates in rendering / picking / selection:
    /// the entity itself is visible AND its layer is not hidden.
    /// </summary>
    public bool IsVisibleForInteraction(IGeometryEntity entity)
    {
        return entity.IsVisible && IsLayerVisible(entity.LayerName);
    }

    /// <summary>All entities that are currently visible for rendering and interaction.</summary>
    public IReadOnlyList<IGeometryEntity> VisibleEntities =>
        _entities.Where(IsVisibleForInteraction).ToList();

    /// <summary>Whether the given layer is visible (default true when unknown).</summary>
    public bool IsLayerVisible(string layerName)
    {
        return !_layerVisibility.TryGetValue(layerName, out bool visible) || visible;
    }

    /// <summary>Sets layer visibility. Hiding a layer does not touch entities.</summary>
    public void SetLayerVisible(string layerName, bool visible)
    {
        _layerVisibility[layerName] = visible;
    }

    /// <summary>Makes every layer visible.</summary>
    public void ShowAllLayers() => _layerVisibility.Clear();

    /// <summary>Hides every known layer.</summary>
    public void HideAllLayers()
    {
        foreach (LayerState layer in Layers)
        {
            SetLayerVisible(layer.Name, false);
        }
    }

    /// <summary>Number of entities living on the given layer.</summary>
    public int EntityCountOnLayer(string layerName) => _entities.Count(e => e.LayerName == layerName);

    /// <summary>
    /// All entities whose geometry passes within <paramref name="tolerance"/>
    /// of <paramref name="p"/> and whose layer is visible. The tolerance is in
    /// world units — pass <c>viewport.PixelsToWorld(pickTolerancePx)</c>.
    /// <summary>
    /// Picks all interact-visible entities whose exact distance to the point
    /// is within the tolerance. Uses a lazily-rebuilt uniform-grid spatial
    /// index (docs/ADR-000/ADR-015-SpatialIndex.md) so picking does not scan
    /// the entity list linearly for large documents.
    /// </summary>
    public List<IGeometryEntity> Pick(Point2 p, double tolerance)
    {
        var index = GetOrBuildIndex();
        var hits = index.Query(p, tolerance);
        LastPickQueryCandidateCount = hits.Count;
        // Preserve the document's interaction-visibility semantics.
        return hits.Where(IsVisibleForInteraction).ToList();
    }

    /// <summary>
    /// Responds with the interact-visible entities within <paramref name="radius"/>
    /// of <paramref name="p"/> via the unbiased per-entity distance check of the
    /// spatial index (same path as <see cref="Pick"/>). Used by the snap hover
    /// pipeline (D13A) to pre-filter candidates near the cursor so the snap
    /// engine never scans the full entity list on mouse move.
    /// </summary>
    public List<IGeometryEntity> QueryNear(Point2 p, double radius)
    {
        var index = GetOrBuildIndex();
        var hits = index.Query(p, radius);
        LastPickQueryCandidateCount = hits.Count;
        return hits.Where(IsVisibleForInteraction).ToList();
    }

    private SpatialIndex GetOrBuildIndex()
    {
        int revision = _revision;
        if (_pickIndex is not null && _pickRevision == revision)
        {
            return _pickIndex;
        }

        _pickIndex = new SpatialIndex(PickCellSize);
        _pickRevision = revision;
        foreach (var e in _entities)
        {
            _pickIndex.Add(e);
        }

        return _pickIndex;
    }
}

/// <summary>
/// Plain layer state used by the document. Color is the ACI index (0=ByBlock,
/// 256=ByLayer) so the presentation layer can map it to actual colors.
/// </summary>
public sealed record LayerState(string Name, bool IsOn, bool IsFrozen, short AciColorIndex, bool IsColorByLayer);
