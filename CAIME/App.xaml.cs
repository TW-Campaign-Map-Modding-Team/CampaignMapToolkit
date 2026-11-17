using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CAIME.Windows;
using Velopack;
using Velopack.Sources;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string GitHubRepoUrl = "https://github.com/TW-Campaign-Map-Modding-Team/CampaignMapToolkit";

        /// <summary>
        /// Entry point. Must be [STAThread] for WPF.
        /// VelopackApp.Build().Run() MUST be the very first thing called.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Velopack bootstraps here: handles install/uninstall hooks and
            // applies a downloaded update on restart before your UI ever appears.
            VelopackApp.Build().Run();

            // The Assembly Kit's XML tables store numbers with a '.' decimal separator. Parse them
            // with the invariant culture whatever the Windows display language, otherwise the
            // database refuses to load on e.g. a French Windows ("Impossible de stocker <533.74>
            // dans la colonne maxx. Type attendu est Double.") and every project opens read-only.
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            // Headless command-line mode: when launched with arguments (other than
            // Velopack's own hook arguments) we run the requested processing and exit
            // before any WPF Application is created, so no window is ever shown.
            bool isVelopackArg = args.Length > 0
                && args[0].StartsWith("--veloapp", StringComparison.OrdinalIgnoreCase);

            if (args.Length > 0 && !isVelopackArg)
            {
                Environment.Exit(CliRunner.Run(args));
                return;
            }

            // Normal WPF startup
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }

        /// <summary>
        /// Called after the main window is shown. Kicks off a background update check
        /// so the UI is never blocked. The update is downloaded silently; the user is
        /// only prompted when a new version is ready to install.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Restore any Assembly Kit an earlier CAIME version's RPFM session left changed when it was
            // killed before it could clean up (End Task, kill, or Visual Studio "Stop Debugging"). Must
            // happen before a project can be opened.
            CAIME.Rpfm.RpfmRecoveryJournal.RecoverAll();

            _ = CheckForUpdatesAsync();

            string currentVersion = AppInfo.Version;
            string lastVersion = PreferencesViewModel.Instance.LastKnownVersion;

            bool isFirstInstall = PreferencesViewModel.Instance.IsFirstAppLaunch;
            bool isUpdate = !isFirstInstall
                && currentVersion != AppInfo.DevBuild
                && Version.TryParse(lastVersion, out var lastVer)
                && Version.TryParse(currentVersion, out var currentVer)
                && currentVer > lastVer;

            PreferencesViewModel.Instance.LastKnownVersion = currentVersion;
            PreferencesViewModel.Instance.Save();

            bool capturedIsUpdate = isUpdate;
            string capturedVersion = currentVersion;

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                var mainWin = MainWindow;
                if (mainWin == null) return;

                void OnMainReady(Window owner)
                {
                    if (capturedIsUpdate)
                        ShowChangelog(owner, capturedVersion);
                    ShowEulaIfNeeded(owner);
                }

                if (mainWin.IsLoaded)
                    OnMainReady(mainWin);
                else
                    mainWin.Loaded += (s, _) => OnMainReady((Window)s);
            }));
        }

        // ---------------------------------------------------------------
        // Startup windows
        // ---------------------------------------------------------------

        private static void ShowChangelog(Window owner, string version)
        {
            var w = TextViewerWindow.FromContentLoader(
                AppWindowChangelog.Caption(version),
                AppWindowChangelog.ContentLoader(version),
                AppWindowChangelog.WindowTitle);
            w.Owner = owner;
            w.Show();
        }

        private static void ShowEulaIfNeeded(Window owner)
        {
            if (EulaAcceptance.IsAccepted()) return;

            bool accepted = false;

            var eula = TextViewerWindow.FromFile(
                AppWindowEula.Caption,
                AppWindowEula.FilePath,
                AppWindowEula.WindowTitle);
            eula.Owner = owner;

            eula.WithAcceptButton("I Accept", () =>
            {
                EulaAcceptance.Accept();
                accepted = true;
                eula.Close();
            });

            // Any close path that isn't the Accept button shuts the app down.
            eula.Closing += (s, e) =>
            {
                if (!accepted)
                    Application.Current.Shutdown();
            };

            // Re-enable the main window only on successful acceptance.
            owner.IsEnabled = false;
            eula.Closed += (s, e) =>
            {
                if (accepted)
                    owner.IsEnabled = true;
            };

            eula.Show();
        }

        // ---------------------------------------------------------------
        // Update logic
        // ---------------------------------------------------------------

        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                var source = new GithubSource(GitHubRepoUrl, null, prerelease: false);
                var mgr = new UpdateManager(source);

                // Not installed via Velopack (e.g. developer running from VS) – skip.
                if (!mgr.IsInstalled)
                    return;

                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null)
                    return; // Already up to date

                // Download the update in the background (delta package if available).
                await mgr.DownloadUpdatesAsync(newVersion);

                // Ask the user before restarting.
                var result = MessageBox.Show(
                    $"Version {newVersion.TargetFullRelease.Version} is ready to install.\n\n" +
                    "Restart now to apply the update?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Applies the update and restarts the app automatically.
                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
            }
            catch (Exception ex)
            {
                // Log or silently ignore – update failures must never crash the app.
                System.Diagnostics.Debug.WriteLine($"[Velopack] Update check failed: {ex.Message}");
            }
        }
    }
}
