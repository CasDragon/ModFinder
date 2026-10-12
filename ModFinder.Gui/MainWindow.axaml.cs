using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ModFinder.Mod;
using ModFinder.Util;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ModFinder.Gui
{
  public partial class MainWindow : Window
  {
    private readonly ObservableCollection<ModViewModel> _displayedMods = new();
    private ModViewModel _selectedMod;

    public MainWindow()
    {
      InitializeComponent();

      ModsListBox.ItemsSource = _displayedMods;
      ModsListBox.SelectionChanged += ModsListBox_SelectionChanged;

      SearchBox.TextChanged += (s, e) => ApplyFilter();
      AllModsRadio.IsCheckedChanged += (s, e) => ApplyFilter();
      InstalledRadio.IsCheckedChanged += (s, e) => ApplyFilter();

      RefreshButton.Click += async (s, e) => await RefreshAll();
      InstallZipButton.Click += async (s, e) => await InstallZipPrompt();
      OpenHomepageButton.Click += OpenHomepage_Click;

      // Add handler for click inside DataTemplate items
      this.AddHandler(Button.ClickEvent, OnItemButtonClicked, RoutingStrategies.Bubble);

      StatusGameDir.Text = $"Game: {ModFinder.Main.RTPath?.FullName ?? "Not found"}";
      StatusPrefixDir.Text = $"Data: {ModFinder.Main.RTDataDir}";

      _ = InitialLoad();
    }

    private async Task InitialLoad()
    {
      await Task.Run(() =>
      {
        ModScanningService.RefreshAllManifests();
        ModScanningService.RefreshInstalledMods();
      });

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        ApplyFilter();
        if (_displayedMods.Count > 0)
        {
          ModsListBox.SelectedIndex = 0;
        }
      });
    }

    private async Task RefreshAll()
    {
      RefreshButton.IsEnabled = false;
      RefreshButton.Content = "Refreshing...";
      await Task.Run(() =>
      {
        ModScanningService.RefreshAllManifests();
        ModScanningService.RefreshInstalledMods();
      });
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        ApplyFilter();
        RefreshButton.Content = "🔄 Refresh All";
        RefreshButton.IsEnabled = true;
      });
    }

    private void ApplyFilter()
    {
      var search = SearchBox.Text?.Trim() ?? "";
      var showOnlyInstalled = InstalledRadio.IsChecked == true;

      var source = showOnlyInstalled ? ModDatabase.Instance.Installed : ModDatabase.Instance.AllMods;

      var filtered = source.Where(m =>
      {
        if (string.IsNullOrEmpty(search)) return true;
        return (m.Name != null && m.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            || (m.Author != null && m.Author.Contains(search, StringComparison.OrdinalIgnoreCase))
            || (m.ModId.Id != null && m.ModId.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
      }).OrderBy(m => m.Name).ToList();

      _displayedMods.Clear();
      foreach (var m in filtered)
      {
        _displayedMods.Add(m);
      }

      ModCountText.Text = $"Showing {_displayedMods.Count} mods";
    }

    private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (ModsListBox.SelectedItem is ModViewModel mod)
      {
        _selectedMod = mod;
        DetailsTitle.Text = mod.Name;
        DetailsAuthor.Text = $"By {mod.Author ?? "Unknown"}";
        DetailsStatus.Text = mod.StatusText;
        DetailsDescription.Text = mod.DescriptionAsText;
        DetailsSource.Text = $"Source: {mod.Source}";
        DetailsVersion.Text = $"Latest: {mod.Latest.Version} | Installed: {(mod.IsInstalled ? mod.InstalledVersion.ToString() : "None")}";
        OpenHomepageButton.IsVisible = mod.HasHomepage;
      }
    }

    private async void OnItemButtonClicked(object sender, RoutedEventArgs e)
    {
      if (e.Source is Button button && button.Tag is ModViewModel mod)
      {
        if (button.Name == "ActionButton")
        {
          await HandleAction(mod);
        }
        else if (button.Name == "UninstallButton")
        {
          await HandleUninstall(mod);
        }
      }
    }

    private async Task HandleAction(ModViewModel mod)
    {
      if (mod.CanInstall)
      {
        mod.InstallState = InstallState.Installing;
        await Task.Run(async () =>
        {
          await ModInstaller.Install(mod, isUpdate: mod.IsInstalled);
          ModScanningService.RefreshInstalledMods();
        });
        ApplyFilter();
      }
      else if (mod.CanDownload && !string.IsNullOrEmpty(mod.Latest.Url))
      {
        OpenBrowser(mod.Latest.Url);
      }
    }

    private async Task HandleUninstall(ModViewModel mod)
    {
      mod.InstallState = InstallState.Uninstalling;
      await Task.Run(async () =>
      {
        await ModCache.Uninstall(mod);
        mod.OnUninstalled();
        ModScanningService.RefreshInstalledMods();
      });
      ApplyFilter();
    }

    private async Task InstallZipPrompt()
    {
      var topLevel = TopLevel.GetTopLevel(this);
      if (topLevel == null) return;

      var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
      {
        Title = "Select Mod Archive (.zip)",
        AllowMultiple = false,
        FileTypeFilter = new[]
        {
          new FilePickerFileType("Archive Files")
          {
            Patterns = new[] { "*.zip" }
          }
        }
      });

      if (files.Count > 0)
      {
        var localPath = files[0].Path.LocalPath;
        if (File.Exists(localPath))
        {
          await Task.Run(async () =>
          {
            await ModInstaller.InstallFromZip(localPath);
            ModScanningService.RefreshInstalledMods();
          });
          ApplyFilter();
        }
      }
    }

    private void OpenHomepage_Click(object sender, RoutedEventArgs e)
    {
      if (_selectedMod != null && _selectedMod.HasHomepage)
      {
        OpenBrowser(_selectedMod.HomepageUrl);
      }
    }

    private static void OpenBrowser(string url)
    {
      try
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = url,
          UseShellExecute = true
        });
      }
      catch { }
    }
  }
}
