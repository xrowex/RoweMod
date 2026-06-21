using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        private const int RequestTimeoutSeconds = 15;
        private const long MaxDownloadBytes = 25L * 1024L * 1024L;
        private const string UpdateDirectoryName = "Updates";

        private static readonly object CoroutineLock = new object();
        private static Rect windowRect = new Rect(80f, 80f, 560f, 320f);
        private static bool initialized;
        private static bool checkInProgress;
        private static bool downloadInProgress;
        private static bool promptVisible;
        private static bool installScheduled;
        private static bool autoInstallStarted;
        private static bool windowPositionInitialized;
        private static bool stylesInitialized;
        private static string statusText = string.Empty;
        private static string stagedUpdatePath;
        private static UpdateManifest availableManifest;
        private static GUIStyle updateWindowStyle;
        private static GUIStyle updateTitleStyle;
        private static GUIStyle updateInfoStyle;
        private static GUIStyle updateStatusStyle;
        private static GUIStyle primaryButtonStyle;
        private static GUIStyle secondaryButtonStyle;
        private static GUIStyle bannerStyle;
        private static GUIStyle bannerTitleStyle;
        private static GUIStyle bannerBodyStyle;

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

            StartCoroutine(CheckForUpdates());
        }

        public static void DrawUpdatePrompt(bool menuOpen)
        {
            if (!promptVisible || availableManifest == null)
                return;

            EnsureStyles();

            if (!menuOpen)
            {
                DrawClosedMenuNotice();
                return;
            }

            if (!windowPositionInitialized)
            {
                CenterUpdateWindow();
                windowPositionInitialized = true;
            }

            windowRect = ClampToScreen(windowRect);
            windowRect = GUI.Window(
                WindowId,
                windowRect,
                (GUI.WindowFunction)DrawUpdateWindow,
                "RoweMod Update",
                updateWindowStyle ?? GUI.skin.window);
        }

        private static void DrawClosedMenuNotice()
        {
            float width = Mathf.Min(620f, Mathf.Max(320f, Screen.width - 32f));
            const float height = 76f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, 18f, width, height);
            GUI.Box(rect, GUIContent.none, bannerStyle ?? GUI.skin.box);
            GUI.Label(
                new Rect(rect.x + 18f, rect.y + 12f, rect.width - 36f, 24f),
                $"ROWE MOD {availableManifest.version} UPDATE AVAILABLE",
                bannerTitleStyle ?? GUI.skin.label);
            GUI.Label(
                new Rect(rect.x + 18f, rect.y + 39f, rect.width - 36f, 24f),
                "Open the RoweMod menu to download and install on exit.",
                bannerBodyStyle ?? GUI.skin.label);
        }

        private static void DrawUpdateWindow(int id)
        {
            GUILayout.Label("Update Available", updateTitleStyle ?? GUI.skin.label);
            GUILayout.Space(6f);
            GUILayout.Label($"Installed: {Main.ModVersion}     Available: {availableManifest.version}", updateInfoStyle ?? GUI.skin.label);

            if (!string.IsNullOrWhiteSpace(statusText))
                GUILayout.Label(statusText, updateStatusStyle ?? GUI.skin.label);
            else
                GUILayout.Label(
                    "Download the update now, then close BMX Streets to finish installing.",
                    updateStatusStyle ?? GUI.skin.label);

            GUILayout.Space(14f);

            GUILayout.BeginHorizontal();
            GUI.enabled = !downloadInProgress && !installScheduled;
            if (GUILayout.Button(
                    stagedUpdatePath == null ? "Download Update" : "Re-download Update",
                    primaryButtonStyle ?? GUI.skin.button,
                    GUILayout.Height(36f)))
            {
                StartCoroutine(DownloadUpdate());
            }

            GUI.enabled = stagedUpdatePath != null && !downloadInProgress && !installScheduled;
            if (GUILayout.Button("Install On Exit", primaryButtonStyle ?? GUI.skin.button, GUILayout.Height(36f)))
            {
                ScheduleInstallOnExit();
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(availableManifest.notesUrl);
            if (GUILayout.Button("Notes", secondaryButtonStyle ?? GUI.skin.button, GUILayout.Width(92f), GUILayout.Height(36f)))
            {
                Application.OpenURL(availableManifest.notesUrl);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Later", secondaryButtonStyle ?? GUI.skin.button, GUILayout.Width(120f), GUILayout.Height(30f)))
            {
                promptVisible = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private static void EnsureStyles()
        {
            if (stylesInitialized)
                return;

            stylesInitialized = true;

            Color background = new Color(0.04f, 0.05f, 0.065f, 0.98f);
            Color panel = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            Color panelHover = new Color(0.13f, 0.145f, 0.18f, 0.98f);
            Color accent = new Color(0.18f, 0.6f, 0.9f, 1f);
            Color accentHover = new Color(0.24f, 0.68f, 1f, 1f);
            Color border = new Color(1f, 1f, 1f, 0.18f);
            Color alert = new Color(0.95f, 0.46f, 0.16f, 1f);
            Color alertSoft = new Color(0.13f, 0.075f, 0.045f, 0.98f);
            Color text = new Color(0.95f, 0.96f, 0.98f, 1f);
            Color muted = new Color(0.72f, 0.76f, 0.82f, 1f);

            Texture2D windowBackground = Menu.MakeRoundedTex(96, 96, background, 12, 1, border);
            Texture2D panelBackground = Menu.MakeRoundedTex(40, 28, panel, 7, 1, border);
            Texture2D panelHoverBackground = Menu.MakeRoundedTex(40, 28, panelHover, 7, 1, border);
            Texture2D accentBackground = Menu.MakeRoundedTex(40, 28, accent, 7, 1, new Color(1f, 1f, 1f, 0.28f));
            Texture2D accentHoverBackground = Menu.MakeRoundedTex(40, 28, accentHover, 7, 1, new Color(1f, 1f, 1f, 0.32f));
            Texture2D bannerBackground = Menu.MakeRoundedTex(96, 48, alertSoft, 10, 2, alert);

            updateWindowStyle = new GUIStyle(Menu.windowStyle ?? GUI.skin.window);
            updateWindowStyle.normal.background = windowBackground;
            updateWindowStyle.hover.background = windowBackground;
            updateWindowStyle.active.background = windowBackground;
            updateWindowStyle.normal.textColor = text;
            updateWindowStyle.hover.textColor = text;
            updateWindowStyle.active.textColor = text;
            updateWindowStyle.fontSize = 15;
            updateWindowStyle.fontStyle = FontStyle.Bold;
            updateWindowStyle.alignment = TextAnchor.UpperCenter;
            updateWindowStyle.padding = new RectOffset(22, 22, 46, 18);

            updateTitleStyle = new GUIStyle(Menu.labelStyle ?? GUI.skin.label);
            updateTitleStyle.normal.textColor = text;
            updateTitleStyle.fontSize = 22;
            updateTitleStyle.fontStyle = FontStyle.Bold;
            updateTitleStyle.alignment = TextAnchor.MiddleCenter;

            updateInfoStyle = new GUIStyle(Menu.labelStyle ?? GUI.skin.label);
            updateInfoStyle.normal.textColor = muted;
            updateInfoStyle.fontSize = 13;
            updateInfoStyle.alignment = TextAnchor.MiddleCenter;

            updateStatusStyle = new GUIStyle(Menu.labelStyle ?? GUI.skin.label);
            updateStatusStyle.normal.textColor = text;
            updateStatusStyle.fontSize = 13;
            updateStatusStyle.wordWrap = true;
            updateStatusStyle.alignment = TextAnchor.MiddleCenter;

            primaryButtonStyle = new GUIStyle(Menu.highQualityButtonStyle ?? GUI.skin.button);
            primaryButtonStyle.normal.background = accentBackground;
            primaryButtonStyle.hover.background = accentHoverBackground;
            primaryButtonStyle.active.background = accentBackground;
            primaryButtonStyle.normal.textColor = Color.white;
            primaryButtonStyle.hover.textColor = Color.white;
            primaryButtonStyle.active.textColor = Color.white;
            primaryButtonStyle.fontSize = 13;
            primaryButtonStyle.fontStyle = FontStyle.Bold;
            primaryButtonStyle.alignment = TextAnchor.MiddleCenter;

            secondaryButtonStyle = new GUIStyle(Menu.highQualityButtonStyle ?? GUI.skin.button);
            secondaryButtonStyle.normal.background = panelBackground;
            secondaryButtonStyle.hover.background = panelHoverBackground;
            secondaryButtonStyle.active.background = panelBackground;
            secondaryButtonStyle.normal.textColor = text;
            secondaryButtonStyle.hover.textColor = text;
            secondaryButtonStyle.active.textColor = text;
            secondaryButtonStyle.fontSize = 12;
            secondaryButtonStyle.alignment = TextAnchor.MiddleCenter;

            bannerStyle = new GUIStyle(GUI.skin.box);
            bannerStyle.normal.background = bannerBackground;
            bannerStyle.border = new RectOffset(10, 10, 10, 10);
            bannerStyle.padding = new RectOffset(18, 18, 10, 10);

            bannerTitleStyle = new GUIStyle(Menu.labelStyle ?? GUI.skin.label);
            bannerTitleStyle.normal.textColor = Color.white;
            bannerTitleStyle.fontSize = 15;
            bannerTitleStyle.fontStyle = FontStyle.Bold;
            bannerTitleStyle.alignment = TextAnchor.MiddleCenter;

            bannerBodyStyle = new GUIStyle(Menu.labelStyle ?? GUI.skin.label);
            bannerBodyStyle.normal.textColor = new Color(1f, 0.86f, 0.72f, 1f);
            bannerBodyStyle.fontSize = 12;
            bannerBodyStyle.alignment = TextAnchor.MiddleCenter;
        }

        private static void CenterUpdateWindow()
        {
            const float width = 560f;
            const float height = 320f;
            windowRect = new Rect(
                Mathf.Max(12f, (Screen.width - width) * 0.5f),
                Mathf.Max(12f, (Screen.height - height) * 0.5f),
                Mathf.Min(width, Mathf.Max(320f, Screen.width - 24f)),
                Mathf.Min(height, Mathf.Max(260f, Screen.height - 24f)));
        }

        private static Rect ClampToScreen(Rect rect)
        {
            rect.width = Mathf.Min(Mathf.Max(rect.width, 320f), Mathf.Max(320f, Screen.width - 24f));
            rect.height = Mathf.Min(Mathf.Max(rect.height, 260f), Mathf.Max(260f, Screen.height - 24f));
            rect.x = Mathf.Clamp(rect.x, 12f, Mathf.Max(12f, Screen.width - rect.width - 12f));
            rect.y = Mathf.Clamp(rect.y, 12f, Mathf.Max(12f, Screen.height - rect.height - 12f));
            return rect;
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
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning($"[AutoUpdater] Update check failed for {manifestUrl}: {request.error}");
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
                windowPositionInitialized = false;
                statusText = "A new RoweMod version is available.";
                Log.Msg($"[AutoUpdater] Update available: local={Main.ModVersion}, remote={manifest.version}.");
                StartAutoDownloadAndSchedule();
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

        private static void StartAutoDownloadAndSchedule()
        {
            if (autoInstallStarted || installScheduled)
                return;

            autoInstallStarted = true;
            StartCoroutine(AutoDownloadAndScheduleInstall());
        }

        private static IEnumerator AutoDownloadAndScheduleInstall()
        {
            Log.Msg($"[AutoUpdater] Auto-download started for {availableManifest?.version ?? "unknown version"}.");

            IEnumerator download = DownloadUpdate();
            while (download.MoveNext())
                yield return download.Current;

            if (installScheduled)
                yield break;

            if (!string.IsNullOrWhiteSpace(stagedUpdatePath) && File.Exists(stagedUpdatePath))
            {
                ScheduleInstallOnExit();
                yield break;
            }

            Log.Warning("[AutoUpdater] Auto-install was not scheduled because the update was not downloaded.");
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
                promptVisible = false;
                statusText = "Install scheduled. Close BMX Streets to finish updating.";
                Log.Msg($"[AutoUpdater] Scheduled update install using helper script: {scriptPath}. Hiding update prompt.");
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
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Wait-Process -Id %GAME_PID% -ErrorAction SilentlyContinue\" >> \"%LOG%\" 2>&1",
                "ping -n 3 127.0.0.1 >NUL",
                "echo Replacing DLL... >> \"%LOG%\"",
                "if exist \"%STAGED%\" goto staged_exists",
                "echo Staged update missing: %STAGED% >> \"%LOG%\"",
                "exit /b 2",
                ":staged_exists",
                "if exist \"%BACKUP%\" del /f /q \"%BACKUP%\" >> \"%LOG%\" 2>&1",
                "if exist \"%TARGET%\" copy /y \"%TARGET%\" \"%BACKUP%\" >> \"%LOG%\" 2>&1",
                "copy /y \"%STAGED%\" \"%TARGET%\" >> \"%LOG%\" 2>&1",
                "if not errorlevel 1 goto copy_ok",
                "echo Copy failed. Attempting rollback. >> \"%LOG%\"",
                "if exist \"%BACKUP%\" copy /y \"%BACKUP%\" \"%TARGET%\" >> \"%LOG%\" 2>&1",
                "exit /b 1",
                ":copy_ok",
                "del /f /q \"%STAGED%\" >> \"%LOG%\" 2>&1",
                "del /f /q \"%PENDING%\" >> \"%LOG%\" 2>&1",
                "echo RoweMod update installed successfully. >> \"%LOG%\"",
                "exit /b 0",
                string.Empty);
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
