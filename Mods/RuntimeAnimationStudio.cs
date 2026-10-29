using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace rowemod.Mods
{
    internal static partial class RuntimeAnimationStudio
    {
        internal static bool IsOpen { get; private set; }
        internal static StudioLibrary Library { get; private set; }
        internal static StudioVariation Working { get; private set; }
        internal static List<StudioSource> Sources { get; private set; } = new();
        internal static StudioPreviewStage Stage { get; private set; }
        internal static StudioSource Source;
        internal static int Phase;
        internal static float Time;
        internal static string SelectedTarget = "Hips";
        internal static string Status = "Choose a trick to make a variation.";
        private static bool playing, dirty, autoKey = true, draftChanged, gesture, wasMenuOpen;
        private static StudioPose draft;
        private static float previewSpeed = 1;
        private static readonly Stack<string> undo = new(), redo = new();
        private static readonly List<EventSystem> disabledEvents = new();
        private static Action pendingAction;
        private static bool confirmDiscard;
        private static bool libraryAttempted;
        private static string search = "";
        private static int libraryTab, targetTab;
        private static bool selectedKeysOnly = true;
        private static bool textInputFocused;
        private static Vector2 libraryScroll, targetScroll;
        private static readonly string[] Phases = { "Enter", "Loop", "Tweak", "Exit" };
        internal static bool OwnsScene(string name) => name == StudioPreviewStage.SceneName;
        internal static void Initialize(bool retry = false)
        {
            if (Library != null || (libraryAttempted && !retry)) return;
            libraryAttempted = true;
            var library = new StudioLibrary(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Mods", "rowemod", "AnimationStudio"));
            try { library.Load(); Library = library; }
            catch (Exception ex) { Log.Warning("[AnimationStudio] Library could not be read: " + ex.Message); }
        }
        internal static void Open()
        {
            if (IsOpen) return;
            if (!Main.IsGameMainMenuActive || Main.playableSceneLoaded)
            { Status = "Open Animation Studio from the Streets main menu."; return; }
            Initialize(true); if (Library == null) { Status = "The variation library could not be read. Check the log and try again."; return; }
            wasMenuOpen = Menu.isOpen;
            try
            {
                Sources = TrickAnimationEditor.StudioSources();
                if (Sources.Count == 0) throw new InvalidOperationException("The game animations are still loading. Try again in a moment.");
                Stage = new StudioPreviewStage(); Stage.Create();
                Main.OpenRoweModMenu();
                foreach (var events in Resources.FindObjectsOfTypeAll<EventSystem>())
                    if (events.gameObject.scene.IsValid() && events.gameObject.scene.isLoaded && events.enabled)
                    { disabledEvents.Add(events); events.enabled = false; }
                IsOpen = true;
                if (Working == null || Source == null || !Sources.Any(s => s.Key == Working.TrickKey)) SelectSource(Sources[0].Key);
                else Source = Sources.First(s => s.Key == Working.TrickKey);
                Status = "Edit a copy, save a variation, then Apply to use it in game.";
                Log.Msg($"[AnimationStudio] Opened with {Sources.Count} game tricks and {Library.Variations.Count} saved variations.");
            }
            catch (Exception ex) { CloseNow(); Status = ex.Message; Log.Warning("[AnimationStudio] " + ex); }
        }
        internal static void RequestClose() => RequestAction(CloseNow);
        private static void RequestAction(Action action)
        {
            if (dirty || draftChanged) { pendingAction = action; confirmDiscard = true; playing = false; }
            else action();
        }
        internal static void CloseNow()
        {
            IsOpen = false; playing = false; confirmDiscard = false; pendingAction = null;
            Stage?.Dispose(); Stage = null;
            foreach (var events in disabledEvents) if (events != null) events.enabled = true;
            disabledEvents.Clear();
            StudioRuntimePlayer.Restore();
            if (!wasMenuOpen) Main.CloseRoweModMenu();
            Working = null; Source = null; dirty = draftChanged = gesture = false; undo.Clear(); redo.Clear();
        }
        internal static void OnSceneChanged(string scene)
        {
            if (IsOpen && !OwnsScene(scene)) CloseNow();
            StudioRuntimePlayer.Restore();
        }
        internal static void SelectSource(string key)
        {
            Source = Sources.FirstOrDefault(s => s.Key == key); if (Source == null) return;
            Working = new StudioVariation { Name = Source.Name + " - My style", TrickKey = Source.Key };
            for (int p = 0; p < 4; p++) { Working.RiderClips[p] = Source.Clip(p, false)?.name; Working.BikeClips[p] = Source.Clip(p, true)?.name; }
            Phase = 0; Time = .5f; dirty = draftChanged = false; undo.Clear(); redo.Clear(); playing = false; SyncDraft();
        }
        private static void OpenVariation(StudioVariation variation)
        {
            Source = Sources.FirstOrDefault(s => s.Key == variation.TrickKey);
            if (Source == null) { Status = "This variation's base trick is not loaded."; return; }
            Working = variation.Copy(); dirty = draftChanged = false; Phase = 0; Time = .5f;
            playing = false;
            undo.Clear(); redo.Clear(); SyncDraft();
            if (Enumerable.Range(0, 4).Any(p => Working.RiderClips[p] != Source.Clip(p, false)?.name || Working.BikeClips[p] != Source.Clip(p, true)?.name))
                Status = "This saved variation used different base clips. Review the stock BMX preview; Save rebases a backed-up copy.";
        }
        private static void SyncDraft() { draft = Working?.Evaluate(Phase, SelectedTarget, Time) ?? default; draftChanged = false; SyncFields(); }
        private static float Duration => Source?.Clip(Phase, false)?.length ?? 1;
        private static float FrameRate => Mathf.Max(1, Source?.Clip(Phase, false)?.frameRate ?? 30);
        private static float Snap(float normalized) => Mathf.Clamp01(Mathf.Round(normalized * Duration * FrameRate) / Mathf.Max(1, Duration * FrameRate));
        private static void JumpKey(int direction)
        {
            if (draftChanged) { Status = "Set a key or discard the unkeyed pose first."; return; }
            var next = Working?.AdjacentKey(Phase, Time, direction, selectedKeysOnly ? SelectedTarget : null);
            if (!next.HasValue) { Status = "No " + (direction < 0 ? "previous" : "next") + " key in this phase."; return; }
            playing = false; Time = next.Value; SyncDraft();
        }
        private static void StepFrame(int direction) { playing = false; Seek(Time + direction / Mathf.Max(1, Duration * FrameRate)); }
        private static void SetContact(int limb, StudioContactMode mode)
        {
            if (Working == null || draftChanged) { Status = "Set a pose key before changing contact."; return; }
            Remember(); Working.SetContact(Phase, limb, Time, mode); dirty = true; playing = false;
            if (mode == StudioContactMode.Locked) Stage.CaptureContact(Working.ContactKeyAt(Phase, limb, Time));
            Status = StudioTargets.Effectors[limb] + ": " + (mode == StudioContactMode.Auto ? "Inherited" : mode.ToString()) + " keyed. Inherited follows the game's animated contact.";
        }
        private static void Seek(float normalized)
        {
            if (draftChanged) { Status = "Set a key or discard the unkeyed pose before scrubbing."; return; }
            Time = Snap(normalized); SyncDraft();
        }
        private static void Remember()
        {
            if (Working == null) return;
            if (undo.Count >= 80) { var last = undo.Take(60).Reverse().ToArray(); undo.Clear(); foreach (var item in last) undo.Push(item); }
            undo.Push(JsonConvert.SerializeObject(Working)); redo.Clear();
        }
        private static void Undo(bool forward)
        {
            var from = forward ? redo : undo; var to = forward ? undo : redo;
            if (from.Count == 0) return;
            to.Push(JsonConvert.SerializeObject(Working)); Working = JsonConvert.DeserializeObject<StudioVariation>(from.Pop());
            dirty = true; SyncDraft();
        }
        private static void ChangePose(StudioPose value)
        {
            if (Working == null) return;
            if (!value.Rotation.Valid(1440) || !value.Position.Valid(2)) { Status = "Enter a finite pose value within the control's range."; return; }
            if (!gesture) { Remember(); gesture = true; }
            draft = value; draftChanged = true; playing = false;
            if (autoKey) { Working.SetKey(Phase, SelectedTarget, Time, draft); draftChanged = false; dirty = true; }
        }
        internal static void SetPoseKey(int phase, string target, float time, StudioPose pose)
        {
            if (Working == null || !StudioTargets.Contains(target)) return;
            Remember(); Working.SetKey(phase, target, Snap(time), pose); dirty = true;
            if (phase == Phase && target == SelectedTarget) SyncDraft();
        }
        private static void KeyPose()
        {
            if (Working == null) return;
            Remember(); Working.SetKey(Phase, SelectedTarget, Time, draft); draftChanged = false; dirty = true;
            Status = "Pose keyed at " + (Time * Duration).ToString("0.00") + " s.";
        }
        private static void DeleteKey()
        {
            if (Working == null) return;
            Remember(); Working.Keys.RemoveAll(k => k.Phase == Phase && k.Target == SelectedTarget && Mathf.Abs(k.Time - Time) < .0001f);
            int limb = Array.IndexOf(StudioTargets.Effectors, SelectedTarget);
            if (limb >= 0) Working.Contacts.RemoveAll(k => k.Phase == Phase && k.Limb == limb && Mathf.Abs(k.Time - Time) < .0001f);
            dirty = true; SyncDraft();
        }
        internal static bool Save()
        {
            if (Working == null) return false;
            try
            {
                if (draftChanged) KeyPose();
                for (int p = 0; p < 4; p++) { Working.RiderClips[p] = Source.Clip(p, false)?.name; Working.BikeClips[p] = Source.Clip(p, true)?.name; }
                Library.Save(Working); dirty = false; Status = "Saved " + Working.Name + ". Apply when you want to ride it.";
                Log.Msg($"[AnimationStudio] Saved variation {Working.Id}: '{Working.Name}', source={Working.TrickKey}, keys={Working.Keys.Count}."); return true;
            }
            catch (Exception ex) { Status = "Could not save: " + ex.Message; Log.Warning("[AnimationStudio] " + Status); return false; }
        }
        internal static bool Apply()
        {
            if (!Save()) return false;
            try
            {
                TrickAnimationEditor.StudioUseStock(Source); Library.Assign(Working); Status = "Applied " + Working.Name + ". Perform " + Source.Name + " in game.";
                Log.Msg("[AnimationStudio] Applied variation " + Working.Id + " to " + Working.TrickKey); return true;
            }
            catch (Exception ex) { Status = "Could not apply: " + ex.Message; return false; }
        }
        private static void SaveCopy()
        {
            if (Working == null) return;
            if (draftChanged) KeyPose();
            Working = Working.Copy(); Working.Id = Guid.NewGuid().ToString("N"); Working.Name += " copy";
            undo.Clear(); redo.Clear(); dirty = true; Save();
        }
        internal static void UseOriginal()
        {
            if (Source == null) return;
            try { Library.Reset(Source.Key); StudioRuntimePlayer.Restore(); Status = "Using the original " + Source.Name + ". Your saved variations are kept."; }
            catch (Exception ex) { Status = "Could not reset: " + ex.Message; }
        }
        internal static void Update()
        {
            Initialize(); if (!IsOpen) return;
            if (!Menu.isOpen || !Main.IsGameMainMenuActive || !RemoteKillSwitched.isModEnabled) { CloseNow(); return; }
            ControllerMenuInput.SetGameplayInputBlocked(true);
            if (playing && !draftChanged)
            {
                Time += UnityEngine.Time.unscaledDeltaTime * previewSpeed / Mathf.Max(.02f, Duration);
                if (Time > 1) Time = 0; SyncDraft();
            }
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) RequestClose();
            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (!confirmDiscard && ctrl && keyboard.sKey.wasPressedThisFrame) Save();
            if (textInputFocused || confirmDiscard) return;
            if (keyboard.spaceKey.wasPressedThisFrame) playing = !playing;
            if (keyboard.kKey.wasPressedThisFrame) KeyPose();
            if (keyboard.deleteKey.wasPressedThisFrame) DeleteKey();
            if (keyboard.leftArrowKey.wasPressedThisFrame) { if (ctrl) JumpKey(-1); else StepFrame(-1); }
            if (keyboard.rightArrowKey.wasPressedThisFrame) { if (ctrl) JumpKey(1); else StepFrame(1); }
            if (keyboard.homeKey.wasPressedThisFrame) { playing = false; Seek(0); }
            if (keyboard.endKey.wasPressedThisFrame) { playing = false; Seek(1); }
            if (ctrl && keyboard.zKey.wasPressedThisFrame) Undo(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }
        internal static void LateUpdate()
        {
            if (!IsOpen || Source == null) return;
            try { Stage.Sample(Source, Working, Phase, Time, SelectedTarget, draftChanged ? draft : null); }
            catch (Exception ex) { Status = "Preview stopped: " + ex.Message; playing = false; Log.Warning("[AnimationStudio] " + Status); CloseNow(); }
        }
    }
}
