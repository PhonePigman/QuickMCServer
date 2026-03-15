using System.Windows;
using System.Windows.Controls;

namespace QuickMCServer
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        // 「新規作成」ボタンが押された時の処理
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // MainWindow（額縁）を探し出して、中身を CreateServerView（新規作成画面）に入れ替える
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.MainContent.Content = new CreateServerView();
        }

        // 「管理」ボタンが押された時の処理
        private void ManageButton_Click(object sender, RoutedEventArgs e)
        {
            // 中身を ServerListView（一覧画面）に入れ替える
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.MainContent.Content = new ServerListView();
        }
    }
}