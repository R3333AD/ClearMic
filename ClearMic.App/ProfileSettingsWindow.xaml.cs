using System.Windows;
using ClearMic.Core;

namespace ClearMic.App;

public partial class ProfileSettingsWindow : Window
{
    private readonly PipelineService _pipeline;

    public ProfileSettingsWindow(PipelineService pipeline)
    {
        InitializeComponent();
        _pipeline = pipeline;
        RefreshList();
    }

    private void RefreshList()
    {
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = _pipeline.Profiles.Profiles;
    }

    private void OnProfileSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is not Profile profile) return;

        ProfileNameBox.Text = profile.Name;
        ProcessBox.Text = string.Join(", ", profile.AutoSwitchProcesses);
        NoiseFilterCheck.IsChecked = profile.NoiseFilter;
        AecCheck.IsChecked = profile.AecEnabled;
        DeleteButton.IsEnabled = !profile.IsDefault;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var name = ProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var existing = _pipeline.Profiles.Profiles.FirstOrDefault(p => p.Name == name);
        if (existing is not null)
        {
            existing.NoiseFilter = NoiseFilterCheck.IsChecked == true;
            existing.AecEnabled = AecCheck.IsChecked == true;
            existing.AutoSwitchProcesses = [.. ProcessBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            _pipeline.Profiles.Save();
        }
        else
        {
            var profile = new Profile
            {
                Name = name,
                NoiseFilter = NoiseFilterCheck.IsChecked == true,
                AecEnabled = AecCheck.IsChecked == true,
                AutoSwitchProcesses = [.. ProcessBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)],
            };
            _pipeline.Profiles.Add(profile);
        }

        RefreshList();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not Profile profile || profile.IsDefault) return;
        _pipeline.Profiles.Remove(profile.Name);
        RefreshList();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
