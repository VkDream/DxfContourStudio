#nullable enable

using System.Globalization;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Localization;

/// <summary>
/// The single place that decides how numbers are displayed in the UI. Every
/// length, coordinate, angle and zoom value must be formatted through this
/// class so the format stays uniform everywhere (tests assert the format).
///
/// Convention (zh-CN UI):
/// - lengths / coordinates: 3 fixed decimals + " mm"  (e.g. "25.000 mm")
/// - angles: 3 fixed decimals + "°"                    (e.g. "90.000°")
/// - zoom: whole percent, no decimals                  (e.g. "405%")
/// All formatting is culture-invariant: a comma decimal separator must never
/// appear, no matter which Windows display language the user has.
/// </summary>
public static class DisplayFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats a length in millimeters: "25.000 mm". NaN / Infinity /
    /// double.MaxValue / double.MinValue are never shown as numbers — they
    /// render as "n/a" (the localized fallback is applied by the caller via
    /// <see cref="NotAvailable"/>); a non-finite value is a bug and must not
    /// reach the user as a gigantic number.
    /// </summary>
    public static string Length(double millimeters) =>
        IsDisplayable(millimeters) ? $"{Fixed(millimeters)} mm" : NotAvailable();

    /// <summary>Formats one world coordinate: "-1.167 mm".</summary>
    public static string Coordinate(double value) =>
        IsDisplayable(value) ? $"{Fixed(value)} mm" : NotAvailable();

    /// <summary>Formats a point: "0.000 mm, 100.000 mm".</summary>
    public static string Point(Point2 p) =>
        IsDisplayable(p.X) && IsDisplayable(p.Y)
            ? $"{Fixed(p.X)} mm, {Fixed(p.Y)} mm"
            : NotAvailable();

    /// <summary>Formats an angle (input in radians): "90.000°".</summary>
    public static string AngleDegrees(double radians) =>
        IsDisplayable(radians) ? $"{Fixed(radians * (180.0 / Math.PI))}°" : NotAvailable();

    /// <summary>Formats a zoom factor as a percentage: PixelsPerWorld 4.05 → "405%".</summary>
    public static string ZoomPercent(double pixelsPerWorld) =>
        IsDisplayable(pixelsPerWorld) ? $"{(pixelsPerWorld * 100.0):0}%" : NotAvailable();

    /// <summary>Formats a plain count: "4".</summary>
    public static string Count(long value) => value.ToString(Invariant);

    /// <summary>Formats an elapsed time in seconds with 3 decimals: "0.125 s".</summary>
    public static string ElapsedSeconds(double seconds) =>
        IsDisplayable(seconds) ? $"{Fixed(seconds)} s" : NotAvailable();

    /// <summary>
    /// The localized "not available" placeholder (zh: "无" / en: "n/a").
    /// Non-finite values must surface as this instead of raw numbers.
    /// </summary>
    public static string NotAvailable() => "无";

    /// <summary>
    /// True only for finite values that are safe to print: NaN, ±Infinity and
    /// the double Min/Max sentinels are excluded so they can never leak into
    /// user-visible distances.
    /// </summary>
    public static bool IsDisplayable(double value) =>
        double.IsFinite(value) &&
        value != double.MaxValue &&
        value != double.MinValue;

    /// <summary>Internal: 3 fixed decimals, invariant culture.</summary>
    private static string Fixed(double value) => value.ToString("0.000", Invariant);
}
