using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModFinder.Mod;
using ModFinder.Util;

namespace ModFinder.Cli
{
  class Program
  {
    static async Task<int> Main(string[] args)
    {
      Console.WriteLine("=========================================================");
      Console.WriteLine(" ModFinder for Warhammer 40k: Rogue Trader (Linux/Proton)");
      Console.WriteLine("=========================================================");

      Console.WriteLine($"Game Directory: {ModFinder.Main.RTPath?.FullName ?? "Not found"}");
      Console.WriteLine($"Data Directory: {ModFinder.Main.RTDataDir}");
      Console.WriteLine($"Game Version:   {ModFinder.Main.GameVersion}");

      var command = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

      if (command == "status")
      {
        Console.WriteLine("\nScanning installed mods...");
        ModScanningService.RefreshInstalledMods();
        Console.WriteLine($"Installed mods found: {ModDatabase.Instance.Installed.Count}");
        foreach (var mod in ModDatabase.Instance.Installed)
        {
          var enabled = mod.Enabled ? "[ENABLED] " : "[DISABLED]";
          Console.WriteLine($"  {enabled} {mod.Name} ({mod.InstalledVersion}) [{mod.Type}]");
        }
        Console.WriteLine("\nAvailable commands:");
        Console.WriteLine("  dotnet run --project ModFinder.Cli -- status");
        Console.WriteLine("  dotnet run --project ModFinder.Cli -- browse");
        Console.WriteLine("  dotnet run --project ModFinder.Cli -- install <ModId>");
        Console.WriteLine("  dotnet run --project ModFinder.Cli -- toggle <ModId> <true|false>");
        return 0;
      }

      if (command == "browse")
      {
        Console.WriteLine("\nFetching manifest from GitHub...");
        ModScanningService.RefreshAllManifests();
        ModScanningService.RefreshInstalledMods();
        Console.WriteLine($"Total mods available in manifest: {ModDatabase.Instance.AllMods.Count()}");
        foreach (var mod in ModDatabase.Instance.AllMods.OrderBy(m => m.Name))
        {
          var status = mod.IsInstalled ? $"[Installed v{mod.InstalledVersion}]" : "[Available]";
          Console.WriteLine($"  - {mod.ModId.Id,-30} | {mod.Name,-35} | {status} | Author: {mod.Author}");
        }
        return 0;
      }

      if (command == "install")
      {
        if (args.Length < 2)
        {
          Console.WriteLine("Usage: dotnet run --project ModFinder.Cli -- install <ModId or Name or ZipPath>");
          return 1;
        }

        var target = args[1];
        if (File.Exists(target))
        {
          Console.WriteLine($"Installing local zip: {target}");
          var res = await ModInstaller.InstallFromZip(target);
          Console.WriteLine($"Result: {res.State} (Error: {res.Error ?? "None"})");
          return res.State == InstallState.Installed ? 0 : 1;
        }

        Console.WriteLine("Fetching manifest...");
        ModScanningService.RefreshAllManifests();
        ModScanningService.RefreshInstalledMods();

        var mod = ModDatabase.Instance.AllMods.FirstOrDefault(m =>
          m.ModId.Id.Equals(target, StringComparison.OrdinalIgnoreCase) ||
          m.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (mod == null)
        {
          Console.WriteLine($"Mod '{target}' not found in database.");
          return 1;
        }

        Console.WriteLine($"Installing {mod.Name} (v{mod.Latest.Version})...");
        var result = await ModInstaller.Install(mod, isUpdate: mod.IsInstalled);
        Console.WriteLine($"Result: {result.State} (Error: {result.Error ?? "None"})");
        return result.State == InstallState.Installed ? 0 : 1;
      }

      if (command == "toggle")
      {
        if (args.Length < 3)
        {
          Console.WriteLine("Usage: dotnet run --project ModFinder.Cli -- toggle <ModId> <true|false>");
          return 1;
        }

        var modId = args[1];
        var state = bool.Parse(args[2]);

        ModScanningService.RefreshInstalledMods();
        var mod = ModDatabase.Instance.Installed.FirstOrDefault(m =>
          m.ModId.Id.Equals(modId, StringComparison.OrdinalIgnoreCase) ||
          m.Name.Equals(modId, StringComparison.OrdinalIgnoreCase));

        if (mod == null)
        {
          Console.WriteLine($"Installed mod '{modId}' not found.");
          return 1;
        }

        mod.Enabled = state;
        Console.WriteLine($"Mod '{mod.Name}' set to Enabled = {state}");
        return 0;
      }

      Console.WriteLine($"Unknown command: {command}");
      return 1;
    }
  }
}
