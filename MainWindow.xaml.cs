using System.Windows;
using System.Windows.Controls;
using KeryxControl.ViewModels;
namespace KeryxControl;
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    public MainWindow() { InitializeComponent(); DataContext = _vm; Loaded += async (_, _) => await _vm.InitializeAsync(); Closed += async (_, _) => await _vm.DisposeAsync(); }
    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: ComboBoxItem item } || item.Tag is not string language) return;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(x => x.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (current is not null) dictionaries.Remove(current);
        dictionaries.Add(new ResourceDictionary { Source = new Uri($"Resources/Strings.{language}.xaml", UriKind.Relative) });
        _vm.SetLanguage();
    }
    private void OpenEscrow_Click(object sender, RoutedEventArgs e)
    {
        new EscrowWindow { Owner = this, DataContext = _vm }.ShowDialog();
    }
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box && !box.IsKeyboardFocusWithin) box.ScrollToEnd();
    }
}
