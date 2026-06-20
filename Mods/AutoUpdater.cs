using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MelonLoader;
using Newtonsoft.Json;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Networking;
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    public static class AutoUpdater
    {
        private const int WindowId = 0x524f5550;
        private const int CheckIntervalHours = 12;
        private const int RequestTimeoutSeconds = 15;
        private const long MaxDownloadBytes = 25L * 1024L * 1024L;
        private const string UpdateDirectoryName = "Updates";

        private static readonly object CoroutineLock = new object();
        private static Rect windowRect = new Rect(80f, 80f, 440f, 230f);
        private static bool initialized;
        private static bool checkInProgress;
        private static bool downloadInProgress;
        private static bool promptVisible;
        private static bool installScheduled;
        private static string statusText = string.Empty;
        private static string stagedUpdatePath;
        private static UpdateManifest availableManifest;

        private class UpdateManifest
        {
            public string version { get; set; }
            public string downloadUrl { get; set; }
            public string sha256 { get; set; }
            public string notesUrl { get; set; }
            public string requiredGameVersion { get; set; }
        }

        public static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            if (!Config.updaterSettings.enabled)
            {
                Log.Msg("[AutoUpdater] Update checks disabled in config.");
                return;
            }

            if (checkInProgress)
                return;

            if (!ShouldCheckNow())
            {
                Log.Msg("[AutoUpdater] Skipping update check; checked recently.");
                return;
            }

            StartCoroutine(CheckForUpdates());
        }

        public static void DrawUpdatePrompt(bool menuOpen)
        {
            if (!promptVisible || availableManifest == null)
                return;

            if (!menuOpen)
            {
                DrawClosedMenuNotice();
                return;
            }

            windowRect = GUI.Window(WindowId, windowRect, (GUI.WindowFunction)DrawUpdateWindow, "RoweMod Update");
        }

        private static void DrawClosedMenuNotice()
        {
            const float width = 420f;
            const float height = 34f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, 18f, width, height);
            GUI.Box(rect, $"RoweMod {availableManifest.version} is available. Open RoweMod to update.");
        }

        private static void DrawUpdateWindow(int id)
        {
            GUILayout.Label($"Installed: {Main.ModVersion}");
            GUILayout.Label($"Available: {availableManifest.version}");

            if (!string.IsNullOrWhiteSpace(statusText))
                GUILayout.Label(statusText);
            else
                GUILayout.Label("Download the update now, then close the game to finish installing.");

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUI.enabled = !downloadInProgress && !installScheduled;
            if (GUILayout.Button(stagedUpdatePath == null ? "Download" : "Re-download", GUILayout.Height(30f)))
            {
                StartCoroutine(DownloadUpdate());
            }

            GUI.enabled = stagedUpdatePath != null && !downloadInProgress && !installScheduled;
            if (GUILayout.Button("Install On Exit", GUILayout.Height(30f)))
            {
                ScheduleInstallOnExit();
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(availableManifest.notesUrl);
            if (GUILayout.Button("Notes", GUILayout.Width(72f), GUILayout.Height(30f)))
            {
                Application.OpenURL(availableManifest.notesUrl);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Later", GUILayout.Height(26f)))
            {
                promptVisible = false;
            }

            if (GUILayout.Button("Skip Version", GUILayout.Height(26f)))
            {
                Config.updaterSettings.skippedVersion = availableManifest.version;
                Config.Save();
                promptVisible = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private static IEnumerator CheckForUpdates()
        {
            checkInProgress = true;
            string manifestUrl = Config.updaterSettings.manifestUrl;
            Log.Msg($"[AutoUpdater] Checking for updates: {manifestUrl}");

            UnityWebRequest request = UnityWebRequest.Get(manifestUrl);
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            try
            {
                Config.updaterSettings.lastCheckUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Config.Save();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning($"[AutoUpdater] Update check failed: {request.error}");
                    yield break;
                }

                UpdateManifest manifest = JsonConvert.DeserializeObject<UpdateManifest>(request.downloadHandler.text);
                if (!IsValidManifest(manifest, out string validationError))
                {
                    Log.Warning($"[AutoUpdater] Ignoring invalid update manifest: {validationError}");
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(manifest.requiredGameVersion) &&
                    !string.Equals(manifest.requiredGameVersion, Application.version, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Msg(
                        $"[AutoUpdater] Update {manifest.version} requires game version {manifest.requiredGameVersion}; " +
                        $"current game version is {Application.version}.");
                    yield break;
                }

                if (!IsRemoteVersionNewer(manifest.version, Main.ModVersion))
                {
                    Log.Msg($"[AutoUpdater] RoweMod is current. local={Main.ModVersion}, remote={manifest.version}.");
                    yield break;
                }

                if (string.Equals(Config.updaterSettings.skippedVersion, manifest.version, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Msg($"[AutoUpdater] Update {manifest.version} is skipped by config.");
                    yield break;
                }

                availableManifest = manifest;
                promptVisible = true;
                statusText = "A new RoweMod version is available.";
                Log.Msg($"[AutoUpdater] Update available: local={Main.ModVersion}, remote={manifest.version}.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoUpdater] Update check error: {ex.Message}");
            }
            finally
            {
                checkInProgress = false;
                request.Dispose();
            }
        }

        private static IEnumerator DownloadUpdate()
        {
            if (availableManifest == null || downloadInProgress)
                yield break;

            downloadInProgress = true;
            statusText = "Downloading update...";
            stagedUpdatePath = null;

            UnityWebRequest request = UnityWebRequest.Get(availableManifest.downloadUrl);
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    statusText = $"Download failed: {request.error}";
                    Log.Warning($"[AutoUpdater] Download failed: {request.error}");
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null || data.Length == 0)
                {
                    statusText = "Download failed: empty file.";
                    yield break;
                }

                if (data.Length > MaxDownloadBytes)
                {
                    statusText = "Download failed: file is too large.";
                    Log.Warning($"[AutoUpdater] Refusing oversized update download: {data.Length} bytes.");
                    yield break;
                }

                if (!VerifySha256(data, availableManifest.sha256, out string actualHash))
                {
                    statusText = "Download failed: hash mismatch.";
                    Log.Warning(
                        $"[AutoUpdater] Hash mismatch for {availableManifest.version}. " +
                        $"expected={availableManifest.sha256}, actual={actualHash}");
                    yield break;
                }

                string updateDir = GetUpdateDirectory();
                Directory.CreateDirectory(updateDir);
                string safeVersion = MakeSafeFilePart(availableManifest.version);
                string stagedPath = Path.Combine(updateDir, $"rowemod-{safeVersion}.dll");
                File.WriteAllBytes(stagedPath, data);
                stagedUpdatePath = stagedPath;
                statusText = "Downloaded. Click Install On Exit, then close BMX Streets.";
                Log.Msg($"[AutoUpdater] Downloaded update to {stagedPath}. sha256={actualHash}");
            }
            catch (Exception ex)
            {
                statusText = $"Download failed: {ex.Message}";
                Log.Error($"[AutoUpdater] Download error: {ex}");
            }
            finally
            {
                downloadInProgress = false;
                request.Dispose();
            }
        }

        private static void ScheduleInstallOnExit()
        {
            if (availableManifest == null || string.IsNullOrWhiteSpace(stagedUpdatePath) || !File.Exists(stagedUpdatePath))
            {
                statusText = "Download the update before installing.";
                return;
            }

            try
            {
                string targetPath = typeof(Main).Assembly.Location;
                string targetDir = Path.GetDirectoryName(targetPath);
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    statusText = "Install failed: could not locate mod DLL.";
                    return;
                }

                string updateDir = GetUpdateDirectory();
                Directory.CreateDirectory(updateDir);

                string backupPath = Path.Combine(updateDir, $"rowemod-{Main.ModVersion}.dll.bak");
                string logPath = Path.Combine(updateDir, "update-install.log");
                string scriptPath = Path.Combine(updateDir, "install-rowemod-update.cmd");
                string pendingPath = Path.Combine(updateDir, "pending-update.json");

                File.WriteAllText(
                    pendingPath,
                    JsonConvert.SerializeObject(new
                    {
                        version = availableManifest.version,
                        stagedPath = stagedUpdatePath,
                        targetPath,
                        backupPath,
                        scheduledUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    }, Formatting.Indented));

                File.WriteAllText(
                    scriptPath,
                    BuildInstallScript(
                        Process.GetCurrentProcess().Id,
                        stagedUpdatePath,
                        targetPath,
                        backupPath,
                        logPath,
                        pendingPath),
                    Encoding.ASCII);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);

                installScheduled = true;
                statusText = "Install scheduled. Close BMX Streets to finish updating.";
                Log.Msg($"[AutoUpdater] Scheduled update install using helper script: {scriptPath}");
            }
            catch (Exception ex)
            {
                statusText = $"Install schedule failed: {ex.Message}";
                Log.Error($"[AutoUpdater] Failed to schedule update install: {ex}");
            }
        }

        private static string BuildInstallScript(
            int processId,
            string stagedPath,
            string targetPath,
            string backupPath,
            string logPath,
            string pendingPath)
        {
            string escapedStagedPath = EscapeCmdValue(stagedPath);
            string escapedTargetPath = EscapeCmdValue(targetPath);
            string escapedBackupPath = EscapeCmdValue(backupPath);
            string escapedLogPath = EscapeCmdValue(logPath);
            string escapedPendingPath = EscapeCmdValue(pendingPath);

            return string.Join(
                "\r\n",
                "@echo off",
                "setlocal EnableExtensions",
                $"set \"GAME_PID={processId}\"",
                $"set \"STAGED={escapedStagedPath}\"",
                $"set \"TARGET={escapedTargetPath}\"",
                $"set \"BACKUP={escapedBackupPath}\"",
                $"set \"LOG={escapedLogPath}\"",
                $"set \"PENDING={escapedPendingPath}\"",
                "echo RoweMod update helper started. > \"%LOG%\"",
                "echo Waiting for BMX Streets process %GAME_PID% to exit... >> \"%LOG%\"",
                ":wait_for_game",
                "tasklist /FI \"PID eq %GAME_PID%\" 2>NUL | findstr /R /C:\"^[ ]*%GAME_PID% \" >NUL",
                "if not errorlevel 1 (",
                "  timeout /t 1 /nobreak >NUL",
                "  goto wait_for_game",
                ")",
                "timeout /t 2 /nobreak >NUL",
                "echo Replacing DLL... >> \"%LOG%\"",
                "if not exist \"%STAGED%\" (",
                "  echo Staged update missing: %STAGED% >> \"%LOG%\"",
                "  exit /b 2",
                ")",
                "if exist \"%BACKUP%\" del /f /q \"%BACKUP%\" >> \"%LOG%\" 2>&1",
                "if exist \"%TARGET%\" copy /y \"%TARGET%\" \"%BACKUP%\" >> \"%LOG%\" 2>&1",
                "copy /y \"%STAGED%\" \"%TARGET%\" >> \"%LOG%\" 2>&1",
                "if errorlevel 1 (",
                "  echo Copy failed. Attempting rollback. >> \"%LOG%\"",
                "  if exist \"%BACKUP%\" copy /y \"%BACKUP%\" \"%TARGET%\" >> \"%LOG%\" 2>&1",
                "  exit /b 1",
                ")",
                "del /f /q \"%STAGED%\" >> \"%LOG%\" 2>&1",
                "del /f /q \"%PENDING%\" >> \"%LOG%\" 2>&1",
                "echo RoweMod update installed successfully. >> \"%LOG%\"",
                "exit /b 0",
                string.Empty);
        }

        private static bool ShouldCheckNow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long lastCheck = Config.updaterSettings.lastCheckUnixUtc;
            return lastCheck <= 0 || now - lastCheck >= CheckIntervalHours * 60L * 60L;
        }

        private static bool IsValidManifest(UpdateManifest manifest, out string reason)
        {
            reason = null;

            if (manifest == null)
            {
                reason = "manifest is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.version))
            {
                reason = "missing version";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.downloadUrl) ||
                !Uri.TryCreate(manifest.downloadUrl, UriKind.Absolute, out Uri downloadUri) ||
                (downloadUri.Scheme != Uri.UriSchemeHttps && downloadUri.Scheme != Uri.UriSchemeHttp))
            {
                reason = "missing or invalid downloadUrl";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(manifest.sha256) &&
                manifest.sha256.Trim().Length != 64)
            {
                reason = "invalid sha256";
                return false;
            }

            return true;
        }

        private static bool IsRemoteVersionNewer(string remoteVersion, string localVersion)
        {
            if (TryParseVersion(remoteVersion, out Version remote) &&
                TryParseVersion(localVersion, out Version local))
            {
                return remote > local;
            }

            return string.Compare(
                NormalizeVersion(remoteVersion),
                NormalizeVersion(localVersion),
                StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            string normalized = NormalizeVersion(value);
            return Version.TryParse(normalized, out version);
        }

        private static string NormalizeVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0.0.0";

            string normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(1);

            int suffixIndex = normalized.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffixIndex >= 0)
                normalized = normalized.Substring(0, suffixIndex);

            return normalized;
        }

        private static bool VerifySha256(byte[] data, string expectedHash, out string actualHash)
        {
            using SHA256 sha256 = SHA256.Create();
            actualHash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();

            return string.IsNullOrWhiteSpace(expectedHash) ||
                   string.Equals(actualHash, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetUpdateDirectory()
        {
            return Path.Combine(Config.modFolder, UpdateDirectoryName);
        }

        private static string MakeSafeFilePart(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalid, '_');
            return safe;
        }

        private static string EscapeCmdValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\"", string.Empty)
                .Replace("%", "%%");
        }

        private static void StartCoroutine(IEnumerator coroutine)
        {
            lock (CoroutineLock)
            {
                MelonCoroutines.Start(coroutine);
            }
        }
    }
}
