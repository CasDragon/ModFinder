using System;
using System.Threading.Tasks;
using Avalonia;

namespace ModFinder.Gui
{
  internal class Program
  {
    [STAThread]
    public static void Main(string[] args)
    {
      AppDomain.CurrentDomain.UnhandledException += (s, e) =>
      {
        if (e.ExceptionObject is TaskCanceledException || e.ExceptionObject is OperationCanceledException)
        {
          // Ignore harmless cancellation during app shutdown / D-Bus teardown
          return;
        }
      };

      TaskScheduler.UnobservedTaskException += (s, e) =>
      {
        if (e.Exception.InnerException is TaskCanceledException || e.Exception.InnerException is OperationCanceledException)
        {
          e.SetObserved();
        }
      };

      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
  }
}
