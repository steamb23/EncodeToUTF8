using System;
using System.Reflection;
using System.Windows.Threading;

namespace SteamB23.EncodeToUTF8;

public class SplashScreen : System.Windows.SplashScreen
{
    private DispatcherTimer? timer;

    public SplashScreen(Assembly resourceAssembly, string resourceName) : base(resourceAssembly, resourceName)
    {
    }

    public SplashScreen(string resourceName) : base(resourceName)
    {
    }

    public void Show(TimeSpan autoCloseDuration, bool topMost = true)
    {
        if (timer != null) return;
        
        base.Show(false, topMost);
        timer = new DispatcherTimer(autoCloseDuration, DispatcherPriority.Loaded, (sender, args) =>
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            Close(TimeSpan.FromMilliseconds(300));
        }, Dispatcher.CurrentDispatcher);
    }
}