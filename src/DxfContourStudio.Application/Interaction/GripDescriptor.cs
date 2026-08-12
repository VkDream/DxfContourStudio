#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// The kind of a grip (drag handle) shown on a selected entity. Pure
/// interaction/view state — Core geometry never references it (ADR-015
/// "Interaction state lives in the Application/UI layers").
/// </summary>
public enum GripKind
{
    LineStart,
    LineEnd,
    CircleCenter,
    CircleRadius,
    ArcCenter,
    ArcStart,
    ArcEnd,
    PolylineVertex,
}

/// <summary>
/// A single grip handle: which entity it belongs to, what it edits and where
/// it currently sits in world coordinates. <see cref="Parameter"/> is the
/// entity-internal index when the grip addresses one of several equal points
/// (polyline vertex index; -1 when not applicable).
/// </summary>
/// <param name="EntityId">The entity this grip edits.</param>
/// <param name="Kind">What the grip drags.</param>
/// <param name="WorldPosition">Current world-space position of the handle.</param>
/// <param name="Parameter">Entity-specific index (polyline vertex index), -1 for singular grips.</param>
/// <param name="Enabled">False when the grip must render but reject dragging (e.g. hidden layer).</param>
public readonly record struct GripDescriptor(
    long EntityId,
    GripKind Kind,
    Point2 WorldPosition,
    int Parameter = -1,
    bool Enabled = true);