using System.Windows;
namespace KeryxControl;
public partial class EscrowWindow : Window
{
    public EscrowWindow() => InitializeComponent();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
