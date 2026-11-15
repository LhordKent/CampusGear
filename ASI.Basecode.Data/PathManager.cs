namespace ASI.Basecode.Data
{
    /// <summary>
    /// Central place for the folders the application writes to on disk.
    /// </summary>
    public class PathManager
    {
        public static class DirectoryPath
        {
            /// <summary>Folder that holds equipment photos uploaded through Admin &gt; Equipment.</summary>
            /// <param name="webRootPath">The application's wwwroot folder.</param>
            public static string EquipmentUploadsDirectory(string webRootPath)
            {
                return Path.Combine(webRootPath, "uploads", "equipment");
            }
        }

        /// <summary>Returns the folder path, creating the folder first when it does not exist yet.</summary>
        public static string GetFolderPath(string path, string folderName = "")
        {
            string result = Path.Combine(path, folderName);
            if (!Directory.Exists(result))
            {
                Directory.CreateDirectory(result);
            }

            return result;
        }
    }
}
