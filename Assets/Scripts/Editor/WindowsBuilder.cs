using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyberDefender.EditorTools
{
    /// <summary>
    /// Сборка игры в Windows .exe одной командой: CyberDefender → Build Windows EXE.
    /// Кладёт рядом с игрой AI-мост (без .venv — он создаётся при первом запуске) и делает ярлык на рабочем столе.
    /// Из командной строки: Unity.exe -batchmode -quit -projectPath . -executeMethod CyberDefender.EditorTools.WindowsBuilder.BuildFromCommandLine
    /// </summary>
    public static class WindowsBuilder
    {
        public const string ProductName = "CyberDefender Kernel Clash";
        private const string ExeName = "CyberDefender.exe";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string OutputDirectory => Path.Combine(ProjectRoot, "Builds", "CyberDefender");

        [MenuItem("CyberDefender/Build Windows EXE")]
        public static void BuildFromMenu()
        {
            if (Build())
                EditorUtility.RevealInFinder(Path.Combine(OutputDirectory, ExeName));
        }

        public static void BuildFromCommandLine()
        {
            if (!Build())
                EditorApplication.Exit(1);
        }

        private static bool Build()
        {
            // Сцена собирается заново, чтобы в .exe попали последние изменения сборщика
            GameSceneBuilder.Build();

            ConfigurePlayer();

            string exePath = Path.Combine(OutputDirectory, ExeName);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[WindowsBuilder] Сборка не удалась: {report.summary.result}, ошибок: {report.summary.totalErrors}");
                return false;
            }

            CopyAiBridge();
            CreateDesktopShortcut(exePath);
            Debug.Log($"[WindowsBuilder] Готово: {exePath} ({report.summary.totalSize / (1024 * 1024)} МБ)");
            return true;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = "CyberDefender Labs";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            // Окна камеры и голоса забирают фокус — без этого игра вставала бы на паузу
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
        }

        /// <summary>Копирует Tools/ai_bridge в папку игры без виртуального окружения и кэша.</summary>
        private static void CopyAiBridge()
        {
            string source = Path.Combine(ProjectRoot, "Tools", "ai_bridge");
            string target = Path.Combine(OutputDirectory, "ai_bridge");
            if (!Directory.Exists(source))
            {
                Debug.LogWarning("[WindowsBuilder] Tools/ai_bridge не найден — игра будет работать только с клавиатуры.");
                return;
            }

            CopyDirectory(source, target);
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(dir);
                // .venv содержит абсолютные пути и не переносится — bat создаст его заново
                if (name == ".venv" || name == "__pycache__")
                    continue;
                CopyDirectory(dir, Path.Combine(target, name));
            }
        }

        private static void CreateDesktopShortcut(string exePath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, ProductName + ".lnk");
                string workDir = Path.GetDirectoryName(exePath);
                string script =
                    "$s = (New-Object -ComObject WScript.Shell).CreateShortcut('" + lnk.Replace("'", "''") + "'); " +
                    "$s.TargetPath = '" + exePath.Replace("'", "''") + "'; " +
                    "$s.WorkingDirectory = '" + workDir.Replace("'", "''") + "'; " +
                    "$s.IconLocation = '" + exePath.Replace("'", "''") + ",0'; " +
                    "$s.Save()";

                using (Process process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{script}\"")
                       {
                           UseShellExecute = false,
                           CreateNoWindow = true,
                       }))
                {
                    process?.WaitForExit(15000);
                }
                Debug.Log($"[WindowsBuilder] Ярлык на рабочем столе: {lnk}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WindowsBuilder] Не удалось создать ярлык: {e.Message}");
            }
        }
    }
}
