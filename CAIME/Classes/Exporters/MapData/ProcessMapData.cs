using System;
using System.IO;

namespace CAIME.Exporters
{
    public static class ProcessMapData
    {
        /// <summary>
        /// Writes the project's map_data.esf with its game's writer (<see cref="MapDataWriter"/>) to
        /// the Assembly Kit's working_data, where the Assembly Kit's own builder used to put it. The
        /// playable area comes from the project's RPFM database tables, never the Assembly Kit's.
        /// </summary>
        public static bool Process(Project project)
        {
            if (project.RpfmTables == null)
            {
                LoggerViewModel.Log("map_data.esf is built from the database tables RPFM reads from the game's packs. " +
                                    "Set Database source to RPFM in Settings > Preferences and reopen the project.", LogLevel.ErrorMessageBox);
                return false;
            }

            var asskitPath      = PreferencesViewModel.Instance.GetAssKitPath(project.Game).Replace('\\', '/');
            var outputFilePath  = $"{asskitPath}/working_data/campaign_maps/{project.MapName}/map_data.esf";

            // The saved map.hex, not the one open in the editor: saving can reorder its regions.
            var map = new MapHexFile();
            if (map.Load(project.FileName) == false)
            {
                LoggerViewModel.Log($"map_data.esf was not processed: {project.FileName} could not be read.", LogLevel.ErrorMessageBox);
                return false;
            }

            byte[] mapData;
            try
            {
                var theatre = MapDataWriter.ReadTheatre(project.Game, project.MapName, project.RpfmTables[Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS]);
                mapData     = MapDataWriter.Write(project.Game, map, theatre, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            // ArgumentException: a game with no writer, a map too small to build, or a name that is not ASCII.
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                LoggerViewModel.Log($"map_data.esf was not processed: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                File.WriteAllBytes(outputFilePath, mapData);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LoggerViewModel.Log($"Could not write map_data.esf to {outputFilePath}: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }

            LoggerViewModel.Log($"Processed map data file was exported to {outputFilePath}", LogLevel.Info);
            if (PreferencesViewModel.Instance.IsMapDataConfigPostProcessEnabled)
            {
                MapDataConfigPostProcessor.PostProcess(project, outputFilePath);
            }

            return true;
        }
    }
}
