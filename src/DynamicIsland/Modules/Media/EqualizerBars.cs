using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DynamicIsland.Modules.Media;

/// <summary>
/// Vài vạch sóng nhạc nhảy lên xuống khi đang phát, đứng yên thấp khi tạm dừng.
/// Thay cho tên bài/ảnh bìa để không lộ nội dung đang nghe.
/// </summary>
public sealed class EqualizerBars : StackPanel
{
    private static readonly double[] Periods = { 0.55, 0.8, 0.45, 0.7, 0.6, 0.9, 0.5 };

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(EqualizerBars),
        new PropertyMetadata(false, (d, _) => ((EqualizerBars)d).UpdateAnimation()));

    public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register(
        nameof(BarCount), typeof(int), typeof(EqualizerBars),
        new PropertyMetadata(4, (d, _) => ((EqualizerBars)d).BuildBars()));

    public static readonly DependencyProperty BarWidthProperty = DependencyProperty.Register(
        nameof(BarWidth), typeof(double), typeof(EqualizerBars),
        new PropertyMetadata(3.0, (d, _) => ((EqualizerBars)d).BuildBars()));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(EqualizerBars),
        new PropertyMetadata(Brushes.White, (d, _) => ((EqualizerBars)d).BuildBars()));

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public int BarCount { get => (int)GetValue(BarCountProperty); set => SetValue(BarCountProperty, value); }
    public double BarWidth { get => (double)GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }
    public Brush BarBrush { get => (Brush)GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }

    public EqualizerBars()
    {
        Orientation = Orientation.Horizontal;
        Loaded += (_, _) => BuildBars();
    }

    private void BuildBars()
    {
        Children.Clear();
        for (var i = 0; i < BarCount; i++)
        {
            Children.Add(new Rectangle
            {
                Width = BarWidth,
                RadiusX = BarWidth / 2,
                RadiusY = BarWidth / 2,
                Fill = BarBrush,
                Margin = new Thickness(i == 0 ? 0 : BarWidth * 0.8, 0, 0, 0),
                RenderTransformOrigin = new Point(0.5, 1),
                RenderTransform = new ScaleTransform(1, 0.3),
            });
        }
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not Rectangle { RenderTransform: ScaleTransform scale }) continue;
            if (IsActive)
            {
                var animation = new DoubleAnimation(0.25, 1, TimeSpan.FromSeconds(Periods[i % Periods.Length]))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    BeginTime = TimeSpan.FromSeconds(i * 0.12),
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
            else
            {
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleY = 0.3;
            }
        }
    }
}
