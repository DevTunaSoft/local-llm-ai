using System.Windows;
using WorkIntelligence.Services;

namespace WorkIntelligence;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Fail closed unless the independently vetted, bundled SQLCipher native library loads.
        // No deprecated e_sqlcipher binary, and never silently fall back to plaintext SQLite.
        try
        {
            System.Runtime.InteropServices.NativeLibrary.Load(RuntimeBundle.Resolve("sqlcipher.dll"));
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlcipher());
        }
        catch (Exception ex)
        {
            AuditLog.Error("SQLCipherInitialization", ex);
            MessageBox.Show("A vetted SQLCipher runtime is required. The encrypted vault cannot start.", "Work Intelligence", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
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
