using ModFinder.Util;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace ModFinder.Mod
{
  public class SettingsData
  {
    [JsonProperty]
    public List<string> EnabledModifications { get; set; } = new();
  }

  public class OwlcatModificationSettingsManager
  {
    public void Remove(string uniqueid)
    {
      IOTool.SafeRun(() =>
      {
        if (OwlcatSettings.EnabledModifications.Remove(uniqueid))
          Save();
      });
    }

    public void Add(string uniqueid)
    {
      IOTool.SafeRun(() =>
      {
        if (!OwlcatSettings.EnabledModifications.Contains(uniqueid))
        {
          OwlcatSettings.EnabledModifications.Add(uniqueid);
          Save();
        }
      });
    }

    public bool Has(string uniqueId)
    {
      return IOTool.SafeGet(() => OwlcatSettings.EnabledModifications.Contains(uniqueId));
    }

    public IReadOnlyList<string> All() => OwlcatSettings.EnabledModifications.AsReadOnly();

    public int Count => OwlcatSettings.EnabledModifications.Count;

    private void Save()
    {
      IOTool.Write(OwlcatSettings, SettingsPath);
    }

    private static SettingsData Load()
    {
      if (File.Exists(SettingsPath))
        return IOTool.Read<SettingsData>(SettingsPath);
      else if (File.Exists(OldSettingsPath))
      {
        File.Move(OldSettingsPath, SettingsPath, overwrite: true);
        return IOTool.Read<SettingsData>(SettingsPath);
      }
      else
        return new();
    }

    private static string SettingsPath => Path.Combine(Main.RTDataDir, "OwlcatModificationManagerSettings.json");
    private static string OldSettingsPath => Path.Combine(Main.RTDataDir, "OwlcatModificationManagerSettings.json");

    private SettingsData _Data;
    private SettingsData OwlcatSettings => _Data ??= Load();
  }
}
