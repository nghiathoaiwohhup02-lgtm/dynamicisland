using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace __TOOLKIT_ID__
{
    public partial class SelectionInfoWindow : Window
    {
        public SelectionInfoWindow(string scope, List<CategoryRow> rows)
        {
            InitializeComponent();
            ScopeText.Text = scope;
            Rows.ItemsSource = rows;
            TotalText.Text = rows.Sum(r => r.Count) + " elements in " + rows.Count + " categories";
        }

        /// <summary>The row the user picked, or null.</summary>
        public CategoryRow Chosen { get; private set; }

        void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectButton.IsEnabled = Rows.SelectedItem != null;
        }

        void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Rows.SelectedItem != null) Select_Click(sender, e);
        }

        void Select_Click(object sender, RoutedEventArgs e)
        {
            Chosen = Rows.SelectedItem as CategoryRow;
            DialogResult = Chosen != null;
        }
    }
}
