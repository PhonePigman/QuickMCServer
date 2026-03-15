using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace QuickMCServer
{
    public partial class CreateServerView : UserControl
    {
        private static readonly HttpClient client = new HttpClient();

        private Dictionary<string, string> vanillaVersionUrls = new Dictionary<string, string>();
        private List<string> paperVersions = new List<string>();
        private List<string> spigotVersions = new List<string>();

        public CreateServerView()
        {
            InitializeComponent();
            this.Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadVanillaVersionsAsync();
            TypeBox.SelectedIndex = 0;
            spigotVersions.AddRange(new[] { "1.20.4", "1.19.4", "1.18.2", "1.17.1", "1.16.5" });
        }

        // --- バージョン取得メソッド群 ---
        private async Task LoadVanillaVersionsAsync()
        {
            StatusText.Text = "Vanillaのバージョンリストを取得中...";
            ProgressPanel.Visibility = Visibility.Visible;
            try
            {
                string json = await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
                using var doc = JsonDocument.Parse(json);
                vanillaVersionUrls = doc.RootElement.GetProperty("versions").EnumerateArray()
                    .Where(v => v.GetProperty("type").GetString() == "release")
                    .ToDictionary(v => v.GetProperty("id").GetString(), v => v.GetProperty("url").GetString());
                UpdateVersionBox(vanillaVersionUrls.Keys.ToList());
            }
            finally { ProgressPanel.Visibility = Visibility.Collapsed; }
        }

        private async Task LoadPaperVersionsAsync()
        {
            if (paperVersions.Any()) { UpdateVersionBox(paperVersions); return; }
            StatusText.Text = "PaperMCのバージョンリストを取得中...";
            ProgressPanel.Visibility = Visibility.Visible;
            try
            {
                string json = await client.GetStringAsync("https://api.papermc.io/v2/projects/paper");
                using var doc = JsonDocument.Parse(json);
                paperVersions = doc.RootElement.GetProperty("versions").EnumerateArray().Select(v => v.GetString()).Reverse().ToList();
                UpdateVersionBox(paperVersions);
            }
            finally { ProgressPanel.Visibility = Visibility.Collapsed; }
        }

        // --- UIのイベント処理 ---
        private async void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeBox.SelectedItem == null) return;
            string selectedType = ((ComboBoxItem)TypeBox.SelectedItem).Content.ToString();

            switch (selectedType)
            {
                case "Vanilla (公式)": await LoadVanillaVersionsAsync(); break;
                case "Paper (プラグイン・軽量)": await LoadPaperVersionsAsync(); break;
                case "Spigot (プラグイン)": UpdateVersionBox(spigotVersions); break;
            }
        }

        private void UpdateVersionBox(List<string> versions)
        {
            VersionBox.ItemsSource = versions;
            if (versions.Any()) VersionBox.SelectedIndex = 0;
        }

        // --- メインのサーバー作成処理 ---
        private async void CreateServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerNameBox.Text)) { MessageBox.Show("サーバーの名前を入力してください。"); return; }

            CreateButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            string serverType = ((ComboBoxItem)TypeBox.SelectedItem).Content.ToString();
            string serverName = ServerNameBox.Text;
            string mcVersion = VersionBox.SelectedItem.ToString();

            string ramInput = RamBox.Text.Trim();
            string ram = int.TryParse(ramInput, out _) ? ramInput + "M" : ramInput;

            // ★追加：シード値をテキストボックスから読み取る
            string seed = SeedBox.Text.Trim();

            string serverDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servers", serverName);

            try
            {
                if (Directory.Exists(serverDir)) { MessageBox.Show("同じ名前のサーバーが存在します。"); return; }
                Directory.CreateDirectory(serverDir);

                bool success = false;
                switch (serverType)
                {
                    case "Vanilla (公式)": success = await CreateVanillaServerAsync(serverDir, mcVersion); break;
                    case "Paper (プラグイン・軽量)": success = await CreatePaperServerAsync(serverDir, mcVersion); break;
                    case "Spigot (プラグイン)": success = await CreateSpigotServerAsync(serverDir, mcVersion); break;
                }

                if (success)
                {
                    // ★シード値(seed)も共通ファイル作成処理に渡す
                    CreateCommonFiles(serverDir, serverName, mcVersion, ram, serverType, seed);
                    MessageBox.Show($"{serverName} の作成が完了しました！", "成功");
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.MainContent.Content = new HomeView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"作成中にエラーが発生しました: {ex.Message}");
                if (Directory.Exists(serverDir)) Directory.Delete(serverDir, true);
            }
            finally
            {
                CreateButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        // --- 各サーバー種類ごとの作成メソッド ---
        private async Task<bool> CreateVanillaServerAsync(string serverDir, string version)
        {
            StatusText.Text = "VanillaサーバーのURLを取得中...";
            string detailJson = await client.GetStringAsync(vanillaVersionUrls[version]);
            using var doc = JsonDocument.Parse(detailJson);
            string downloadUrl = doc.RootElement.GetProperty("downloads").GetProperty("server").GetProperty("url").GetString();
            StatusText.Text = "server.jar をダウンロード中...";
            await DownloadFileAsync(downloadUrl, Path.Combine(serverDir, "server.jar"));
            return true;
        }

        private async Task<bool> CreatePaperServerAsync(string serverDir, string version)
        {
            StatusText.Text = "Paperのビルド情報を取得中...";
            string buildJson = await client.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}/builds");
            using var doc = JsonDocument.Parse(buildJson);
            string latestBuild = doc.RootElement.GetProperty("builds").EnumerateArray().Last().GetProperty("build").GetInt32().ToString();
            string downloadUrl = $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{latestBuild}/downloads/paper-{version}-{latestBuild}.jar";
            StatusText.Text = "server.jar をダウンロード中...";
            await DownloadFileAsync(downloadUrl, Path.Combine(serverDir, "server.jar"));
            return true;
        }

        private async Task<bool> CreateSpigotServerAsync(string serverDir, string version)
        {
            StatusText.Text = $"spigot-{version}.jar をダウンロード中...";
            try { await DownloadFileAsync($"https://cdn.getbukkit.org/spigot/spigot-{version}.jar", Path.Combine(serverDir, "server.jar")); return true; }
            catch { MessageBox.Show("Spigotのダウンロードに失敗しました。"); return false; }
        }

        // --- 共通のヘルパーメソッド ---
        private async Task DownloadFileAsync(string url, string path)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }

        // ★追加：引数に seed を追加し、propertiesContentに level-seed=... を書き込む
        private void CreateCommonFiles(string serverDir, string serverName, string version, string ram, string type, string seed)
        {
            StatusText.Text = "共通ファイルを作成中...";
            File.WriteAllText(Path.Combine(serverDir, "eula.txt"), "eula=true");

            string pvp = "true";
            string maxPlayers = MaxPlayersBox.Text;
            string gameMode = ((ComboBoxItem)GameModeBox.SelectedItem).Content.ToString();

            // ここで level-seed を設定ファイルに書き込みます
            string propertiesContent = $"max-players={maxPlayers}\ngamemode={gameMode}\npvp={pvp}\nlevel-seed={seed}";
            File.WriteAllText(Path.Combine(serverDir, "server.properties"), propertiesContent);

            string infoJson = $@"{{
                ""ServerName"": ""{serverName}"", ""Version"": ""{version}"", ""Ram"": ""{ram}"", ""Type"": ""{type}""
            }}";
            File.WriteAllText(Path.Combine(serverDir, "server_info.json"), infoJson);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.MainContent.Content = new HomeView();
        }
    }
}