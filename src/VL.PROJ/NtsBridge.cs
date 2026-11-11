using NetTopologySuite.Geometries;
using SharpProj;

namespace VL.PROJ;

// Runs a PROJ transform over every coordinate of an NTS geometry, in place - on a COPY the caller
// made (Engine.Reproject), never on the caller's geometry.
//
// Ours rather than SharpProj.NetTopologySuite's (docs/DESIGN.md): that package puts its .NET Core
// build in lib\netcore, which is not a target-framework folder, so NuGet falls back to its net47
// build (NU1701, measured 2026-11-11); and its Reproject needs every SRID registered in a global
// SridRegister first. What it does for us is this one filter.
//
// Z is transformed when the coordinate has one, and left NaN when it has not: PROJ is given a 2D
// point then, so a 2D CRS pair never invents a height. M is not touched.

/// <summary>Applies a PROJ transform to every coordinate of a geometry, in place.</summary>
internal sealed class TransformFilter : ICoordinateSequenceFilter
{
    private readonly CoordinateTransform _transform;

    public TransformFilter(CoordinateTransform transform) => _transform = transform;

    /// <summary>True once a coordinate came back infinite or NaN: outside what the CRS can express.</summary>
    public bool Failed { get; private set; }

    public void Filter(CoordinateSequence seq, int i)
    {
        var x = seq.GetX(i);
        var y = seq.GetY(i);
        var hasZ = seq.HasZ && !double.IsNaN(seq.GetZ(i));
        var p = _transform.Apply(hasZ ? new PPoint(x, y, seq.GetZ(i)) : new PPoint(x, y));
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) Failed = true;
        seq.SetX(i, p.X);
        seq.SetY(i, p.Y);
        if (hasZ) seq.SetZ(i, p.Z);
    }

    public bool Done => Failed;

    public bool GeometryChanged => true;
}
