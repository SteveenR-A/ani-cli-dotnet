using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AniCS.Desktop.Controls;

public class RadialMenuOption
{
    public string Text { get; set; } = "";
    public bool? IsSupported { get; set; } = null;
}

public partial class HudRadialMenuDialog : Window
{
    private List<RadialMenuOption> _options = new();
    private int _selectedIndex = -1;
    private double _cx = 250;
    private double _cy = 250;
    private double _r1 = 60;
    private double _r2 = 200;
    
    private IBrush _normalBrush = new SolidColorBrush(Color.Parse("#333333"));
    private IBrush _hoverBrush = new SolidColorBrush(Color.Parse("#555555"));
    private IBrush _strokeBrush = new SolidColorBrush(Color.Parse("#111111"));
    private IBrush _textBrush = Brushes.White;

    public HudRadialMenuDialog()
    {
        InitializeComponent();
    }

    public static async Task<int> ShowAsync(Window owner, List<RadialMenuOption> options, string helperText = "")
    {
        var dialog = new HudRadialMenuDialog();
        dialog._options = options;
        dialog.RequestedThemeVariant = owner.RequestedThemeVariant;
        
        dialog.Loaded += dialog.OnLoaded;

        return await dialog.ShowDialog<int>(owner);
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Try to fetch theme colors
        if (Application.Current?.Resources.TryGetResource("AppSurfaceColor", Application.Current.ActualThemeVariant, out var surfaceObj) == true && surfaceObj is IBrush surfaceBrush)
            _normalBrush = surfaceBrush;
            
        if (Application.Current?.Resources.TryGetResource("AppPrimaryColor", Application.Current.ActualThemeVariant, out var primaryObj) == true && primaryObj is IBrush primaryBrush)
            _hoverBrush = primaryBrush;
            
        if (Application.Current?.Resources.TryGetResource("AppTitleColor", Application.Current.ActualThemeVariant, out var textObj) == true && textObj is IBrush titleBrush)
            _textBrush = titleBrush;

        BuildRadialMenu();
    }

    private void BuildRadialMenu()
    {
        MenuCanvas.Children.Clear();
        int n = _options.Count;
        if (n == 0) return;

        double sweep = 360.0 / n;
        // Start from top (-90 degrees)
        double startOffset = -90.0 - (sweep / 2.0); 

        for (int i = 0; i < n; i++)
        {
            double startAngle = startOffset + (i * sweep);
            var geo = CreateWedgeGeometry(_cx, _cy, _r1, _r2, startAngle, sweep);
            
            // Calculate center points for text and dots
            double midAngleDeg = startAngle + (sweep / 2.0);
            double midAngleRad = midAngleDeg * Math.PI / 180.0;
            double rMid = (_r1 + _r2) / 2.0;
            
            double tx = _cx + rMid * Math.Cos(midAngleRad);
            double ty = _cy + rMid * Math.Sin(midAngleRad);
            
            var textBlock = new TextBlock
            {
                Text = _options[i].Text,
                Foreground = _textBrush,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false // Let pointer events pass to Path
            };

            var path = new Path
            {
                Data = geo,
                Fill = _normalBrush,
                Stroke = _strokeBrush,
                StrokeThickness = 2,
                Tag = i,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            
            // Hover events
            path.PointerEntered += (s, e) => { 
                if (s is Path p) p.Fill = _hoverBrush; 
                textBlock.Foreground = _normalBrush; // Invert color for contrast
            };
            path.PointerExited += (s, e) => { 
                if (s is Path p) p.Fill = _normalBrush; 
                textBlock.Foreground = _textBrush;
            };
            
            // Click event
            path.PointerPressed += (s, e) => 
            {
                if (s is Path p && p.Tag is int idx)
                {
                    _selectedIndex = idx;
                    Close(_selectedIndex);
                }
            };
            
            MenuCanvas.Children.Add(path);

            if (_options[i].IsSupported.HasValue)
            {
                double rDot = _r2 - 14; // Position dot at the outer tip of the slice
                double dotTx = _cx + rDot * Math.Cos(midAngleRad);
                double dotTy = _cy + rDot * Math.Sin(midAngleRad);
                
                var dot = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(Color.Parse(_options[i].IsSupported == true ? "#22C55E" : "#F59E0B")),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(dot, dotTx - 6);
                Canvas.SetTop(dot, dotTy - 6);
                MenuCanvas.Children.Add(dot);
            }
            
            // Text rotation
            double textAngle = 0;
            if (n > 2)
            {
                textAngle = midAngleDeg % 360;
                if (textAngle < 0) textAngle += 360;
                if (textAngle > 90 && textAngle < 270)
                {
                    textAngle -= 180;
                }
            }
            
            textBlock.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            textBlock.RenderTransform = new RotateTransform(textAngle);
            
            // Measure text to center it properly
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(textBlock, tx - (textBlock.DesiredSize.Width / 2.0));
            Canvas.SetTop(textBlock, ty - (textBlock.DesiredSize.Height / 2.0));
            
            MenuCanvas.Children.Add(textBlock);
        }

        // Add Center Cancel Button
        var centerBorder = new Border
        {
            Width = _r1 * 2 - 10,
            Height = _r1 * 2 - 10,
            CornerRadius = new CornerRadius(_r1),
            Background = _normalBrush,
            BorderBrush = _strokeBrush,
            BorderThickness = new Thickness(2),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = "X",
                Foreground = _textBrush,
                FontWeight = FontWeight.Bold,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        
        Canvas.SetLeft(centerBorder, _cx - (_r1 * 2 - 10) / 2.0);
        Canvas.SetTop(centerBorder, _cy - (_r1 * 2 - 10) / 2.0);
        
        centerBorder.PointerEntered += (s, e) => centerBorder.Background = new SolidColorBrush(Colors.DarkRed);
        centerBorder.PointerExited += (s, e) => centerBorder.Background = _normalBrush;
        centerBorder.PointerPressed += (s, e) => Close(-1); // -1 means cancel
        
        MenuCanvas.Children.Add(centerBorder);
    }

    private PathGeometry CreateWedgeGeometry(double cx, double cy, double r1, double r2, double startAngleDeg, double sweepAngleDeg)
    {
        var geo = new PathGeometry { Figures = new PathFigures() };
        var fig = new PathFigure { IsClosed = true, Segments = new PathSegments() };
        
        double startRad = startAngleDeg * Math.PI / 180.0;
        double endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180.0;
        
        var p1 = new Point(cx + r1 * Math.Cos(startRad), cy + r1 * Math.Sin(startRad));
        var p2 = new Point(cx + r2 * Math.Cos(startRad), cy + r2 * Math.Sin(startRad));
        var p3 = new Point(cx + r2 * Math.Cos(endRad), cy + r2 * Math.Sin(endRad));
        var p4 = new Point(cx + r1 * Math.Cos(endRad), cy + r1 * Math.Sin(endRad));
        
        fig.StartPoint = p1;
        
        var seg1 = new LineSegment { Point = p2 };
        fig.Segments.Add(seg1);
        
        bool isLargeArc = sweepAngleDeg > 180.0;
        
        var seg2 = new ArcSegment 
        { 
            Point = p3, 
            Size = new Size(r2, r2), 
            SweepDirection = SweepDirection.Clockwise, 
            IsLargeArc = isLargeArc 
        };
        fig.Segments.Add(seg2);
        
        var seg3 = new LineSegment { Point = p4 };
        fig.Segments.Add(seg3);
        
        var seg4 = new ArcSegment 
        { 
            Point = p1, 
            Size = new Size(r1, r1), 
            SweepDirection = SweepDirection.CounterClockwise, 
            IsLargeArc = isLargeArc 
        };
        fig.Segments.Add(seg4);
        
        geo.Figures.Add(fig);
        return geo;
    }
}
