using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using Design = Napkin.Modules.Editing.Design;

namespace Napkin.App;

/// <summary>
/// Project → Site plan (docs/design/permit-set.md §5, #223): the lot's courses, its point of beginning
/// and north, typed; the closure and each line's distance from the structure said beneath. Apply is one
/// undo step through the owner's editor.
/// </summary>
public partial class SitePlanWindow : Window
{
    /// <summary>Creates the window.</summary>
    public SitePlanWindow()
    {
        InitializeComponent();
        ZoningText.Text = SitePlan.ZoningNote;
    }

    /// <summary>The design the window shows.</summary>
    public Design? Design { get; private set; }

    /// <summary>How the window changes the design: one request, named for undo.</summary>
    public Action<Request, string>? ApplyRequest { get; set; }

    /// <summary>The Site layer's id, and the request that adds it when the design has none.</summary>
    public Func<(LayerId Layer, Request? Add)>? SiteLayer { get; set; }

    /// <summary>Brings an image in under the site plan (the editor's AddUnderlay): the underlay it made, or null when it is not a PNG or JPEG.</summary>
    public Func<byte[], string, SurveyUnderlay?>? AddUnderlay { get; set; }

    /// <summary>Asks the plan for two calibration clicks.</summary>
    public Action<Action<Point2, Point2>>? ArmCalibration { get; set; }

    /// <summary>The underlay's controls, for the GUI suite.</summary>
    public (TextBox Distance, Button Calibrate) UnderlayFields => (CalibrationDistanceBox, CalibrateButton);

    /// <summary>What the window says about the survey image.</summary>
    public string UnderlayLine => UnderlayText.Text ?? string.Empty;

    /// <summary>The fields, for the GUI suite.</summary>
    public (TextBox North, TextBox StartX, TextBox StartY, TextBox Courses, Button Apply) Fields => (NorthBox, StartXBox, StartYBox, CoursesBox, ApplyButton);

    /// <summary>The distances as shown.</summary>
    public string Readout => ReadoutText.Text ?? string.Empty;

    /// <summary>What is wrong with what was typed, or empty.</summary>
    public string Problem => ProblemText.IsVisible ? ProblemText.Text ?? string.Empty : string.Empty;

    /// <summary>Shows a design: its boundary's courses, north and point of beginning, and the distances.</summary>
    public void ShowDesign(Design? design)
    {
        Design = design;
        Sketch sketch = design?.Sketch ?? Sketch.Empty;
        Title = design is null ? "Site plan" : $"Site plan — {design.Name}";
        DesignHeadline.Text = design is null ? "No design is open." : $"The lot {design.Name} stands on";
        Boundary? boundary = sketch.Entities.Values.OfType<Boundary>().OrderBy(b => b.Id).FirstOrDefault();
        LengthFormat feet = new FeetInchesFormat(16);
        NorthBox.Text = CourseText.NorthWords(sketch.Site.North);
        StartXBox.Text = (boundary?.Start.X ?? Length.Zero).Format(feet).Text;
        StartYBox.Text = (boundary?.Start.Y ?? Length.Zero).Format(feet).Text;
        CoursesBox.Text = boundary is null ? string.Empty : CourseText.Of(boundary.Courses);
        UnderlayText.Text = sketch.Site.Underlay is { } underlay
            ? $"Survey underlay: {underlay.Name}, calibrated to {underlay.Distance.Format(feet).Text} between two points. This site plan is not a survey."
            : "No survey image.";
        ShowReadout(sketch);
    }

    void ShowReadout(Sketch sketch)
    {
        ReadoutText.Text = SitePlan.Of(sketch) is { } measure
            ? string.Join("\n", new[] { measure.ClosureText }.Concat(measure.Lines.Select(line => line.Text)))
            : "No property lines yet.";
    }

    void Complain(string problem)
    {
        ProblemText.Text = problem;
        ProblemText.IsVisible = true;
    }

    void OnApplyClicked(object? sender, RoutedEventArgs e) => Apply();

    /// <summary>
    /// Puts an image under the site plan. The dialog's handler calls this, and so does the GUI suite,
    /// which cannot drive the platform's file picker; everything after the bytes are read is the same.
    /// </summary>
    public bool SetUnderlay(byte[] bytes, string name)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.LongLength > Napkin.Core.Project.ContainerLimits.MaxAssetBytes)
        {
            Complain($"{name} is larger than 8 MB, which is as large an image as a napkin project carries. Nothing was changed.");
            return false;
        }

        if (AddUnderlay?.Invoke(bytes, name) is null)
        {
            Complain($"{name} is not a PNG or JPEG napkin can read. Nothing was changed.");
            return false;
        }

        ProblemText.IsVisible = false;
        return true;
    }

    async void OnChooseImageClicked(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }

        IReadOnlyList<Avalonia.Platform.Storage.IStorageFile> chosen = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Choose the survey image",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }],
        }).ConfigureAwait(true);
        if (chosen.Count == 0 || chosen[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            SetUnderlay(await System.IO.File.ReadAllBytesAsync(path).ConfigureAwait(true), System.IO.Path.GetFileName(path));
        }
        catch (Exception exception) when (Napkin.Core.Project.ProjectFile.IsFileException(exception))
        {
            Complain($"{System.IO.Path.GetFileName(path)} could not be read: {exception.Message}");
        }
    }

    void OnCalibrateClicked(object? sender, RoutedEventArgs e) => Calibrate();

    /// <summary>Reads the typed distance and asks the plan for the two points; the calibration is applied on the second click, one undo step.</summary>
    public bool Calibrate()
    {
        if (Design?.Sketch.Site.Underlay is null || ApplyRequest is null || ArmCalibration is null)
        {
            Complain("Choose the survey image first.");
            return false;
        }

        if (!Length.TryParse(CalibrationDistanceBox.Text, out Length distance, out _) || distance <= Length.Zero)
        {
            Complain("The calibration distance is a length longer than zero, like 100'. Nothing was changed.");
            return false;
        }

        ProblemText.IsVisible = false;
        ArmCalibration((first, second) =>
        {
            if (Design?.Sketch.Site.Underlay is not { } underlay || Underlays.Calibrate(underlay, first, second, distance) is not { } calibrated)
            {
                Complain("The two points are the same point: click two different ones. Nothing was changed.");
                return;
            }

            ApplyRequest(new SetSite(Design.Sketch.Site with { Underlay = calibrated }), "Calibrated the survey image");
        });
        return true;
    }

    /// <summary>Reads the fields and sets the lot and north: one undo step. A field that does not read is said, and nothing changes.</summary>
    public bool Apply()
    {
        if (Design is null || ApplyRequest is null || SiteLayer is null)
        {
            return false;
        }

        if (!CourseText.TryParseNorth(NorthBox.Text, out Angle north))
        {
            Complain("North is degrees clockwise from the drawing's up, from 0 to under 360, like 12°30'. Nothing was changed.");
            return false;
        }

        if (!Length.TryParse(StartXBox.Text, out Length x, out _) || !Length.TryParse(StartYBox.Text, out Length y, out _))
        {
            Complain("The point of beginning is two lengths, like 0 and -20'. Nothing was changed.");
            return false;
        }

        if (!CourseText.TryParse(CoursesBox.Text, out ImmutableArray<Course> courses, out string problem))
        {
            Complain($"{problem} Nothing was changed.");
            return false;
        }

        if (BoundaryRules.Refusal(courses) is { } why)
        {
            Complain($"The courses do not make a lot: {why}. Nothing was changed.");
            return false;
        }

        ProblemText.IsVisible = false;
        Sketch sketch = Design.Sketch;
        List<Request> requests = [];
        if (sketch.Site.North != north)
        {
            requests.Add(new SetSite(sketch.Site with { North = north }));
        }

        Point2 start = new(x, y);
        if (sketch.Entities.Values.OfType<Boundary>().OrderBy(b => b.Id).FirstOrDefault() is { } boundary)
        {
            if (boundary.Start != start || !boundary.Courses.SequenceEqual(courses))
            {
                requests.Add(new SetBoundary(boundary.Id, start, courses));
            }
        }
        else
        {
            (LayerId layer, Request? add) = SiteLayer();
            if (add is not null)
            {
                requests.Add(add);
            }

            requests.Add(new AddEntity(new Boundary(EntityId.New(), layer, start, courses) { Name = "Lot", Phase = Phase.Existing }));
        }

        if (requests.Count == 0)
        {
            return false;
        }

        ApplyRequest(requests.Count == 1 ? requests[0] : Batch.Of([.. requests]), "Set the site plan");
        return true;
    }
}
