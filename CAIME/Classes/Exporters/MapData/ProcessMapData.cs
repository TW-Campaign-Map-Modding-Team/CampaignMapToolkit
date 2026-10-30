using System;
using System.IO;

namespace CAIME.Exporters
{
    public static class ProcessMapData
    {
        /// <summary>
        /// Writes the project's map_data.esf with its game's writer (<see cref="MapDataWriter"/>) into
        /// <paramref name="outputPath"/>. The playable area comes from the project's database
        /// tables, read through RPFM.
        /// </summary>
        public static bool Process(Project project, string outputPath)
        {
            if (project.RpfmTables == null)
            {
                LoggerViewModel.Log("map_data.esf was not processed: the project's database was not read through RPFM. " +
                                    "Set the RPFM path in Settings > Preferences and reopen the project.", LogLevel.ErrorMessageBox);
                return false;
            }

            var outputFilePath = Path.Combine(outputPath, "map_data.esf");

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
                var theatre = MapDataWriter.ReadTheatre(project.Game, project.MapName, project.RpfmTables[Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS].Rows);
                mapData     = MapDataWriter.Write(project.Game, map, theatre, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            // ArgumentException: a game with no writer, or a map too small to build.
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
