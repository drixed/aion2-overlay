// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// A Blazor window that was just closed can still get a render or an error report from Blazor; posting it to the
    /// disposed WebView2 throws on the UI thread and Windows ends the whole app. Only that exception is swallowed (and
    /// logged): the window is gone anyway. Everything else stays unhandled, as upstream has it.
    /// </summary>
    public static class DisposedWebViewGuard
    {
        private static bool installed;

        public static void Install(ILogger logger)
        {
            if (installed || Application.Current is null) return;
            installed = true;
            Application.Current.DispatcherUnhandledException += (_, e) =>
            {
                if (!IsDisposedWebView(e.Exception)) return;
                logger.LogWarning(e.Exception, "A closed window was still being drawn; ignored");
                e.Handled = true;
            };
        }

        public static bool IsDisposedWebView(Exception? ex)
        {
            for (; ex is not null; ex = ex.InnerException)
            {
                if (ex is InvalidOperationException && ex.Message.Contains("WebView2", StringComparison.Ordinal)
                    && ex.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (ex is ObjectDisposedException && ex.Message.Contains("WebView2", StringComparison.Ordinal))
                    return true;
                if (ex is COMException { HResult: unchecked((int)0x8007139F) } && ex.StackTrace?.Contains("CoreWebView2", StringComparison.Ordinal) == true)
                    return true;
            }
            return false;
        }
    }
}
