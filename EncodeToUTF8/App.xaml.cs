using System;
using System.Windows;

namespace SteamB23.EncodeToUTF8
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <inheritdoc />
        App()
        {
            var splash = new SplashScreen($"SplashImage/{Random.Shared.Next(0, 3)}.png");
            splash.Show(TimeSpan.FromSeconds(2));
        }
    }
}