using System.Runtime.InteropServices;

namespace DownloadAssistant.Base
{
    /// <summary>
    /// Provides functionality to generate User-Agent strings tailored to different operating systems and platforms
    /// </summary>
    public static class UserAgentBuilder
    {
        /// <summary>
        /// Generates a User-Agent string based on the current operating system and environment
        /// </summary>
        /// <returns>A formatted User-Agent string containing platform-specific information</returns>
        public static string Generate()
        {
            (OSPlatform osPlatform, Version osVersion) = GetOSInfo();
            PlatformTemplate template = GetPlatformTemplate(osPlatform, osVersion);
            return ComposeUserAgent(template, GetArchitecture(), GetDeviceModel());
        }

        /// <summary>
        /// Constructs a User-Agent string using the specified template and system information
        /// </summary>
        /// <param name="template">Platform-specific template containing format string</param>
        /// <param name="architecture">System architecture information</param>
        /// <param name="deviceModel">Device model information (optional)</param>
        /// <returns>Complete User-Agent string with all placeholders replaced</returns>
        private static string ComposeUserAgent(
            PlatformTemplate template,
            string architecture,
            string deviceModel = "")
        {
            const string chromeVersion = "124.0.6367.79";
            const string webKitToken = "AppleWebKit/537.36 (KHTML, like Gecko)";
            string buildToken = DateTime.UtcNow.ToString("yyyyMMdd");

            Dictionary<string, object> values = new()
            {
                ["{OsMajor}"] = template.OsVersion.Major,
                ["{OsMinor}"] = template.OsVersion.Minor,
                ["{OsBuild}"] = template.OsVersion.Build,
                ["{Arch}"] = architecture,
                ["{Device}"] = deviceModel,
                ["{Chrome}"] = chromeVersion,
                ["{WebKit}"] = webKitToken,
                ["{Build}"] = buildToken
            };

            return ReplaceNamedPlaceholders(template.FormatString, values);
        }

        /// <summary>
        /// Replaces named placeholders in a format string with corresponding values
        /// </summary>
        /// <param name="format">String containing placeholders</param>
        /// <param name="values">Dictionary of placeholder-value pairs</param>
        /// <returns>Formatted string with replaced values</returns>
        private static string ReplaceNamedPlaceholders(
            string format,
            IReadOnlyDictionary<string, object> values)
        {
            foreach (KeyValuePair<string, object> kvp in values)
            {
                format = format.Replace(kvp.Key, kvp.Value.ToString() ?? string.Empty);
            }
            return format;
        }

        /// <summary>
        /// Represents a platform-specific User-Agent template
        /// </summary>
        /// <param name="FormatString">Format string with placeholders</param>
        /// <param name="OsVersion">Operating system version information</param>
        private record PlatformTemplate(string FormatString, Version OsVersion);

        /// <summary>
        /// Gets the appropriate User-Agent template for the specified platform
        /// </summary>
        /// <param name="osPlatform">Target operating system platform</param>
        /// <param name="osVersion">Operating system version</param>
        /// <returns>Platform-specific User-Agent template</returns>
        private static PlatformTemplate GetPlatformTemplate(
            OSPlatform osPlatform,
            Version osVersion)
        {
            Dictionary<OSPlatform, Func<Version, PlatformTemplate>> templates = new()
            {
                [OSPlatform.Windows] = v => new(
                    "Mozilla/5.0 (Windows NT {OsMajor}.{OsMinor}; {Arch}) " +
                    "{WebKit} Chrome/{Chrome} Safari/537.36 Build/{Build}",
                    v
                ),
                [OSPlatform.OSX] = v => new(
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X {OsMajor}_{OsMinor}_{OsBuild}) " +
                    "{WebKit} Chrome/{Chrome} Safari/537.36 Build/{Build}",
                    v
                ),
                [OSPlatform.Linux] = v => IsAndroid()
                    ? new(
                        "Mozilla/5.0 (Linux; Android {OsMajor}.{OsMinor}; {Arch}; {Device}) " +
                        "{WebKit} Chrome/{Chrome} Mobile Safari/537.36 Build/{Build}",
                        v
                      )
                    : IsTizen()
                    ? new(
                        "Mozilla/5.0 (Mobile; Tizen {OsMajor}.{OsMinor}; {Arch}) " +
                        "{WebKit} Chrome/{Chrome} Mobile Safari/537.36 Build/{Build}",
                        v
                      )
                    : new(
                        "Mozilla/5.0 (X11; Linux {Arch}) " +
                        "{WebKit} Chrome/{Chrome} Safari/537.36 Build/{Build}",
                        v
                      )
            };

            return templates.TryGetValue(osPlatform, out Func<Version, PlatformTemplate>? templateFactory)
                ? templateFactory(osVersion)
                : new PlatformTemplate(
                    $"Mozilla/5.0 ({osPlatform}; {{OsMajor}}) {{WebKit}} Chrome/{{Chrome}} Build/{{Build}}",
                    osVersion
                  );
        }

        /// <summary>
        /// Detects the current operating system and its version
        /// </summary>
        /// <returns>
        /// Tuple containing detected OS platform and version information
        /// </returns>
        private static (OSPlatform Platform, Version Version) GetOSInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return (OSPlatform.Windows, Environment.OSVersion.Version);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return (OSPlatform.OSX, ParseDarwinVersion(Environment.OSVersion.Version));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return (OSPlatform.Linux, IsAndroid() || IsTizen() ? Environment.OSVersion.Version : new Version(0, 0));

            return (OSPlatform.Create("Other"), new Version());
        }

        /// <summary>
        /// Converts Darwin kernel version to macOS version number
        /// </summary>
        /// <param name="darwinVersion">Darwin kernel version</param>
        /// <returns>Mapped macOS version number</returns>
        private static Version ParseDarwinVersion(Version darwinVersion) => darwinVersion.Major switch
        {
            23 => new Version(14, darwinVersion.Minor),  // macOS Sonoma
            22 => new Version(13, darwinVersion.Minor),  // macOS Ventura
            21 => new Version(12, darwinVersion.Minor),  // macOS Monterey
            20 => new Version(11, darwinVersion.Minor),  // macOS Big Sur
            _ => darwinVersion
        };

        /// <summary>
        /// Gets the system architecture in standardized format
        /// </summary>
        /// <returns>
        /// Normalized architecture string (x86_64, arm64, armv7l, or OS architecture)
        /// </returns>
        private static string GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "armv7l",
            _ => RuntimeInformation.OSArchitecture.ToString().ToLower()
        };

        /// <summary>
        /// Determines if the current OS is Android
        /// </summary>
        /// <returns>True if running on Android, false otherwise</returns>
        private static bool IsAndroid() =>
            RuntimeInformation.OSDescription.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
            Environment.OSVersion.VersionString.Contains("Android");

        /// <summary>
        /// Determines if the current OS is Tizen
        /// </summary>
        /// <returns>True if running on Tizen, false otherwise</returns>
        private static bool IsTizen() =>
            RuntimeInformation.OSDescription.Contains("Tizen", StringComparison.OrdinalIgnoreCase) ||
            Environment.OSVersion.VersionString.Contains("Tizen");

        /// <summary>
        /// Attempts to retrieve the Android device model
        /// </summary>
        /// <returns>
        /// Device model name if available, "Mobile" as fallback, or "Generic Device" for non-Android systems
        /// </returns>
        private static string GetDeviceModel()
        {
            if (IsAndroid() && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Environment.GetEnvironmentVariable("ANDROID_MODEL") ?? "Mobile";
            return "Generic Device";
        }
    }
}