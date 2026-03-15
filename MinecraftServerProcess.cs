using System;
using System.Diagnostics;
using System.IO;

namespace QuickMCServer
{
    public class MinecraftServerProcess
    {
        private Process serverProcess;
        public event EventHandler<string> OnLogReceived;

        // 実行中かどうかの判定（エラーが出ないように安全にしています）
        public bool IsRunning
        {
            get
            {
                try
                {
                    return serverProcess != null && !serverProcess.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Start(string serverFolderPath, string ram)
        {
            string jarPath = Path.Combine(serverFolderPath, "server.jar");

            serverProcess = new Process();
            serverProcess.StartInfo.FileName = "java";
            serverProcess.StartInfo.Arguments = $"-Xmx{ram} -Xms{ram} -jar \"{jarPath}\" nogui";
            serverProcess.StartInfo.WorkingDirectory = serverFolderPath;

            serverProcess.StartInfo.UseShellExecute = false;
            serverProcess.StartInfo.RedirectStandardOutput = true;
            serverProcess.StartInfo.RedirectStandardInput = true;
            serverProcess.StartInfo.RedirectStandardError = true;
            serverProcess.StartInfo.CreateNoWindow = true;

            serverProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) OnLogReceived?.Invoke(this, e.Data); };
            serverProcess.ErrorDataReceived += (sender, e) => { if (e.Data != null) OnLogReceived?.Invoke(this, "[ERROR] " + e.Data); };

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();
        }

        public void SendCommand(string command)
        {
            if (IsRunning) serverProcess.StandardInput.WriteLine(command);
        }

        public void Stop()
        {
            SendCommand("stop");
        }

        // ★★★ タスクマネージャーと同じ完全な強制終了 ★★★
        public void Kill()
        {
            try
            {
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    // Kill(true) と書くことで、WindowsのAPIを直接叩き、
                    // Java本体とその裏で動いている全ての子プロセスを一撃で「タスクキル」します。
                    serverProcess.Kill(true);

                    // 確実に消滅するまで、最大3秒間だけプログラム側で待機して見届けます
                    serverProcess.WaitForExit(3000);
                }
            }
            catch
            {
                // すでに終了している場合などのエラーは無視して安全に処理を終わります
            }
        }
    }
}