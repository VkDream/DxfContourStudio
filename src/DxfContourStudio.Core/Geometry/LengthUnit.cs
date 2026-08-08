namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Recognized drawing length units. The set mirrors the most common values of
/// the DXF <c>$INSUNITS</c> header together with explicit "unknown" and
/// "unitless" states, which DXF files legitimately carry and which must never
/// be silently interpreted as millimeters.
/// </summary>
public enum LengthUnit
{
    /// <summary>Unit not declared / could not be read from the source file.</summary>
    Unknown = 0,

    /// <summary>Explicitly unitless (no physical meaning assumed).</summary>
    Unitless = 1,

    /// <summary>Millimeters — the canonical internal unit.</summary>
    Millimeter = 2,

    /// <summary>Centimeters.</summary>
    Centimeter = 3,

    /// <summary>Meters.</summary>
    Meter = 4,

    /// <summary>Inches.</summary>
    Inch = 5,

    /// <summary>Feet.</summary>
    Foot = 6,
}

/// <summary>
/// Converts lengths from a source <see cref="LengthUnit"/> into millimeters.
///
/// The Core layer always works with millimeters; this class is the single
/// place where non-metric input units are turned into millimeters so that
/// unexpected units are handled centrally rather than scattered across the
/// import pipeline or the UI.
/// </summary>
public static class UnitConverter
{
    /// <summary>
    /// Factor that multiplies a value expressed in <paramref name="from"/> so
    /// that the result is expressed in millimeters. Unknown / Unitless return 1.0
    /// (i.e. treated as millimeter) — callers must rely on the import report
    /// documenting the declared unit, not on this fallback.
    /// </summary>
    public static double ToMillimetersFactor(LengthUnit from)
    {
        return from switch
        {
            LengthUnit.Millimeter => 1.0,
            LengthUnit.Centimeter => 10.0,
            LengthUnit.Meter => 1000.0,
            LengthUnit.Inch => 25.4,
            LengthUnit.Foot => 304.8,
            _ => 1.0, // Unknown / Unitless are assumed mm in the engine, reported separately
        };
    }

    /// <summary>Converts a value from <paramref name="from"/> unit to millimeters.</summary>
    public static double ToMillimeters(double value, LengthUnit from)
    {
        return value * ToMillimetersFactor(from);
    }

    /// <summary>Converts a value from millimeters into <paramref name="to"/> unit.</summary>
    public static double FromMillimeters(double millimeters, LengthUnit to)
    {
        return millimeters / ToMillimetersFactor(to);
    }

    /// <summary>
    /// Directly maps a numeric DXF <c>$INSUNITS</c> value (group code 70) to
    /// a <see cref="LengthUnit"/>. Values matching AutoCAD’s table are mapped,
    /// everything else returns <see cref="LengthUnit.Unknown"/>.
    /// </summary>
    public static LengthUnit FromDxfInsUnits(int insUnits)
    {
        return insUnits switch
        {
            0 => LengthUnit.Unitless,
            1 => LengthUnit.Inch,
            2 => LengthUnit.Foot,
            4 => LengthUnit.Millimeter,
            5 => LengthUnit.Centimeter,
            6 => LengthUnit.Meter,
            _ => LengthUnit.Unknown,
        };
    }
}