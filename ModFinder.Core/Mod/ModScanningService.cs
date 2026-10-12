using ModFinder.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ModFinder.Mod
{
  public static class ModScanningService
  {
    public static MasterManifest Manifest { get; private set; }

    public static void RefreshAllManifests()
    {
      try
      {
        Logger.Log.Verbose("Fetching remote master manifest.");
        var json = HttpHelper.GetResponseContent("https://raw.githubusercontent.com/CasDragon/ModFinder/RogueTrader/ManifestUpdater/Resources/master_manifest.json");
        Manifest = IOTool.FromString<MasterManifest>(json);

        RefreshGeneratedManifest();

        if (Manifest?.ExternalManifestUrls != null)
        {
          foreach (var url in Manifest.ExternalManifestUrls)
          {
            Logger.Log.Verbose($"Loading manifest from external URL: {url}");
            var extJson = HttpHelper.GetResponseContent(url);
            RefreshManifest(IOTool.FromString<ModManifest>(extJson));
          }
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error("Failed to refresh manifests.", e);
      }
    }

    private static void RefreshGeneratedManifest()
    {
      if (Manifest == null || string.IsNullOrEmpty(Manifest.GeneratedManifestUrl))
        return;

      var rawstring = HttpHelper.GetResponseContent(Manifest.GeneratedManifestUrl);
      var manifests = IOTool.FromString<List<ModManifest>>(rawstring);
      if (manifests != null)
      {
        foreach (var manifest in manifests)
        {
          RefreshManifest(manifest);
        }
      }
    }

    private static void RefreshManifest(ModManifest manifest)
    {
      if (manifest == null) return;
      if (ModDatabase.Instance.TryGet(manifest.Id, out var viewModel))
        viewModel.Refresh(manifest);
      else
        ModDatabase.Instance.Add(new(manifest));
    }

    public static void RefreshInstalledMods()
    {
      IOTool.SafeRun(CheckInstalledModsInternal);
      IOTool.SafeRun(CheckUMMState);
    }

    private static void CheckInstalledModsInternal()
    {
      try
      {
        foreach (var mod in ModDatabase.Instance.AllMods)
        {
          mod.InstallState = InstallState.None;
          mod.InstalledVersion = default;
        }

        var installedMods = new Dictionary<ModId, ModVersion>();
        var ummBase = ModInstaller.GetModPath(ModType.UMM);
        var ummDir = Directory.Exists(ummBase) ? Directory.GetDirectories(ummBase) : Array.Empty<string>();
        foreach (var dir in ummDir)
        {
          Logger.Log.Info($"Found installed UMM mod: {dir}");
          var mod = ProcessUmmModDir(new(dir));
          if (mod is not null)
            installedMods[mod.ModId] = mod.InstalledVersion;
        }

        var owlcatBase = ModInstaller.GetModPath(ModType.Owlcat);
        var owlDir = Directory.Exists(owlcatBase) ? Directory.GetDirectories(owlcatBase) : Array.Empty<string>();
        foreach (var dir in owlDir)
        {
          Logger.Log.Info($"Found installed Owlcat mod: {dir}");
          var mod = ProcessOwlModDirectory(new(dir));
          if (mod is not null)
            installedMods[mod.ModId] = mod.InstalledVersion;
        }

        if (Directory.Exists(ModCache.CacheDir))
        {
          foreach (var dir in Directory.GetDirectories(ModCache.CacheDir))
          {
            Logger.Log.Info($"Found cached mod: {dir}");
            ProcessUmmModDir(new(dir), updateStatus: false);
          }
        }

        foreach (var mod in ModDatabase.Instance.AllMods)
        {
          mod.CheckRequirements(installedMods);
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error("Failed to check installed mods.", e);
      }
    }

    private static void CheckUMMState()
    {
      try
      {
        if (!File.Exists(Main.UMMParamsPath))
          return;

        XElement ummParams = XElement.Load(Main.UMMParamsPath);
        var mods =
          ummParams.Descendants("Mod")
            .Select(x => (x.Attribute("Id")?.Value, bool.TryParse(x.Attribute("Enabled")?.Value, out var en) && en))
            .Where(x => x.Item1 != null);

        foreach (var mod in ModDatabase.Instance.Installed)
        {
          if (mod.Type != ModType.UMM)
            continue;

          var modConfig = mods.FirstOrDefault(m => mod.ModId.Id == m.Item1);
          if (modConfig == default)
            mod.SetEnabledSilently(true);
          else
            mod.SetEnabledSilently(modConfig.Item2);
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error("Failed to check UMM state.", e);
      }
    }

    public static ModViewModel ProcessOwlModDirectory(DirectoryInfo modDir, bool updateStatus = true)
    {
      var infoFile = 
        modDir.GetFiles().FirstOrDefault(f => f.Name.Equals("OwlcatModificationManifest.json", StringComparison.OrdinalIgnoreCase));
      if (infoFile != null)
      {
        var info = IOTool.Read<OwlcatModInfo>(infoFile.FullName);
        var manifest = ModManifest.ForLocal(info);
        if (manifest is null)
        {
          Logger.Log.Warning($"Invalid manifest: {infoFile}");
          return null;
        }

        if (!ModDatabase.Instance.TryGet(manifest.Id, out var mod))
        {
          mod = new(manifest);
          ModDatabase.Instance.Add(mod);
        }

        if (updateStatus)
        {
          mod.ModDir = modDir;
          mod.InstallState = InstallState.Installed;
          mod.InstalledVersion = ModVersion.Parse(info.Version);
          mod.SetEnabledSilently(Main.OwlcatMods.Has(info.UniqueName));
          mod.SetRequirements(new List<string> { "MicroPatches" });
        }
        return mod;
      }
      return null;
    }

    public static ModViewModel ProcessUmmModDir(DirectoryInfo modDir, bool updateStatus = true)
    {
      var infoFile =
        modDir.GetFiles().FirstOrDefault(f => f.Name.Equals("Info.json", StringComparison.OrdinalIgnoreCase));
      if (infoFile != null)
      {
        var info = IOTool.Read<UMMModInfo>(infoFile.FullName);
        var manifest = ModManifest.ForLocal(info);
        if (manifest is null)
        {
          Logger.Log.Warning($"Invalid manifest: {infoFile}");
          return null;
        }

        if (!ModDatabase.Instance.TryGet(manifest.Id, out var mod))
        {
          mod = new(manifest);
          ModDatabase.Instance.Add(mod);
        }

        if (updateStatus)
        {
          mod.ModDir = modDir;
          mod.InstallState = InstallState.Installed;
          mod.InstalledVersion = ModVersion.Parse(info.Version);
          mod.SetRequirements(info.Requirements);
        }
        return mod;
      }
      return null;
    }
  }
}
