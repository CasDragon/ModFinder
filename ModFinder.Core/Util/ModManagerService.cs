using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ModFinder.Mod;

namespace ModFinder.Util
{
  public static class ModManagerService
  {
    public static void SetModEnabled(ModId id, bool enabled)
    {
      try
      {
        if (id.Type == ModType.UMM)
        {
          if (!File.Exists(Main.UMMParamsPath))
          {
            Logger.Log.Warning($"UMM Params.xml not found at {Main.UMMParamsPath}");
            return;
          }

          XDocument ummParams = XDocument.Load(Main.UMMParamsPath);
          var mod = ummParams.Descendants("Mod").FirstOrDefault(x => id.Id.Equals(x.Attribute("Id")?.Value));

          var enabledString = enabled ? "true" : "false";
          if (mod is null)
          {
            var root = ummParams.Descendants("ModParams").FirstOrDefault();
            if (root is not null)
            {
              root.Add(XElement.Parse($"<Mod Id=\"{id.Id}\" Enabled=\"{enabledString}\" />"));
            }
          }
          else
          {
            mod.SetAttributeValue("Enabled", enabledString);
          }
          ummParams.Save(Main.UMMParamsPath);
        }
        else if (id.Type == ModType.Owlcat)
        {
          if (enabled)
            Main.OwlcatMods.Add(id.Id);
          else
            Main.OwlcatMods.Remove(id.Id);
        }
      }
      catch (Exception e)
      {
        Logger.Log.Error($"Failed to set mod enabled state: {id.Id}", e);
      }
    }
  }
}
