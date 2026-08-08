#nullable enable

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Projects;

/// <summary>
/// Saves / loads the .dxfstudio project format. The mapping between
/// <see cref="CadDocument"/> and <see cref="ProjectFile"/> is 1:1 for the
/// geometry (Line/Arc/Circle/Polyline with arc segments) so a save→load cycle
/// reproduces the same document within double precision; analysis results are
/// not persisted (re-run analysis after load).
/// </summary>
public sealed class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        // Numbers must round-trip exactly: invariant culture, no scientific
        // abbreviation, 15-17 significant digits preserved.
        o.NumberHandling = JsonNumberHandling.Strict;
        return o;
    }

    /// <summary>Serializes the document into the project model (no file I/O).</summary>
    public static ProjectFile ToProject(CadDocument document, GeometryTolerance tolerance)
    {
        var p = new ProjectFile
        {
            Units = document.Units,
            Tolerance = new ToleranceSettings
            {
                PointEqualityTolerance = tolerance.PointEqualityTolerance,
                EndpointSnapTolerance = tolerance.EndpointSnapTolerance,
                ZeroLengthTolerance = tolerance.ZeroLengthTolerance,
                ClosureTolerance = tolerance.ClosureTolerance,
                SmallGeometryThreshold = tolerance.SmallGeometryThreshold,
            },
            Diagnostics = new DiagnosticSettings
            {
                DuplicateTolerance = tolerance.DuplicateTolerance,
                SelfIntersectionTolerance = tolerance.SelfIntersectionTolerance,
            },
        };

        if (document.SourceFilePath is { } src)
        {
            p.Source = new ProjectSourceInfo
            {
                FileName = Path.GetFileName(src),
                FilePath = src,
                DxfVersion = null,
                ImportSummary = document.ImportSummary,
            };
        }

        foreach (LayerState layer in document.Layers)
        {
            p.Layers.Add(new LayerProjection
            {
                Name = layer.Name,
                IsOn = layer.IsOn,
                IsFrozen = layer.IsFrozen,
                AciColorIndex = layer.AciColorIndex,
                IsColorByLayer = layer.IsColorByLayer,
                IsVisible = document.IsLayerVisible(layer.Name),
            });
        }

        foreach (IGeometryEntity e in document.Entities)
        {
            p.Entities.Add(ProjectEntity(e));
        }

        return p;
    }

    private static EntityProjection ProjectEntity(IGeometryEntity e)
    {
        var ep = new EntityProjection { Id = e.Id, Layer = e.LayerName, Visible = e.IsVisible };
        switch (e)
        {
            case LineGeometry l:
                ep.Kind = "Line";
                ep.Line = new LineProjection { P0X = l.P0.X, P0Y = l.P0.Y, P1X = l.P1.X, P1Y = l.P1.Y };
                break;
            case CircleGeometry c:
                ep.Kind = "Circle";
                ep.Circle = new CircleProjection { CenterX = c.Center.X, CenterY = c.Center.Y, Radius = c.Radius };
                break;
            case ArcGeometry a:
                ep.Kind = "Arc";
                ep.Arc = new ArcProjection
                {
                    CenterX = a.Center.X,
                    CenterY = a.Center.Y,
                    Radius = a.Radius,
                    StartAngleRadians = a.StartAngleRadians,
                    SweepRadians = a.SweepRadians,
                };
                break;
            case PolylineGeometry p:
                ep.Kind = "Polyline";
                ep.Polyline = new PolylineProjection { IsClosed = p.IsClosed };
                foreach (var s in p.Segments)
                {
                    ep.Polyline.Segments.Add(new SegmentProjection
                    {
                        Kind = s.GeometryType == GeometryType.Arc ? "Arc" : "Line",
                        StartX = s.StartPoint.X,
                        StartY = s.StartPoint.Y,
                        EndX = s.EndPoint.X,
                        EndY = s.EndPoint.Y,
                        CenterX = s is ArcSegment arc ? arc.Center.X : 0,
                        CenterY = s is ArcSegment arc2 ? arc2.Center.Y : 0,
                        Radius = s is ArcSegment arc3 ? arc3.Radius : 0,
                        StartAngleRadians = s is ArcSegment arc4 ? arc4.StartAngleRadians : 0,
                        SweepRadians = s is ArcSegment arc5 ? arc5.SweepRadians : 0,
                    });
                }

                break;
        }

        return ep;
    }

    /// <summary>Serializes the project model to JSON (no file I/O).</summary>
    public static string Serialize(ProjectFile project) =>
        JsonSerializer.Serialize(project, Options);

    /// <summary>Parses JSON into a project model; throws on malformed input.</summary>
    public static ProjectFile Deserialize(string json) =>
        JsonSerializer.Deserialize<ProjectFile>(json, Options) ?? throw new InvalidDataException("Project JSON is empty.");

    /// <summary>Writes the project file to disk (atomic: temp + move).</summary>
    public static void Save(ProjectFile project, string path)
    {
        string dir = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        string tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tmp, Serialize(project), System.Text.Encoding.UTF8);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Reads a project file from disk and returns the model.</summary>
    public static ProjectFile Load(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>
    /// Materializes a document from a project model. Tolerance settings are
    /// applied to the returned <see cref="GeometryTolerance"/> (which is also
    /// returned for the caller to adopt). Entity ids and order are preserved.
    /// </summary>
    public static (CadDocument Document, GeometryTolerance Tolerance) ToDocument(ProjectFile project)
    {
        if (project.SchemaVersion > ProjectFile.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Project schema version {project.SchemaVersion} is newer than supported ({ProjectFile.CurrentSchemaVersion}).");
        }

        var tolerance = new GeometryTolerance
        {
            PointEqualityTolerance = project.Tolerance.PointEqualityTolerance,
            EndpointSnapTolerance = project.Tolerance.EndpointSnapTolerance,
            ZeroLengthTolerance = project.Tolerance.ZeroLengthTolerance,
            ClosureTolerance = project.Tolerance.ClosureTolerance,
            SmallGeometryThreshold = project.Tolerance.SmallGeometryThreshold,
            DuplicateTolerance = project.Diagnostics.DuplicateTolerance,
        };

        var entities = new List<IGeometryEntity>();
        foreach (EntityProjection ep in project.Entities)
        {
            IGeometryEntity? entity = UnprojectEntity(ep);
            if (entity is not null)
            {
                entities.Add(entity);
            }
        }

        var layers = project.Layers
            .Select(l => new LayerState(l.Name, l.IsOn, l.IsFrozen, l.AciColorIndex, l.IsColorByLayer))
            .ToList();

        var doc = new CadDocument();
        doc.ReplaceContent(entities, layers, project.Source.FilePath, project.Source.ImportSummary, null);
        doc.Units = project.Units;

        foreach (LayerProjection lp in project.Layers)
        {
            doc.SetLayerVisible(lp.Name, lp.IsVisible);
        }

        return (doc, tolerance);
    }

    private static IGeometryEntity? UnprojectEntity(EntityProjection ep)
    {
        switch (ep.Kind)
        {
            case "Line" when ep.Line is { } l:
                return new LineGeometry(ep.Id, ep.Layer, new Point2(l.P0X, l.P0Y), new Point2(l.P1X, l.P1Y), ep.Visible);
            case "Circle" when ep.Circle is { } c:
                return new CircleGeometry(ep.Id, ep.Layer, new Point2(c.CenterX, c.CenterY), c.Radius, ep.Visible);
            case "Arc" when ep.Arc is { } a:
                return new ArcGeometry(ep.Id, ep.Layer, new Point2(a.CenterX, a.CenterY), a.Radius, a.StartAngleRadians, a.SweepRadians, a.SweepRadians >= 0, ep.Visible);
            case "Polyline" when ep.Polyline is { } p:
            {
                var segments = new List<IPathSegment>();
                foreach (SegmentProjection s in p.Segments)
                {
                    if (s.Kind == "Arc")
                    {
                        segments.Add(new ArcSegment(
                            new Point2(s.CenterX, s.CenterY), s.Radius,
                            s.StartAngleRadians, s.SweepRadians, s.SweepRadians >= 0));
                    }
                    else
                    {
                        segments.Add(new LineSegment(new Point2(s.StartX, s.StartY), new Point2(s.EndX, s.EndY)));
                    }
                }

                return new PolylineGeometry(ep.Id, ep.Layer, segments, p.IsClosed, ep.Visible);
            }

            default:
                return null;
        }
    }
}
