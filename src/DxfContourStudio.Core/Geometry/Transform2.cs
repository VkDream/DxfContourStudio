#nullable enable
using System.Numerics;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Immutable 2D affine transform built from a 3x3 matrix
/// (translation, rotation, uniform / non-uniform scale, shear).
/// Stored so that <c>p' = M * p</c>.
/// </summary>
public readonly struct Transform2 : IEquatable<Transform2>
{
    /// <summary>Row-major 3x3 affine matrix: [m11 m12 m13; m21 m22 m23; 0 0 1].</summary>
    private readonly double _m11, _m12, _m13, _m21, _m22, _m23;

    private Transform2(double m11, double m12, double m13, double m21, double m22, double m23)
    {
        _m11 = m11; _m12 = m12; _m13 = m13;
        _m21 = m21; _m22 = m22; _m23 = m23;
    }

    /// <summary>The identity transform.</summary>
    public static Transform2 Identity { get; } = new Transform2(1, 0, 0, 0, 1, 0);

    /// <summary>Creates a pure translation transform.</summary>
    public static Transform2 CreateTranslation(double tx, double ty) => new(1, 0, tx, 0, 1, ty);

    /// <summary>Creates a pure translation transform.</summary>
    public static Transform2 CreateTranslation(Vector2 v) => CreateTranslation(v.X, v.Y);

    /// <summary>Creates a uniform scale around the origin.</summary>
    public static Transform2 CreateScale(double sx, double sy) => new(sx, 0, 0, 0, sy, 0);

    /// <summary>Creates a uniform scale around the origin.</summary>
    public static Transform2 CreateScale(double uniform) => CreateScale(uniform, uniform);

    /// <summary>Creates a rotation in radians (math convention, counter-clockwise with Y up).</summary>
    public static Transform2 CreateRotation(double angleRadians)
    {
        double c = Math.Cos(angleRadians);
        double s = Math.Sin(angleRadians);
        return new Transform2(c, -s, 0, s, c, 0);
    }

    /// <summary>
    /// Creates a transform that mirrors across the X axis — exactly the
    /// flip needed when converting between CAD world (Y up) and WPF screen (Y down).
    /// </summary>
    public static Transform2 CreateScaleXFlip() => new(1, 0, 0, 0, -1, 0);

    /// <summary>Applies this transform to a point.</summary>
    public Point2 Apply(Point2 p)
    {
        return new Point2(
            _m11 * p.X + _m12 * p.Y + _m13,
            _m21 * p.X + _m22 * p.Y + _m23);
    }

    /// <summary>Applies this transform to a vector (translation is ignored).</summary>
    public Vector2 ApplyVector(Vector2 v)
    {
        return new Vector2(
            _m11 * v.X + _m12 * v.Y,
            _m21 * v.X + _m22 * v.Y);
    }

/// <summary>
    /// Composition: this transform applied after <paramref name="first"/>.
    /// i.e. result of combining two transforms, so that
    /// <c>result.Apply(p) = this.Apply(first.Apply(p))</c>.
    /// </summary>
    public Transform2 Compose(Transform2 first)
    {
        // rows of this  ×  columns of first
        double m11 = _m11 * first._m11 + _m12 * first._m21;
        double m12 = _m11 * first._m12 + _m12 * first._m22;
        double m13 = _m11 * first._m13 + _m12 * first._m23 + _m13;
        double m21 = _m21 * first._m11 + _m22 * first._m21;
        double m22 = _m21 * first._m12 + _m22 * first._m22;
        double m23 = _m21 * first._m13 + _m22 * first._m23 + _m23;
        return new Transform2(m11, m12, m13, m21, m22, m23);
    }

    /// <summary>
    /// Attempts to invert the transform. Pure translations/rotations/uniform
    /// scales always succeed. Singular matrices (e.g. zero scale) return false
    /// and out an identity.
    /// </summary>
    public bool TryInvert(out Transform2 inverse)
    {
        double det = _m11 * _m22 - _m12 * _m21;
        if (Math.Abs(det) < 1e-12)
        {
            inverse = Identity;
            return false;
        }

        double invDet = 1.0 / det;
        double ia11 = _m22 * invDet;
        double ia12 = -_m12 * invDet;
        double ia21 = -_m21 * invDet;
        double ia22 = _m11 * invDet;
        double b1 = _m13, b2 = _m23;
        double ib1 = -(ia11 * b1 + ia12 * b2);
        double ib2 = -(ia21 * b1 + ia22 * b2);

        inverse = new Transform2(ia11, ia12, ib1, ia21, ia22, ib2);
        return true;
    }

    /// <summary>Determinant of the linear part.</summary>
    public double Determinant => _m11 * _m22 - _m12 * _m21;

    /// <summary>Scaling factors (magnitudes of basis vectors). 1.0 if uniform identity.</summary>
    public (double ScaleX, double ScaleY) Scale()
    {
        return (Math.Sqrt(_m11 * _m11 + _m21 * _m21),
                Math.Sqrt(_m12 * _m12 + _m22 * _m22));
    }

    public bool Equals(Transform2 other)
    {
        const double eps = 1e-9;
        return Math.Abs(_m11 - other._m11) < eps &&
               Math.Abs(_m12 - other._m12) < eps &&
               Math.Abs(_m13 - other._m13) < eps &&
               Math.Abs(_m21 - other._m21) < eps &&
               Math.Abs(_m22 - other._m22) < eps &&
               Math.Abs(_m23 - other._m23) < eps;
    }

    public override bool Equals(object? obj) => obj is Transform2 t && Equals(t);

    public override int GetHashCode()
    {
        return HashCode.Combine(_m11, _m12, _m13, _m21, _m22, _m23);
    }

    public static bool operator ==(Transform2 left, Transform2 right) => left.Equals(right);
    public static bool operator !=(Transform2 left, Transform2 right) => !left.Equals(right);
}