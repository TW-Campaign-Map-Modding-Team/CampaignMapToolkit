using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal sealed class CreateCommand : CliCommand
    {
        private const string NameOption          = "--name";
        private const string GameOption          = "--game";
        private const string WidthOption         = "--width";
        private const string HeightOption        = "--height";
        private const string TemplateOption      = "--template";
        private const string OutputOption        = "--output";
        private const string ForceOption         = "--force";
        private const string ListTemplatesOption = "--list-templates";

        private const uint DefaultWidth  = 1016;
        private const uint DefaultHeight = 720;

        public override string Name    => "create";
        public override string Summary => "Create a new project, like File > Create new map.";

        public override string[] Usage => new[]
        {
            "--name <map-name> --game <game> [--width <n>] [--height <n>] [--output <folder>] [--force]",
            "--name <map-name> --template <template> [--output <folder>] [--force]",
            "--list-templates",
        };

        public override int Run(string[] args)
        {
            if (WantsHelp(args))
            {
                return CliConsole.ExitSuccess;
            }

            var options = new CliOptions()
                .Value(NameOption, "-n")
                .Value(GameOption, "-g")
                .Value(WidthOption)
                .Value(HeightOption)
                .Value(TemplateOption, "-t")
                .Value(OutputOption, "-o")
                .Flag(ForceOption)
                .Flag(ListTemplatesOption);

            if (options.TryParse(args, out var parsed) == false)
            {
                CliConsole.UsageHint(Name);
                return CliConsole.ExitUsageError;
            }

            var projectManager = new ProjectManager();

            if (parsed.Has(ListTemplatesOption))
            {
                return ListTemplates(projectManager);
            }

            if (TryReadRequest(parsed, projectManager, out var request) == false)
            {
                CliConsole.UsageHint(Name);
                return CliConsole.ExitUsageError;
            }

            return Create(projectManager, request, parsed.Has(ForceOption));
        }

        private sealed class CreateRequest
        {
            public string       MapName;
            public string       ProjectsDir;
            public string       Template;
            public GameTemplate Game;
            public uint         Width;
            public uint         Height;

            public bool UsesTemplate => Template != ProjectManager.TEMPLATE_NONE;
        }

        private bool TryReadRequest(CliArguments args, ProjectManager projectManager, out CreateRequest request)
        {
            request = new CreateRequest
            {
                Template    = args.Value(TemplateOption) ?? ProjectManager.TEMPLATE_NONE,
                ProjectsDir = projectManager.ProjectsDir,
                Width       = DefaultWidth,
                Height      = DefaultHeight,
            };

            return TryReadMapName(args, request)
                && TryReadProjectsDir(args, request)
                && (request.UsesTemplate ? TryReadTemplate(args, projectManager, request) : TryReadGameAndSize(args, request));
        }

        private static bool TryReadMapName(CliArguments args, CreateRequest request)
        {
            var name = args.Value(NameOption);
            if (string.IsNullOrWhiteSpace(name))
            {
                CliConsole.Error($"Missing required option '{NameOption} <map-name>'.");
                return false;
            }

            request.MapName = StringHelper.NameFixup(name.Trim());
            if (request.MapName.Length == 0)
            {
                CliConsole.Error($"'{name}' has no characters a map name can use.");
                return false;
            }

            if (request.MapName != name)
            {
                CliConsole.Info($"The map name '{name}' is written as '{request.MapName}', as the editor would.");
            }

            return true;
        }

        private static bool TryReadProjectsDir(CliArguments args, CreateRequest request)
        {
            if (args.Has(OutputOption) == false)
            {
                return true;
            }

            if (CliPaths.TryGetFullPath(args.Value(OutputOption), "output folder", out var folder) == false)
            {
                return false;
            }

            request.ProjectsDir = folder;
            return true;
        }

        private static bool TryReadTemplate(CliArguments args, ProjectManager projectManager, CreateRequest request)
        {
            if (args.Has(GameOption) || args.Has(WidthOption) || args.Has(HeightOption))
            {
                CliConsole.Error($"A template sets the game and map size itself, so '{TemplateOption}' cannot be combined with " +
                                 $"'{GameOption}', '{WidthOption}' or '{HeightOption}'.");
                return false;
            }

            var template = GetTemplates(projectManager).FirstOrDefault(t => string.Equals(t, request.Template, StringComparison.OrdinalIgnoreCase));
            if (template == null)
            {
                CliConsole.Error($"No template named '{request.Template}'. List them with: create {ListTemplatesOption}");
                return false;
            }

            request.Template = template;
            return true;
        }

        private static bool TryReadGameAndSize(CliArguments args, CreateRequest request)
        {
            var gameName = args.Value(GameOption);
            if (gameName == null)
            {
                CliConsole.Error($"Missing required option '{GameOption} <game>' (or use '{TemplateOption} <template>').");
                return false;
            }

            if (CliNames.TryParseGame(gameName, out request.Game) == false)
            {
                CliConsole.Error($"Unknown game '{gameName}'. Games: {string.Join(", ", CliNames.AllGames.Select(CliNames.Game))}.");
                return false;
            }

            return TryReadDimension(args, WidthOption, ref request.Width)
                && TryReadDimension(args, HeightOption, ref request.Height)
                && ValidateMapSize(request.Width, request.Height);
        }

        private static bool TryReadDimension(CliArguments args, string option, ref uint dimension)
        {
            var value = args.Value(option);
            if (value == null)
            {
                return true;
            }

            if (uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out dimension))
            {
                return true;
            }

            CliConsole.Error($"'{option}' must be a whole number, not '{value}'.");
            return false;
        }

        private static bool ValidateMapSize(uint width, uint height)
        {
            if (width == 0 || height == 0)
            {
                CliConsole.Error("A map can't have no hexes - width and height must both be at least 1.");
                return false;
            }

            if (width % 2 == 1)
            {
                CliConsole.Error($"The map's width needs to be even, but it is {width}.");
                return false;
            }

            if ((ulong)width * height > (ulong)MapHexFile.MAX_HEX_COUNT)
            {
                CliConsole.Warning($"{width} x {height} exceeds the recommended hex count ({MapHexFile.MAX_HEX_COUNT}).");
            }

            return true;
        }

        private static int Create(ProjectManager projectManager, CreateRequest request, bool overwrite)
        {
            var projectPath = ProjectManager.GetProjectPath(request.ProjectsDir, request.MapName);
            if (Directory.Exists(projectPath) && overwrite == false)
            {
                CliConsole.Error($"A project already exists at '{projectPath}'. Pass {ForceOption} to delete and replace it.");
                return CliConsole.ExitUsageError;
            }

            var project = projectManager.CreateProject(request.ProjectsDir, request.MapName, request.Width, request.Height,
                                                       request.Game, request.Template, () => overwrite);

            try
            {
                if (project == null)
                {
                    CliConsole.Error("The project could not be created. See the messages above for details.");
                    return CliConsole.ExitProcessingError;
                }

                var source = request.UsesTemplate ? $"from template '{request.Template}'" : "from scratch";
                CliConsole.Info($"Created project '{request.MapName}' {source}: {project.Game}, {project.MapWidth} x {project.MapHeight} hexes.");
                if (project.MapName != request.MapName)
                {
                    CliConsole.Info($"The campaign map inside is named '{project.MapName}', as the template sets.");
                }

                CliConsole.Info($"Map file: {project.FileName}");
                return CliConsole.ExitSuccess;
            }
            finally
            {
                project?.CleanupRpfmSession();
                projectManager.CloseProject();
            }
        }

        private static int ListTemplates(ProjectManager projectManager)
        {
            var templates = GetTemplates(projectManager);
            if (templates.Length == 0)
            {
                CliConsole.Info($"No templates found in '{projectManager.TemplatesPath}'.");
                return CliConsole.ExitSuccess;
            }

            CliConsole.Info($"Templates in '{projectManager.TemplatesPath}':");
            foreach (var template in templates)
            {
                CliConsole.Info($"  {template}");
            }

            return CliConsole.ExitSuccess;
        }

        private static string[] GetTemplates(ProjectManager projectManager)
        {
            if (Directory.Exists(projectManager.TemplatesPath) == false)
            {
                return Array.Empty<string>();
            }

            return Directory.GetDirectories(projectManager.TemplatesPath).Select(Path.GetFileName).ToArray();
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            Option(help, "--name, -n <map-name>", "Campaign map name. Also names the project folder.\nRequired. Written the way the editor writes it:\nlower case, spaces become '_', punctuation is dropped.");
            Option(help, "--game, -g <game>", "Game the map is for. Required unless --template is used.\nOne of: " + string.Join(", ", CliNames.AllGames.Select(CliNames.Game)) + ".");
            Option(help, "--width <n>", $"Map width in hexes; must be even. Default {DefaultWidth}.");
            Option(help, "--height <n>", $"Map height in hexes. Default {DefaultHeight}.");
            Option(help, "--template, -t <template>", "Start from a bundled template instead. The template\nsets the game, map size and campaign map name.");
            Option(help, "--output, -o <folder>", "Folder to create the project folder in.\nDefault: the editor's Projects folder.");
            Option(help, "--force", "Delete and replace an existing project folder of the\nsame name. Without it, an existing project is an error.");
            Option(help, "--list-templates", "List the available templates and exit.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("--name my_map --game warhammer3 --width 800 --height 600");
            help.AppendLine("--name my_map --game rome2 --output \"D:\\mods\\maps\"");
            help.AppendLine("--list-templates");
            help.AppendLine("--name my_map --template <template>");
        }
    }
}
