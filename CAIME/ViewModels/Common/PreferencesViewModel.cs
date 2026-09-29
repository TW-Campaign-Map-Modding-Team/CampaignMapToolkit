using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    using CAIME.Rpfm;

    public class PreferencesViewModel : BaseViewModel
    {
        public static readonly PreferencesViewModel Instance = new PreferencesViewModel();

        private const string LAYER_ORDERS_KEY = "LayerOrders";

        private readonly string preferencesPath;
        private readonly string[] asskitDirs;
        private readonly string[] vanillaPackPaths;
        private readonly Dictionary<GameTemplate, List<LayerType>> layerOrders = new Dictionary<GameTemplate, List<LayerType>>();

        #region Actual preferences
        private float hexSpacing;
        public float HexSpacing
        {
            get
            {
                return hexSpacing;
            }
            set
            {
                hexSpacing = value;
                OnPropertyChanged(nameof(HexSpacing));
            }
        }
        private string assKitPath;
        public string AssKitPath
        {
            get
            {
                return assKitPath;
            }
            set
            {
                assKitPath = value;
                SetAssKitPathForCurrentGame(value);
                OnPropertyChanged(nameof(AssKitPath));
            }
        }

        public bool IsFirstAppLaunch { get; set; }

        public string LastKnownVersion { get; set; }

        private bool bLogToFile;
        public bool LogToFile
        {
            get => bLogToFile;
            set
            {
                bLogToFile = value;
                LoggerViewModel.SetLogToFile(bLogToFile);
                OnPropertyChanged(nameof(LogToFile));
            }
        }

        private bool bAutoSave;
        public bool IsAutoSave
        {
            get => bAutoSave;
            set
            {
                bAutoSave = value;
                OnPropertyChanged(nameof(IsAutoSave));
            }
        }

        private bool bAutoBackup;
        public bool IsAutoBackup
        {
            get => bAutoBackup;
            set
            {
                bAutoBackup = value;
                OnPropertyChanged(nameof(IsAutoBackup));
            }
        }

        /// <summary>Value of <see cref="AutoBackupsToKeep"/> that means "never prune".</summary>
        public const int KEEP_ALL_BACKUPS = 0;

        private int autoBackupsToKeep = KEEP_ALL_BACKUPS;

        /// <summary>
        /// How many of the newest auto-backups to keep in a project's backups folder. Older ones are
        /// deleted after each new backup. <see cref="KEEP_ALL_BACKUPS"/> (the default) keeps every
        /// backup, which is what the tool has always done.
        /// </summary>
        public int AutoBackupsToKeep
        {
            get => autoBackupsToKeep;
            set
            {
                autoBackupsToKeep = value < 0 ? KEEP_ALL_BACKUPS : value;
                OnPropertyChanged(nameof(AutoBackupsToKeep));
            }
        }

        private bool bMapDataConfigPostProcess;
        public bool IsMapDataConfigPostProcessEnabled
        {
            get => bMapDataConfigPostProcess;
            set
            {
                bMapDataConfigPostProcess = value;
                OnPropertyChanged(nameof(IsMapDataConfigPostProcessEnabled));
            }
        }

        private DatabaseSource databaseSource;
        public DatabaseSource DatabaseSource
        {
            get => databaseSource;
            set
            {
                databaseSource = value;
                OnPropertyChanged(nameof(DatabaseSource));
                OnPropertyChanged(nameof(IsRpfmSource));
                OnPropertyChanged(nameof(RpfmSettingsVisibility));
                OnPropertyChanged(nameof(VanillaPackWarningVisibility));
            }
        }

        private string rpfmPath;
        public string RpfmPath
        {
            get => rpfmPath;
            set
            {
                rpfmPath = value;
                OnPropertyChanged(nameof(RpfmPath));
            }
        }

        // The pack containing this game's vanilla DB tables, for the RPFM workflow's vanilla-fallback
        // tier. Per-game like AssKitPath, and entirely optional - which pack actually holds a game's
        // DB tables is not a stable filename to guess (it has changed even within one game's
        // lifetime), so the user points at it explicitly rather than CAIME assuming one.
        private string vanillaPackPath;
        public string VanillaPackPath
        {
            get => vanillaPackPath;
            set
            {
                vanillaPackPath = value;
                SetVanillaPackPathForCurrentGame(value);
                OnPropertyChanged(nameof(VanillaPackPath));
                OnPropertyChanged(nameof(VanillaPackWarningVisibility));
            }
        }

        public bool IsRpfmSource => DatabaseSource == DatabaseSource.RPFM;

        public Visibility RpfmSettingsVisibility => IsRpfmSource ? Visibility.Visible : Visibility.Collapsed;

        // Surfaces the gap this state can end up in even though OnApply blocks *saving* it directly:
        // the database source is global but the vanilla pack is per-game, and the Base game selector
        // can land on an unconfigured game without going through OnApply at all - e.g. ProjectManager
        // remembers whichever game you last opened a project for via a direct Save() call (see its
        // "Remembered as the Preferences window's default Base game" comment), which is legitimate and
        // not itself a bug, but can leave this window showing RPFM selected with an empty/stale
        // Vanilla pack for whatever game that was. This is a passive, always-visible hint about that -
        // not a new restriction; PrepareRpfmDatabaseIfNeeded already degrades safely at runtime
        // (a project for an unconfigured game just opens with database features disabled, never a
        // crash), so this exists purely so the gap is self-explanatory here rather than looking like
        // corruption.
        public Visibility VanillaPackWarningVisibility =>
            IsRpfmSource && (string.IsNullOrWhiteSpace(VanillaPackPath) || !File.Exists(VanillaPackPath))
                ? Visibility.Visible
                : Visibility.Collapsed;
        #endregion

        #region UI Bindings
        public GameTemplate[] SupportedGamesList { get; set; }

        // Which game the Preferences window's "Base game" selector defaults to. Persisted so it
        // survives a restart (remembers the last game you worked with); ProjectManager.Open overrides
        // it in-memory (and re-saves) to the just-opened project's game, and MenuControl.Preferences_Click
        // re-applies that override every time the window opens while a project is still open, so an
        // open project always wins over whatever was last browsed to manually.
        public int SelectedGameIndex { get; set; }

        public DatabaseSource[] DatabaseSources { get; } =
        {
            DatabaseSource.AssemblyKit,
            DatabaseSource.RPFM,
        };
        #endregion

        public event RoutedEventHandler OnPreferencesChanged;

        private PreferencesViewModel()
        {
            var appdataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            preferencesPath = Path.Combine(appdataDir, @"CampaignMapToolkit\Caime\preferences.json");

            SupportedGamesList = new GameTemplate[(int)GameTemplate.Count];
            for (int i = 0; i < (int)GameTemplate.Count; ++i)
            {
                SupportedGamesList[i] = (GameTemplate)i;
            }

            asskitDirs       = new string[(int)GameTemplate.Count];
            vanillaPackPaths = new string[(int)GameTemplate.Count];

            SetDefaults();
            PreferencesMigration.MigrateIfNeeded(appdataDir);
            Load();
        }

        public bool OnApply()
        {
            if (AssKitPath == null)
            {
                var msg = "Please, provide a path to the assembly_kit folder!";
                LoggerViewModel.Log(msg, LogLevel.Warning);
                MessageBox.Show(msg, "Missing Aseembly Kit path");
                return false;
            }

            if (DatabaseSource == DatabaseSource.RPFM)
            {
                if (string.IsNullOrWhiteSpace(RpfmPath))
                {
                    var msg = "The database source is set to RPFM, but no RPFM installation path is provided. " +
                              "Please provide one, or switch the database source back to Assembly Kit.";
                    LoggerViewModel.Log(msg, LogLevel.Warning);
                    MessageBox.Show(msg, "Missing RPFM path");
                    return false;
                }

                // Never store an invalid RPFM installation path.
                if (!RpfmService.ValidateInstallation(RpfmPath, out var rpfmError))
                {
                    RpfmPath = null;
                    var msg = $"The provided RPFM installation path is not valid and was not saved.\n\n{rpfmError}";
                    LoggerViewModel.Log(msg, LogLevel.Warning);
                    MessageBox.Show(msg, "Invalid RPFM path");
                    return false;
                }

                // Without a vanilla pack, every table the modded pack doesn't itself contain would
                // silently fall back to the Assembly Kit's static copy - the very thing choosing RPFM
                // as the source is meant to avoid - so this is required, not optional, same as the
                // RPFM path above.
                if (string.IsNullOrWhiteSpace(VanillaPackPath) || !File.Exists(VanillaPackPath))
                {
                    var game = (GameTemplate)SelectedGameIndex;
                    var msg = $"The database source is set to RPFM, but no vanilla pack is set for {game} " +
                              "(or the file it points at no longer exists). Please provide one, or switch the " +
                              "database source back to Assembly Kit.";
                    LoggerViewModel.Log(msg, LogLevel.Warning);
                    MessageBox.Show(msg, "Missing vanilla pack");
                    return false;
                }
            }

            Save();
            OnPreferencesChanged?.Invoke(this, null);
            return true;
        }

        public void OnCancel()
        {
            // Reload preferences
            Load();
        }

        public void Save()
        {
            var path = Path.GetDirectoryName(preferencesPath);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var json = new JObject();
            json[nameof(HexSpacing)] = hexSpacing;
            json[nameof(IsFirstAppLaunch)] = IsFirstAppLaunch;
            json[nameof(LastKnownVersion)] = LastKnownVersion ?? "";
            json[nameof(LogToFile)] = LogToFile;
            json[nameof(IsAutoSave)] = IsAutoSave;
            json[nameof(IsAutoBackup)] = IsAutoBackup;
            json[nameof(AutoBackupsToKeep)] = AutoBackupsToKeep;
            json[nameof(IsMapDataConfigPostProcessEnabled)] = IsMapDataConfigPostProcessEnabled;
            json[nameof(DatabaseSource)] = DatabaseSource.ToString();
            json[nameof(RpfmPath)] = RpfmPath ?? "";
            json[nameof(SelectedGameIndex)] = SelectedGameIndex;

            var assKitPaths = new JObject();
            for (int i = 0; i < asskitDirs.Length; ++i)
            {
                assKitPaths[$"{(GameTemplate)i}_AssKitPath"] = asskitDirs[i] ?? "";
            }
            json["AssemblyKitPaths"] = assKitPaths;

            var vanillaPacks = new JObject();
            for (int i = 0; i < vanillaPackPaths.Length; ++i)
            {
                vanillaPacks[$"{(GameTemplate)i}_VanillaPackPath"] = vanillaPackPaths[i] ?? "";
            }
            json["VanillaPackPaths"] = vanillaPacks;

            json[LAYER_ORDERS_KEY] = LayerOrdersJson.ToJson(layerOrders);

            File.WriteAllText(preferencesPath, json.ToString());
        }

        private void Load()
        {
            if (!File.Exists(preferencesPath))
            {
                IsFirstAppLaunch = true;
                return;
            }

            try
            {
                var json = JObject.Parse(File.ReadAllText(preferencesPath));

                // Load simple properties
                // Written by JObject with an invariant decimal point, so it must be read back the
                // same way - a comma-decimal locale otherwise fails to parse its own saved value.
                if (json.TryGetValue(nameof(HexSpacing), out var hexSpacingToken)
                    && float.TryParse(hexSpacingToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float hexSpacingVal))
                {
                    HexSpacing = hexSpacingVal;
                }

                if (json.TryGetValue(nameof(IsFirstAppLaunch), out var firstLaunchToken) && bool.TryParse(firstLaunchToken.ToString(), out bool firstLaunch))
                {
                    IsFirstAppLaunch = firstLaunch;
                }

                if (json.TryGetValue(nameof(LastKnownVersion), out var lastVersionToken))
                {
                    string lastVersion = lastVersionToken.ToString();
                    LastKnownVersion = string.IsNullOrEmpty(lastVersion) ? null : lastVersion;
                }

                if (json.TryGetValue(nameof(LogToFile), out var logToFileToken) && bool.TryParse(logToFileToken.ToString(), out bool logToFile))
                {
                    LogToFile = logToFile;
                }

                if (json.TryGetValue(nameof(IsAutoSave), out var autoSaveToken) && bool.TryParse(autoSaveToken.ToString(), out bool autoSave))
                {
                    IsAutoSave = autoSave;
                }

                if (json.TryGetValue(nameof(IsAutoBackup), out var autoBackupToken) && bool.TryParse(autoBackupToken.ToString(), out bool autoBackup))
                {
                    IsAutoBackup = autoBackup;
                }

                if (json.TryGetValue(nameof(AutoBackupsToKeep), out var backupsToKeepToken) && int.TryParse(backupsToKeepToken.ToString(), out int backupsToKeep))
                {
                    AutoBackupsToKeep = backupsToKeep;
                }

                if (json.TryGetValue(nameof(IsMapDataConfigPostProcessEnabled), out var postProcessToken) && bool.TryParse(postProcessToken.ToString(), out bool postProcess))
                {
                    IsMapDataConfigPostProcessEnabled = postProcess;
                }

                if (json.TryGetValue(nameof(DatabaseSource), out var dbSourceToken) &&
                    Enum.TryParse(dbSourceToken.ToString(), out DatabaseSource dbSource))
                {
                    DatabaseSource = dbSource;
                }

                if (json.TryGetValue(nameof(RpfmPath), out var rpfmPathToken))
                {
                    string rpfmPathValue = rpfmPathToken.ToString();
                    RpfmPath = string.IsNullOrEmpty(rpfmPathValue) ? null : rpfmPathValue;
                }

                if (json.TryGetValue(nameof(SelectedGameIndex), out var selectedGameToken) &&
                    int.TryParse(selectedGameToken.ToString(), out int selectedGameIndex) &&
                    selectedGameIndex >= 0 && selectedGameIndex < (int)GameTemplate.Count)
                {
                    SelectedGameIndex = selectedGameIndex;
                }

                // Load assembly kit paths
                if (json.TryGetValue("AssemblyKitPaths", out var assKitPathsToken) && assKitPathsToken is JObject assKitPaths)
                {
                    for (int i = 0; i < asskitDirs.Length; i++)
                    {
                        var game = (GameTemplate)i;
                        var key = $"{game}_AssKitPath";

                        if (assKitPaths.TryGetValue(key, out var pathToken))
                        {
                            string pathValue = pathToken.ToString();
                            asskitDirs[i] = string.IsNullOrEmpty(pathValue) ? null : pathValue;
                        }
                    }
                }

                // Load vanilla pack paths
                if (json.TryGetValue("VanillaPackPaths", out var vanillaPacksToken) && vanillaPacksToken is JObject vanillaPacks)
                {
                    for (int i = 0; i < vanillaPackPaths.Length; i++)
                    {
                        var game = (GameTemplate)i;
                        var key = $"{game}_VanillaPackPath";

                        if (vanillaPacks.TryGetValue(key, out var pathToken))
                        {
                            string pathValue = pathToken.ToString();
                            vanillaPackPaths[i] = string.IsNullOrEmpty(pathValue) ? null : pathValue;
                        }
                    }
                }

                layerOrders.Clear();
                if (json.TryGetValue(LAYER_ORDERS_KEY, out var layerOrdersToken) && layerOrdersToken is JObject layerOrdersJson)
                {
                    foreach (var layerOrder in LayerOrdersJson.FromJson(layerOrdersJson))
                    {
                        layerOrders[layerOrder.Key] = layerOrder.Value;
                    }
                }

                Save();
            }
            catch
            {
                IsFirstAppLaunch = true;
            }
        }

        public void SetAssKitPathForCurrentGame(string path)
        {
            asskitDirs[SelectedGameIndex] = path;
        }

        public void SetAssKitPath(GameTemplate game, string path)
        {
            int index = (int)game;
            asskitDirs[index] = path;
        }

        public string GetAssKitPath(GameTemplate game)
        {
            return asskitDirs[(int)game];
        }

        public void SetVanillaPackPathForCurrentGame(string path)
        {
            vanillaPackPaths[SelectedGameIndex] = path;
        }

        public void SetVanillaPackPath(GameTemplate game, string path)
        {
            vanillaPackPaths[(int)game] = path;
        }

        public string GetVanillaPackPath(GameTemplate game)
        {
            return vanillaPackPaths[(int)game];
        }

        /// <summary>
        /// The editor's layers stack order the user arranged for a game, topmost first,
        /// or null when they kept the built-in order
        /// </summary>
        public IReadOnlyList<LayerType> GetLayerOrder(GameTemplate game)
        {
            return layerOrders.TryGetValue(game, out var order) ? order : null;
        }

        /// <summary>
        /// Remembers the layers stack order for a game; null forgets it so the built-in order applies.
        /// Call <see cref="Save"/> to persist it.
        /// </summary>
        public void SetLayerOrder(GameTemplate game, IEnumerable<LayerType> order)
        {
            if (order == null)
            {
                layerOrders.Remove(game);
            }
            else
            {
                layerOrders[game] = order.ToList();
            }
        }

        private void SetDefaults()
        {
            HexSpacing                          = 0.0f;
            IsFirstAppLaunch                    = true;
            LastKnownVersion                    = null;
            LogToFile                           = true;
            IsAutoSave                          = true;
            IsAutoBackup                        = true;
            IsMapDataConfigPostProcessEnabled   = false;
            DatabaseSource                      = DatabaseSource.AssemblyKit;
            RpfmPath                            = null;
        }
    }
}
