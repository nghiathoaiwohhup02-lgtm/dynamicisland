using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace DynamicIsland.Shell;

public partial class IslandWindow : Window
{
    private static readonly Duration Morph = new(TimeSpan.FromMilliseconds(380));
    private static readonly Duration Fade = new(TimeSpan.FromMilliseconds(180));

    private readonly IslandViewModel _vm;
    private FullscreenWatcher? _fullscreen;
    private bool _hiddenByUser;
    private bool _hiddenByFullscreen;

    public IslandWindow(IslandViewModel vm, bool hideWhenFullscreen)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            // Không hiện trong Alt+Tab và không giành focus khi bấm (đang gõ trong Revit vẫn giữ nguyên).
            var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));

            if (hideWhenFullscreen)
            {
                _fullscreen = new FullscreenWatcher(hwnd);
                _fullscreen.FullscreenChanged += (_, isFullscreen) =>
                {
                    _hiddenByFullscreen = isFullscreen;
                    UpdateVisibility();
                };
                _fullscreen.Start();
            }
        };

        Loaded += (_, _) =>
        {
            PlaceOnScreen();
            ApplyState(animate: false);
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void ToggleUserVisibility()
    {
        _hiddenByUser = !_hiddenByUser;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_hiddenByUser || _hiddenByFullscreen) Hide();
        else Show();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(PlaceOnScreen);

    // Giữa mép trên màn hình chính.
    private void PlaceOnScreen()
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
    }

    private void Island_MouseEnter(object sender, MouseEventArgs e) => _vm.PointerEntered();

    private void Island_MouseLeave(object sender, MouseEventArgs e) => _vm.PointerLeft();

    private void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Bấm vào đảo khi chưa mở thì mở ngay, không cần chờ hover.
        if (_vm.State != IslandState.Expanded) _vm.ToggleExpanded();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IslandViewModel.State) or nameof(IslandViewModel.LiveModule))
            ApplyState(animate: true);
    }

    private void ApplyState(bool animate)
    {
        var (width, height, radius) = _vm.State switch
        {
            IslandState.Expanded => (560.0, 250.0, 28.0),
            IslandState.Peek => (400.0, 62.0, 31.0),
            _ when _vm.LiveModule is not null => (330.0, 36.0, 18.0),
            _ => (190.0, 34.0, 17.0),
        };

        Island.CornerRadius = new CornerRadius(radius);
        Animate(Island, WidthProperty, width, animate);
        Animate(Island, HeightProperty, height, animate);

        ShowLayer(CompactLayer, _vm.State == IslandState.Collapsed, animate);
        ShowLayer(PeekLayer, _vm.State == IslandState.Peek, animate);
        ShowLayer(ExpandedLayer, _vm.State == IslandState.Expanded, animate);
    }

    private static void Animate(UIElement target, DependencyProperty property, double to, bool animate)
    {
        if (!animate)
        {
            target.BeginAnimation(property, null);
            target.SetValue(property, to);
            return;
        }

        target.BeginAnimation(property, new DoubleAnimation(to, Morph)
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 },
        });
    }

    private static void ShowLayer(FrameworkElement layer, bool visible, bool animate)
    {
        if (visible) layer.Visibility = Visibility.Visible;

        if (!animate)
        {
            layer.BeginAnimation(OpacityProperty, null);
            layer.Opacity = visible ? 1 : 0;
            if (!visible) layer.Visibility = Visibility.Collapsed;
            return;
        }

        var fade = new DoubleAnimation(visible ? 1 : 0, Fade)
        {
            // Hiện lớp mới sau khi khối đen đã kịp nở ra một chút.
            BeginTime = visible ? TimeSpan.FromMilliseconds(120) : TimeSpan.Zero,
        };
        if (!visible)
            fade.Completed += (_, _) =>
            {
                if (layer.Opacity == 0) layer.Visibility = Visibility.Collapsed;
            };
        layer.BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _fullscreen?.Dispose();
        base.OnClosed(e);
    }
}
