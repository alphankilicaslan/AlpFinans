using System;
using System.IO;
using System.Windows;

namespace AlpFinans;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), args.ExceptionObject.ToString()); } catch { }
        };
        DispatcherUnhandledException += (s, args) =>
        {
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), args.Exception.ToString()); } catch { }
        };
        base.OnStartup(e);
    }
}
