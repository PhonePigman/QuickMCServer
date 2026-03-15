using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Open.Nat;

namespace QuickMCServer
{
    public class OpPlayer
    {
        public string uuid { get; set; }
        public string name { get; set; }
        public int level { get; set; }
        public bool bypassesPlayerLimit { get; set; }
    }

    public partial class ServerManagerView : UserControl
    {
        private ServerInfo currentServer;
        private MinecraftServerProcess serverProcess;
        private string propertiesPath, opsPath, pluginsPath;
        private bool isPortOpenedByApp = false;

        // --- MainWindowから命令を受け取るための窓口 ---
        public bool IsServerRunning => serverProcess.IsRunning;
        public bool IsPortPotentiallyOpen => isPortOpenedByApp;
        public void ForceKillServer() => serverProcess.Kill();
        public async Task ClosePortOnExitAsync()
        {
            if (isPortOpenedByApp)
            {
                try
                {
                    int port = int.Parse(PortNumberBox.Text);
                    var discoverer = new NatDiscoverer();
                    var device = await discoverer.DiscoverDeviceAsync();
                    await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port));
                    isPortOpenedByApp = false;
                }
                catch { }
            }
        }

        public ServerManagerView(ServerInfo serverToManage)
        {
            InitializeComponent();
            currentServer = serverToManage;
            serverProcess = new MinecraftServerProcess();
            propertiesPath = Path.Combine(currentServer.FolderPath, "server.properties");
            opsPath = Path.Combine(currentServer.FolderPath, "ops.json");
            pluginsPath = Path.Combine(currentServer.FolderPath, "plugins");

            ServerNameText.Text = currentServer.ServerName;
            ServerInfoText.Text = $"バージョン: {currentServer.Version}";
            RamSettingBox.Text = currentServer.Ram;

            serverProcess.OnLogReceived += (s, log) => Dispatcher.Invoke(() => { if (log != null) { LogBox.AppendText(log + "\n"); LogBox.ScrollToEnd(); } });

            LoadServerProperties();
            LoadGlobalIpAsync();
            LoadOpList();
            LoadPluginsList();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (serverProcess.IsRunning)
            {
                MessageBoxResult result = MessageBox.Show("サーバーが起動中です！\nこのまま管理画面を戻ると、サーバーは強制終了されますがよろしいですか？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No) return;
                serverProcess.Kill();
            }

            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.MainContent.Content = new ServerListView();
        }

        private async void LoadGlobalIpAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string ip = await client.GetStringAsync("https://api.ipify.org");
                Dispatcher.Invoke(() => { string port = PortNumberBox.Text; IpAddressBox.Text = $"{ip.Trim()}:{port}"; });
            }
            catch { Dispatcher.Invoke(() => IpAddressBox.Text = "取得失敗"); }
        }

        private void CopyIpButton_Click(object sender, RoutedEventArgs e)
        {
            if (IpAddressBox.Text != "取得中..." && IpAddressBox.Text != "取得失敗")
            {
                Clipboard.SetText(IpAddressBox.Text);
                MessageBox.Show("IPアドレスをコピーしました！", "コピー成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadServerProperties()
        {
            if (!File.Exists(propertiesPath)) return;
            Dictionary<string, string> settings = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(propertiesPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                string[] parts = line.Split('=', 2);
                if (parts.Length == 2) settings[parts[0]] = parts[1];
            }

            if (settings.ContainsKey("max-players")) MaxPlayersSettingBox.Text = settings["max-players"];
            if (settings.ContainsKey("gamemode")) GameModeSettingBox.SelectedItem = GameModeSettingBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content.ToString() == settings["gamemode"]);
            if (settings.ContainsKey("pvp")) PvpSettingBox.IsChecked = settings["pvp"] == "true";
            if (settings.ContainsKey("enable-command-block")) CommandBlockSettingBox.IsChecked = settings["enable-command-block"] == "true";
            if (settings.ContainsKey("server-port")) PortNumberBox.Text = settings["server-port"];
        }

        private void SaveServerProperties()
        {
            if (File.Exists(propertiesPath))
            {
                List<string> lines = File.ReadAllLines(propertiesPath).ToList();
                UpdateProperty(lines, "max-players", MaxPlayersSettingBox.Text);
                UpdateProperty(lines, "gamemode", ((ComboBoxItem)GameModeSettingBox.SelectedItem).Content.ToString());
                UpdateProperty(lines, "pvp", PvpSettingBox.IsChecked == true ? "true" : "false");
                UpdateProperty(lines, "enable-command-block", CommandBlockSettingBox.IsChecked == true ? "true" : "false");
                UpdateProperty(lines, "server-port", PortNumberBox.Text);
                File.WriteAllLines(propertiesPath, lines);
            }

            string ramInput = RamSettingBox.Text.Trim();
            currentServer.Ram = int.TryParse(ramInput, out _) ? ramInput + "M" : ramInput;
            RamSettingBox.Text = currentServer.Ram;

            string infoPath = Path.Combine(currentServer.FolderPath, "server_info.json");
            File.WriteAllText(infoPath, JsonSerializer.Serialize(currentServer, new JsonSerializerOptions { WriteIndented = true }));

            MessageBox.Show("設定とメモリ割り当てを保存しました！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadGlobalIpAsync();
        }

        private void UpdateProperty(List<string> lines, string key, string value)
        {
            int index = lines.FindIndex(line => line.TrimStart().StartsWith(key + "="));
            if (index != -1) lines[index] = $"{key}={value}";
            else lines.Add($"{key}={value}");
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e) => SaveServerProperties();

        private void LoadOpList()
        {
            OpListBox.Items.Clear();
            if (File.Exists(opsPath))
            {
                try
                {
                    string json = File.ReadAllText(opsPath);
                    var ops = JsonSerializer.Deserialize<List<OpPlayer>>(json);
                    if (ops != null) foreach (var op in ops) OpListBox.Items.Add(op.name);
                }
                catch { }
            }
        }

        private async void AddOpButton_Click(object sender, RoutedEventArgs e)
        {
            string playerName = NewOpNameBox.Text.Trim();
            if (string.IsNullOrEmpty(playerName)) return;
            AddOpButton.IsEnabled = false;

            if (serverProcess.IsRunning)
            {
                serverProcess.SendCommand($"op {playerName}");
                MessageBox.Show($"{playerName} にOP権限を付与しました！", "成功");
                if (!OpListBox.Items.Contains(playerName)) OpListBox.Items.Add(playerName);
            }
            else
            {
                try
                {
                    using HttpClient client = new HttpClient();
                    string response = await client.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{playerName}");
                    using JsonDocument doc = JsonDocument.Parse(response);

                    string rawUuid = doc.RootElement.GetProperty("id").GetString();
                    string actualName = doc.RootElement.GetProperty("name").GetString();
                    string formattedUuid = $"{rawUuid.Substring(0, 8)}-{rawUuid.Substring(8, 4)}-{rawUuid.Substring(12, 4)}-{rawUuid.Substring(16, 4)}-{rawUuid.Substring(20)}";

                    var ops = new List<OpPlayer>();
                    if (File.Exists(opsPath) && new FileInfo(opsPath).Length > 0) ops = JsonSerializer.Deserialize<List<OpPlayer>>(File.ReadAllText(opsPath)) ?? new List<OpPlayer>();

                    if (!ops.Any(o => o.name.Equals(actualName, StringComparison.OrdinalIgnoreCase)))
                    {
                        ops.Add(new OpPlayer { uuid = formattedUuid, name = actualName, level = 4, bypassesPlayerLimit = false });
                        File.WriteAllText(opsPath, JsonSerializer.Serialize(ops, new JsonSerializerOptions { WriteIndented = true }));
                    }

                    LoadOpList();
                    MessageBox.Show($"{actualName} をOPリストに追加しました！", "成功");
                }
                catch { MessageBox.Show("プレイヤー情報の取得に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            NewOpNameBox.Clear();
            AddOpButton.IsEnabled = true;
        }

        private void RemoveOpButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpListBox.SelectedItem == null) return;
            string playerName = OpListBox.SelectedItem.ToString();

            if (serverProcess.IsRunning)
            {
                serverProcess.SendCommand($"deop {playerName}");
                MessageBox.Show($"{playerName} のOP権限を剥奪しました。", "成功");
                OpListBox.Items.Remove(playerName);
            }
            else
            {
                if (File.Exists(opsPath))
                {
                    var ops = JsonSerializer.Deserialize<List<OpPlayer>>(File.ReadAllText(opsPath));
                    ops.RemoveAll(o => o.name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                    File.WriteAllText(opsPath, JsonSerializer.Serialize(ops, new JsonSerializerOptions { WriteIndented = true }));
                    LoadOpList();
                    MessageBox.Show($"{playerName} をOPリストから削除しました。", "成功");
                }
            }
        }

        // --- ★★★ ここからが復活させた起動・停止などの処理です ★★★ ---
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Text = "サーバーを起動しています...\n";
            serverProcess.Start(currentServer.FolderPath, currentServer.Ram);
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            serverProcess.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void KillButton_Click(object sender, RoutedEventArgs e)
        {
            serverProcess.Kill();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void SendCommandButton_Click(object sender, RoutedEventArgs e)
        {
            serverProcess.SendCommand(CommandBox.Text);
            LogBox.AppendText($"> {CommandBox.Text}\n");
            CommandBox.Clear();
        }

        private void CommandBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendCommandButton_Click(sender, e);
        }

        // --- ★★★ ここからが復活させたポート開放・プラグイン管理の処理です ★★★ ---
        private async void OpenPortButton_Click(object sender, RoutedEventArgs e)
        {
            NatStatusText.Text = "状態: ルーターを検索中...";
            NatStatusText.Foreground = Brushes.Orange;
            try
            {
                int port = int.Parse(PortNumberBox.Text);
                var discoverer = new NatDiscoverer();
                var device = await discoverer.DiscoverDeviceAsync();
                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, $"Minecraft Server ({currentServer.ServerName})"));
                NatStatusText.Text = $"状態: 成功！ ポート {port} を開放しました。";
                NatStatusText.Foreground = Brushes.Green;
                isPortOpenedByApp = true;
            }
            catch (Exception ex)
            {
                NatStatusText.Text = "状態: 失敗";
                NatStatusText.Foreground = Brushes.Red;
                MessageBox.Show("ポート開放に失敗しました。\n詳細: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ClosePortButton_Click(object sender, RoutedEventArgs e)
        {
            NatStatusText.Text = "状態: ルーターを検索中...";
            NatStatusText.Foreground = Brushes.Orange;
            try
            {
                int port = int.Parse(PortNumberBox.Text);
                var discoverer = new NatDiscoverer();
                var device = await discoverer.DiscoverDeviceAsync();
                await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port));
                NatStatusText.Text = $"状態: ポート {port} を閉じました。";
                NatStatusText.Foreground = Brushes.Black;
                isPortOpenedByApp = false;
            }
            catch { NatStatusText.Text = "状態: 失敗、または既に閉じています。"; NatStatusText.Foreground = Brushes.Gray; }
        }

        private void LoadPluginsList()
        {
            PluginsListBox.Items.Clear();
            if (Directory.Exists(pluginsPath))
            {
                string[] pluginFiles = Directory.GetFiles(pluginsPath, "*.jar");
                foreach (string file in pluginFiles)
                {
                    PluginsListBox.Items.Add(Path.GetFileName(file));
                }
            }
        }

        private void PluginsDropArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Copy;
            else e.Effects = DragDropEffects.None;
        }

        private void PluginsDropArea_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void PluginsDropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Directory.CreateDirectory(pluginsPath);

                int successCount = 0;
                foreach (string srcPath in files)
                {
                    if (Path.GetExtension(srcPath).Equals(".jar", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string destPath = Path.Combine(pluginsPath, Path.GetFileName(srcPath));
                            File.Copy(srcPath, destPath, true);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"ファイルのコピーに失敗しました: {Path.GetFileName(srcPath)}\n\n{ex.Message}", "エラー");
                        }
                    }
                }

                if (successCount > 0)
                {
                    MessageBox.Show($"{successCount}個のプラグインを追加しました！", "成功");
                    LoadPluginsList();
                }
            }
        }

        private void RemovePluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (PluginsListBox.SelectedItem == null)
            {
                MessageBox.Show("削除したいプラグインをリストから選択してください。", "情報");
                return;
            }

            string pluginName = PluginsListBox.SelectedItem.ToString();
            MessageBoxResult result = MessageBox.Show($"本当にプラグイン「{pluginName}」を削除しますか？", "最終確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(Path.Combine(pluginsPath, pluginName));
                    MessageBox.Show("プラグインを削除しました。", "成功");
                    LoadPluginsList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"削除に失敗しました。\n\n{ex.Message}", "エラー");
                }
            }
        }
    }
}