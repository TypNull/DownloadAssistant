using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace DownloadAssistant.Utilities
{
    internal static class IOManager
    {
        public static char[] InvalidFileNameChars = new char[]
          {
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '?', '\\', '/'
          };
        /// <summary>
        /// Converts Bytes to Megabytes
        /// InputBytes : 1.048.576
        /// </summary>
        /// <param name="bytes">Bytes to convert</param>
        /// <returns>Convertet megabytes value as double</returns>
        public static double BytesToMegabytes(long bytes) => bytes / 1048576;

        /// <summary>
        /// Removes all invalid Characters for a filename out of a string
        /// </summary>
        /// <param name="name">input filename</param>
        /// <returns>Clreared filename</returns>
        public static string RemoveInvalidFileNameChars(string name)
        {
            StringBuilder fileBuilder = new(name);
            foreach (char c in InvalidFileNameChars)
                fileBuilder.Replace(c.ToString(), string.Empty);
            return fileBuilder.ToString();
        }




        /// <summary>
        /// Gets a value that indicates whether <paramref name="path"/>
        /// is a valid path.
        /// </summary>
        /// <returns>Returns <c>true</c> if <paramref name="path"/> is a
        /// valid path; <c>false</c> otherwise. Also returns <c>false</c> if
        /// the caller does not have the required permissions to access
        /// <paramref name="path"/>.
        /// </returns>
        /// <seealso cref="Path.GetFullPath(string)"/>
        /// <seealso cref="TryGetFullPath"/>
        public static bool IsValidPath(string path) => TryGetFullPath(path, out _);


        /// <summary>
        /// Returns the absolute path for the specified path string. A return
        /// value indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="path">The file or directory for which to obtain absolute
        /// path information.
        /// </param>
        /// <param name="result">When this method returns, contains the absolute
        /// path representation of <paramref name="path"/>, if the conversion
        /// succeeded, or <see cref="string.Empty"/> if the conversion failed.
        /// The conversion fails if <paramref name="path"/> is null or
        /// <see cref="string.Empty"/>, or is not of the correct format. This
        /// parameter is passed uninitialized; any value originally supplied
        /// in <paramref name="result"/> will be overwritten.
        /// </param>
        /// <returns><c>true</c> if <paramref name="path"/> was converted
        /// to an absolute path successfully; otherwise, false.
        /// </returns>
        /// <seealso cref="Path.GetFullPath(string)"/>
        /// <seealso cref="IsValidPath"/>
        public static bool TryGetFullPath(string path, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || path[1] != ':')
                return false;
            bool status = false;

            try
            {
                result = Path.GetFullPath(path);
                status = true;
            }
            catch (ArgumentException) { }
            catch (SecurityException) { }
            catch (NotSupportedException) { }
            catch (PathTooLongException) { }

            return status;
        }

        /// <summary>
        /// Gets the Home or Desktop path
        /// </summary>
        /// <returns>Returns Path to Desktop</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="SecurityException"></exception>
        public static string? GetHomePath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                return Environment.GetEnvironmentVariable("HOME");
            return Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
        }

        /// <summary>
        /// Gets the download folder path
        /// </summary>
        /// <returns>A path to the download folder</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="SecurityException"></exception>
        public static string? GetDownloadFolderPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string? homePath = GetHomePath();
                if (string.IsNullOrEmpty(homePath))
                    return null;
                return Path.Combine(homePath, "Downloads");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string path = SHGetKnownFolderPath(new("374DE290-123F-4565-9164-39C4925E467B"), 0);
                if (string.IsNullOrEmpty(path))
                    return Convert.ToString(
                    Registry.GetValue(
                         @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"
                        , "{374DE290-123F-4565-9164-39C4925E467B}"
                        , string.Empty));
                else return path;
            }
            else return null;
        }

        [DllImport("shell32",
        CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
        nint hToken = 0);


        /// <summary>
        /// Moves a file to another destination. If it existst it will be overwritten
        /// </summary>
        /// <param name="path">Source file path</param>
        /// <param name="destination">Destination file path</param>
        public static void Move(string path, string destination) => File.Move(path, destination, true);

        /// <summary>
        /// Creates a file or clears an existing one
        /// </summary>
        /// <param name="path">Path to the file that should be created</param>
        public static void Create(string path) => File.Create(path).Close();
    }
}


