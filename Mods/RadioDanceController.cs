using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using rowemod.Utils;

namespace rowemod.Mods
{
    internal static class RadioDanceController
    {
        private sealed class PreparedTrack
        {
            public string Path;
            public string VideoPath;
            public string Title;
            public string Url;
            public string VideoId;
            public float Duration;
            public bool IncludeTv;
            public bool NetworkRemote;
            public long NetworkStartedUtcMs;
            public Vector3 NetworkRadioPosition;
            public Quaternion NetworkRadioRotation;
            public Vector3 NetworkTvPosition;
            public Quaternion NetworkTvRotation;
            public string NetworkPlaybackId;
        }

        private sealed class ToolResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }

        private const string HelperDownloadUrl =
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string HelperChecksumsUrl =
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";
        private const long MaximumHelperBytes = 40L * 1024L * 1024L;
        private const float MaximumTrackSeconds = 60f * 60f;
        private static readonly object Sync = new object();
        private static readonly string[] AudioExtensions =
        {
            ".m4a", ".mp3", ".ogg", ".opus", ".wav", ".webm"
        };

        private static string _youtubeUrl = string.Empty;
        private static string _status =
            "Paste a public YouTube song link. RoweMod caches the audio locally, then starts the placed radio.";
        private static bool _busy;
        private static int _generation;
        private static PreparedTrack _pendingTrack;
        private static string _pendingError;
        private static bool _ownsPlayback;
        private static PreparedTrack _pendingSynchronizedTrack;
        private static PreparedTrack _activeTrack;
        private static string _preparingNetworkPlaybackId;

        public static string YouTubeUrl
        {
            get => _youtubeUrl;
            set => _youtubeUrl = value ?? string.Empty;
        }

        public static IReadOnlyList<RecentRadioSong> RecentSongs =>
            Config.replaySettings.recentRadioSongs ??= new List<RecentRadioSong>();

        public static bool SelectRecentSong(int index)
        {
            IReadOnlyList<RecentRadioSong> songs = RecentSongs;
            if (index < 0 || index >= songs.Count || songs[index] == null)
                return false;
            _youtubeUrl = songs[index].url ?? string.Empty;
            SetStatus($"Selected {songs[index].title}. Choose radio or radio + TV.");
            return true;
        }

        public static string Status
        {
            get
            {
                lock (Sync)
                    return _status;
            }
        }

        public static bool IsBusy
        {
            get
            {
                lock (Sync)
                    return _busy || _pendingSynchronizedTrack != null;
            }
        }

        public static bool IsPlaying => EmotePropController.IsRadioPlaying;

        public static float Volume
        {
            get => Mathf.Clamp01(Config.replaySettings.radioVolume);
            set
            {
                float volume = Mathf.Clamp01(value);
                if (Mathf.Approximately(Config.replaySettings.radioVolume, volume))
                    return;

                Config.replaySettings.radioVolume = volume;
                EmotePropController.SetRadioVolume(volume);
                Config.RequestSave();
            }
        }

        public static bool StartFromYouTube()
        {
            return StartFromYouTube(false);
        }

        public static bool StartRadioAndTvFromYouTube()
        {
            return StartFromYouTube(true);
        }

        private static bool StartFromYouTube(bool includeTv)
        {
            string url = (_youtubeUrl ?? string.Empty).Trim();
            if (!TryValidateYouTubeUrl(url, out string validationError))
            {
                SetStatus(validationError);
                return false;
            }

            string modRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", "RoweMod"));
            string toolsDirectory = Path.Combine(modRoot, "Tools");
            string cacheDirectory = Path.Combine(modRoot, "RadioCache");
            int generation;
            lock (Sync)
            {
                if (_busy)
                    return false;

                _busy = true;
                _pendingTrack = null;
                _pendingError = null;
                _preparingNetworkPlaybackId = null;
                _status = includeTv
                    ? "Preparing YouTube audio and a compatible 720p TV video. The first run also downloads and verifies the official yt-dlp helper."
                    : "Preparing YouTube radio audio. The first run also downloads and verifies the official yt-dlp helper.";
                generation = ++_generation;
            }

            Task.Run(() => PrepareInBackground(url, toolsDirectory, cacheDirectory, generation, includeTv, null));
            return true;
        }

        public static bool IsAllowedYouTubeUrl(string value)
        {
            return TryValidateYouTubeUrl((value ?? string.Empty).Trim(), out _);
        }

        public static void StartSynchronizedFromNetwork(
            string url,
            string title,
            string videoId,
            float duration,
            bool includeTv,
            long startedUtcMs,
            Vector3 radioPosition,
            Quaternion radioRotation,
            Vector3 tvPosition,
            Quaternion tvRotation,
            string playbackId)
        {
            string validationError = null;
            if (!Config.replaySettings.multiplayerMediaEnabled ||
                !TryValidateYouTubeUrl((url ?? string.Empty).Trim(), out validationError))
            {
                SetStatus(validationError ?? "Multiplayer media sync is disabled.");
                return;
            }

            if ((_activeTrack != null &&
                 string.Equals(_activeTrack.NetworkPlaybackId, playbackId, StringComparison.Ordinal)) ||
                (_pendingSynchronizedTrack != null &&
                 string.Equals(_pendingSynchronizedTrack.NetworkPlaybackId, playbackId, StringComparison.Ordinal)))
                return;

            double elapsed = CalculateElapsedSeconds(startedUtcMs);
            if (duration > 0f && elapsed >= duration)
            {
                SetStatus("The shared track ended before this client finished joining.");
                return;
            }

            StopInternal(false, true);
            string modRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", "RoweMod"));
            string toolsDirectory = Path.Combine(modRoot, "Tools");
            string cacheDirectory = Path.Combine(modRoot, "RadioCache");
            int generation;
            var networkContext = new PreparedTrack
            {
                Title = title ?? string.Empty,
                Url = url.Trim(),
                VideoId = videoId ?? string.Empty,
                Duration = duration,
                IncludeTv = includeTv,
                NetworkRemote = true,
                NetworkStartedUtcMs = startedUtcMs,
                NetworkRadioPosition = radioPosition,
                NetworkRadioRotation = radioRotation,
                NetworkTvPosition = tvPosition,
                NetworkTvRotation = tvRotation,
                NetworkPlaybackId = playbackId
            };
            lock (Sync)
            {
                _busy = true;
                _pendingTrack = null;
                _pendingError = null;
                _preparingNetworkPlaybackId = playbackId;
                _status = $"Preparing shared station: {CleanTitle(title)}";
                generation = ++_generation;
            }
            Task.Run(() => PrepareInBackground(
                url.Trim(),
                toolsDirectory,
                cacheDirectory,
                generation,
                includeTv,
                networkContext));
        }

        public static void StopSynchronizedFromNetwork(string playbackId)
        {
            bool matches = (_activeTrack != null && _activeTrack.NetworkRemote &&
                            string.Equals(_activeTrack.NetworkPlaybackId, playbackId, StringComparison.Ordinal)) ||
                           (_pendingSynchronizedTrack != null && _pendingSynchronizedTrack.NetworkRemote &&
                            string.Equals(_pendingSynchronizedTrack.NetworkPlaybackId, playbackId, StringComparison.Ordinal));
            lock (Sync)
            {
                matches |= _pendingTrack != null && _pendingTrack.NetworkRemote &&
                           string.Equals(_pendingTrack.NetworkPlaybackId, playbackId, StringComparison.Ordinal) ||
                           string.Equals(_preparingNetworkPlaybackId, playbackId, StringComparison.Ordinal);
            }
            if (!matches)
                return;
            StopInternal(false, true);
            SetStatus("The shared multiplayer station was stopped.");
        }

        public static void Update()
        {
            bool tvStarted = VideoTvController.ConsumePlaybackStarted();
            bool tvFailed = VideoTvController.ConsumePlaybackFailed();
            if (_pendingSynchronizedTrack != null && (tvStarted || tvFailed))
            {
                PreparedTrack synchronizedTrack = _pendingSynchronizedTrack;
                _pendingSynchronizedTrack = null;
                if (synchronizedTrack.NetworkRemote && tvStarted)
                    VideoTvController.SeekTo(CalculateElapsedSeconds(synchronizedTrack.NetworkStartedUtcMs));
                if (TryBeginRadio(synchronizedTrack))
                {
                    MarkTrackStarted(synchronizedTrack);
                    SetStatus(tvStarted
                        ? synchronizedTrack.NetworkRemote
                            ? $"Joined shared radio and TV: {synchronizedTrack.Title}."
                            : $"Playing {synchronizedTrack.Title} on the synchronized radio and TV."
                        : $"Playing {synchronizedTrack.Title} on the radio; the TV could not start.");
                }
                else
                {
                    SetStatus(tvStarted
                        ? "The TV started, but FMOD could not start its 3D radio audio."
                        : "The TV and FMOD radio could not start.");
                }
            }

            PreparedTrack track = null;
            string error = null;
            lock (Sync)
            {
                if (_pendingTrack != null)
                {
                    track = _pendingTrack;
                    _pendingTrack = null;
                }
                if (!string.IsNullOrEmpty(_pendingError))
                {
                    error = _pendingError;
                    _pendingError = null;
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(error);
                Log.Warning("[RadioDance] " + error);
            }

            if (track != null)
            {
                if (track.NetworkRemote && track.Duration > 0f &&
                    CalculateElapsedSeconds(track.NetworkStartedUtcMs) >= track.Duration)
                {
                    SetStatus("The shared track ended while its media was being prepared.");
                    return;
                }

                if (!string.IsNullOrEmpty(track.VideoPath))
                {
                    bool videoStarted = track.NetworkRemote
                        ? VideoTvController.BeginVideoAt(
                            track.VideoPath,
                            track.Title,
                            track.Duration,
                            track.NetworkTvPosition,
                            track.NetworkTvRotation,
                            CalculateElapsedSeconds(track.NetworkStartedUtcMs))
                        : VideoTvController.BeginVideo(track.VideoPath, track.Title, track.Duration);
                    if (videoStarted)
                    {
                        _pendingSynchronizedTrack = track;
                        SetStatus(track.NetworkRemote
                            ? $"Preparing the shared TV frame for {track.Title}; radio audio will join at the current time."
                            : $"Preparing the first TV frame for {track.Title}; radio audio will start in sync.");
                    }
                    else if (TryBeginRadio(track))
                    {
                        MarkTrackStarted(track);
                        SetStatus($"Playing {track.Title} on the radio; the TV could not start.");
                    }
                    else
                    {
                        SetStatus("The prepared TV video and FMOD radio could not start.");
                    }
                }
                else if (TryBeginRadio(track))
                {
                    MarkTrackStarted(track);
                    SetStatus(track.NetworkRemote
                        ? $"Joined shared radio: {track.Title}."
                        : $"Playing {track.Title}. Choose or change a dance separately.");
                }
                else
                {
                    SetStatus("The audio was prepared, but FMOD could not start the 3D radio.");
                }
            }

            if (EmotePropController.ConsumeRadioPlaybackEnded())
            {
                PreparedTrack completedTrack = _activeTrack;
                _activeTrack = null;
                _ownsPlayback = false;
                if (completedTrack != null && !completedTrack.NetworkRemote)
                {
                    RememberCompletedTrack(completedTrack);
                    MultiplayerMediaSync.PublishStopped();
                }
                SetStatus("The radio track ended. Any active dance continues.");
                return;
            }

            if (_ownsPlayback && !EmotePropController.IsRadioPlaying)
            {
                if (_activeTrack != null && !_activeTrack.NetworkRemote)
                    MultiplayerMediaSync.PublishStopped();
                _activeTrack = null;
                _ownsPlayback = false;
                SetStatus("Radio stopped.");
            }
        }

        public static void Stop()
        {
            bool publishStop = _activeTrack != null && !_activeTrack.NetworkRemote;
            bool stopTv = _pendingSynchronizedTrack != null ||
                          (publishStop && Config.replaySettings.multiplayerMediaEnabled &&
                           _activeTrack.IncludeTv);
            StopInternal(publishStop, stopTv);
            SetStatus(stopTv
                ? "Shared radio and TV stopped. Any active dance continues."
                : "Radio stopped. Any active dance continues.");
        }

        private static void StopInternal(bool publishStop, bool stopTv)
        {
            lock (Sync)
            {
                _generation++;
                _busy = false;
                _pendingTrack = null;
                _pendingError = null;
                _pendingSynchronizedTrack = null;
                _preparingNetworkPlaybackId = null;
            }
            if (stopTv)
                VideoTvController.Stop();
            if (publishStop)
                MultiplayerMediaSync.PublishStopped();
            _ownsPlayback = false;
            _activeTrack = null;
            EmotePropController.StopRadio();
        }

        private static bool TryBeginRadio(PreparedTrack track)
        {
            if (track == null)
                return false;
            if (!track.NetworkRemote)
                return EmotePropController.BeginRadioTrack(track.Path, track.Duration);

            double elapsed = CalculateElapsedSeconds(track.NetworkStartedUtcMs);
            if (track.Duration > 0f && elapsed >= track.Duration)
                return false;
            return EmotePropController.BeginRadioTrackAt(
                track.Path,
                track.Duration,
                elapsed,
                track.NetworkRadioPosition,
                track.NetworkRadioRotation,
                true);
        }

        private static void MarkTrackStarted(PreparedTrack track)
        {
            _ownsPlayback = true;
            _activeTrack = track;
            _preparingNetworkPlaybackId = null;
            if (track == null || track.NetworkRemote)
                return;

            if (!EmotePropController.TryGetLiveRadioPose(out Vector3 radioPosition, out Quaternion radioRotation))
                return;
            Vector3 tvPosition = Vector3.zero;
            Quaternion tvRotation = Quaternion.identity;
            bool includeTv = track.IncludeTv && VideoTvController.TryGetPose(out tvPosition, out tvRotation);
            MultiplayerMediaSync.PublishStarted(
                track.Title,
                track.Url,
                track.VideoId,
                track.Duration,
                includeTv,
                radioPosition,
                radioRotation,
                includeTv ? tvPosition : Vector3.zero,
                includeTv ? tvRotation : Quaternion.identity);
        }

        public static void OnSceneInitialized()
        {
            lock (Sync)
            {
                _generation++;
                _busy = false;
                _pendingTrack = null;
                _pendingError = null;
                _ownsPlayback = false;
                _pendingSynchronizedTrack = null;
                _activeTrack = null;
                _preparingNetworkPlaybackId = null;
                _status = "Paste a public YouTube song link to start the placed radio.";
            }
            CleanupExistingDuplicateDownloads();
        }

        private static void PrepareInBackground(
            string url,
            string toolsDirectory,
            string cacheDirectory,
            int generation,
            bool includeTv,
            PreparedTrack networkContext)
        {
            try
            {
                Directory.CreateDirectory(toolsDirectory);
                Directory.CreateDirectory(cacheDirectory);
                string helperPath = EnsureHelper(toolsDirectory);
                PreparedTrack track = ResolveAndDownload(helperPath, cacheDirectory, url);
                if (networkContext != null &&
                    !string.Equals(track.VideoId, networkContext.VideoId, StringComparison.Ordinal))
                    throw new InvalidOperationException("the shared YouTube link did not resolve to the advertised video");
                if (includeTv)
                    track.VideoPath = ResolveAndDownloadVideo(helperPath, cacheDirectory, url, track);
                track.IncludeTv = includeTv;
                if (networkContext != null)
                {
                    // Trust downloaded metadata and paths, but retain the validated network
                    // placement/timing envelope used to join the already-running station.
                    track.NetworkRemote = true;
                    track.NetworkStartedUtcMs = networkContext.NetworkStartedUtcMs;
                    track.NetworkRadioPosition = networkContext.NetworkRadioPosition;
                    track.NetworkRadioRotation = networkContext.NetworkRadioRotation;
                    track.NetworkTvPosition = networkContext.NetworkTvPosition;
                    track.NetworkTvRotation = networkContext.NetworkTvRotation;
                    track.NetworkPlaybackId = networkContext.NetworkPlaybackId;
                }
                lock (Sync)
                {
                    if (generation != _generation)
                        return;
                    _pendingTrack = track;
                    _busy = false;
                    _status = $"Ready: {track.Title}";
                }
            }
            catch (Exception ex)
            {
                lock (Sync)
                {
                    if (generation != _generation)
                        return;
                    _pendingError = (includeTv ? "YouTube radio + TV failed: " : "YouTube radio failed: ") +
                                    CleanError(ex.Message);
                    _busy = false;
                }
            }
        }

        private static string EnsureHelper(string toolsDirectory)
        {
            string localPath = Path.Combine(toolsDirectory, "yt-dlp.exe");
            if (HasValidCachedHelper(localPath))
                return localPath;

            string pathTool = FindOnPath("yt-dlp.exe") ?? FindOnPath("yt-dlp");
            if (!string.IsNullOrEmpty(pathTool))
                return pathTool;

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RoweMod/3.3.4");
            string checksums = client.GetStringAsync(HelperChecksumsUrl).GetAwaiter().GetResult();
            string expectedHash = ParseExpectedHelperHash(checksums);
            byte[] helper = client.GetByteArrayAsync(HelperDownloadUrl).GetAwaiter().GetResult();
            if (helper.LongLength < 1024L * 1024L || helper.LongLength > MaximumHelperBytes)
                throw new InvalidOperationException("the yt-dlp helper download had an unexpected size");

            string actualHash;
            using (SHA256 sha = SHA256.Create())
                actualHash = Convert.ToHexString(sha.ComputeHash(helper));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("the yt-dlp helper checksum did not match the official release");

            string temporaryPath = localPath + ".download";
            File.WriteAllBytes(temporaryPath, helper);
            File.Move(temporaryPath, localPath, true);
            File.WriteAllText(localPath + ".sha256", actualHash);
            return localPath;
        }

        private static bool HasValidCachedHelper(string helperPath)
        {
            try
            {
                if (!File.Exists(helperPath) || new FileInfo(helperPath).Length <= 1024L * 1024L)
                    return false;
                string sidecarPath = helperPath + ".sha256";
                if (!File.Exists(sidecarPath))
                    return false;
                string expected = File.ReadAllText(sidecarPath).Trim();
                if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
                    return false;
                using SHA256 sha = SHA256.Create();
                using FileStream stream = File.OpenRead(helperPath);
                string actual = Convert.ToHexString(sha.ComputeHash(stream));
                return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static PreparedTrack ResolveAndDownload(string helperPath, string cacheDirectory, string url)
        {
            var metadataArguments = new List<string>();
            AddYouTubeRuntimeArguments(metadataArguments);
            metadataArguments.AddRange(new[]
            {
                "--no-playlist",
                "--skip-download",
                "--print", "ROWEMOD_ID=%(id)s",
                "--print", "ROWEMOD_TITLE=%(title)s",
                "--print", "ROWEMOD_DURATION=%(duration)s",
                url
            });
            ToolResult metadata = RunTool(helperPath, metadataArguments);
            if (metadata.ExitCode != 0)
                throw new InvalidOperationException(UsefulToolError(metadata));

            string id = ReadTaggedLine(metadata.Output, "ROWEMOD_ID=");
            string title = ReadTaggedLine(metadata.Output, "ROWEMOD_TITLE=");
            string durationText = ReadTaggedLine(metadata.Output, "ROWEMOD_DURATION=");
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new InvalidOperationException("YouTube returned an invalid video identifier");
            if (!float.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
                duration = 0f;
            if (duration > MaximumTrackSeconds)
                throw new InvalidOperationException("the selected video is longer than one hour");

            string cached = FindCachedTrack(cacheDirectory, id);
            if (cached == null)
            {
                string outputTemplate = Path.Combine(cacheDirectory, "%(id)s.%(ext)s");
                var downloadArguments = new List<string>();
                AddYouTubeRuntimeArguments(downloadArguments);
                downloadArguments.AddRange(new[]
                {
                    "--no-playlist",
                    "--no-progress",
                    "--windows-filenames",
                    "--max-filesize", "200M",
                    "-f", "bestaudio[ext=m4a]/bestaudio[ext=mp3]/bestaudio",
                    "-o", outputTemplate,
                    url
                });
                ToolResult download = RunTool(helperPath, downloadArguments);
                if (download.ExitCode != 0)
                    throw new InvalidOperationException(UsefulToolError(download));
                cached = FindCachedTrack(cacheDirectory, id);
            }

            if (cached == null)
                throw new InvalidOperationException("yt-dlp finished without creating a supported audio file");
            cached = NormalizeForFmodWhenPossible(cached, cacheDirectory, id);
            CleanupSupersededAudioFiles(cacheDirectory, id, cached);
            return new PreparedTrack
            {
                Path = cached,
                Title = string.IsNullOrWhiteSpace(title) ? id : title.Trim(),
                Url = url,
                VideoId = id,
                Duration = duration
            };
        }

        private static void AddYouTubeRuntimeArguments(List<string> arguments)
        {
            string node = FindOnPath("node.exe") ?? FindOnPath("node");
            if (string.IsNullOrEmpty(node))
                return;
            arguments.Add("--js-runtimes");
            arguments.Add("node:" + node);
        }

        private static string ResolveAndDownloadVideo(
            string helperPath,
            string cacheDirectory,
            string url,
            PreparedTrack track)
        {
            string id = Path.GetFileNameWithoutExtension(track.Path);
            int suffix = id.IndexOf(".rowemod", StringComparison.OrdinalIgnoreCase);
            if (suffix > 0)
                id = id.Substring(0, suffix);
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new InvalidOperationException("could not derive the YouTube video identifier for the TV cache");

            string cached = FindCachedVideo(cacheDirectory, id);
            if (cached != null)
                return NormalizeVideoForUnityWhenPossible(cached, cacheDirectory, id);

            string outputTemplate = Path.Combine(cacheDirectory, "%(id)s.rowemod_tv.%(ext)s");
            var downloadArguments = new List<string>();
            AddYouTubeRuntimeArguments(downloadArguments);
            downloadArguments.AddRange(new[]
            {
                "--no-playlist",
                "--no-progress",
                "--windows-filenames",
                "--max-filesize", "500M",
                "-f", "bestvideo[ext=mp4][vcodec^=avc1][height<=720]/bestvideo[ext=mp4][height<=720]",
                "-o", outputTemplate,
                url
            });
            ToolResult download = RunTool(helperPath, downloadArguments, 15 * 60 * 1000);
            if (download.ExitCode != 0)
                throw new InvalidOperationException(UsefulToolError(download));

            cached = FindCachedVideo(cacheDirectory, id);
            if (cached == null)
                throw new InvalidOperationException("yt-dlp finished without creating a Unity-compatible MP4 video");
            return NormalizeVideoForUnityWhenPossible(cached, cacheDirectory, id);
        }

        private static string NormalizeVideoForUnityWhenPossible(string sourcePath, string cacheDirectory, string id)
        {
            string normalizedPath = Path.Combine(cacheDirectory, id + ".rowemod_tv.unity.mp4");
            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(normalizedPath), StringComparison.OrdinalIgnoreCase))
            {
                CleanupSupersededVideoFiles(cacheDirectory, id, normalizedPath);
                return sourcePath;
            }
            if (File.Exists(normalizedPath) && new FileInfo(normalizedPath).Length > 4096L)
            {
                CleanupSupersededVideoFiles(cacheDirectory, id, normalizedPath);
                return normalizedPath;
            }

            string ffmpeg = FindOnPath("ffmpeg.exe") ?? FindOnPath("ffmpeg");
            if (string.IsNullOrEmpty(ffmpeg))
                return sourcePath;

            ToolResult conversion = RunTool(ffmpeg, new[]
            {
                "-hide_banner",
                "-loglevel", "error",
                "-y",
                "-i", sourcePath,
                "-an",
                "-vf", "scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2:black,setsar=1",
                "-r", "30",
                "-codec:v", "libx264",
                "-profile:v", "baseline",
                "-level:v", "3.1",
                "-pix_fmt", "yuv420p",
                "-preset", "veryfast",
                "-crf", "20",
                "-g", "60",
                "-keyint_min", "30",
                "-sc_threshold", "0",
                "-movflags", "+faststart",
                normalizedPath
            }, 20 * 60 * 1000);
            if (conversion.ExitCode != 0 || !File.Exists(normalizedPath) || new FileInfo(normalizedPath).Length <= 4096L)
                throw new InvalidOperationException("ffmpeg could not create a Unity-safe TV video: " + UsefulToolError(conversion));

            CleanupSupersededVideoFiles(cacheDirectory, id, normalizedPath);
            return normalizedPath;
        }

        private static void CleanupSupersededVideoFiles(string cacheDirectory, string id, string retainedPath)
        {
            string retainedFullPath = Path.GetFullPath(retainedPath);
            foreach (string sourcePath in Directory.EnumerateFiles(
                         cacheDirectory,
                         id + ".rowemod_tv*.mp4",
                         SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFullPath(sourcePath), retainedFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    File.Delete(sourcePath);
                    Log.Msg($"[RadioDance] Removed superseded TV download: {Path.GetFileName(sourcePath)}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RadioDance] Could not remove superseded TV download {Path.GetFileName(sourcePath)}: {CleanError(ex.Message)}");
                }
            }
        }

        private static string NormalizeForFmodWhenPossible(string sourcePath, string cacheDirectory, string id)
        {
            string extension = Path.GetExtension(sourcePath);
            if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                return sourcePath;

            string ffmpeg = FindOnPath("ffmpeg.exe") ?? FindOnPath("ffmpeg");
            if (string.IsNullOrEmpty(ffmpeg))
                return sourcePath;

            string normalizedPath = Path.Combine(cacheDirectory, id + ".rowemod.mp3");
            if (File.Exists(normalizedPath) && new FileInfo(normalizedPath).Length > 4096L)
                return normalizedPath;

            ToolResult conversion = RunTool(ffmpeg, new[]
            {
                "-hide_banner",
                "-loglevel", "error",
                "-y",
                "-i", sourcePath,
                "-vn",
                "-ac", "2",
                "-ar", "44100",
                "-codec:a", "libmp3lame",
                "-q:a", "2",
                normalizedPath
            });
            if (conversion.ExitCode != 0 || !File.Exists(normalizedPath))
                throw new InvalidOperationException("ffmpeg could not convert the downloaded audio: " + UsefulToolError(conversion));
            return normalizedPath;
        }

        private static void CleanupSupersededAudioFiles(string cacheDirectory, string id, string retainedPath)
        {
            string retainedFullPath = Path.GetFullPath(retainedPath);
            foreach (string candidate in Directory.EnumerateFiles(cacheDirectory, id + ".*", SearchOption.TopDirectoryOnly))
            {
                string extension = Path.GetExtension(candidate);
                if (!AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFullPath(candidate), retainedFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    File.Delete(candidate);
                    Log.Msg($"[RadioDance] Removed superseded audio download: {Path.GetFileName(candidate)}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RadioDance] Could not remove superseded audio {Path.GetFileName(candidate)}: {CleanError(ex.Message)}");
                }
            }

            foreach (string suffix in new[] { ".part", ".ytdl", ".temp" })
            {
                foreach (string candidate in Directory.EnumerateFiles(cacheDirectory, id + "*" + suffix, SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(candidate); }
                    catch { }
                }
            }
        }

        private static void CleanupExistingDuplicateDownloads()
        {
            try
            {
                string modRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", "RoweMod"));
                string cacheDirectory = Path.Combine(modRoot, "RadioCache");
                if (!Directory.Exists(cacheDirectory))
                    return;

                foreach (string sourcePath in Directory.EnumerateFiles(cacheDirectory, "*.*", SearchOption.TopDirectoryOnly))
                {
                    string extension = Path.GetExtension(sourcePath);
                    if (!AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        continue;

                    string id = Path.GetFileNameWithoutExtension(sourcePath);
                    if (id.EndsWith(".rowemod", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string normalizedPath = Path.Combine(cacheDirectory, id + ".rowemod.mp3");
                    if (!File.Exists(normalizedPath) || new FileInfo(normalizedPath).Length <= 4096L)
                        continue;

                    File.Delete(sourcePath);
                    Log.Msg($"[RadioDance] Removed existing superseded audio download: {Path.GetFileName(sourcePath)}");
                }

                foreach (string normalizedVideo in Directory.EnumerateFiles(
                             cacheDirectory,
                             "*.rowemod_tv.unity.mp4",
                             SearchOption.TopDirectoryOnly))
                {
                    const string suffix = ".rowemod_tv.unity.mp4";
                    string fileName = Path.GetFileName(normalizedVideo);
                    if (fileName.Length <= suffix.Length || !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string id = fileName.Substring(0, fileName.Length - suffix.Length);
                    CleanupSupersededVideoFiles(cacheDirectory, id, normalizedVideo);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RadioDance] Existing download cleanup failed: {CleanError(ex.Message)}");
            }
        }

        private static void RememberCompletedTrack(PreparedTrack track)
        {
            if (track == null || string.IsNullOrWhiteSpace(track.Title) ||
                string.IsNullOrWhiteSpace(track.Url) || string.IsNullOrWhiteSpace(track.VideoId))
                return;

            List<RecentRadioSong> songs = Config.replaySettings.recentRadioSongs ??= new List<RecentRadioSong>();
            songs.RemoveAll(song => song == null ||
                                    string.Equals(song.url, track.Url, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(song.videoId, track.VideoId, StringComparison.OrdinalIgnoreCase));
            songs.Insert(0, new RecentRadioSong
            {
                title = track.Title.Trim(),
                url = track.Url.Trim(),
                videoId = track.VideoId.Trim()
            });
            RecentRadioSong evicted = null;
            if (songs.Count > 5)
            {
                evicted = songs[5];
                songs.RemoveRange(5, songs.Count - 5);
            }
            Config.RequestSave();
            DeleteEvictedSongCache(evicted, songs);
        }

        private static void DeleteEvictedSongCache(RecentRadioSong evicted, IReadOnlyList<RecentRadioSong> retainedSongs)
        {
            string id = evicted?.videoId?.Trim();
            if (string.IsNullOrWhiteSpace(id) ||
                retainedSongs.Any(song => song != null &&
                                           string.Equals(song.videoId, id, StringComparison.OrdinalIgnoreCase)))
                return;

            try
            {
                string modRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", "RoweMod"));
                string cacheDirectory = Path.Combine(modRoot, "RadioCache");
                if (!Directory.Exists(cacheDirectory))
                    return;

                foreach (string candidate in Directory.EnumerateFiles(cacheDirectory, id + ".*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(candidate);
                    Log.Msg($"[RadioDance] Deleted evicted recent-song cache: {Path.GetFileName(candidate)}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RadioDance] Could not delete the evicted recent-song cache: {CleanError(ex.Message)}");
            }
        }

        private static ToolResult RunTool(
            string executable,
            IEnumerable<string> arguments,
            int timeoutMilliseconds = 5 * 60 * 1000)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using Process process = Process.Start(startInfo) ??
                                    throw new InvalidOperationException("could not start yt-dlp");
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try { process.Kill(true); }
                catch { }
                throw new TimeoutException($"the media helper did not finish within {timeoutMilliseconds / 60000} minutes");
            }
            Task.WaitAll(stdoutTask, stderrTask);
            return new ToolResult
            {
                ExitCode = process.ExitCode,
                Output = stdoutTask.Result ?? string.Empty,
                Error = stderrTask.Result ?? string.Empty
            };
        }

        private static string FindCachedTrack(string directory, string id)
        {
            if (!Directory.Exists(directory))
                return null;
            return Directory.EnumerateFiles(directory, id + ".*", SearchOption.TopDirectoryOnly)
                .Where(path => AudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string FindCachedVideo(string directory, string id)
        {
            if (!Directory.Exists(directory))
                return null;
            return Directory.EnumerateFiles(directory, id + ".rowemod_tv.*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string FindOnPath(string executable)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string folder in path.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim(), executable);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch { }
            }
            return null;
        }

        private static string ParseExpectedHelperHash(string checksums)
        {
            foreach (string rawLine in (checksums ?? string.Empty).Split('\n'))
            {
                string line = rawLine.Trim();
                if (!line.EndsWith("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                string hash = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (hash != null && hash.Length == 64 && hash.All(Uri.IsHexDigit))
                    return hash;
            }
            throw new InvalidOperationException("the official yt-dlp checksum list did not contain yt-dlp.exe");
        }

        private static string ReadTaggedLine(string output, string tag)
        {
            foreach (string rawLine in (output ?? string.Empty).Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.StartsWith(tag, StringComparison.Ordinal))
                    return line.Substring(tag.Length).Trim();
            }
            return string.Empty;
        }

        private static string UsefulToolError(ToolResult result)
        {
            string source = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            string[] lines = (source ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length == 0 ? $"yt-dlp exited with code {result.ExitCode}" : lines[^1].Trim();
        }

        private static string CleanError(string value)
        {
            string clean = (value ?? "unknown error").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= 260 ? clean : clean.Substring(0, 257) + "...";
        }

        private static string CleanTitle(string value)
        {
            string clean = (value ?? "shared track").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= 100 ? clean : clean.Substring(0, 97) + "...";
        }

        private static double CalculateElapsedSeconds(long startedUtcMs)
        {
            if (startedUtcMs <= 0L)
                return 0d;
            return Math.Max(0d,
                (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedUtcMs) / 1000d);
        }

        private static bool TryValidateYouTubeUrl(string value, out string error)
        {
            error = null;
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                error = "Paste a complete YouTube URL first.";
                return false;
            }

            string host = uri.Host.TrimEnd('.');
            bool allowed = host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
                           host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                           host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);
            if (!allowed)
            {
                error = "Radio Dance currently accepts youtube.com and youtu.be links only.";
                return false;
            }
            return true;
        }

        private static void SetStatus(string value)
        {
            lock (Sync)
                _status = value;
        }
    }
}
