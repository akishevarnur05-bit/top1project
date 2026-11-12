using System.Diagnostics;
using System.IO;
using CyberDefender.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyberDefender.InputProviders
{
    /// <summary>
    /// Сам запускает Python-мост (камера + микрофон), чтобы игру можно было открыть одним ярлыком.
    /// Если мост уже работает (присылает данные), второй экземпляр не запускается.
    /// В .exe мост лежит рядом с игрой в папке ai_bridge, в редакторе — в Tools/ai_bridge.
    /// Окна моста закрываются при выходе из .exe (в редакторе остаются — так быстрее перезапускать Play).
    /// </summary>
    public sealed class AiBridgeLauncher : MonoBehaviour
    {
        private const float DetectDelay = 2.5f;
        private const string BatchFile = "start_ai_bridge.bat";
        private static readonly string[] WindowTitles = { "CyberDefender - Gestures", "CyberDefender - Voice" };

        private bool bridgeOnline;
        private bool launched;
        private float checkAt;

        public static string BridgeDirectory =>
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "ai_bridge"))
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "ai_bridge");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
                return;
            var go = new GameObject("AI Bridge Launcher");
            DontDestroyOnLoad(go);
            go.AddComponent<AiBridgeLauncher>();
        }

        private void OnEnable()
        {
            GameEventBus.OnAiBridgeStatusChanged += HandleStatus;
            GameSettings.Changed += HandleSettingsChanged;
            checkAt = Time.unscaledTime + DetectDelay;
        }

        private void OnDisable()
        {
            GameEventBus.OnAiBridgeStatusChanged -= HandleStatus;
            GameSettings.Changed -= HandleSettingsChanged;
        }

        private void Update()
        {
            // Ждём пару секунд: вдруг мост уже запущен вручную и сейчас пришлёт данные
            if (checkAt <= 0f || Time.unscaledTime < checkAt)
                return;
            checkAt = 0f;
            if (GameSettings.AutoStartBridge && !bridgeOnline)
                Launch();
        }

        private void HandleStatus(InputSource source, bool online)
        {
            if (online)
                bridgeOnline = true;
        }

        private void HandleSettingsChanged()
        {
            // Включили в настройках — запускаем сразу, выключили — закрываем то, что запустили сами
            if (GameSettings.AutoStartBridge && !launched && !bridgeOnline && checkAt <= 0f)
                Launch();
            else if (!GameSettings.AutoStartBridge && launched)
                Stop();
        }

        private void Launch()
        {
            // Окно установки или мост уже открыты (запущены вручную, но ещё не прислали данные) — второй раз не запускаем
            if (IsBridgeWindowOpen())
            {
                Debug.Log("[AiBridgeLauncher] AI-мост уже запускается — жду подключения.");
                return;
            }

            string dir = BridgeDirectory;
            string bat = Path.Combine(dir, BatchFile);
            if (!File.Exists(bat))
            {
                Debug.LogWarning($"[AiBridgeLauncher] Не найден {bat} — камера и микрофон недоступны, работает клавиатура.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{BatchFile}\"")
                {
                    WorkingDirectory = dir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                });
                launched = true;
                Debug.Log("[AiBridgeLauncher] AI-мост запущен.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AiBridgeLauncher] Не удалось запустить AI-мост: {e.Message}");
            }
        }

        private static bool IsBridgeWindowOpen()
        {
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string title = process.MainWindowTitle;
                    if (!string.IsNullOrEmpty(title) && (title.Contains("start_ai_bridge") || title.Contains("CyberDefender - ")))
                        return true;
                }
                catch (System.Exception)
                {
                    // процесс завершился или недоступен — пропускаем
                }
                finally
                {
                    process.Dispose();
                }
            }
            return false;
        }

        private void Stop()
        {
            launched = false;
            bridgeOnline = false;
            foreach (string title in WindowTitles)
            {
                try
                {
                    // /T — вместе с python внутри окна
                    using (Process.Start(new ProcessStartInfo("taskkill", $"/F /T /FI \"WINDOWTITLE eq {title}*\"")
                           {
                               UseShellExecute = false,
                               CreateNoWindow = true,
                           }))
                    {
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AiBridgeLauncher] Не удалось закрыть «{title}»: {e.Message}");
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (launched && !Application.isEditor)
                Stop();
        }
    }
}
