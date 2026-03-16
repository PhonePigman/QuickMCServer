# QuickMCServer

MinecraftサーバーをGUIで直感的に管理・起動できるツールです。
A simple and intuitive GUI tool for managing and launching Minecraft servers.

## 概要 (Overview)
「コマンド操作は難しいけど、自分専用のマイクラサーバーを立てたい」という方のためのツールです。
WPF (.NET 8.0) で構築されており、複雑な設定を直感的な画面操作で完結させることを目指しています。

## 主な機能 (Features)
*   **ワンクリック起動/停止**: サーバーの開始・停止をボタンひとつで実行
*   **簡単設定管理**: サーバーのプロパティをGUIから編集可能
*   **リアルタイムログ**: コンソールログをアプリ画面上で確認
*   **軽量・高速**: .NET 8.0 の最新ランタイムで動作

## 使い方 (How to Use)
1.  Releases ページから最新の `QuickMCServer_v1.0.0.zip` をダウンロードします。
2.  サーバーデータを保存したい任意のフォルダに展開したファイルを配置します。
3.  exe を実行し、画面の指示に従ってサーバーのセットアップを行ってください。

### 動作要件 (Requirements)
*   **OS**: Windows 10 / 11 (64bit)
*   **Runtime**: [.NET Desktop Runtime 8.0](https://dotnet.microsoft.com) 
*   **Java**: Minecraftサーバーのバージョンに対応した Java (1.20.x以降なら Java 17以上推奨)

## ライセンス (License)
このプロジェクトは **MIT License** のもとで公開されています。詳細は LICENSE ファイルをご覧ください。
This project is licensed under the MIT License - see the LICENSE file for details.

## 開発について (Development)
このソフトウェアのコードの大部分は、生成AIの支援を受けて作成されました。
Most of the code for this software was generated with the assistance of AI.

## 注意事項　(Important Notices)
* ルーターが2台重なっている環境では、本ソフトで自動開放できるのは直近のルーターのみです。その場合は親ルーター側で手動設定が必要です。
  In a double router setup, this app can only open the port on the closest router. You will need to manually configure the primary router.
* ルーター側のUPnP機能が「無効」に設定されている場合、自動開放は動作しません。
  This feature will not work if UPnP is disabled in your router's settings.
* プロバイダやマンションの回線仕様により、ポート開放自体が制限されている場合があります。
  Port forwarding may be restricted depending on your ISP or shared network environment.
* WindowsファイアウォールでJavaおよび本ソフトの通信を許可してください。
  Ensure Java and this app are allowed through your Windows Firewall.
