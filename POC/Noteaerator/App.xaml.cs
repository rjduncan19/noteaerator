using System;
using System.Diagnostics;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Noteaerator;

public partial class App : Application
{
    // URL to the official WebView2 runtime download page. Shown in the
    // friendly error if the runtime is missing.
    private const string WebView2DownloadUrl =
        "https://developer.microsoft.com/microsoft-edge/webview2/?form=MA13LH";

    protected override void OnStartup(StartupEventArgs e)
    {
        // Note Aerator depends on the Evergreen WebView2 runtime, which is
        // included with Microsoft Edge on Windows 10/11 by default. On the
        // rare machine where it's missing (older Server SKUs, custom-stripped
        // images, etc.) the embedded WebView2 control would otherwise fail
        // with an obscure error inside the project view. Detect upfront and
        // give the user a clear, actionable message.
        if (!IsWebView2RuntimeAvailable())
        {
            var result = MessageBox.Show(
                "Note Aerator needs the Microsoft Edge WebView2 runtime, which is " +
                "normally included with Microsoft Edge on Windows 10 and 11.\n\n" +
                "It doesn't appear to be installed on this machine.\n\n" +
                "Open the WebView2 runtime download page now?",
                "WebView2 runtime not found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = WebView2DownloadUrl,
                        UseShellExecute = true
                    });
                }
                catch { /* best-effort */ }
            }
            Shutdown(exitCode: 2);
            return;
        }

        base.OnStartup(e);
    }

    private static bool IsWebView2RuntimeAvailable()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrWhiteSpace(version);
        }
        catch
        {
            // GetAvailableBrowserVersionString throws WebView2RuntimeNotFoundException
            // (and a couple of others) when the runtime is missing or unusable.
            return false;
        }
    }
}
