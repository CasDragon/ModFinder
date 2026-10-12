using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ModFinder
{
  public static class Main
  {
    /// <summary>
    /// NexusMods slug
    /// </summary>
    public const string NexusSlug = "modfinder";
    /// <summary>
    /// Modfinder version
    /// </summary>
    public static string ProductVersion
    {
      get
      {
        try
        {
          var entry = Assembly.GetEntryAssembly();
          if (entry != null && !string.IsNullOrEmpty(entry.Location))
          {
            return FileVersionInfo.GetVersionInfo(entry.Location).ProductVersion ?? "1.6.3";
          }
        }
        catch { }
        return "1.6.3";
      }
    }

    /// <summary>
    /// Modfinder settings
    /// </summary>
    public static Settings Settings => m_Settings ??= Settings.Load();

    private static Settings m_Settings;

    /// <summary>
    /// Owlcat mods, use to toggle enabled status
    /// </summary>
    public static readonly OwlcatModificationSettingsManager OwlcatMods = new();

    /// <summary>
    /// Folder where we put modfinder stuff
    /// </summary>
    public static string AppFolder
    {
      get
      {
        if (!Directory.Exists(_appFolder))
        {
          _ = Directory.CreateDirectory(_appFolder);
        }
        return _appFolder;
      }
    }

    private static readonly string _appFolder =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModfinderRT");

    /// <summary>
    /// Get path to file in modfinder app folder
    /// </summary>
    public static string AppPath(string file) => Path.Combine(AppFolder, file);

    /// <summary>
    /// Try to read a file in the modfinder app folder
    /// </summary>
    public static bool TryReadFile(string file, out string contents)
    {
      var path = AppPath(file);
      if (File.Exists(path))
      {
        contents = File.ReadAllText(path);
        return true;
      }
      else
      {
        contents = null;
        return false;
      }
    }

    public static string AppDataRoot => Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))?.FullName ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string RTDataDir
    {
      get
      {
        // 1. Explicit override from Settings if set
        if (!string.IsNullOrEmpty(Settings.RTPath))
        {
          var candidate = ProbeDataDirFromGameDir(Settings.RTPath);
          if (candidate != null) return candidate;
        }

        if (!string.IsNullOrEmpty(Settings.AutoRTPath))
        {
          var candidate = ProbeDataDirFromGameDir(Settings.AutoRTPath);
          if (candidate != null) return candidate;
        }

        // 2. Linux / Proton detection: check known Steam library paths for appid 2186680
        if (!OperatingSystem.IsWindows())
        {
          var protonDir = FindProtonDataDir();
          if (protonDir != null)
          {
            return protonDir;
          }
        }

        // 3. Fallback to Windows default
        return Path.Combine(AppDataRoot, "LocalLow", "Owlcat Games", "Warhammer 40000 Rogue Trader");
      }
    }

    private static string ProbeDataDirFromGameDir(string gameDir)
    {
      if (string.IsNullOrEmpty(gameDir)) return null;

      // In Proton, gameDir is .../steamapps/common/Warhammer 40,000 Rogue Trader
      // Compatdata is usually ../../compatdata/2186680/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Warhammer 40000 Rogue Trader
      try
      {
        var commonDir = Directory.GetParent(gameDir);
        var steamappsDir = commonDir?.Parent;
        if (steamappsDir != null)
        {
          var pfxDir = Path.Combine(steamappsDir.FullName, "compatdata", "2186680", "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "Owlcat Games", "Warhammer 40000 Rogue Trader");
          if (Directory.Exists(pfxDir))
            return pfxDir;
        }
      }
      catch { }

      return null;
    }

    private static string FindProtonDataDir()
    {
      string[] candidateBases = {
        "/run/media/system/NVME_GAME_1/SteamLibrary/steamapps/compatdata/2186680/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Warhammer 40000 Rogue Trader",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam/steamapps/compatdata/2186680/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Warhammer 40000 Rogue Trader"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam/root/steamapps/compatdata/2186680/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Warhammer 40000 Rogue Trader")
      };

      foreach (var c in candidateBases)
      {
        if (Directory.Exists(c))
          return c;
      }

      // If directory doesn't exist yet, return preferred proton path if parent exists
      if (Directory.Exists("/run/media/system/NVME_GAME_1/SteamLibrary/steamapps/common/Warhammer 40,000 Rogue Trader"))
      {
        return candidateBases[0];
      }

      return null;
    }

    public static Func<string> PromptForRTPathManual;

    public static void GetRTPathManual()
    {
      Logger.Log.Info($"RTPath not found, prompting user.");
      var selected = PromptForRTPathManual?.Invoke();
      if (!string.IsNullOrEmpty(selected) && File.Exists(selected))
      {
        _RTPath = new(Path.GetDirectoryName(selected));
      }
      else
      {
        Logger.Log.Error("Unable to find RT installation path.");
      }
    }

    private static Regex extractGameVersion = new(".*?Found game version string: '(.*)'.*");
    public static ModVersion GameVersion;

    private static ModVersion GameVersionRaw
    {
      get
      {
        if (RTPath == null) { return new(); }

        var log = Path.Combine(RTDataDir, "Player.log");
        if (!File.Exists(log))
        {
          return new();
        }

        try
        {
          using var sr = new StreamReader(File.OpenRead(log));

          for (int i = 0; i < 200; i++)
          {
            var line = sr.ReadLine();
            if (line is null) return new();

            var match = extractGameVersion.Match(line);
            if (match.Success)
            {
              Logger.Log.Info($"Found game version: {match.Groups[1].Value}");
              return ModVersion.Parse(match.Groups[1].Value);
            }
          }
        }
        catch (Exception e)
        {
          Logger.Log.Error("Unable to find RT game version", e);
        }

        return new();
      }
    }

    private static readonly string[] KnownLinuxGamePaths = {
      "/run/media/system/NVME_GAME_1/SteamLibrary/steamapps/common/Warhammer 40,000 Rogue Trader",
      "/run/media/system/NVME_GAME_1/SteamLibrary/steamapps/common/Warhammer 40000 Rogue Trader",
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam/steamapps/common/Warhammer 40,000 Rogue Trader"),
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam/steamapps/common/Warhammer 40000 Rogue Trader")
    };

    /// <summary>
    /// Path to the installed RT folder
    /// </summary>
    public static DirectoryInfo RTPath
    {
      get
      {
        if (_RTPath != null) return _RTPath;

        if (!string.IsNullOrEmpty(Settings.RTPath) && Directory.Exists(Settings.RTPath) && File.Exists(Path.Combine(Settings.RTPath, "WH40KRT.exe")))
        {
          Logger.Log.Info($"Using RTPath from settings: {Settings.RTPath}");
          _RTPath = new(Settings.RTPath);
        }
        else if (!string.IsNullOrEmpty(Settings.AutoRTPath) && Directory.Exists(Settings.AutoRTPath) && File.Exists(Path.Combine(Settings.AutoRTPath, "WH40KRT.exe")))
        {
          Logger.Log.Info($"Using auto RTPath from settings: {Settings.AutoRTPath}");
          _RTPath = new(Settings.AutoRTPath);
        }
        else
        {
          // Check known Linux paths
          foreach (var path in KnownLinuxGamePaths)
          {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "WH40KRT.exe")))
            {
              Logger.Log.Info($"Found game at known path: {path}");
              _RTPath = new(path);
              break;
            }
          }

          if (_RTPath == null)
          {
            var log = Path.Combine(RTDataDir, "Player.log");
            if (!File.Exists(log))
            {
              GetRTPathManual();
            }
            else
            {
              try
              {
                Logger.Log.Info($"Getting RTPath from player log.");

                using var sr = new StreamReader(File.OpenRead(log));
                var firstline = sr.ReadLine();

                var extractPath = new Regex(".*?'(.*)'");
                _RTPath = new(extractPath.Match(firstline).Groups[1].Value);
                _RTPath = _RTPath.Parent.Parent;
              }
              catch (Exception e)
              {
                Logger.Log.Error("Unable to find RT installation path, prompting manual input.", e);
                GetRTPathManual();
              }
            }
          }

          if (_RTPath != null)
          {
            Settings.AutoRTPath = _RTPath.FullName;
            Settings.Save();
          }
        }

        GameVersion = GameVersionRaw;
        return _RTPath;
      }
    }

    public static void SetRTPath(string path)
    {
      if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
      {
        _RTPath = new(path);
        Settings.RTPath = path;
        Settings.Save();
        GameVersion = GameVersionRaw;
      }
    }

    public static string UMMParamsPath => Path.Combine(RTDataDir, "UnityModManager", "Params.xml");

    public static string UMMInstallPath => Path.Combine(RTDataDir, "UnityModManager");

    private static DirectoryInfo _RTPath; 
  }
}
