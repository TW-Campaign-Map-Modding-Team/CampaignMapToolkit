using System;
using System.IO;

namespace CAIME.Exporters
{
    internal static class ProcessDynamicResources
    {
        private const string IMAGE_FILE_NAME    = "dynamic_resources.png";
        private const string DATABASE_FILE_NAME = "dynamic_resources_database.xml";

        /// <summary>
        /// Writes the project's dynamic_resources.esf (<see cref="DynamicResourcesWriter"/>) from its
        /// saved map.hex, dynamic_resources.png and dynamic_resources_database.xml to the Assembly
        /// Kit's working_data, next to map_data.esf.
        /// </summary>
        public static bool Process(Project project)
        {
            var imagePath       = Path.Combine(project.ProjectPath, IMAGE_FILE_NAME);
            var databasePath    = Path.Combine(project.ProjectPath, DATABASE_FILE_NAME);

            foreach (var input in new[] { imagePath, databasePath })
            {
                if (File.Exists(input) == false)
                {
                    LoggerViewModel.Log($"dynamic_resources.esf was not processed: {input} was not found.", LogLevel.ErrorMessageBox);
                    return false;
                }
            }

            // The saved map.hex, not the one open in the editor: saving can reorder its regions.
            var map = new MapHexFile();
            if (map.Load(project.FileName) == false)
            {
                LoggerViewModel.Log($"dynamic_resources.esf was not processed: {project.FileName} could not be read.", LogLevel.ErrorMessageBox);
                return false;
            }

            var asskitPath      = PreferencesViewModel.Instance.GetAssKitPath(project.Game).Replace('\\', '/');
            var outputFilePath  = $"{asskitPath}/working_data/campaign_maps/{project.MapName}/dynamic_resources.esf";

            try
            {
                var regions = DynamicResourceRegions.Find(map, DynamicResourcesImage.Load(imagePath), DynamicResourceType.LoadDatabase(databasePath), project.Game);
                var bytes   = DynamicResourcesWriter.Write(regions, DateTime.UtcNow, project.Game);

                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                File.WriteAllBytes(outputFilePath, bytes);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is ArgumentException || ex is System.Xml.XmlException)
            {
                LoggerViewModel.Log($"dynamic_resources.esf was not processed: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }

            LoggerViewModel.Log($"Processed dynamic resources file was exported to {outputFilePath}", LogLevel.Info);
            return true;
        }
    }
}
