using System.Windows;

namespace SimplePLC.Studio;

public partial class App : System.Windows.Application
{
    private bool _isCrashing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        this.DispatcherUnhandledException += (s, ev) => 
        {
            if (_isCrashing) return;
            _isCrashing = true;
            try
            {
                System.IO.File.WriteAllText("crash.log", ev.Exception.ToString());
                MessageBox.Show(ev.Exception.Message, "SimplePLC Studio Crash");
            }
            catch { }
            ev.Handled = true;
            Shutdown(1);
        };
    }
}
