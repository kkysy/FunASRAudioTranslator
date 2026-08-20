using System.IO;

namespace LiveCaptionsTranslator.utils
{
    public static class AppDataPaths
    {
        public const string ApplicationDirectoryName = "LiveCaptionsTranslator";

        public static string ApplicationDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationDirectoryName);

        public static string ConfigurationPath => Path.Combine(ApplicationDirectory, "setting.json");

        public static string HistoryDatabasePath => Path.Combine(ApplicationDirectory, "translation_history.db");

        public static void EnsureApplicationDirectory()
        {
            Directory.CreateDirectory(ApplicationDirectory);
        }
    }
}
