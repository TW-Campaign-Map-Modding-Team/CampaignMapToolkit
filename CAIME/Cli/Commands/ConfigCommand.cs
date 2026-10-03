using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CAIME.Rpfm;

namespace CAIME
{
    internal sealed class ConfigCommand : CliCommand
    {
        private const string GameOption = "--game";
        private const string NotSet     = "(not set)";

        private const float MaxHexSpacing = 0.2f;

        private delegate bool TryNormalise(string key, string input, out string value);

        private sealed class Setting
        {
            public string Key;
            public string Description;
            public bool   IsPerGame;
            public Func<GameTemplate, string> Read;
            public TryNormalise Normalise;
            public Action<GameTemplate, string> Write;
        }

        private sealed class Change
        {
            public Setting Setting;
            public string  Value;
        }

        private static PreferencesViewModel Preferences => PreferencesViewModel.Instance;

        private static readonly Setting[] Settings =
        {
            new Setting
            {
                Key         = "assembly-kit-path",
                Description = "Assembly Kit folder for a game. Needs --game.",
                IsPerGame   = true,
                Read        = game => Preferences.GetAssKitPath(game),
                Normalise   = TryExistingFolder,
                Write       = (game, value) => Preferences.SetAssKitPath(game, value),
            },
            new Setting
            {
                Key         = "vanilla-pack-path",
                Description = "Vanilla .pack the RPFM database source reads a game's\ntables from. Needs --game.",
                IsPerGame   = true,
                Read        = game => Preferences.GetVanillaPackPath(game),
                Normalise   = TryExistingFile,
                Write       = (game, value) => Preferences.SetVanillaPackPath(game, value),
            },
            new Setting
            {
                Key         = "database-source",
                Description = "Where database tables come from: assembly-kit or rpfm.",
                Read        = _ => Preferences.DatabaseSource == DatabaseSource.RPFM ? "rpfm" : "assembly-kit",
                Normalise   = TryDatabaseSource,
                Write       = (_, value) => Preferences.DatabaseSource = value == "rpfm" ? DatabaseSource.RPFM : DatabaseSource.AssemblyKit,
            },
            new Setting
            {
                Key         = "rpfm-path",
                Description = "RPFM installation folder, for the rpfm database source.",
                Read        = _ => Preferences.RpfmPath,
                Normalise   = TryRpfmFolder,
                Write       = (_, value) => Preferences.RpfmPath = value,
            },
            new Setting
            {
                Key         = "hex-spacing",
                Description = $"Gap drawn between hexes in the editor, 0 to {MaxHexSpacing.ToString(CultureInfo.InvariantCulture)}.",
                Read        = _ => Preferences.HexSpacing.ToString(CultureInfo.InvariantCulture),
                Normalise   = TryHexSpacing,
                Write       = (_, value) => Preferences.HexSpacing = float.Parse(value, CultureInfo.InvariantCulture),
            },
            new Setting
            {
                Key         = "log-to-file",
                Description = "Also write the log to a file: true or false.",
                Read        = _ => FormatBool(Preferences.LogToFile),
                Normalise   = TryBool,
                Write       = (_, value) => Preferences.LogToFile = value == "true",
            },
            new Setting
            {
                Key         = "auto-save",
                Description = "Auto-save open projects: true or false.",
                Read        = _ => FormatBool(Preferences.IsAutoSave),
                Normalise   = TryBool,
                Write       = (_, value) => Preferences.IsAutoSave = value == "true",
            },
            new Setting
            {
                Key         = "auto-backup",
                Description = "Back up open projects periodically: true or false.",
                Read        = _ => FormatBool(Preferences.IsAutoBackup),
                Normalise   = TryBool,
                Write       = (_, value) => Preferences.IsAutoBackup = value == "true",
            },
            new Setting
            {
                Key         = "backups-to-keep",
                Description = "How many auto-backups to keep per project; 0 keeps all.",
                Read        = _ => Preferences.AutoBackupsToKeep.ToString(CultureInfo.InvariantCulture),
                Normalise   = TryWholeNumber,
                Write       = (_, value) => Preferences.AutoBackupsToKeep = int.Parse(value, CultureInfo.InvariantCulture),
            },
            new Setting
            {
                Key         = "map-data-post-process",
                Description = "Post-process the map data config after Map Data\nprocessing: true or false.",
                Read        = _ => FormatBool(Preferences.IsMapDataConfigPostProcessEnabled),
                Normalise   = TryBool,
                Write       = (_, value) => Preferences.IsMapDataConfigPostProcessEnabled = value == "true",
            },
        };

        public override string Name    => "config";
        public override string Summary => "Show or change settings, like Settings > Preferences.";

        public override string[] Usage => new[]
        {
            "list",
            "get <key> [<key> ...] [--game <game>]",
            "set <key>=<value> [<key>=<value> ...] [--game <game>]",
        };

        public override int Run(string[] args)
        {
            if (WantsHelp(args))
            {
                return CliConsole.ExitSuccess;
            }

            var options = new CliOptions().Value(GameOption, "-g").Positionals();
            if (options.TryParse(args, out var parsed) == false ||
                TryGetGame(parsed, out var game) == false)
            {
                CliConsole.UsageHint(Name);
                return CliConsole.ExitUsageError;
            }

            var action = parsed.Positionals.FirstOrDefault() ?? "list";
            var rest   = parsed.Positionals.Skip(1).ToList();

            switch (action.ToLowerInvariant())
            {
                case "list": return rest.Count == 0 ? List() : UsageError("'config list' takes no further arguments.");
                case "get":  return Get(rest, game);
                case "set":  return Set(rest, game);
            }

            return UsageError($"Unknown config action '{action}'. Use list, get or set.");
        }

        private static bool TryGetGame(CliArguments args, out GameTemplate? game)
        {
            game = null;

            if (args.Has(GameOption) == false)
            {
                return true;
            }

            if (CliNames.TryParseGame(args.Value(GameOption), out var parsedGame) == false)
            {
                CliConsole.Error($"Unknown game '{args.Value(GameOption)}'. Games: {string.Join(", ", CliNames.AllGames.Select(CliNames.Game))}.");
                return false;
            }

            game = parsedGame;
            return true;
        }

        private static int List()
        {
            var keyWidth = Settings.Max(s => s.Key.Length);

            foreach (var setting in Settings.Where(s => s.IsPerGame == false))
            {
                CliConsole.Info($"{setting.Key.PadRight(keyWidth)}  {Display(setting.Read(GameTemplate.Invalid))}");
            }

            foreach (var setting in Settings.Where(s => s.IsPerGame))
            {
                CliConsole.Info(string.Empty);
                CliConsole.Info($"{setting.Key}:");
                PrintPerGame(setting);
            }

            return CliConsole.ExitSuccess;
        }

        private int Get(List<string> keys, GameTemplate? game)
        {
            if (keys.Count == 0)
            {
                return UsageError("'config get' needs at least one setting key.");
            }

            var settings = new List<Setting>();
            foreach (var key in keys)
            {
                if (TryFindSetting(key, out var setting) == false)
                {
                    return CliConsole.ExitUsageError;
                }

                settings.Add(setting);
            }

            foreach (var setting in settings)
            {
                if (setting.IsPerGame && game == null)
                {
                    CliConsole.Info($"{setting.Key}:");
                    PrintPerGame(setting);
                }
                else if (settings.Count == 1)
                {
                    CliConsole.Info(setting.Read(game ?? GameTemplate.Invalid) ?? string.Empty);
                }
                else
                {
                    CliConsole.Info($"{setting.Key} = {setting.Read(game ?? GameTemplate.Invalid) ?? string.Empty}");
                }
            }

            return CliConsole.ExitSuccess;
        }

        private int Set(List<string> assignments, GameTemplate? game)
        {
            if (assignments.Count == 0)
            {
                return UsageError("'config set' needs at least one <key>=<value>.");
            }

            if (TryReadChanges(assignments, game, out var changes) == false)
            {
                CliConsole.UsageHint(Name);
                return CliConsole.ExitUsageError;
            }

            var gameValue = game ?? GameTemplate.Invalid;
            foreach (var change in changes)
            {
                var previous = change.Setting.Read(gameValue);
                change.Setting.Write(gameValue, change.Value);

                var scope = change.Setting.IsPerGame ? $" ({CliNames.Game(gameValue)})" : string.Empty;
                CliConsole.Info($"{change.Setting.Key}{scope}: {Display(previous)} -> {Display(change.Value)}");
            }

            Preferences.Save();
            WarnAboutIncompleteRpfmSetup(game);

            return CliConsole.ExitSuccess;
        }

        private static bool TryReadChanges(List<string> assignments, GameTemplate? game, out List<Change> changes)
        {
            changes = new List<Change>();

            foreach (var assignment in assignments)
            {
                var separator = assignment.IndexOf('=');
                if (separator <= 0)
                {
                    CliConsole.Error($"'{assignment}' is not a <key>=<value> pair.");
                    return false;
                }

                if (TryFindSetting(assignment.Substring(0, separator).Trim(), out var setting) == false)
                {
                    return false;
                }

                if (setting.IsPerGame && game == null)
                {
                    CliConsole.Error($"'{setting.Key}' is set per game. Pass {GameOption} <game>.");
                    return false;
                }

                if (setting.Normalise(setting.Key, assignment.Substring(separator + 1).Trim(), out var value) == false)
                {
                    return false;
                }

                changes.Add(new Change { Setting = setting, Value = value });
            }

            var switchesToRpfm = changes.Any(c => c.Setting.Key == "database-source" && c.Value == "rpfm");
            var rpfmPath       = changes.Where(c => c.Setting.Key == "rpfm-path").Select(c => c.Value).DefaultIfEmpty(Preferences.RpfmPath).Last();
            if (switchesToRpfm && string.IsNullOrEmpty(rpfmPath))
            {
                CliConsole.Error("The rpfm database source needs an RPFM installation. Set rpfm-path as well, e.g. " +
                                 "config set rpfm-path=\"C:\\RPFM\" database-source=rpfm");
                return false;
            }

            return true;
        }

        private static void WarnAboutIncompleteRpfmSetup(GameTemplate? game)
        {
            if (Preferences.DatabaseSource != DatabaseSource.RPFM || game == null)
            {
                return;
            }

            var vanillaPack = Preferences.GetVanillaPackPath(game.Value);
            if (string.IsNullOrEmpty(vanillaPack) || File.Exists(vanillaPack) == false)
            {
                CliConsole.Warning($"The database source is rpfm, but {CliNames.Game(game.Value)} has no vanilla pack set. " +
                                   $"Its projects won't open until vanilla-pack-path is set for it.");
            }
        }

        private static bool TryFindSetting(string key, out Setting setting)
        {
            setting = Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
            if (setting != null)
            {
                return true;
            }

            CliConsole.Error($"Unknown setting '{key}'. Settings: {string.Join(", ", Settings.Select(s => s.Key))}.");
            return false;
        }

        private static void PrintPerGame(Setting setting)
        {
            var gameWidth = CliNames.AllGames.Max(g => CliNames.Game(g).Length);
            foreach (var game in CliNames.AllGames)
            {
                CliConsole.Info($"  {CliNames.Game(game).PadRight(gameWidth)}  {Display(setting.Read(game))}");
            }
        }

        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? NotSet : value;
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool Invalid(string message, out string value)
        {
            CliConsole.Error(message);
            value = null;
            return false;
        }

        private static bool TryExistingFolder(string key, string input, out string value)
        {
            value = null;

            if (input.Length == 0)
            {
                return true;
            }

            if (CliPaths.TryGetFullPath(input, key, out value) == false)
            {
                return false;
            }

            return Directory.Exists(value) || Invalid($"{key}: folder not found: '{value}'.", out value);
        }

        private static bool TryExistingFile(string key, string input, out string value)
        {
            value = null;

            if (input.Length == 0)
            {
                return true;
            }

            if (CliPaths.TryGetFullPath(input, key, out value) == false)
            {
                return false;
            }

            return File.Exists(value) || Invalid($"{key}: file not found: '{value}'.", out value);
        }

        private static bool TryRpfmFolder(string key, string input, out string value)
        {
            if (TryExistingFolder(key, input, out value) == false)
            {
                return false;
            }

            if (value == null)
            {
                return true;
            }

            return RpfmService.ValidateInstallation(value, out var error) || Invalid($"{key}: {error}", out value);
        }

        private static bool TryDatabaseSource(string key, string input, out string value)
        {
            var allowed = new[] { "assembly-kit", "rpfm" };

            value = allowed.FirstOrDefault(a => string.Equals(a, input, StringComparison.OrdinalIgnoreCase));
            return value != null || Invalid($"{key} must be one of: {string.Join(", ", allowed)}.", out value);
        }

        private static bool TryBool(string key, string input, out string value)
        {
            switch (input.ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1":
                    value = "true";
                    return true;
                case "false": case "no": case "off": case "0":
                    value = "false";
                    return true;
            }

            return Invalid($"{key} must be true or false, not '{input}'.", out value);
        }

        private static bool TryWholeNumber(string key, string input, out string value)
        {
            if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                value = number.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            return Invalid($"{key} must be a whole number of 0 or more, not '{input}'.", out value);
        }

        private static bool TryHexSpacing(string key, string input, out string value)
        {
            if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var spacing) && spacing >= 0 && spacing <= MaxHexSpacing)
            {
                value = spacing.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            return Invalid($"{key} must be a number from 0 to {MaxHexSpacing.ToString(CultureInfo.InvariantCulture)} (use '.' for decimals), not '{input}'.", out value);
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            Option(help, "list", "Show every setting. The default action.");
            Option(help, "get <key> ...", "Print the value of one or more settings. A per-game\nsetting without --game prints every game's value.");
            Option(help, "set <key>=<value> ...", "Change one or more settings. All values are checked\nbefore any is saved. An empty value clears a path.");
            Option(help, "--game, -g <game>", "Game for per-game settings.");

            help.AppendLine();
            help.AppendLine("SETTINGS:");
            foreach (var setting in Settings)
            {
                Option(help, setting.Key, setting.Description);
            }
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("list");
            help.AppendLine("get assembly-kit-path --game warhammer3");
            help.AppendLine("set assembly-kit-path=\"D:\\Steam\\steamapps\\common\\Total War WARHAMMER III\\assembly_kit\" --game warhammer3");
            help.AppendLine("set auto-save=false auto-backup=false backups-to-keep=5");
        }
    }
}
