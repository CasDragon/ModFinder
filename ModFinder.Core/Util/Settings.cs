using Newtonsoft.Json;
using NexusModsNET;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModFinder.Util
{
  public class Settings
  {
    public string AutoRTPath { get; set; }
    public string RTPath { get; set; }
    public string NexusApiKey { get; set; }
    public byte[] NexusApiKeyBytes { get; set; }

    [JsonIgnore]
    internal bool? IsKeyActuallyPremium;

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ModFinderRT_LinuxPort_Salt");

    private string GetPlainNexusKey()
    {
      if (!string.IsNullOrEmpty(NexusApiKey))
      {
        return NexusApiKey;
      }

      if (NexusApiKeyBytes != null && OperatingSystem.IsWindows())
      {
        try
        {
          var plain = ProtectedData.Unprotect(NexusApiKeyBytes, null, DataProtectionScope.CurrentUser);
          return Encoding.UTF8.GetString(plain);
        }
        catch { }
      }
      return null;
    }

    public void SetNexusKey(string key)
    {
      NexusApiKey = key;
      if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(key))
      {
        try
        {
          var bytes = Encoding.UTF8.GetBytes(key);
          NexusApiKeyBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        }
        catch { }
      }
      Save();
      Task.Run(VerifyNexusPremium);
    }

    public string MaybeGetNexusKey()
    {
      if (!IsKeyActuallyPremium.HasValue || !IsKeyActuallyPremium.Value)
      {
        return null;
      }
      return GetPlainNexusKey();
    }

    private static Settings _Instance;
    public static Settings Load()
    {
      if (_Instance == null)
      {
        if (Main.TryReadFile("Settings.json", out var settingsRaw))
        {
          _Instance = IOTool.FromString<Settings>(settingsRaw);
          Task.Run(_Instance.VerifyNexusPremium);
        }
        else
        {
          _Instance = new();
          _Instance.Save();
        }
      }

      return _Instance;
    }

    public void Save()
    {
      IOTool.SafeRun(() =>
      {
        IOTool.Write(this, Main.AppPath("Settings.json"));
      });
    }

    internal async void VerifyNexusPremium()
    {
      var maybeKey = GetPlainNexusKey();
      if (!string.IsNullOrEmpty(maybeKey))
      {
        try
        {
          var client = NexusModsFactory.New(maybeKey, "Modfinder", Main.ProductVersion);
          var user = await client.CreateUserInquirer().GetUserAsync();
          IsKeyActuallyPremium = user.IsPremium;
        }
        catch (Exception e)
        {
          Logger.Log.Warning($"Unable to verify Nexus key: {e.Message}");
          IsKeyActuallyPremium = false;
        }
      }
    }
  }
}
