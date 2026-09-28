using System.Windows;
using OpenDock.Core.Models;
using OpenDock.Core.Services;

namespace OpenDock;

public partial class PreferencesWindow : Window
{
    private readonly SettingsService _settings;

    public PreferencesWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();

        ThemeCombo.ItemsSource = Enum.GetValues<DockTheme>();
        MinimizeCombo.ItemsSource = Enum.GetValues<MinimizeEffect>();
        StackViewCombo.ItemsSource = Enum.GetValues<StackViewMode>();

        var s = _settings.Current;
        IconSizeSlider.Value = s.IconSize;
        MagnificationCheck.IsChecked = s.MagnificationEnabled;
        MagnifiedSizeSlider.Value = s.MagnifiedSize;
        ThemeCombo.SelectedItem = s.Theme;
        AutoHideCheck.IsChecked = s.AutoHide;
        HideTaskbarCheck.IsChecked = s.HideTaskbar;
        PreviewsCheck.IsChecked = s.ShowWindowPreviews;
        MinimizeCombo.SelectedItem = s.MinimizeEffect;
        MenuBarCheck.IsChecked = s.MenuBarEnabled;
        LaunchpadHotkeyBox.Text = s.LaunchpadHotkey;
        SpotlightHotkeyBox.Text = s.SpotlightHotkey;
        ExposeHotkeyBox.Text = s.ExposeHotkey;
        StackViewCombo.SelectedItem = s.DefaultStackView;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.Update(s =>
        {
            s.IconSize = IconSizeSlider.Value;
            s.MagnificationEnabled = MagnificationCheck.IsChecked == true;
            s.MagnifiedSize = MagnifiedSizeSlider.Value;
            s.Theme = (DockTheme)ThemeCombo.SelectedItem;
            s.AutoHide = AutoHideCheck.IsChecked == true;
            s.HideTaskbar = HideTaskbarCheck.IsChecked == true;
            s.ShowWindowPreviews = PreviewsCheck.IsChecked == true;
            s.MinimizeEffect = (MinimizeEffect)MinimizeCombo.SelectedItem;
            s.MenuBarEnabled = MenuBarCheck.IsChecked == true;
            s.LaunchpadHotkey = LaunchpadHotkeyBox.Text.Trim();
            s.SpotlightHotkey = SpotlightHotkeyBox.Text.Trim();
            s.ExposeHotkey = ExposeHotkeyBox.Text.Trim();
            s.DefaultStackView = (StackViewMode)StackViewCombo.SelectedItem;
        });
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
