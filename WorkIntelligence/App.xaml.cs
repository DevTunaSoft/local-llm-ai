using System.Windows;
using WorkIntelligence.Services;

namespace WorkIntelligence;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Resolve the scheduled task's explicit data location before any LocalPaths
        // access; Task Scheduler need not inherit the launching process environment.
        try
        {
            var root = StartupOptions.DataRoot(e.Args);
            if (root is not null) Environment.SetEnvironmentVariable("WORKINTELLIGENCE_DATA_ROOT", root);
        }
        catch (ArgumentException)
        {
            Shutdown(2);
            return;
        }
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            AuditLog.Error("UnhandledUI", args.Exception);
            MessageBox.Show("An unexpected error occurred. The details were saved to the local diagnostic log.", "Work Intelligence", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AuditLog.Error("UnobservedTask", args.Exception);
            args.SetObserved();
        };
        if (e.Args.Contains("--scheduled-backup",StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            _=Task.Run(async () =>
            {
                var code=0;
                try { await ScheduledBackup.RunJobAsync(CancellationToken.None); }
                catch(Exception ex) { AuditLog.Error("ScheduledBackupFailure",ex);code=1; }
                await Dispatcher.InvokeAsync(() => Shutdown(code));
            });
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
