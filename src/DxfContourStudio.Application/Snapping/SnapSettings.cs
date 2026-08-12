#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Snapping;

/// <summary>
/// Session-level snap settings (D13A). Deliberately NOT persisted into the
/// project schema: these are user-session preferences only until a later
/// milestone decides on a settings store. The hover pipeline reads this via
/// the ViewModel; nothing here mutates the document.
/// </summary>
public sealed class SnapSettings
{
    /// <summary>Master switch — when false no snap candidate is produced.</summary>
    public bool Enabled { get; set; } = true;

    public bool EndpointEnabled { get; set; } = true;

    public bool MidpointEnabled { get; set; } = true;

    public bool CenterEnabled { get; set; } = true;

    public bool IntersectionEnabled { get; set; } = true;

    /// <summary>Off by default: avoids the cursor being glued to every path.</summary>
    public bool NearestEnabled { get; set; }

    /// <summary>Snap radius in device pixels (default 8 px).</summary>
    public double PixelTolerance { get; set; } = 8.0;

    /// <summary>The kinds this session enables, as a flags mask for the engine.</summary>
    public SnapKinds EnabledKinds
    {
        get
        {
            var kinds = SnapKinds.None;
            if (EndpointEnabled)
            {
                kinds |= SnapKinds.Endpoint;
            }

            if (MidpointEnabled)
            {
                kinds |= SnapKinds.Midpoint;
            }

            if (CenterEnabled)
            {
                kinds |= SnapKinds.Center;
            }

            if (IntersectionEnabled)
            {
                kinds |= SnapKinds.Intersection;
            }

            if (NearestEnabled)
            {
                kinds |= SnapKinds.Nearest;
            }

            return kinds;
        }
    }
}