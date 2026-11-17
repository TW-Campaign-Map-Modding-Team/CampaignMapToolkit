using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;

namespace CAIME
{
    class GameExportDefaults
    {
        static Tuple<bool, string> Land_Land_1  = new Tuple<bool, string>(true, "");
        static Tuple<bool, string> Land_TI_1    = new Tuple<bool, string>(false, " unusual");
        static Tuple<bool, string> Land_Sea_1   = new Tuple<bool, string>(false, " unusual, not recommended ");
        static Tuple<bool, string> Land_MI_1    = new Tuple<bool, string>(false, " unusual | won't be visible "); //TODO: check if visible

        static Tuple<bool, string> Sea_Sea_1    = new Tuple<bool, string>(true, "");
        static Tuple<bool, string> Sea_MI_1     = new Tuple<bool, string>(true, "");
        static Tuple<bool, string> Sea_Land_1   = new Tuple<bool, string>(false, " unusual, not recommended ");
        static Tuple<bool, string> Sea_TI_1     = new Tuple<bool, string>(false, " unusual | won't be visible "); //TODO: check if visible

        static Tuple<bool, string> TI_TI_1      = new Tuple<bool, string>(false, " unusual | won't be visible ");
        static Tuple<bool, string> TI_Land_1    = new Tuple<bool, string>(false, " unusual | won't be visible ");
        static Tuple<bool, string> TI_Sea_1     = new Tuple<bool, string>(false, " unusual | won't be visible "); //TODO: check if visible
        static Tuple<bool, string> TI_MI_1      = new Tuple<bool, string>(false, " unusual | won't be visible "); //TODO: check if visible
                                                                                                              
        static Tuple<bool, string> MI_MI_1      = new Tuple<bool, string>(false, " unusual | won't be visible ");
        static Tuple<bool, string> MI_Sea_1     = new Tuple<bool, string>(false, " unusual | won't be visible ");
        static Tuple<bool, string> MI_Land_1    = new Tuple<bool, string>(false, " unusual | won't be visible "); //TODO: check if visible
        static Tuple<bool, string> MI_TI_1      = new Tuple<bool, string>(false, " unusual | won't be visible "); //TODO: check if visible

        static Tuple<bool, string> Land_TI_2    = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> Land_Sea_2   = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> Land_MI_2    = new Tuple<bool, string>(true, " won't be visible ");
                                                                                                   
        static Tuple<bool, string> Sea_Sea_2    = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> Sea_MI_2     = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> Sea_Land_2   = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> Sea_TI_2     = new Tuple<bool, string>(true, " won't be visible ");
                                                                                                   
        static Tuple<bool, string> TI_TI_2      = new Tuple<bool, string>(true, " won't be visible "); //unsure but probably
        static Tuple<bool, string> TI_Land_2    = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> TI_Sea_2     = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> TI_MI_2      = new Tuple<bool, string>(true, " won't be visible ");
                                                                                                   
        static Tuple<bool, string> MI_MI_2      = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> MI_Sea_2     = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> MI_Land_2    = new Tuple<bool, string>(true, " won't be visible ");
        static Tuple<bool, string> MI_TI_2      = new Tuple<bool, string>(true, " won't be visible ");

        static Tuple<bool, string> Sea_Sea_3    = new Tuple<bool, string>(false, " unknown behaviour ");

        public static Dictionary<GameTemplate, Tuple<bool, string>> Land_Land = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Land_Land_1 },
            { GameTemplate.Attila,                  Land_Land_1 },
            { GameTemplate.Warhammer,               Land_Land_1 },
            { GameTemplate.Warhammer2,              Land_Land_1 },
            { GameTemplate.Thrones_Of_Britannia,    Land_Land_1 },
            { GameTemplate.Three_Kingdoms,          Land_Land_1 },
            { GameTemplate.Troy,                    Land_Land_1 }, //TODO
            { GameTemplate.Pharaoh,                 Land_Land_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Land_Land_1 }, //TODO
            { GameTemplate.Warhammer3,              Land_Land_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Land_TI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Land_TI_1 },
            { GameTemplate.Attila,                  Land_TI_1 },
            { GameTemplate.Warhammer,               Land_TI_1 },
            { GameTemplate.Warhammer2,              Land_TI_1 },
            { GameTemplate.Thrones_Of_Britannia,    Land_TI_1 },
            { GameTemplate.Three_Kingdoms,          Land_TI_2 },
            { GameTemplate.Troy,                    Land_TI_1 }, //TODO
            { GameTemplate.Pharaoh,                 Land_TI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Land_TI_1 }, //TODO
            { GameTemplate.Warhammer3,              Land_TI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Land_Sea = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Land_Sea_1 },
            { GameTemplate.Attila,                  Land_Sea_1 },
            { GameTemplate.Warhammer,               Land_Sea_1 },
            { GameTemplate.Warhammer2,              Land_Sea_1 },
            { GameTemplate.Thrones_Of_Britannia,    Land_Sea_1 },
            { GameTemplate.Three_Kingdoms,          Land_Sea_2 },
            { GameTemplate.Troy,                    Land_Sea_1 }, //TODO
            { GameTemplate.Pharaoh,                 Land_Sea_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Land_Sea_1 }, //TODO
            { GameTemplate.Warhammer3,              Land_Sea_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Land_MI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Land_MI_1 },
            { GameTemplate.Attila,                  Land_MI_1 },
            { GameTemplate.Warhammer,               Land_MI_1 },
            { GameTemplate.Warhammer2,              Land_MI_1 },
            { GameTemplate.Thrones_Of_Britannia,    Land_MI_1 },
            { GameTemplate.Three_Kingdoms,          Land_MI_2 },
            { GameTemplate.Troy,                    Land_MI_1 }, //TODO
            { GameTemplate.Pharaoh,                 Land_MI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Land_MI_1 }, //TODO
            { GameTemplate.Warhammer3,              Land_MI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Sea_Sea = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Sea_Sea_1 },
            { GameTemplate.Attila,                  Sea_Sea_1 },
            { GameTemplate.Warhammer,               Sea_Sea_1 },
            { GameTemplate.Warhammer2,              Sea_Sea_1 },
            { GameTemplate.Thrones_Of_Britannia,    Sea_Sea_1 },
            { GameTemplate.Three_Kingdoms,          Sea_Sea_2 },
            { GameTemplate.Troy,                    Sea_Sea_3 }, //TODO
            { GameTemplate.Pharaoh,                 Sea_Sea_3 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Sea_Sea_3 }, //TODO
            { GameTemplate.Warhammer3,              Sea_Sea_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Sea_MI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Sea_MI_1 },
            { GameTemplate.Attila,                  Sea_MI_1 },
            { GameTemplate.Warhammer,               Sea_MI_1 },
            { GameTemplate.Warhammer2,              Sea_MI_1 },
            { GameTemplate.Thrones_Of_Britannia,    Sea_MI_1 },
            { GameTemplate.Three_Kingdoms,          Sea_MI_2 },
            { GameTemplate.Troy,                    Sea_MI_1 }, //TODO
            { GameTemplate.Pharaoh,                 Sea_MI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Sea_MI_1 }, //TODO
            { GameTemplate.Warhammer3,              Sea_MI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Sea_Land = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Sea_Land_1 },
            { GameTemplate.Attila,                  Sea_Land_1 },
            { GameTemplate.Warhammer,               Sea_Land_1 },
            { GameTemplate.Warhammer2,              Sea_Land_1 },
            { GameTemplate.Thrones_Of_Britannia,    Sea_Land_1 },
            { GameTemplate.Three_Kingdoms,          Sea_Land_2 },
            { GameTemplate.Troy,                    Sea_Land_1 }, //TODO
            { GameTemplate.Pharaoh,                 Sea_Land_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Sea_Land_1 }, //TODO
            { GameTemplate.Warhammer3,              Sea_Land_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> Sea_TI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   Sea_TI_1 },
            { GameTemplate.Attila,                  Sea_TI_1 },
            { GameTemplate.Warhammer,               Sea_TI_1 },
            { GameTemplate.Warhammer2,              Sea_TI_1 },
            { GameTemplate.Thrones_Of_Britannia,    Sea_TI_1 },
            { GameTemplate.Three_Kingdoms,          Sea_TI_2 },
            { GameTemplate.Troy,                    Sea_TI_1 }, //TODO
            { GameTemplate.Pharaoh,                 Sea_TI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       Sea_TI_1 }, //TODO
            { GameTemplate.Warhammer3,              Sea_TI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> TI_TI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   TI_TI_1 },
            { GameTemplate.Attila,                  TI_TI_1 },
            { GameTemplate.Warhammer,               TI_TI_1 },
            { GameTemplate.Warhammer2,              TI_TI_1 },
            { GameTemplate.Thrones_Of_Britannia,    TI_TI_1 },
            { GameTemplate.Three_Kingdoms,          TI_TI_2 },
            { GameTemplate.Troy,                    TI_TI_1 }, //TODO
            { GameTemplate.Pharaoh,                 TI_TI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       TI_TI_1 }, //TODO
            { GameTemplate.Warhammer3,              TI_TI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> TI_Land = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   TI_Land_1 },
            { GameTemplate.Attila,                  TI_Land_1 },
            { GameTemplate.Warhammer,               TI_Land_1 },
            { GameTemplate.Warhammer2,              TI_Land_1 },
            { GameTemplate.Thrones_Of_Britannia,    TI_Land_1 },
            { GameTemplate.Three_Kingdoms,          TI_Land_2 },
            { GameTemplate.Troy,                    TI_Land_1 }, //TODO
            { GameTemplate.Pharaoh,                 TI_Land_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       TI_Land_1 }, //TODO
            { GameTemplate.Warhammer3,              TI_Land_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> TI_Sea = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   TI_Sea_1 },
            { GameTemplate.Attila,                  TI_Sea_1 },
            { GameTemplate.Warhammer,               TI_Sea_1 },
            { GameTemplate.Warhammer2,              TI_Sea_1 },
            { GameTemplate.Thrones_Of_Britannia,    TI_Sea_1 },
            { GameTemplate.Three_Kingdoms,          TI_Sea_2 },
            { GameTemplate.Troy,                    TI_Sea_1 }, //TODO
            { GameTemplate.Pharaoh,                 TI_Sea_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       TI_Sea_1 }, //TODO
            { GameTemplate.Warhammer3,              TI_Sea_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> TI_MI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   TI_MI_1 },
            { GameTemplate.Attila,                  TI_MI_1 },
            { GameTemplate.Warhammer,               TI_MI_1 },
            { GameTemplate.Warhammer2,              TI_MI_1 },
            { GameTemplate.Thrones_Of_Britannia,    TI_MI_1 },
            { GameTemplate.Three_Kingdoms,          TI_MI_2 },
            { GameTemplate.Troy,                    TI_MI_1 }, //TODO
            { GameTemplate.Pharaoh,                 TI_MI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       TI_MI_1 }, //TODO
            { GameTemplate.Warhammer3,              TI_MI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> MI_MI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   MI_MI_1 },
            { GameTemplate.Attila,                  MI_MI_1 },
            { GameTemplate.Warhammer,               MI_MI_1 },
            { GameTemplate.Warhammer2,              MI_MI_1 },
            { GameTemplate.Thrones_Of_Britannia,    MI_MI_1 },
            { GameTemplate.Three_Kingdoms,          MI_MI_2 },
            { GameTemplate.Troy,                    MI_MI_1 }, //TODO
            { GameTemplate.Pharaoh,                 MI_MI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       MI_MI_1 }, //TODO
            { GameTemplate.Warhammer3,              MI_MI_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> MI_Sea = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   MI_Sea_1 },
            { GameTemplate.Attila,                  MI_Sea_1 },
            { GameTemplate.Warhammer,               MI_Sea_1 },
            { GameTemplate.Warhammer2,              MI_Sea_1 },
            { GameTemplate.Thrones_Of_Britannia,    MI_Sea_1 },
            { GameTemplate.Three_Kingdoms,          MI_Sea_2 },
            { GameTemplate.Troy,                    MI_Sea_1 }, //TODO
            { GameTemplate.Pharaoh,                 MI_Sea_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       MI_Sea_1 }, //TODO
            { GameTemplate.Warhammer3,              MI_Sea_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> MI_Land = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   MI_Land_1 },
            { GameTemplate.Attila,                  MI_Land_1 },
            { GameTemplate.Warhammer,               MI_Land_1 },
            { GameTemplate.Warhammer2,              MI_Land_1 },
            { GameTemplate.Thrones_Of_Britannia,    MI_Land_1 },
            { GameTemplate.Three_Kingdoms,          MI_Land_2 },
            { GameTemplate.Troy,                    MI_Land_1 }, //TODO
            { GameTemplate.Pharaoh,                 MI_Land_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       MI_Land_1 }, //TODO
            { GameTemplate.Warhammer3,              MI_Land_1 }
        };
        public static Dictionary<GameTemplate, Tuple<bool, string>> MI_TI = new Dictionary<GameTemplate, Tuple<bool, string>>()
        {
            { GameTemplate.Rome2,                   MI_TI_1 },
            { GameTemplate.Attila,                  MI_TI_1 },
            { GameTemplate.Warhammer,               MI_TI_1 },
            { GameTemplate.Warhammer2,              MI_TI_1 },
            { GameTemplate.Thrones_Of_Britannia,    MI_TI_1 },
            { GameTemplate.Three_Kingdoms,          MI_TI_2 },
            { GameTemplate.Troy,                    MI_TI_1 }, //TODO
            { GameTemplate.Pharaoh,                 MI_TI_1 }, //TODO
            { GameTemplate.Pharaoh_Dynasties,       MI_TI_1 }, //TODO
            { GameTemplate.Warhammer3,              MI_TI_1 }
        };
    }

    internal class BorderExportOption : ObservableObject
    {
        private Dictionary<GameTemplate, Tuple<bool, string>> _map;
        private GameTemplate _game;

        public string SourceRegion { get; private set; }
        public string TargetRegion { get; private set; }

        private bool? _export = null;
        public bool Export
        {
            get
            {
                return _export is null ? _map[_game].Item1 : _export.Value;
            }
            set
            {
                _export = value;
            }
        }

        public string Notes => _map[_game].Item2;

        public BorderExportOption(Project project, string sourceRegion, string targetRegion, Dictionary<GameTemplate, Tuple<bool, string>> map)
        {
            SourceRegion = sourceRegion;
            TargetRegion = targetRegion;

            _game = project.Game;
            _map = map;
        }

        public DataRow Row { get; set; } // Used to avoid binding error

        public void Reset()
        {
            _export = null;
            OnPropertyChanged(nameof(Export));
        }

        public static implicit operator bool(BorderExportOption obj) => obj.Export;
    }

    class BorderExportOptionsViewModel : BaseViewModel
    {
        public static BorderExportOption Land_Land  { get; private set; }
        public static BorderExportOption Land_TI    { get; private set; }
        public static BorderExportOption Land_Sea   { get; private set; }
        public static BorderExportOption Land_MI    { get; private set; }

        public static BorderExportOption Sea_Sea    { get; private set; }
        public static BorderExportOption Sea_MI     { get; private set; }
        public static BorderExportOption Sea_Land   { get; private set; }
        public static BorderExportOption Sea_TI     { get; private set; }

        public static BorderExportOption TI_TI      { get; private set; }
        public static BorderExportOption TI_Land    { get; private set; }
        public static BorderExportOption TI_Sea     { get; private set; }
        public static BorderExportOption TI_MI      { get; private set; }

        public static BorderExportOption MI_MI      { get; private set; }
        public static BorderExportOption MI_Sea     { get; private set; }
        public static BorderExportOption MI_Land    { get; private set; }
        public static BorderExportOption MI_TI      { get; private set; }

        public static ObservableCollection<BorderExportOption> ExportOptions { get; private set; }

        public void Reset(object sender, ProjectEventArgs e)
        {
            foreach (BorderExportOption option in ExportOptions)
            {
                option.Reset();
            }
        }

        public BorderExportOptionsViewModel(Project project)
        {
            SeedGameDefaults(project);
        }

        public static void SeedGameDefaultsIfUnset(Project project)
        {
            if (ExportOptions == null)
            {
                SeedGameDefaults(project);
            }
        }

        private static void SeedGameDefaults(Project project)
        {
            ExportOptions = new ObservableCollection<BorderExportOption>();

            // SourceRegion_TargetRegion
            // TI = Terra Incognita, used as synonym for all land regions without sprawl data
            // MI = Mare Incognita, used as synomen for all sea regions without sprawl data

            Land_Land   = new BorderExportOption(project, "Land", "Land", GameExportDefaults.Land_Land);
            Land_TI     = new BorderExportOption(project, "Land", "Land region without town sprawl", GameExportDefaults.Land_TI);
            Land_Sea    = new BorderExportOption(project, "Land", "Sea", GameExportDefaults.Land_Sea);
            Land_MI     = new BorderExportOption(project, "Land", "Sea region without town sprawl (port)", GameExportDefaults.Land_MI);

            Sea_Sea     = new BorderExportOption(project, "Sea", "Sea", GameExportDefaults.Sea_Sea);
            Sea_MI      = new BorderExportOption(project, "Sea", "Sea region without town sprawl (port)", GameExportDefaults.Sea_MI);
            Sea_Land    = new BorderExportOption(project, "Sea", "Land", GameExportDefaults.Sea_Land);
            Sea_TI      = new BorderExportOption(project, "Sea", "Land region without town sprawl", GameExportDefaults.Sea_TI);

            TI_TI       = new BorderExportOption(project, "Land region without town sprawl", "Land region without town sprawl", GameExportDefaults.TI_TI);
            TI_Land     = new BorderExportOption(project, "Land region without town sprawl", "Land", GameExportDefaults.TI_Land);
            TI_Sea      = new BorderExportOption(project, "Land region without town sprawl", "Sea", GameExportDefaults.TI_Sea);
            TI_MI       = new BorderExportOption(project, "Land region without town sprawl", "Sea region without town sprawl (port)", GameExportDefaults.TI_MI);

            MI_MI       = new BorderExportOption(project, "Sea region without town sprawl (port)", "Sea region without town sprawl (port)", GameExportDefaults.MI_MI);
            MI_Sea      = new BorderExportOption(project, "Sea region without town sprawl (port)", "Sea", GameExportDefaults.MI_Sea);
            MI_Land     = new BorderExportOption(project, "Sea region without town sprawl (port)", "Land", GameExportDefaults.MI_Land);
            MI_TI       = new BorderExportOption(project, "Sea region without town sprawl (port)", "Land region without town sprawl", GameExportDefaults.MI_TI);

            ExportOptions.Add(Land_Land);
            ExportOptions.Add(Land_TI);
            ExportOptions.Add(Land_Sea);
            ExportOptions.Add(Land_MI);

            ExportOptions.Add(Sea_Sea);
            ExportOptions.Add(Sea_MI);
            ExportOptions.Add(Sea_Land);
            ExportOptions.Add(Sea_TI);

            ExportOptions.Add(TI_TI);
            ExportOptions.Add(TI_Land);
            ExportOptions.Add(TI_Sea);
            ExportOptions.Add(TI_MI);

            ExportOptions.Add(MI_MI);
            ExportOptions.Add(MI_Sea);
            ExportOptions.Add(MI_Land);
            ExportOptions.Add(MI_TI);
        }
    }
}
