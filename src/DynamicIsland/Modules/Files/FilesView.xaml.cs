using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland.Modules.Files;

public partial class FilesView : UserControl
{
    private Point _pressPoint;
    private TrayItem? _pressed;

    public FilesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not FilesModule module) return;
            module.Items.CollectionChanged += (_, _) => UpdateHint(module);
            module.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(FilesModule.OneDriveOk) or nameof(FilesModule.HasConflicts)) UpdateDot(module);
            };
            UpdateHint(module);
            UpdateDot(module);
        };
    }

    private void UpdateHint(FilesModule module) => Hint.Visibility = module.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateDot(FilesModule module)
    {
        Dot.Fill = (Brush)FindResource(module.HasConflicts ? "RedBrush" : module.OneDriveOk ? "GreenBrush" : "OrangeBrush");
    }

    private void Tray_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(this);
        _pressed = (e.OriginalSource as FrameworkElement)?.DataContext as TrayItem;
    }

    // Kéo một mục ra khỏi đảo: đưa đường dẫn file cho Windows như khi kéo từ Explorer.
    private void Tray_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _pressed;
        _pressed = null;
        var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });
        DragDrop.DoDragDrop(Tray, data, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
    }

    private void Tray_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is TrayItem item && DataContext is FilesModule module)
            module.OpenCommand.Execute(item);
    }
}
