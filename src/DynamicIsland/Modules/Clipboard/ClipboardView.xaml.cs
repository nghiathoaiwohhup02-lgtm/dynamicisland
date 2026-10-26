using System.Windows;

namespace DynamicIsland.Modules.Clipboard;

public partial class ClipboardView : System.Windows.Controls.UserControl
{
    public ClipboardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not ClipboardModule module) return;
            module.Entries.CollectionChanged += (_, _) => UpdateHint(module);
            UpdateHint(module);
        };
    }

    private void UpdateHint(ClipboardModule module) => Hint.Visibility = module.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
