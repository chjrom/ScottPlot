﻿using System.ComponentModel.Design;

namespace ScottPlot.Plottable;

/// <summary>
/// This class represents a single radial gauge.
/// It has level and styling options and can be rendered onto an existing bitmap using any radius.
/// </summary>
internal class RadialGauge
{
    /// <summary>
    /// Location of the base of the gauge (degrees)
    /// </summary>
    public double StartAngle { get; set; }

    /// <summary>
    /// Current level of this gauge (degrees)
    /// </summary>
    public double SweepAngle { get; set; }

    /// <summary>
    /// Maximum angular size of the gauge (swept degrees)
    /// </summary>
    public double MaximumSizeAngle { get; set; }

    /// <summary>
    /// Angle where the background starts (degrees)
    /// </summary>
    public double BackStartAngle { get; set; }

    /// <summary>
    /// If true angles end clockwise relative to their base
    /// </summary>
    public bool Clockwise { get; set; }

    /// <summary>
    /// Used internally to get the angle swept by the gauge background. It's equal to 360 degrees if CircularBackground is set to true. Also, returns a positive value is the gauge is drawn clockwise and a negative one otherwise
    /// </summary>
    internal double BackAngleSweep
    {
        get
        {
            double maxBackAngle = CircularBackground ? 360 : MaximumSizeAngle;
            if (!Clockwise) maxBackAngle = -maxBackAngle;
            return maxBackAngle;
        }
        private set { BackAngleSweep = value; } // Added for the sweepAngle check in DrawArc due to System.Drawing throwing an OutOfMemoryException.
    }

    /// <summary>
    /// If true the background will always be drawn as a complete circle regardless of MaximumSizeAngle
    /// </summary>
    public bool CircularBackground { get; set; } = true;

    /// <summary>
    /// Font used to render values at the tip of the gauge
    /// </summary>
    public FontStyle Font { get; set; } = new();

    /// <summary>
    /// Size of the font relative to the line thickness
    /// </summary>
    public double FontSizeFraction { get; set; }

    /// <summary>
    /// Text to display on top of the label
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Location of the label text along the length of the gauge.
    /// Low values place the label near the base and high values place the label at its tip.
    /// </summary>
    public double LabelPositionFraction { get; set; }

    /// <summary>
    /// Size of the gauge (pixels)
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Color of the gauge foreground
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// Color of the gauge background
    /// </summary>
    public Color BackgroundColor { get; set; }

    /// <summary>
    /// Style of the base of the gauge
    /// </summary>
    public SkiaSharp.SKStrokeCap StartCap { get; set; } = SKStrokeCap.Round;

    /// <summary>
    /// Style of the tip of the gauge
    /// </summary>
    public SkiaSharp.SKStrokeCap EndCap { get; set; } = SKStrokeCap.Round;

    /// <summary>
    /// Defines the location of each gauge relative to the start angle and distance from the center
    /// </summary>
    public RadialGaugeMode Mode { get; set; }

    /// <summary>
    /// Indicates whether or not labels will be rendered as text
    /// </summary>
    public bool ShowLabels { get; set; }

    /// <summary>
    /// Render the gauge onto an existing Bitmap
    /// </summary>
    /// <param name="gfx">active graphics object</param>
    public void Render(RenderPack rp, float radius)
    {
        RenderBackground(rp, radius);
        RenderGaugeForeground(rp, radius);
        RenderGaugeLabels(rp, radius);
    }

    private void RenderBackground(RenderPack rp, float radius)
    {
        if (Mode == RadialGaugeMode.SingleGauge)
            return;

        // See some examples here: https://learn.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/curves/arcs
        using SKPaint skPaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)Width,
            StrokeCap = StartCap,
            Color = new(BackgroundColor.ARGB)
        };

        using SKPath skPath = new();
        skPath.AddArc(new(rp.FigureRect.BottomCenter.X - radius, rp.FigureRect.LeftCenter.Y - radius, rp.FigureRect.BottomCenter.X + radius, rp.FigureRect.LeftCenter.Y + radius),
            (float)BackStartAngle,
            (float)BackAngleSweep);

        rp.Canvas.DrawPath(skPath, skPaint);
    }

    public void RenderGaugeForeground(RenderPack rp, float radius)
    {

        // This check is specific to System.Drawing since DrawArc throws an OutOfMemoryException when the sweepAngle is very small.
        if (Math.Abs(SweepAngle) <= 0.01)
            SweepAngle = 0;

        using SKPaint skPaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)Width,
            StrokeCap = SKStrokeCap.Butt,
            Color = new(Color.ARGB)
        };

        using SKPath skPath = new();
        skPath.AddArc(new(rp.FigureRect.BottomCenter.X - radius, rp.FigureRect.LeftCenter.Y - radius, rp.FigureRect.BottomCenter.X + radius, rp.FigureRect.LeftCenter.Y + radius),
            (float)StartAngle,
            (float)SweepAngle);

        rp.Canvas.DrawPath(skPath, skPaint);
    }

    private const double DEG_PER_RAD = 180.0 / Math.PI;

    private void RenderGaugeLabels(RenderPack rp, float radius)
    {
        if (!ShowLabels || Label == string.Empty)
            return;

        using SKPaint skPaint = new()
        {
            TextSize = (float)Width * (float)FontSizeFraction,
            IsAntialias = true,
            SubpixelText = true,
            Color = new(Font.Color.ARGB),
            Typeface = Font.Typeface
        };

        // Text is measured (in linear form) and converted to angular dimensions
        SKRect textBounds = new();
        skPaint.MeasureText($"{Label}.", ref textBounds);
        double textWidthFrac = textBounds.Width / radius;
        double textAngle = (1 - 2 * LabelPositionFraction) * DEG_PER_RAD * textWidthFrac;

        // This is a hack since sometimes when text is at the very beginning or at the very end of the gauge, some small clipping occurs.
        SKRect spaceBounds = new();
        skPaint.MeasureText(".", ref spaceBounds);
        double spaceAngle = (1 - 2 * LabelPositionFraction) * DEG_PER_RAD * spaceBounds.Width / radius;

        // This is done in order to draw the text in the correct orientation. The trick consists in modifying the sweep angle with either the original value or the 360-complimentary value when needed. Other options would be to flip the canvas, draw the text and restore the canvas.
        double angle = ReduceAngle(StartAngle + SweepAngle * LabelPositionFraction);
        bool isBelow = angle <= 180 && angle > 0; 
        double sweepAngle;
        if (SweepAngle > 0)
            sweepAngle = isBelow ? -(360 - SweepAngle - textAngle) : SweepAngle;
        else
            sweepAngle = isBelow ? SweepAngle : 360 + SweepAngle - textAngle;

        // This is part of the above hack to adjust text clipping at the gauges's very end
        float textLinearWidth = SweepAngle > 0 == isBelow ? textBounds.Width - spaceBounds.Width: textBounds.Width;

        using SKPath skPath = new();
        skPath.AddArc(new(rp.FigureRect.BottomCenter.X - radius, rp.FigureRect.LeftCenter.Y - radius, rp.FigureRect.BottomCenter.X + radius, rp.FigureRect.LeftCenter.Y + radius),
            (float)StartAngle,
            (float)sweepAngle);

        using SKPathMeasure skMeasure = new(skPath);

        SKPoint skPoint = new()
        {
            Y = -(float)textBounds.MidY,    // Displacement along the y axis (radial-wise), so we can center the text on the gauge path
            X = (float)(LabelPositionFraction / 2) * (skMeasure.Length - textLinearWidth)  // Displacement along the x axis (the length of the path), so that we can set the text at any position along the path
        };

        rp.Canvas.DrawTextOnPath(Label, skPath, skPoint, skPaint);
    }

    /// <summary>
    /// Reduces an angle into the range [0°-360°].
    /// Angles greater than 360 will roll-over (370º becomes 10º).
    /// Angles less than 0 will roll-under (-10º becomes 350º).
    /// </summary>
    /// <param name="angle">Angle value</param>
    /// <returns>Angle whithin [0°-360°]</returns>
    public static double ReduceAngle(double angle)
    {
        angle %= 360;

        if (angle < 0)
            angle += 360;

        return angle;
    }
}
