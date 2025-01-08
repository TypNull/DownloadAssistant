using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace DownloadAssistant.Utilities
{
    /// <summary>
    /// Provides utility methods for I/O operations.
    /// </summary>
    internal static class IOManager
    {
        /// <summary>
        /// Array of characters that are not allowed in file names.
        /// </summary>
        public static char[] InvalidFileNameCharsWindows = new char[]
        {
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '?', '\\', '/', '*'
        };

        public static char[] InvalidFileNameCharsUnix = new char[] { '/', '\0' };

        /// <summary>
        /// Converts bytes to megabytes.
        /// </summary>
        /// <param name="bytes">The number of bytes to convert.</param>
        /// <returns>The converted value in megabytes as a double.</returns>
        /// <remarks>
        /// For example, if <paramref name="bytes"/> is 1,048,576, the return value is 1.
        /// </remarks>
        public static double BytesToMegabytes(long bytes) => bytes / 1048576;

        /// <summary>
        /// Removes all invalid characters from a file name.
        /// </summary>
        /// <param name="name">The file name to clean.</param>
        /// <returns>The cleaned file name.</returns>
        public static string RemoveInvalidFileNameChars(string name)
        {
            StringBuilder fileBuilder = new(name);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                foreach (char c in InvalidFileNameCharsWindows.Concat(Path.GetInvalidFileNameChars()))
                    fileBuilder.Replace(c.ToString(), string.Empty);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                foreach (char c in InvalidFileNameCharsUnix.Concat(Path.GetInvalidFileNameChars()))
                    fileBuilder.Replace(c.ToString(), string.Empty);
            return fileBuilder.ToString();
        }



        /// <summary>
        /// Determines whether a path is valid.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="path"/> is a valid path; otherwise, <c>false</c>.
        /// Also returns <c>false</c> if the caller does not have the required permissions to access <paramref name="path"/>.
        /// </returns>
        public static bool IsValidPath(string path) => TryGetFullPath(path, out _);


        public static bool TryGetFullPath(string path, out string result)
        {
            result = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (path.Length < 2 || path[1] != ':')
                    return false;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                char[] invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                    return false;

                if (Encoding.UTF8.GetByteCount(path) > 4096)
                    return false;

                if (!path.StartsWith("/") && !path.StartsWith("./") && !path.StartsWith("../"))
                    return false;
            }

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
        /// Gets the home directory path.
        /// </summary>
        /// <returns>The path to the home directory, or <c>null</c> if the path cannot be determined.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the environment variable for the home directory is not set on Unix platforms.</exception>
        /// <exception cref="SecurityException">Thrown when the caller does not have the required permission to perform this operation.</exception>
        public static string? GetHomePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Environment.GetEnvironmentVariable("HOME");
            throw new PlatformNotSupportedException("The platform is not supported.");
        }

        /// <summary>
        /// Gets the path to the downloads folder.
        /// </summary>
        /// <returns>The path to the downloads folder, or <c>null</c> if the path cannot be determined.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the home path is null or an empty string on Unix platforms.</exception>
        /// <exception cref="ArgumentException">Thrown when the path to the downloads folder is not of the correct format.</exception>
        /// <exception cref="SecurityException">Thrown when the caller does not have the required permission to perform this operation.</exception>
        public static string? GetDownloadFolderPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string? path = null;
                try
                {
                    path = SHGetKnownFolderPath(new("374DE290-123F-4565-9164-39C4925E467B"), 0);
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
                catch { }

                try
                {
                    path = Convert.ToString(
                     Registry.GetValue(
                          @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"
                         , "{374DE290-123F-4565-9164-39C4925E467B}"
                         , string.Empty));
                }
                catch { }

                return string.IsNullOrEmpty(path) ? null : path;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string? homePath = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(homePath))
                    return null;
                return Path.Combine(homePath, "Downloads");
            }
            else return null;
        }

        [DllImport("shell32",
        CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
        nint hToken = 0);


        /// <summary>
        /// Moves a file to a new location, overwriting the existing file if it exists.
        /// </summary>
        /// <param name="path">The path of the file to move.</param>
        /// <param name="destination">The path to the new location for the file.</param>
        public static void Move(string path, string destination) => File.Move(path, destination, true);

        /// <summary>
        /// Creates a new file or overwrites an existing file.
        /// </summary>
        /// <param name="path">The path of the file to create.</param>
        public static void Create(string path) => File.Create(path).Close();
    }
}


