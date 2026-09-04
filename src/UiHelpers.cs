using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowMemory
{
    public static class Ui
    {
        public static TextBlock Text(string text, double size, Brush brush, FontWeight weight)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = brush,
                FontWeight = weight,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextBlock Heading(string text, double size)
        {
            return Text(text, size, Brush("TextBrush"), FontWeights.SemiBold);
        }

        public static TextBlock Muted(string text, double size)
        {
            return Text(text, size, Brush("MutedTextBrush"), FontWeights.Normal);
        }

        public static TextBlock Label(string text)
        {
            TextBlock label = Text(text, 12, Brush("MutedTextBrush"), FontWeights.SemiBold);
            label.Margin = new Thickness(0, 0, 0, 7);
            return label;
        }

        public static Brush Brush(string key)
        {
            object value = Application.Current.TryFindResource(key);
            return value as Brush ?? Brushes.Transparent;
        }

        public static Border Card(UIElement child, Thickness margin)
        {
            Border card = new Border { Child = child, Margin = margin };
            card.SetResourceReference(FrameworkElement.StyleProperty, "Card");
            return card;
        }

        public static Button Button(string content, RoutedEventHandler click, string styleKey)
        {
            Button button = new Button { Content = content };
            if (!string.IsNullOrWhiteSpace(styleKey)) button.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
            if (click != null) button.Click += click;
            return button;
        }

        public static void SetRow(FrameworkElement element, int row)
        {
            Grid.SetRow(element, row);
        }

        public static void SetColumn(FrameworkElement element, int column)
        {
            Grid.SetColumn(element, column);
        }

        public static Grid TwoColumn(double leftWidth, double gap)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(leftWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return grid;
        }
    }

    public sealed class PlacementPreview : FrameworkElement
    {
        public SavedPlacement Placement { get; set; }

        public PlacementPreview()
        {
            MinHeight = 130;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double width = Math.Max(1, ActualWidth);
            double height = Math.Max(1, ActualHeight);
            Rect outer = new Rect(1, 1, width - 2, height - 2);
            drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(14, 23, 40)),
                new Pen(new SolidColorBrush(Color.FromRgb(38, 54, 80)), 1), outer, 10, 10);

            SavedPlacement p = Placement;
            if (p == null || p.WorkWidth <= 0 || p.WorkHeight <= 0) return;
            double pad = 12;
            double sx = (width - pad * 2) / p.WorkWidth;
            double sy = (height - pad * 2) / p.WorkHeight;
            double scale = Math.Min(sx, sy);
            double screenWidth = p.WorkWidth * scale;
            double screenHeight = p.WorkHeight * scale;
            double ox = (width - screenWidth) / 2;
            double oy = (height - screenHeight) / 2;

            Rect screen = new Rect(ox, oy, screenWidth, screenHeight);
            drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(18, 29, 49)),
                new Pen(new SolidColorBrush(Color.FromRgb(65, 84, 112)), 1), screen, 6, 6);

            double rx = p.ScaleWithMonitor ? p.RelativeX : (double)(p.X - p.WorkX) / p.WorkWidth;
            double ry = p.ScaleWithMonitor ? p.RelativeY : (double)(p.Y - p.WorkY) / p.WorkHeight;
            double rw = p.ScaleWithMonitor ? p.RelativeWidth : (double)p.Width / p.WorkWidth;
            double rh = p.ScaleWithMonitor ? p.RelativeHeight : (double)p.Height / p.WorkHeight;
            Rect window = new Rect(ox + rx * screenWidth, oy + ry * screenHeight,
                Math.Max(8, rw * screenWidth), Math.Max(8, rh * screenHeight));
            drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(185, 21, 185, 129)),
                new Pen(new SolidColorBrush(Color.FromRgb(89, 227, 180)), 1.5), window, 5, 5);
        }
    }

    public sealed class ModeOption
    {
        public TitleMatchMode Value { get; private set; }
        public string Label { get; private set; }
        public ModeOption(TitleMatchMode value, string label) { Value = value; Label = label; }
        public override string ToString() { return Label; }
    }
}
