using System.Reflection;

namespace ReceptyOks.Services;

public static class VersionInfo
{
    private static readonly Assembly Assembly = typeof(VersionInfo).Assembly;
    
    /// <summary>
    /// Wersja wyœwietlana u¿ytkownikowi (np. "1.0.0")
    /// </summary>
    public static string DisplayVersion => 
        AppInfo.Current.VersionString;
    
    /// <summary>
    /// Numer buildu (np. "250126" dla 26 stycznia 2025)
    /// </summary>
    public static string BuildNumber => 
        AppInfo.Current.BuildString;
    
    /// <summary>T
    /// Pe³na wersja informacyjna z metadanymi (np. "1.0.0+250126")
    /// </summary>
    public static string FullVersion
    {
        get
        {
            var informationalVersion = Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            return informationalVersion ?? $"{DisplayVersion}+{BuildNumber}";
        }
    }
    
    /// <summary>
    /// Wersja z assembly (np. "1.0.0.0")
    /// </summary>
    public static string AssemblyVersion => 
        Assembly.GetName().Version?.ToString() ?? "Unknown";
    
    /// <summary>
    /// Sformatowana wersja do wyœwietlenia (np. "v1.0.0 (build 250126)")
    /// </summary>
    public static string FormattedVersion => 
        $"v{DisplayVersion} (build {BuildNumber})";
    
    /// <summary>
    /// Build date z BuildNumber w formacie YYMMDD
    /// </summary>
    public static string BuildDate
    {
        get
        {
            if (BuildNumber.Length == 6 && int.TryParse(BuildNumber, out var dateNum))
            {
                var year = 2000 + int.Parse(BuildNumber.Substring(0, 2));
                var month = int.Parse(BuildNumber.Substring(2, 2));
                var day = int.Parse(BuildNumber.Substring(4, 2));
                
                try
                {
                    var date = new DateTime(year, month, day);
                    return date.ToString("yyyy-MM-dd");
                }
                catch
                {
                    return BuildNumber;
                }
            }
            return BuildNumber;
        }
    }
}
