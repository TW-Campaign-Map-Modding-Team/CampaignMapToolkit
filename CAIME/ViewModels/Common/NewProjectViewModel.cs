using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace CAIME
{
    class NewProjectViewModel : BaseViewModel
    {
        private readonly ProjectManager _projectManager;

        public string CampaignMapName { get; set; }

        private uint mapWidth;
        public string MapWidth
        {
            get
            {
                return mapWidth.ToString();
            }
            set
            {
                if (!uint.TryParse(value, out mapWidth))
                {
                    LoggerViewModel.Log("Map width value should be an integer!", LogLevel.Error);
                    MessageBox.Show("Error. Map width value should be an integer!", "Parsing error");
                }
                else
                {
                    OnPropertyChanged(nameof(MapWidth));
                }
            }
        }
        
        private uint mapHeight;
        public string MapHeight
        {
            get
            {
                return mapHeight.ToString();
            }
            set
            {
                if (!uint.TryParse(value, out mapHeight))
                {
                    LoggerViewModel.Log("Map height value should be an integer!", LogLevel.Error);
                    MessageBox.Show("Error. Map height value should be an integer!", "Parsing error");
                }
                else
                {
                    OnPropertyChanged(nameof(MapHeight));
                }
            }
        }

        private string selectedTemplate;
        /// <summary>
        /// Currently selected project template
        /// </summary>
        public string SelectedTemplate
        {
            get => selectedTemplate;
            set
            {
                selectedTemplate = value;
                OnPropertyChanged(nameof(SelectedTemplate));

                bool bFullSettings = selectedTemplate == ProjectManager.TEMPLATE_NONE;
                IsGameBoxEnabled = IsMapSizeXEnabled = IsMapSizeYEnabled = bFullSettings;
            }
        }

        private bool isGameBoxEnabled;
        /// <summary>
        /// Is Game dropdown list enabled?
        /// </summary>
        public bool IsGameBoxEnabled
        {
            get => isGameBoxEnabled;
            private set
            {
                isGameBoxEnabled = value;
                OnPropertyChanged(nameof(IsGameBoxEnabled));
            }
        }

        private bool isMapSizeXEnabled;
        /// <summary>
        /// Is map size X text box enabled?
        /// </summary>
        public bool IsMapSizeXEnabled
        {
            get => isMapSizeXEnabled;
            private set
            {
                isMapSizeXEnabled = value;
                OnPropertyChanged(nameof(IsMapSizeXEnabled));
            }
        }

        private bool isMapSizeYEnabled;
        /// <summary>
        /// Is map size Y text box enabled?
        /// </summary>
        public bool IsMapSizeYEnabled
        {
            get => isMapSizeYEnabled;
            private set
            {
                isMapSizeYEnabled = value;
                OnPropertyChanged(nameof(IsMapSizeYEnabled));
            }
        }
        
        /// <summary>
        /// List of all the templates, found under Tool's working directory Templates folder
        /// </summary>
        public string[] Templates { get; private set; }
        /// <summary>
        /// List of all supported games
        /// </summary>
        public GameTemplate[] Games { get; private set; }
        /// <summary>
        /// Currently selected game
        /// </summary>
        public GameTemplate SelectedGame { get; set; }

        public NewProjectViewModel(ProjectManager projectManager)
        {
            _projectManager = projectManager;
            var templatesFolderPath = projectManager.TemplatesPath;

            if (Directory.Exists(templatesFolderPath))
            {
                var templates = Directory.GetDirectories(templatesFolderPath)
                    .Select(Path.GetFileName)
                    .ToArray();

                Templates = new string[templates.Length + 1];

                for (int i = 0; i < templates.Length; ++i)
                {
                    Templates[i + 1] = templates[i];
                }

            }
            else
            {
                Templates = new string[1];
            }

            Templates[0] = ProjectManager.TEMPLATE_NONE;

            Games = new GameTemplate[(int)GameTemplate.Count];
            for (int i = 0; i < (int)GameTemplate.Count; ++i)
            {
                Games[i] = (GameTemplate)i;
            }

            MapWidth            = "1016";
            MapHeight           = "720";
            SelectedTemplate    = Templates[0];
            SelectedGame        = Games[0];
        }

        public bool CreateProject()
        {
            if (mapWidth % 2 == 1)
            {
                var msg = $"Map's Width needs to be even!";
                LoggerViewModel.Log(msg, LogLevel.Error);
                MessageBox.Show($"Error. {msg}", "Map resolution error");
                return false;
            }
            if (mapWidth * mapHeight <= 0)
            {
                var msg = $"Map can't have no hexes!";
                LoggerViewModel.Log(msg, LogLevel.Error);
                MessageBox.Show($"Error. {msg}", "Map resolution error");
                return false;
            }
            if (mapWidth * mapHeight > MapHexFile.MAX_HEX_COUNT)
            {
                var msg = $"Multitude of map's Width x Height exceeds recommended hexes count ({MapHexFile.MAX_HEX_COUNT})!";
                LoggerViewModel.Log(msg, LogLevel.Warning);
                MessageBox.Show($"Warning. {msg}", "Map resolution warning");
            }
            if (CampaignMapName == null || CampaignMapName.Length <= 0)
            {
                var msg = "Please, provide a map name to proceed!";
                LoggerViewModel.Log(msg, LogLevel.Error);
                MessageBox.Show($"Error. {msg}", "Missing campaign map name");
                return false;
            }

            _projectManager.CreateProject(CampaignMapName, mapWidth, mapHeight, SelectedGame, SelectedTemplate);
            return true;
        }
    }
}
