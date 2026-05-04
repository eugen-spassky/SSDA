using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SSDA.App.Controls;

/// <summary>
/// Round progress indicator. <see cref="Progress"/> is in <c>[0, 1]</c> and represents the
/// remaining fraction of the current 30-second TOTP window. The ring is drawn as a single
/// arc that shrinks counter-clockwise from a full circle.
/// </summary>
public partial class RingTimer : UserControl
{
    /// <summary>Backing dependency property for <see cref="Progress"/>.</summary>
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(RingTimer),
        new PropertyMetadata(1.0, OnProgressChanged));

    /// <summary>Backing dependency property for <see cref="Seconds"/>.</summary>
    public static readonly DependencyProperty SecondsProperty = DependencyProperty.Register(
        nameof(Seconds),
        typeof(int),
        typeof(RingTimer),
        new PropertyMetadata(30, OnSecondsChanged));

    /// <summary>0..1 fraction representing how much of the window remains.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Whole-second readout shown inside the ring.</summary>
    public int Seconds
    {
        get => (int)GetValue(SecondsProperty);
        set => SetValue(SecondsProperty, value);
    }

    public RingTimer()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RingTimer)d).Render();

    private static void OnSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RingTimer)d).SecondsText.Text = ((int)e.NewValue).ToString();

    private void Render()
    {
        var progress = Math.Clamp(Progress, 0.0, 1.0);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var inset = 6.0;
        var radius = (Math.Min(w, h) - inset * 2) / 2;
        var cx = w / 2;
        var cy = h / 2;

        // Use 0.9999 (instead of 1) so the arc never collapses into a degenerate full circle.
        var sweepFraction = Math.Clamp(progress, 0.0001, 0.9999);
        var angle = sweepFraction * 360.0;

        // Start at the top (12 o'clock), sweep clockwise.
        var startRad = -Math.PI / 2;
        var endRad = startRad + angle * Math.PI / 180.0;
        var start = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
        var end = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));

        var isLargeArc = angle > 180;
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(
            point: end,
            size: new Size(radius, radius),
            rotationAngle: 0,
            isLargeArc: isLargeArc,
            sweepDirection: SweepDirection.Clockwise,
            isStroked: true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ArcPath.Data = geometry;
    }
}
