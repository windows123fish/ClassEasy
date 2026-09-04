using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using ClassEasy.Models;
using ClassEasy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassEasy.ViewModels
{
    /// <summary>设置窗口视图模型：外观（主题模式/强调色/亚克力强度）+ 数据（城市/数据源）+ 自启。</summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly MainViewModel? _main;

        public List<AccentPreset> AccentPresets { get; } =
            ThemeService.AccentPresets.Select(p => new AccentPreset(p.Name, p.Hex)).ToList();

        [ObservableProperty]
        private AccentPreset? _selectedAccent;

        /// <summary>0=跟随系统 1=浅色 2=深色。</summary>
        [ObservableProperty]
        private int _themeModeIndex;

        /// <summary>0=uapis 1=open-meteo。</summary>
        [ObservableProperty]
        private int _weatherSourceIndex;

        [ObservableProperty]
        private string _city = "上海";

        [ObservableProperty]
        private bool _autoStart;

        [ObservableProperty]
        private double _backdropStrength = 0.65;

        public SettingsViewModel(MainViewModel? main)
        {
            _main = main;

            var cfg = main?.Config ?? ConfigService.Load<AppConfig>(Paths.ConfigFile);
            City = cfg.City;
            WeatherSourceIndex = cfg.WeatherSource == "openmeteo" ? 1 : 0;
            ThemeModeIndex = cfg.ThemeMode == "Light" ? 1 : cfg.ThemeMode == "Dark" ? 2 : 0;
            AutoStart = cfg.AutoStart;
            BackdropStrength = cfg.BackdropStrength;
            SelectedAccent = AccentPresets.FirstOrDefault(p =>
                string.Equals(p.Hex, cfg.AccentColor, StringComparison.OrdinalIgnoreCase)) ?? AccentPresets[0];
        }

        [RelayCommand]
        private void Save()
        {
            // 主 VM 存在时直接改它的配置实例；否则新建（兜底）
            var cfg = _main?.Config ?? new AppConfig();
            cfg.City = City.Trim();
            cfg.WeatherSource = WeatherSourceIndex == 1 ? "openmeteo" : "uapis";
            cfg.ThemeMode = ThemeModeIndex switch { 1 => "Light", 2 => "Dark", _ => "Auto" };
            cfg.AccentColor = SelectedAccent?.Hex ?? cfg.AccentColor;
            cfg.AutoStart = AutoStart;
            cfg.BackdropStrength = BackdropStrength;

            // 主题即时生效
            ThemeService.Apply(cfg.AccentColor, cfg.ThemeMode);
            AutoStartService.SetEnabled(AutoStart);
            _main?.ApplyConfig(cfg);

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

        /// <summary>请求关闭设置窗口。</summary>
        public event EventHandler? CloseRequested;
    }

    /// <summary>强调色预设项（供色板 ListBox 绑定）。</summary>
    public class AccentPreset
    {
        public AccentPreset(string name, string hex)
        {
            Name = name;
            Hex = hex;
            Brush = new SolidColorBrush(Color.Parse(hex));
        }

        public string Name { get; }
        public string Hex { get; }
        public IBrush Brush { get; }

        public override string ToString() => Name;
    }
}
