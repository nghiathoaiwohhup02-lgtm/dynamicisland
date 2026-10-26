using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DynamicIsland.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DynamicIsland.Shell;

public partial class IslandWindow : Window
{
    private static readonly Duration Morph = new(TimeSpan.FromMilliseconds(380));
    private static readonly Duration Fade = new(TimeSpan.FromMilliseconds(180));

    private const double CollapsedWidth = 200;
    private const double CollapsedHeight = 30;
    private const double DefaultTopMargin = 6;
    private const int DragThresholdPx = 4;

    private readonly IslandViewModel _vm;
    private readonly SettingsStore _settings;
    private readonly DispatcherTimer _pressTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    // Taskbar cũng là cửa sổ "luôn ở trên": bấm vào taskbar (hay đổi cửa sổ) là nó đè lên đảo.
    // Bắt sự kiện đổi cửa sổ phía trước để đưa đảo lên lại ngay; bộ đếm giờ chỉ để dự phòng.
    private readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly NativeMethods.WinEventProc _foregroundChanged;
    private IntPtr _foregroundHook;
    private FullscreenWatcher? _fullscreen;
    private bool _hiddenByUser;
    private bool _hiddenByFullscreen;

    // Vị trí đảo lúc thu gọn (tâm ngang, mép trên) và màn hình đang chứa đảo, đơn vị DIP.
    private double _centerX;
    private double _islandTop;
    private Rect _monitor;

    // Đảo ở nửa dưới màn hình (vd. trên taskbar) thì giữ mép dưới, bảng bung lên trên.
    private bool _growUp;

    private System.Drawing.Point _pressCursor;
    private double _pressCenterX;
    private double _pressTop;
    private bool _dragging;
    private bool _keyboardMode;

    public IslandWindow(IslandViewModel vm, SettingsStore settings)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _settings = settings;
        _vm.PropertyChanged += OnViewModelChanged;
        _pressTimer.Tick += OnPressTick;
        _topmostTimer.Tick += (_, _) => BringToFront();
        _topmostTimer.Start();
        // Giữ tham chiếu tới delegate, nếu không GC thu hồi sẽ làm sập ứng dụng khi Windows gọi lại.
        _foregroundChanged = (_, _, _, _, _, _, _) =>
        {
            BringToFront();
            // Taskbar có thể tự nâng mình lên ngay sau sự kiện; nâng đảo thêm một lần nữa.
            Dispatcher.BeginInvoke(BringToFront, DispatcherPriority.Background);
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            // Không hiện trong Alt+Tab và không giành focus khi bấm (đang gõ trong Revit vẫn giữ nguyên).
            var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
            ClipboardMonitor.Attach(hwnd);
            _foregroundHook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundChanged, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (settings.Current.HideWhenFullscreen)
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

        Loaded += (_, _) => RestorePosition();
        Deactivated += OnDeactivated;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    // Ô nhập chữ (ghi chú, việc cần làm) cần bàn phím: tạm cho cửa sổ nhận focus, giữ đảo mở tới khi bấm ra ngoài.
    private void Island_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || !IsInside<TextBoxBase>(source) || _keyboardMode) return;
        _keyboardMode = true;
        SetNoActivate(false);
        Activate();
        _vm.SetHoldOpen(true);
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_keyboardMode) return;
        _keyboardMode = false;
        Keyboard.ClearFocus();
        SetNoActivate(true);
        _vm.SetHoldOpen(false);
    }

    private void SetNoActivate(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        style = enabled ? style | NativeMethods.WS_EX_NOACTIVATE : style & ~(long)NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    // Kéo file từ Explorer vào đảo: mở tab khay file và giữ mở trong lúc kéo.
    private void Island_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        _vm.ShowModule("files");
        _vm.SetHoldOpen(true);
    }

    private void Island_DragLeave(object sender, DragEventArgs e) => _vm.SetHoldOpen(_keyboardMode);

    private void Island_Drop(object sender, DragEventArgs e)
    {
        _vm.SetHoldOpen(_keyboardMode);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) _vm.DropFiles(files);
    }

    private void Island_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        OnDeactivated(this, EventArgs.Empty);
        _vm.Collapse();
    }

    private static bool IsInside<T>(DependencyObject element)
    {
        for (var current = element; current is not null; current = ParentOf(current))
            if (current is T) return true;
        return false;
    }

    private void BringToFront()
    {
        if (IsVisible) NativeMethods.BringToTopmost(new WindowInteropHelper(this).Handle);
    }

    public void ToggleUserVisibility()
    {
        _hiddenByUser = !_hiddenByUser;
        UpdateVisibility();
    }

    /// <summary>Đưa đảo về giữa mép trên màn hình chính và quên vị trí đã kéo.</summary>
    public void ResetPosition()
    {
        _settings.Current.IslandX = null;
        _settings.Current.IslandY = null;
        _settings.Save();
        RestorePosition();
    }

    private void UpdateVisibility()
    {
        if (_hiddenByUser || _hiddenByFullscreen) Hide();
        else Show();
    }

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RestorePosition);

    private void RestorePosition()
    {
        var s = _settings.Current;
        if (s.IslandX is double x && s.IslandY is double y)
        {
            _monitor = MonitorAtPixel(new System.Drawing.Point((int)(x * DpiScale), (int)(y * DpiScale)));
            _centerX = x;
            _islandTop = y;
        }
        else
        {
            _monitor = ToDip(Forms.Screen.PrimaryScreen!.Bounds);
            _centerX = _monitor.Left + _monitor.Width / 2;
            _islandTop = _monitor.Top + DefaultTopMargin;
        }
        Relayout(animate: false);
    }

    private Rect MonitorAtPixel(System.Drawing.Point point) => ToDip(Forms.Screen.FromPoint(point).Bounds);

    private Rect ToDip(System.Drawing.Rectangle r)
    {
        var scale = DpiScale;
        return new Rect(r.Left / scale, r.Top / scale, r.Width / scale, r.Height / scale);
    }

    // Đặt cửa sổ (trong suốt) sao cho nằm trọn trong màn hình và chứa được đảo khi mở rộng theo hướng phù hợp.
    private void Relayout(bool animate)
    {
        var half = CollapsedWidth / 2;
        _centerX = Math.Clamp(_centerX, _monitor.Left + half, Math.Max(_monitor.Left + half, _monitor.Right - half));
        _islandTop = Math.Clamp(_islandTop, _monitor.Top, Math.Max(_monitor.Top, _monitor.Bottom - CollapsedHeight));
        _growUp = _islandTop + CollapsedHeight / 2 > _monitor.Top + _monitor.Height / 2;

        Left = Math.Clamp(_centerX - Width / 2, _monitor.Left, Math.Max(_monitor.Left, _monitor.Right - Width));
        var desiredTop = _growUp ? _islandTop + CollapsedHeight - Height : _islandTop;
        Top = Math.Clamp(desiredTop, _monitor.Top, Math.Max(_monitor.Top, _monitor.Bottom - Height));
        ApplyState(animate);
    }

    private void Island_MouseEnter(object sender, MouseEventArgs e) => _vm.PointerEntered();

    private void Island_MouseLeave(object sender, MouseEventArgs e) => _vm.PointerLeft();

    // Nhấn chuột lên nền đảo (không phải nút): nhả ngay là mở đảo, giữ và kéo là dời đảo.
    // Theo dõi bằng cách đọc vị trí con trỏ định kỳ thay vì capture chuột, vì cửa sổ không lấy focus.
    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideInteractiveControl(source)) return;
        e.Handled = true;
        _pressCursor = Forms.Cursor.Position;
        _pressCenterX = _centerX;
        _pressTop = _islandTop;
        _dragging = false;
        _pressTimer.Start();
    }

    private static bool IsInsideInteractiveControl(DependencyObject element)
    {
        for (var current = element; current is not null; current = ParentOf(current))
        {
            if (current is ButtonBase or ListBoxItem or Slider or ScrollBar or TextBox) return true;
            if (current is IslandWindow) return false;
        }
        return false;
    }

    // VisualTreeHelper chỉ nhận Visual; chữ trong TextBlock (Run) phải đi theo cây logic.
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);

    private void OnPressTick(object? sender, EventArgs e)
    {
        var cursor = Forms.Cursor.Position;
        var dx = cursor.X - _pressCursor.X;

        if (!_dragging && (Math.Abs(dx) > DragThresholdPx || Math.Abs(cursor.Y - _pressCursor.Y) > DragThresholdPx))
        {
            _dragging = true;
            _vm.BeginDrag();
        }

        if (_dragging)
        {
            _monitor = MonitorAtPixel(cursor);
            _centerX = _pressCenterX + dx / DpiScale;
            _islandTop = _pressTop + (cursor.Y - _pressCursor.Y) / DpiScale;
            Relayout(animate: false);
        }

        if (NativeMethods.IsLeftButtonDown()) return;

        _pressTimer.Stop();
        if (_dragging)
        {
            _dragging = false;
            _vm.EndDrag();
            _settings.Current.IslandX = Math.Round(_centerX, 1);
            _settings.Current.IslandY = Math.Round(_islandTop, 1);
            _settings.Save();
        }
        else if (_vm.State != IslandState.Expanded && !_vm.ActivatePeek())
        {
            _vm.ToggleExpanded();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IslandViewModel.State) or nameof(IslandViewModel.LiveModule))
            ApplyState(animate: !_dragging);
    }

    private void ApplyState(bool animate)
    {
        var (width, height) = _vm.State switch
        {
            // Gọn cỡ một cụm nút trên ribbon Revit.
            IslandState.Expanded => (420.0, 120.0),
            IslandState.Peek => (340.0, 48.0),
            _ when _vm.LiveModule is not null => (300.0, CollapsedHeight),
            _ => (CollapsedWidth, CollapsedHeight),
        };
        // Viên thuốc: thu gọn bo tròn hẳn hai đầu, mở rộng bo vừa phải.
        var radius = _vm.State == IslandState.Expanded ? 20.0 : height / 2;

        // Đảo nở ra hai bên quanh tâm, nhưng bị đẩy vào trong nếu sát mép màn hình.
        var anchorX = _centerX - Left;
        var x = Math.Clamp(anchorX - width / 2, 0, Math.Max(0, Width - width));

        // Nở xuống từ mép trên, hoặc nở lên từ mép dưới khi đảo ở nửa dưới màn hình.
        var top = _islandTop - Top;
        var y = _growUp ? top + CollapsedHeight - height : top;
        y = Math.Clamp(y, 0, Math.Max(0, Height - height));

        Island.CornerRadius = new CornerRadius(radius);
        Animate(Island, WidthProperty, width, animate);
        Animate(Island, HeightProperty, height, animate);
        Animate(Island, Canvas.LeftProperty, x, animate);
        Animate(Island, Canvas.TopProperty, y, animate);

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
        _pressTimer.Stop();
        _topmostTimer.Stop();
        if (_foregroundHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(_foregroundHook);
        _fullscreen?.Dispose();
        ClipboardMonitor.Detach();
        base.OnClosed(e);
    }
}
