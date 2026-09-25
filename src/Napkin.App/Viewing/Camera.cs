using Avalonia;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// Where the 3D view looks at the drawing from: an orthographic camera
/// (<c>docs/design/assembly-model.md</c> &#xA7;8.1). A value; every navigation verb returns a new one.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Orthographic, not perspective.</strong> Parallel edges stay parallel, a length along an
/// axis is the same number of pixels anywhere on the screen, and the ray through a pixel is simply
/// the view direction placed at that pixel — which is what makes picking and dragging along one
/// axis a single projection each (&#xA7;8.2, &#xA7;8.3). Perspective is &#xA7;11 decision 10, not
/// this slice.
/// </para>
/// <para>
/// <strong>Everything here is <see cref="double"/>, as it is in <see cref="ViewTransform"/>.</strong>
/// This is the render edge: exact model points come in, pixels go out, and no view operation ever
/// writes back to an entity. Orbit, pan and zoom change the camera and nothing else.
/// </para>
/// <para>
/// <strong>The angles.</strong> <see cref="ElevationDegrees"/> is how far above the horizon the eye
/// is: 0 looks at the drawing from the side, 90 straight down. <see cref="AzimuthDegrees"/> spins the
/// eye about world Z, right-handed: at 0 the eye is due south looking north, at 90 due east looking
/// west. So the plan canvas is this camera at elevation 90&#xB0;, azimuth 0&#xB0; — screen right is
/// world +X and screen up is world +Y, exactly <see cref="ViewTransform"/>'s mapping, which a test
/// holds (&#xA7;7.1). <see cref="Isometric"/> is the eye at the south-east, 45&#xB0; round and
/// arctan(1/&#x221A;2) up, where the three axes project to equal lengths.
/// </para>
/// <para>
/// <strong>The centre is a point in space</strong>, and the screen is the plane through it
/// perpendicular to the view. An orthographic camera has no near plane, so <see cref="Ray"/> starts
/// on that plane and a part in front of it is simply at a negative distance along the ray; the
/// picker orders hits by that signed distance.
/// </para>
/// </remarks>
/// <param name="AzimuthDegrees">The spin of the eye about world Z; 0 is from the south.</param>
/// <param name="ElevationDegrees">How far above the horizon the eye is, clamped to [&#x2212;90, 90].</param>
/// <param name="CenterX">The model X, in inches, drawn at the centre of the viewport.</param>
/// <param name="CenterY">The model Y, in inches, drawn at the centre of the viewport.</param>
/// <param name="CenterZ">The model Z, in inches, drawn at the centre of the viewport.</param>
/// <param name="PixelsPerInch">How many pixels one inch covers, clamped as <see cref="ViewTransform.PixelsPerInch"/> is.</param>
/// <param name="Viewport">The size of the control the camera paints into.</param>
public readonly record struct Camera(
    double AzimuthDegrees,
    double ElevationDegrees,
    double CenterX,
    double CenterY,
    double CenterZ,
    double PixelsPerInch,
    Size Viewport)
{
    /// <summary>The vertical field of view a perspective camera opens at.</summary>
    public const double DefaultFieldOfViewDegrees = 45.0;

    /// <summary>The narrowest and widest field of view a perspective camera is held to.</summary>
    public const double MinFieldOfViewDegrees = 10.0;

    /// <inheritdoc cref="CameraProjection"/>
    public CameraProjection Projection { get; init; } = CameraProjection.Orthographic;

    /// <summary>
    /// The vertical field of view of a perspective camera, in degrees. A narrow one flattens the
    /// picture towards orthographic; a wide one exaggerates the foreshortening. Unused by an
    /// orthographic camera.
    /// </summary>
    public double FieldOfViewDegrees { get; init; } = DefaultFieldOfViewDegrees;

    /// <summary>Whether this camera shrinks things with distance.</summary>
    public bool IsPerspective => Projection == CameraProjection.Perspective;

    /// <summary>
    /// How far the eye is from <see cref="Center"/>, in inches, along <see cref="TowardViewer"/>.
    /// Derived, not stored: <see cref="PixelsPerInch"/> is the scale <em>at the centre plane</em>
    /// in both projections, so switching projection keeps the framing there, and the eye sits
    /// wherever makes the viewport's height span the field of view at that scale. Zooming in
    /// therefore brings the eye closer, as a dolly does. Infinite for an orthographic camera.
    /// </summary>
    public double EyeDistance => IsPerspective
        ? Math.Max(Viewport.Height, 1.0) / 2.0
            / (PixelsPerInch * Math.Tan(Radians(ClampFieldOfView(FieldOfViewDegrees)) / 2.0))
        : double.PositiveInfinity;

    /// <summary>The eye, in inches. For an orthographic camera it is <see cref="Center"/>, the point the rays are placed round.</summary>
    public Vector3d Eye => IsPerspective ? Center + (TowardViewer * EyeDistance) : Center;

    /// <summary>
    /// How much a point's offsets from the centre line are scaled by its depth: 1 on the centre
    /// plane, below 1 further away, above 1 nearer, and not finite at or behind the eye. Always 1
    /// for an orthographic camera.
    /// </summary>
    public double ScaleAtDepth(double depth)
    {
        if (!IsPerspective)
        {
            return 1.0;
        }

        double distance = EyeDistance;
        return distance + depth > 1e-9 ? distance / (distance + depth) : double.PositiveInfinity;
    }

    /// <summary>Whether a perspective camera can see a point at all: it is in front of the eye. Always true when orthographic.</summary>
    public bool IsInFront(Vector3d point) => !IsPerspective || EyeDistance + DepthOf(point) > NearClipInches;

    /// <summary>The nearest a perspective camera draws, in inches in front of the eye.</summary>
    public const double NearClipInches = 0.25;

    /// <summary>
    /// How far a point is beyond the near plane, in inches: positive when a perspective camera can
    /// draw it, negative when it is too close to the eye or behind it. Positive everywhere when
    /// orthographic, which has no near plane.
    /// </summary>
    public double BeyondNearPlane(Vector3d point) =>
        IsPerspective ? EyeDistance + DepthOf(point) - NearClipInches : 1.0;

    /// <summary>
    /// How far a point is from where its ray starts, along that ray: the distance from the eye in
    /// perspective, and the depth past the centre plane when orthographic (where every ray starts on
    /// it). What a picker compares a hit's distance with.
    /// </summary>
    public double DistanceAlongRay(Vector3d point)
    {
        if (!IsPerspective)
        {
            return DepthOf(point);
        }

        Vector3d fromEye = point - Eye;
        return Math.Sqrt(Vector3d.Dot(fromEye, fromEye));
    }

    /// <summary>
    /// Cuts a segment off at the near plane, so that what is left can be projected. Returns false when
    /// none of it is in front of the eye. Never changes an orthographic camera's segment.
    /// </summary>
    public bool TryClipToNearPlane(ref Vector3d from, ref Vector3d to)
    {
        double a = BeyondNearPlane(from);
        double b = BeyondNearPlane(to);
        if (a >= 0 && b >= 0)
        {
            return true;
        }

        if (a < 0 && b < 0)
        {
            return false;
        }

        Vector3d crossing = from + ((to - from) * (a / (a - b)));
        if (a < 0)
        {
            from = crossing;
        }
        else
        {
            to = crossing;
        }

        return true;
    }

    /// <summary>The isometric azimuth: the eye at the south-east.</summary>
    public const double IsometricAzimuthDegrees = 45.0;

    /// <summary>
    /// The isometric elevation, arctan(1/&#x221A;2) &#x2248; 35.264&#xB0;: the angle at which world X, Y
    /// and Z all project to the same length.
    /// </summary>
    public static readonly double IsometricElevationDegrees = Math.Atan(1 / Math.Sqrt(2)) * 180 / Math.PI;

    /// <summary>How many degrees one pixel of an orbit drag turns the view.</summary>
    public const double DegreesPerOrbitPixel = 0.4;

    readonly double _elevation = ClampElevation(ElevationDegrees);
    readonly double _pixelsPerInch = ClampScale(PixelsPerInch);

    /// <inheritdoc cref="Camera(double, double, double, double, double, double, Size)"/>
    public double ElevationDegrees
    {
        get => _elevation;
        init => _elevation = ClampElevation(value);
    }

    /// <inheritdoc cref="Camera(double, double, double, double, double, double, Size)"/>
    public double PixelsPerInch
    {
        get => _pixelsPerInch;
        init => _pixelsPerInch = ClampScale(value);
    }

    /// <summary>
    /// The default 3D view: isometric from the south-east, centred on the origin at the scale a
    /// blank plan opens at. <see cref="FitTo"/> frames a drawing in it.
    /// </summary>
    public static Camera Isometric(Size viewport = default) => new(
        IsometricAzimuthDegrees,
        IsometricElevationDegrees,
        0,
        0,
        0,
        CanvasView.BlankSheetPixelsPerInch,
        viewport);

    /// <summary>
    /// The plan canvas as a camera: straight down, north up. <see cref="ViewTransform"/> is this
    /// camera's special case (&#xA7;7.1), and the plan keeps using that simpler type.
    /// </summary>
    public static Camera Plan(double centerX, double centerY, double pixelsPerInch, Size viewport) =>
        new(0, 90, centerX, centerY, 0, pixelsPerInch, viewport);

    /// <summary>The zoom as a percentage of life size, for the status bar.</summary>
    public double ZoomPercent => PixelsPerInch / ViewTransform.PixelsPerInchAt100Percent * 100.0;

    /// <summary>Whether the viewport has a positive area — false before the first layout pass.</summary>
    public bool HasViewport => Viewport.Width > 0 && Viewport.Height > 0;

    /// <summary>The model point at the centre of the viewport, in inches.</summary>
    public Vector3d Center => new(CenterX, CenterY, CenterZ);

    /// <summary>The world direction drawn as screen right: horizontal whatever the elevation.</summary>
    public Vector3d Right
    {
        get
        {
            (double sinAz, double cosAz) = SinCos(AzimuthDegrees);
            return new Vector3d(cosAz, sinAz, 0);
        }
    }

    /// <summary>The world direction drawn as screen up.</summary>
    public Vector3d Up
    {
        get
        {
            (double sinAz, double cosAz) = SinCos(AzimuthDegrees);
            (double sinEl, double cosEl) = SinCos(ElevationDegrees);
            return new Vector3d(-sinAz * sinEl, cosAz * sinEl, cosEl);
        }
    }

    /// <summary>The direction from the drawing towards the eye: the outward normal of the screen.</summary>
    public Vector3d TowardViewer
    {
        get
        {
            (double sinAz, double cosAz) = SinCos(AzimuthDegrees);
            (double sinEl, double cosEl) = SinCos(ElevationDegrees);
            return new Vector3d(sinAz * cosEl, -cosAz * cosEl, sinEl);
        }
    }

    /// <summary>The direction the camera looks in: away from the eye, into the drawing.</summary>
    public Vector3d ViewDirection => -TowardViewer;

    /// <summary>Where an exact model point is drawn.</summary>
    public Point Project(Point3 point) => Project(Vector3d.From(point));

    /// <summary>Where a model point, in inches, is drawn. The screen flip is the minus sign on Up.</summary>
    public Point Project(Vector3d point)
    {
        Vector3d offset = point - Center;
        double scale = PixelsPerInch;
        if (IsPerspective)
        {
            // A point at or behind the eye has no picture; callers clip first (IsInFront). The
            // clamp only keeps a stray one finite.
            double denominator = Math.Max(EyeDistance + Vector3d.Dot(offset, ViewDirection), 1e-6);
            scale *= EyeDistance / denominator;
        }

        return new Point(
            (Viewport.Width / 2.0) + (Vector3d.Dot(offset, Right) * scale),
            (Viewport.Height / 2.0) - (Vector3d.Dot(offset, Up) * scale));
    }

    /// <summary>
    /// How far, in screen pixels, one inch along a world direction moves a point: the direction's
    /// image on the screen. Zero when the direction points straight at the eye.
    /// </summary>
    public Vector ProjectDirection(Vector3d direction) => new(
        Vector3d.Dot(direction, Right) * PixelsPerInch,
        -Vector3d.Dot(direction, Up) * PixelsPerInch);

    /// <summary>
    /// How far a point lies along the view direction, in inches from the centre's plane: larger is
    /// further from the eye. What the painter sorts by.
    /// </summary>
    /// <summary>
    /// Where one inch along <paramref name="direction"/> moves a point that is at
    /// <paramref name="at"/>, in screen pixels. In perspective that depends on where the point is;
    /// in orthographic it is <see cref="ProjectDirection(Vector3d)"/> whatever <paramref name="at"/> is.
    /// </summary>
    public Vector ProjectDirection(Vector3d direction, Vector3d at) => IsPerspective
        ? Project(at + direction) - Project(at)
        : ProjectDirection(direction);

    /// <summary>
    /// The direction from a point towards the eye: <see cref="TowardViewer"/> when orthographic,
    /// and the line to <see cref="Eye"/> when not, which is what decides whether a face is turned
    /// towards the eye.
    /// </summary>
    public Vector3d TowardViewerAt(Vector3d point)
    {
        if (!IsPerspective)
        {
            return TowardViewer;
        }

        Vector3d toward = Eye - point;
        double length = Math.Sqrt(Vector3d.Dot(toward, toward));
        return length > 1e-12 ? toward / length : TowardViewer;
    }

    /// <summary>
    /// The point on the centre plane — the plane through <see cref="Center"/> perpendicular to the
    /// view — that a screen point shows. Both projections agree about it.
    /// </summary>
    public Vector3d OnCenterPlane(Point screen) => Center
        + (Right * ((screen.X - (Viewport.Width / 2.0)) / PixelsPerInch))
        - (Up * ((screen.Y - (Viewport.Height / 2.0)) / PixelsPerInch));

    public double DepthOf(Vector3d point) => Vector3d.Dot(point - Center, ViewDirection);

    /// <summary>
    /// The line of model points drawn at a screen point: an origin on the plane through the centre,
    /// and the view direction, unit length. A point at a negative distance along it is in front of
    /// that plane, which an orthographic camera allows.
    /// </summary>
    public (Vector3d Origin, Vector3d Direction) Ray(Point screen)
    {
        Vector3d onPlane = OnCenterPlane(screen);
        if (!IsPerspective)
        {
            return (onPlane, ViewDirection);
        }

        // Every ray starts at the eye and runs through the point the pixel shows on the centre plane.
        Vector3d eye = Eye;
        Vector3d through = onPlane - eye;
        return (eye, through / Math.Sqrt(Vector3d.Dot(through, through)));
    }

    /// <summary>The same view in a viewport of another size. The model point at the centre stays there.</summary>
    public Camera WithViewport(Size viewport) => this with { Viewport = viewport };

    /// <summary>
    /// The view after an orbit drag by a screen displacement. Dragging right turns the drawing the
    /// way the hand moves, as a turntable would; dragging down tips its top towards the eye. The
    /// centre and the scale do not change, so the point being looked at stays where it is.
    /// </summary>
    public Camera Orbit(Vector screenDelta) => OrbitBy(
        -screenDelta.X * DegreesPerOrbitPixel,
        screenDelta.Y * DegreesPerOrbitPixel);

    /// <summary>The view turned by a number of degrees of azimuth and of elevation.</summary>
    public Camera OrbitBy(double azimuthDegrees, double elevationDegrees) => this with
    {
        AzimuthDegrees = NormalizeAzimuth(AzimuthDegrees + azimuthDegrees),
        ElevationDegrees = ElevationDegrees + elevationDegrees,
    };

    /// <summary>
    /// The view after dragging the drawing by a screen displacement: the model point under the
    /// pointer stays under it, so the drawing follows the hand.
    /// </summary>
    public Camera Pan(Vector screenDelta)
    {
        Vector3d shift = (Right * (-screenDelta.X / PixelsPerInch)) + (Up * (screenDelta.Y / PixelsPerInch));
        Vector3d center = Center + shift;
        return this with { CenterX = center.X, CenterY = center.Y, CenterZ = center.Z };
    }

    /// <summary>
    /// The view after zooming about a screen point: the model point under that point, on the plane
    /// through the centre, is still under it afterwards.
    /// </summary>
    /// <remarks>
    /// As in <see cref="ViewTransform.ZoomAt"/>, the anchor is honoured even when the factor is
    /// clipped by the scale limits, because the centre is recomputed from the scale actually applied.
    /// </remarks>
    public Camera ZoomAt(Point anchor, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            return this;
        }

        double scale = ClampScale(PixelsPerInch * factor);
        Vector3d under = OnCenterPlane(anchor);
        Vector3d center = under
                          - (Right * ((anchor.X - (Viewport.Width / 2.0)) / scale))
                          + (Up * ((anchor.Y - (Viewport.Height / 2.0)) / scale));
        return this with { CenterX = center.X, CenterY = center.Y, CenterZ = center.Z, PixelsPerInch = scale };
    }

    /// <summary>The view after zooming about the centre of the viewport — what the +/- keys do.</summary>
    public Camera ZoomAtCenter(double factor) =>
        ZoomAt(new Point(Viewport.Width / 2.0, Viewport.Height / 2.0), factor);

    /// <summary>
    /// The view, from the same direction, that frames <paramref name="bounds"/> in
    /// <paramref name="viewport"/> with a margin all round — keeping this camera's scale and centre
    /// when there is nothing to frame.
    /// </summary>
    /// <param name="bounds">What to frame.</param>
    /// <param name="viewport">The view's size.</param>
    /// <param name="marginFraction">How much of each edge to leave empty, as a fraction of the viewport.</param>
    /// <param name="coveredRight">
    /// How many pixels at the right of the viewport something else covers — the window's side
    /// panels (#90) — so the drawing is framed in what is left.
    /// </param>
    public Camera FitTo(Bounds3 bounds, Size viewport, double marginFraction = ViewTransform.FitMarginFraction, double coveredRight = 0)
    {
        if (bounds.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return WithViewport(viewport);
        }

        Vector3d right = Right;
        Vector3d up = Up;
        double minRight = double.PositiveInfinity, maxRight = double.NegativeInfinity;
        double minUp = double.PositiveInfinity, maxUp = double.NegativeInfinity;
        foreach (Vector3d corner in bounds.CornersInInches())
        {
            double across = Vector3d.Dot(corner, right);
            double along = Vector3d.Dot(corner, up);
            minRight = Math.Min(minRight, across);
            maxRight = Math.Max(maxRight, across);
            minUp = Math.Min(minUp, along);
            maxUp = Math.Max(maxUp, along);
        }

        double covered = Math.Clamp(coveredRight, 0, viewport.Width / 2);
        double usableWidth = Math.Max((viewport.Width - covered) * (1 - (2 * marginFraction)), 1);
        double usableHeight = Math.Max(viewport.Height * (1 - (2 * marginFraction)), 1);
        double width = maxRight - minRight;
        double height = maxUp - minUp;

        // A drawing with no extent across the screen one way — one part seen edge-on — is framed by
        // the way it has one; a single point keeps the current scale.
        double byWidth = width > 1e-9 ? usableWidth / width : double.PositiveInfinity;
        double byHeight = height > 1e-9 ? usableHeight / height : double.PositiveInfinity;
        double scale = Math.Min(byWidth, byHeight);
        if (!double.IsFinite(scale))
        {
            scale = PixelsPerInch;
        }

        // The drawing's middle goes to the middle of the uncovered part: the view's own middle is
        // half the covered strip further right, in the drawing.
        Vector3d toward = TowardViewer;
        Vector3d center = (right * (((minRight + maxRight) / 2) + (covered / 2 / ClampScale(scale))))
                          + (up * ((minUp + maxUp) / 2))
                          + (toward * Vector3d.Dot(bounds.CenterInInches, toward));

        Camera fitted = this with
        {
            CenterX = center.X,
            CenterY = center.Y,
            CenterZ = center.Z,
            PixelsPerInch = scale,
            Viewport = viewport,
        };

        return IsPerspective ? fitted.RefineFit(bounds, usableWidth, usableHeight, covered) : fitted;
    }

    /// <summary>
    /// Corrects an orthographic fit for perspective: the eye moves as the scale changes, so what
    /// fits is found by iterating — project every corner, scale to what is usable, put the middle of
    /// what was drawn in the middle of what is uncovered — and backing off whenever a corner would
    /// reach the eye.
    /// </summary>
    Camera RefineFit(Bounds3 bounds, double usableWidth, double usableHeight, double covered)
    {
        Camera camera = this;
        Point target = new((Viewport.Width - covered) / 2.0, Viewport.Height / 2.0);
        List<Vector3d> corners = [.. bounds.CornersInInches()];

        for (int i = 0; i < 60; i++)
        {
            if (!corners.All(camera.IsInFront))
            {
                camera = camera with { PixelsPerInch = ClampScale(camera.PixelsPerInch * 0.8) };
                continue;
            }

            Point[] shown = [.. corners.Select(camera.Project)];
            double minX = shown.Min(p => p.X), maxX = shown.Max(p => p.X);
            double minY = shown.Min(p => p.Y), maxY = shown.Max(p => p.Y);
            Vector toTarget = new(target.X - ((minX + maxX) / 2), target.Y - ((minY + maxY) / 2));

            double width = maxX - minX;
            double height = maxY - minY;
            double byWidth = width > 1e-9 ? usableWidth / width : double.PositiveInfinity;
            double byHeight = height > 1e-9 ? usableHeight / height : double.PositiveInfinity;
            double factor = Math.Min(byWidth, byHeight);
            if (!double.IsFinite(factor))
            {
                factor = 1;
            }

            if (Math.Abs(factor - 1) < 1e-9 && Math.Abs(toTarget.X) < 1e-6 && Math.Abs(toTarget.Y) < 1e-6)
            {
                break;
            }

            camera = camera.Pan(toTarget) with { PixelsPerInch = ClampScale(camera.PixelsPerInch * factor) };
        }

        return camera;
    }

    static double Radians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// The sine and cosine of an angle in degrees, exact at every multiple of 90: cos 90° is ~6e-17 in
    /// <see langword="double"/>, and a standard view must look exactly along its axis
    /// (docs/design/standard-views.md §4.1's trig trap).
    /// </summary>
    static (double Sin, double Cos) SinCos(double degrees)
    {
        double quarters = degrees / 90.0;
        if (quarters == Math.Round(quarters))
        {
            return (((long)Math.Round(quarters) % 4 + 4) % 4) switch
            {
                0 => (0, 1),
                1 => (1, 0),
                2 => (0, -1),
                _ => (-1, 0),
            };
        }

        double radians = Radians(degrees);
        return (Math.Sin(radians), Math.Cos(radians));
    }

    static double NormalizeAzimuth(double degrees)
    {
        double wrapped = degrees % 360.0;
        return wrapped < 0 ? wrapped + 360.0 : wrapped;
    }

    static double ClampElevation(double degrees) => double.IsFinite(degrees)
        ? Math.Clamp(degrees, -90.0, 90.0)
        : IsometricElevationDegrees;

    static double ClampFieldOfView(double degrees) => double.IsFinite(degrees)
        ? Math.Clamp(degrees, MinFieldOfViewDegrees, 120.0)
        : DefaultFieldOfViewDegrees;

    static double ClampScale(double pixelsPerInch) => double.IsFinite(pixelsPerInch)
        ? Math.Clamp(pixelsPerInch, ViewTransform.MinPixelsPerInch, ViewTransform.MaxPixelsPerInch)
        : ViewTransform.PixelsPerInchAt100Percent;
}

/// <summary>How a <see cref="Camera"/> turns space into a picture.</summary>
public enum CameraProjection
{
    /// <summary>
    /// Parallel projection: a length along an axis is the same number of pixels anywhere on the
    /// screen. To measure with.
    /// </summary>
    Orthographic,

    /// <summary>
    /// Pinhole projection: things shrink with distance from the eye and parallel edges converge.
    /// To judge how it looks with.
    /// </summary>
    Perspective,
}
