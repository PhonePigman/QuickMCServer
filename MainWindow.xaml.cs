using System.ComponentModel;
using System.Windows;

namespace QuickMCServer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new HomeView();
        }

        // ★★★ ここからが新しく追加された警告処理 ★★★
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            // まず、現在表示されているのが「管理画面(ServerManagerView)」かどうかを調べる
            if (MainContent.Content is ServerManagerView view)
            {
                bool isServerRunning = view.IsServerRunning;
                bool isPortPotentiallyOpen = view.IsPortPotentiallyOpen; // ポートが開いている可能性

                // サーバーが起動中、またはポートが開いている可能性がある場合のみ警告を出す
                if (isServerRunning || isPortPotentiallyOpen)
                {
                    // 警告メッセージを組み立てる
                    string warningMessage = "アプリケーションを閉じようとしています。\n\n";
                    if (isServerRunning)
                    {
                        warningMessage += "● サーバーが起動中です。このまま閉じると強制終了されます。\n";
                    }
                    if (isPortPotentiallyOpen)
                    {
                        warningMessage += "● ポートが開放されている可能性があります。\n";
                    }
                    warningMessage += "\nポートも閉じてから終了しますか？";

                    // 「はい」「いいえ」「キャンセル」の3つのボタンがある特別なメッセージボックスを表示
                    MessageBoxResult result = MessageBox.Show(warningMessage, "終了前の最終確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    switch (result)
                    {
                        // 「はい」：ポートを閉じてから、サーバーを強制終了して、アプリを閉じる
                        case MessageBoxResult.Yes:
                            await view.ClosePortOnExitAsync();
                            view.ForceKillServer();
                            break;

                        // 「いいえ」：サーバーだけ強制終了して、アプリを閉じる（ポートは開いたまま）
                        case MessageBoxResult.No:
                            view.ForceKillServer();
                            break;

                        // 「キャンセル」：アプリを閉じるのをやめて、元の画面に戻る
                        case MessageBoxResult.Cancel:
                            e.Cancel = true; // ★これが「閉じるのをキャンセルする」という魔法の命令
                            break;
                    }
                }
            }
        }
    }
}