using System.Reflection;
using System.Windows;

namespace SteamB23.EncodeToUTF8;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionTextBlock.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version}";
    }
}