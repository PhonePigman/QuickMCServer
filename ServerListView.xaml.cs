using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace QuickMCServer
{
    public class ServerInfo
    {
        public string ServerName { get; set; }
        public string Version { get; set; }
        public string Ram { get; set; }
        public string FolderPath { get; set; }
    }

    public partial class ServerListView : UserControl
    {
        public ServerListView()
        {
            InitializeComponent();
            this.Loaded += ServerListView_Loaded;
        }

        private void ServerListView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadServers();
        }

        private void LoadServers()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string serversRootPath = Path.Combine(baseDir, "Servers");

            if (!Directory.Exists(serversRootPath))
            {
                return;
            }

            List<ServerInfo> servers = new List<ServerInfo>();
            foreach (string serverDir in Directory.GetDirectories(serversRootPath))
            {
                string infoPath = Path.Combine(serverDir, "server_info.json");
                if (File.Exists(infoPath))
                {
                    string json = File.ReadAllText(infoPath);
                    ServerInfo info = JsonSerializer.Deserialize<ServerInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    info.FolderPath = serverDir;
                    servers.Add(info);
                }
            }
            ServerList.ItemsSource = servers;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.MainContent.Content = new HomeView();
        }

        private void ManageServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerList.SelectedItem != null)
            {
                ServerInfo selectedServer = (ServerInfo)ServerList.SelectedItem;
                MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                mainWindow.MainContent.Content = new ServerManagerView(selectedServer);
            }
            else
            {
                MessageBox.Show("リストから管理したいサーバーを選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- ★★★ ここにサーバー削除の処理を追加 ★★★ ---
        private void DeleteServerButton_Click(object sender, RoutedEventArgs e)
        {
            // まず、リストで何かが選択されているか確認
            if (ServerList.SelectedItem == null)
            {
                MessageBox.Show("リストから削除したいサーバーを選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                return; // 何もせず処理を中断
            }

            // 選択されているサーバーの情報を取得
            ServerInfo selectedServer = (ServerInfo)ServerList.SelectedItem;

            // 「本当に削除しますか？」とユーザーに最終確認を取る
            MessageBoxResult result = MessageBox.Show(
                $"本当にサーバー「{selectedServer.ServerName}」を削除しますか？\nこの操作は元に戻せません。",
                "最終確認",
                MessageBoxButton.YesNo, // 「はい」と「いいえ」のボタン
                MessageBoxImage.Warning // 警告アイコン
            );

            // もしユーザーが「はい(Yes)」を押したら...
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // サーバーのフォルダを、中身ごとすべて削除する
                    // true を指定することで、フォルダが空でなくても強制的に削除できる
                    Directory.Delete(selectedServer.FolderPath, true);

                    MessageBox.Show($"サーバー「{selectedServer.ServerName}」を削除しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 画面のリストを再読み込みして、削除されたサーバーが消えたことを反映させる
                    LoadServers();
                }
                catch (Exception ex)
                {
                    // もし何らかの理由で削除に失敗した場合（ファイルが使用中など）
                    MessageBox.Show("サーバーの削除に失敗しました。\n\n詳細: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}