using System;
using System.Linq;
using UnityEngine;

namespace rowemod.Mods
{
    internal static partial class RuntimeAnimationStudio
    {
        private static GUIStyle title, text, muted, small, button, active, field;
        private static readonly Color Ink = new(.055f, .065f, .085f), Panel = new(.085f, .10f, .125f), Line = new(.16f, .19f, .23f), Blue = new(.29f, .61f, 1);
        private static readonly string[] fieldText = new string[6];
        private static int dragMode;
        private static StudioPose dragPose;
        private static float keyFrom;
        private static string keyTarget;
        private static int contactDragLimb;
        private static void ContactIcon(Rect rect, StudioContactMode mode, float weight)
        {
            var color = mode == StudioContactMode.Free ? new Color(1, .65f, .35f) : weight >= .95f ? Blue : new Color(.65f, .7f, .75f);
            Fill(rect, Ink);
            float x = rect.x + 4, y = rect.y + 3;
            Fill(new Rect(x, y + 7, 13, 10), color);
            float open = weight >= .95f ? 0 : 4;
            Fill(new Rect(x + 2 + open, y, 2, 8), color); Fill(new Rect(x + 2 + open, y, 9, 2), color);
            Fill(new Rect(x + 9 + open, y, 2, 5), color);
            if (mode == StudioContactMode.Auto) GUI.Label(new Rect(rect.x + 22, rect.y, 18, 22), "G", small);
        }
        private static void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            title.normal.textColor = Color.white;
            text = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) }; text.normal.textColor = new Color(.9f, .93f, .97f);
            muted = new GUIStyle(text) { fontSize = 12, wordWrap = true }; muted.normal.textColor = new Color(.55f, .61f, .69f);
            small = new GUIStyle(text) { fontSize = 11 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(8, 8, 4, 4) };
            button.normal.background = Texture2D.whiteTexture; button.normal.textColor = Color.white;
            button.hover.background = Texture2D.whiteTexture; button.hover.textColor = Color.white;
            active = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 14, padding = new RectOffset(8, 8, 6, 4) };
        }
        private static void Fill(Rect rect, Color color)
        { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private static bool Button(Rect rect, string label, bool chosen = false)
        {
            var old = GUI.backgroundColor; GUI.backgroundColor = chosen ? Blue : new Color(.17f, .20f, .25f);
            bool clicked = GUI.Button(rect, label, chosen ? active : button); GUI.backgroundColor = old;
            if (clicked) GUI.FocusControl("");
            return clicked;
        }
        internal static void Draw()
        {
            if (!IsOpen) return; Styles();
            var matrix = GUI.matrix; var color = GUI.color; var background = GUI.backgroundColor;
            bool pointerReleased = Event.current.rawType == EventType.MouseUp;
            float scale = Screen.height / 900f, width = Screen.width / scale, height = 900;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); GUI.color = Color.white;
                GUI.enabled = !confirmDiscard;
                Fill(new Rect(0, 0, width, height), Ink);
                GUI.Label(new Rect(24, 12, 300, 34), "ANIMATION STUDIO", title);
                GUI.Label(new Rect(25, 49, 500, 20), "ROWE  /  Make the trick your own", muted);
                if (Working != null)
                {
                    GUI.SetNextControlName("studio_text_name");
                    string next = GUI.TextField(new Rect(330, 23, Mathf.Max(170, width - 930), 34), Working.Name ?? "", 100, field);
                    if (next != Working.Name) { Working.Name = next; dirty = true; }
                }
                GUI.enabled = !confirmDiscard && undo.Count > 0; if (Button(new Rect(width - 560, 23, 60, 34), "Undo")) Undo(false);
                GUI.enabled = !confirmDiscard && redo.Count > 0; if (Button(new Rect(width - 492, 23, 60, 34), "Redo")) Undo(true); GUI.enabled = !confirmDiscard;
                if (Button(new Rect(width - 412, 23, 100, 34), dirty ? "Save *" : "Save")) Save();
                if (Button(new Rect(width - 304, 23, 172, 34), "Apply to game", true)) Apply();
                if (Button(new Rect(width - 120, 23, 96, 34), "Close")) RequestClose();
                Fill(new Rect(16, 77, width - 32, 1), Line);
                var library = new Rect(16, 90, 280, 762); var viewport = new Rect(312, 90, width - 624, 574);
                var inspector = new Rect(width - 296, 90, 280, 574); var timeline = new Rect(312, 680, width - 328, 172);
                DrawLibrary(library); DrawViewport(viewport, scale); DrawInspector(inspector); DrawTimeline(timeline);
                GUI.Label(new Rect(24, 862, width - 48, 26), Status, muted);
                if (confirmDiscard) { GUI.enabled = true; DrawConfirmation(width); }
                textInputFocused = GUI.GetNameOfFocusedControl().StartsWith("studio_text_", StringComparison.Ordinal);
                if (pointerReleased) { gesture = false; dragMode = 0; }
            }
            finally { GUI.enabled = true; GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; }
        }
        private static void DrawLibrary(Rect rect)
        {
            Fill(rect, Panel); GUI.Label(new Rect(rect.x + 16, rect.y + 12, 230, 24), "ANIMATIONS", text);
            if (Button(new Rect(rect.x + 12, rect.y + 45, 124, 31), "Game tricks", libraryTab == 0)) { libraryTab = 0; libraryScroll = Vector2.zero; }
            if (Button(new Rect(rect.x + 144, rect.y + 45, 124, 31), "My variations", libraryTab == 1)) { libraryTab = 1; libraryScroll = Vector2.zero; }
            GUI.SetNextControlName("studio_text_search");
            search = GUI.TextField(new Rect(rect.x + 12, rect.y + 88, 256, 31), search, field);
            var entries = libraryTab == 0 ? Sources.Where(s => s.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).Select(s => (Key: s.Key, s.Name, saved: (StudioVariation)null)).ToArray() :
                Library.Variations.Where(v => v.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(v => v.Name).Select(v => (Key: v.TrickKey, v.Name, saved: v)).ToArray();
            Rect scroll = new(rect.x + 8, rect.y + 132, rect.width - 16, rect.height - 286);
            libraryScroll = GUI.BeginScrollView(scroll, libraryScroll, new Rect(0, 0, scroll.width - 18, entries.Length * 52));
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; bool selected = entry.saved != null ? Working?.Id == entry.saved.Id : Source?.Key == entry.Key && libraryTab == 0;
                if (Button(new Rect(0, i * 52, scroll.width - 21, 46), entry.Name, selected))
                {
                    GUI.FocusControl(""); RequestAction(() => { if (entry.saved == null) SelectSource(entry.Key); else OpenVariation(entry.saved); });
                }
            }
            GUI.EndScrollView();
            string applied = Source == null ? null : Library.Active(Source.Key)?.Name;
            GUI.Label(new Rect(rect.x + 16, rect.yMax - 144, 246, 42), applied == null ? "In game: original animation" : "In game: " + applied, muted);
            if (Button(new Rect(rect.x + 12, rect.yMax - 94, 256, 32), "Save as a new variation")) SaveCopy();
            if (Button(new Rect(rect.x + 12, rect.yMax - 48, 256, 32), "Use original")) UseOriginal();
        }
        private static void DrawViewport(Rect rect, float scale)
        {
            Fill(rect, new Color(.07f, .085f, .11f));
            if (Stage?.Image != null)
            {
                if (Event.current.type == EventType.Repaint) Stage.Resize(Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale));
                GUI.DrawTexture(rect, Stage.Image, ScaleMode.StretchToFill, false);
            }
            if (Source == null) return;
            GUI.Label(new Rect(rect.x + 18, rect.y + 12, rect.width - 36, 25), Source.Name, text);
            GUI.Label(new Rect(rect.x + 18, rect.y + 36, rect.width - 36, 24), Phases[Phase] + "  /  " + (Time * Duration).ToString("0.00") + " s", muted);
            GUI.Label(new Rect(rect.x + 18, rect.y + 62, rect.width - 36, 20), "Rider: " + (Source.Clip(Phase, false)?.name ?? "No clip"), small);
            GUI.Label(new Rect(rect.x + 18, rect.y + 84, rect.width - 36, 20), "Bike: " + (Source.Clip(Phase, true)?.name ?? "No clip"), small);
            GUI.Label(new Rect(rect.x + 18, rect.yMax - 31, rect.width - 36, 22), "Select a joint · Drag X / Y / Z rotation rings · Right drag to orbit · Scroll to zoom", muted);
            if (Stage?.Camera == null || confirmDiscard) return;
            for (int i = 0; i < 4; i++)
            {
                var end = Stage.Target(StudioTargets.Effectors[i]); if (end == null) continue;
                var p = Stage.Camera.WorldToViewportPoint(end.position); if (p.z <= 0) continue;
                var icon = new Rect(rect.x + p.x * rect.width + 13, rect.y + (1 - p.y) * rect.height - 24, 40, 24);
                if (!rect.Contains(icon.center)) continue;
                var mode = Working.Contact(Phase, i, Time); ContactIcon(icon, mode, Stage.ContactWeights[i]);
                if (Stage.ContactWeights[i] > .95f && Stage.ContactGaps[i] > .025f) GUI.Label(new Rect(icon.x + 25, icon.y + 8, 12, 18), "!", small);
                if (GUI.Button(icon, "", GUIStyle.none))
                { SetContact(i, (StudioContactMode)(((int)mode + 1) % 3)); SelectTarget(StudioTargets.Effectors[i]); }
            }
            DrawRotationGizmo(rect);
            foreach (string target in StudioTargets.Rider.Concat(StudioTargets.Bike))
            {
                if ((targetTab == 0) != !target.StartsWith("Bike:", StringComparison.Ordinal)) continue;
                var node = Stage.Target(target); if (node == null) continue;
                Vector3 p = Stage.Camera.WorldToViewportPoint(node.position); if (p.z <= 0) continue;
                Vector2 point = new(rect.x + p.x * rect.width, rect.y + (1 - p.y) * rect.height);
                if (!rect.Contains(point)) continue;
                float size = SelectedTarget == target ? 10 : 6;
                Fill(new Rect(point.x - size / 2, point.y - size / 2, size, size), SelectedTarget == target ? Color.white : Blue);
                if (SelectedTarget == target) GUI.Label(new Rect(point.x + 12, point.y - 12, 170, 24), NiceTarget(target), small);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Vector2.Distance(Event.current.mousePosition, point) < 13)
                {
                    SelectTarget(target); Event.current.Use(); break;
                }
            }
            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel) { Stage.Distance = Mathf.Clamp(Stage.Distance + e.delta.y * .15f, 1.2f, 8); Stage.Orbit(); e.Use(); }
                if (e.type == EventType.MouseDown && e.button == 1 && dragMode == 0) { dragMode = 2; e.Use(); }
            }
            if (e.type == EventType.MouseDrag && dragMode == 2)
            {
                Stage.Yaw -= e.delta.x * .45f; Stage.Pitch = Mathf.Clamp(Stage.Pitch + e.delta.y * .4f, -35, 65); Stage.Orbit();
                e.Use();
            }
        }
        private static string NiceTarget(string target) => target.Replace("Bike:", "").Replace("Upper", " upper ").Replace("Lower", " lower ").Replace("Left", "Left ").Replace("Right", "Right ");
        private static void SelectTarget(string target)
        {
            if (draftChanged) { Status = "Set a key or discard your unkeyed pose first."; return; }
            SelectedTarget = target; targetTab = target.StartsWith("Bike:", StringComparison.Ordinal) ? 1 : 0;
            SyncDraft(); GUI.FocusControl("");
        }
        private static void SelectTargetGroup(int tab)
        {
            if (draftChanged) { Status = "Set a key or discard your unkeyed pose first."; return; }
            targetTab = tab; targetScroll = Vector2.zero;
            if ((tab == 1) != SelectedTarget.StartsWith("Bike:", StringComparison.Ordinal))
                SelectTarget(tab == 1 ? StudioTargets.Bike[0] : StudioTargets.Rider[0]);
        }
        private static void DrawInspector(Rect rect)
        {
            Fill(rect, Panel); GUI.Label(new Rect(rect.x + 16, rect.y + 12, 240, 24), "JOINTS", text);
            if (Button(new Rect(rect.x + 12, rect.y + 45, 124, 30), "Rider", targetTab == 0)) SelectTargetGroup(0);
            if (Button(new Rect(rect.x + 144, rect.y + 45, 124, 30), "Bike", targetTab == 1)) SelectTargetGroup(1);
            var targets = targetTab == 0 ? StudioTargets.Rider : StudioTargets.Bike;
            targetScroll = GUI.BeginScrollView(new Rect(rect.x + 12, rect.y + 85, 256, 125), targetScroll, new Rect(0, 0, 234, targets.Length * 30));
            for (int i = 0; i < targets.Length; i++) if (Button(new Rect(0, i * 30, 232, 27), NiceTarget(targets[i]), targets[i] == SelectedTarget)) SelectTarget(targets[i]);
            GUI.EndScrollView();
            GUI.Label(new Rect(rect.x + 16, rect.y + 222, 244, 24), NiceTarget(SelectedTarget), text);
            GUI.Label(new Rect(rect.x + 16, rect.y + 248, 244, 20), "ROTATION OFFSET", muted);
            Axis(rect.x + 16, rect.y + 274, 0, "X", -180, 180); Axis(rect.x + 16, rect.y + 308, 1, "Y", -180, 180); Axis(rect.x + 16, rect.y + 342, 2, "Z", -180, 180);
            if (SelectedTarget == "Hips" || SelectedTarget.StartsWith("Bike:", StringComparison.Ordinal))
            {
                GUI.Label(new Rect(rect.x + 16, rect.y + 379, 244, 20), "POSITION OFFSET · METRES", muted);
                Axis(rect.x + 16, rect.y + 405, 3, "X", -.5f, .5f); Axis(rect.x + 16, rect.y + 439, 4, "Y", -.5f, .5f); Axis(rect.x + 16, rect.y + 473, 5, "Z", -.5f, .5f);
            }
            else if (Array.IndexOf(StudioTargets.Effectors, SelectedTarget) is int limb && limb >= 0)
            {
                GUI.Label(new Rect(rect.x + 16, rect.y + 379, 244, 22), "BIKE CONTACT", muted);
                var mode = Working.Contact(Phase, limb, Time);
                for (int c = 0; c < 3; c++) if (Button(new Rect(rect.x + 12 + c * 86, rect.y + 407, 82, 30), c == 0 ? "Inherited" : ((StudioContactMode)c).ToString(), (int)mode == c)) SetContact(limb, (StudioContactMode)c);
                string reach = Stage.ContactWeights[limb] > .95f && Stage.ContactGaps[limb] > .025f ? " Target is beyond this pose's reach." : "";
                GUI.Label(new Rect(rect.x + 16, rect.y + 445, 244, 70), "Inherited follows Streets' animated contact. Lock/free changes are keyed at the playhead." + reach, muted);
            }
            else GUI.Label(new Rect(rect.x + 16, rect.y + 390, 242, 72), "Rotate each joint to pose the limb. Hips and bike joints also support movement.", muted);
            if (Button(new Rect(rect.x + 12, rect.yMax - 46, 120, 32), "Set key  K", true)) KeyPose();
            if (Button(new Rect(rect.x + 140, rect.yMax - 46, 128, 32), "Reset pose")) { ChangePose(default); SyncFields(); }
        }
        private static void SyncFields()
        {
            float[] values = { draft.Rotation.X, draft.Rotation.Y, draft.Rotation.Z, draft.Position.X, draft.Position.Y, draft.Position.Z };
            for (int i = 0; i < 6; i++) fieldText[i] = values[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
        private static void Axis(float x, float y, int axis, string label, float min, float max)
        {
            float[] values = { draft.Rotation.X, draft.Rotation.Y, draft.Rotation.Z, draft.Position.X, draft.Position.Y, draft.Position.Z };
            GUI.Label(new Rect(x, y, 18, 26), label, muted);
            bool changed = GUI.changed; GUI.changed = false;
            float slider = GUI.HorizontalSlider(new Rect(x + 25, y + 8, 134, 18), Mathf.Clamp(values[axis], min, max), min, max);
            bool moved = GUI.changed; GUI.changed |= changed;
            float value = moved ? slider : values[axis];
            GUI.SetNextControlName("studio_text_axis_" + axis);
            string next = GUI.TextField(new Rect(x + 168, y, 76, 27), fieldText[axis] ?? "0", field);
            if (next != fieldText[axis])
            { fieldText[axis] = next; if (!float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) return; value = Mathf.Clamp(value, min, max); }
            if (Mathf.Abs(value - values[axis]) < .00001f) return;
            values[axis] = value;
            ChangePose(new StudioPose(new StudioVector(values[0], values[1], values[2]), new StudioVector(values[3], values[4], values[5])));
            fieldText[axis] = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
        private static void DrawTimeline(Rect rect)
        {
            Fill(rect, Panel);
            if (Button(new Rect(rect.x + 12, rect.y + 12, 64, 30), playing ? "Pause" : "Play", playing))
            { if (draftChanged) Status = "Set a key or discard your unkeyed pose first."; else { if (Time >= 1) Seek(0); playing = !playing; } }
            for (int p = 0; p < 4; p++)
            {
                GUI.enabled = !confirmDiscard && Source?.Clip(p, false) != null;
                if (Button(new Rect(rect.x + 92 + p * 85, rect.y + 12, 78, 30), Phases[p], Phase == p) && !draftChanged)
                { Phase = p; Time = 0; SyncDraft(); }
            }
            GUI.enabled = !confirmDiscard;
            autoKey = GUI.Toggle(new Rect(rect.x + 450, rect.y + 17, 105, 24), autoKey, "Auto key");
            previewSpeed = GUI.HorizontalSlider(new Rect(rect.x + 570, rect.y + 22, 100, 15), previewSpeed, .1f, 2f);
            GUI.Label(new Rect(rect.x + 680, rect.y + 14, 118, 28), previewSpeed.ToString("0.0") + "x preview", muted);
            if (Button(new Rect(rect.xMax - 110, rect.y + 12, 96, 30), "Delete key")) DeleteKey();
            float start = rect.x + 80, width = rect.width - 105;
            GUI.Label(new Rect(rect.x + 13, rect.y + 69, 65, 22), "Rider", muted); GUI.Label(new Rect(rect.x + 13, rect.y + 94, 65, 22), "Bike", muted);
            GUI.Label(new Rect(rect.x + 13, rect.y + 119, 65, 22), "Contacts", muted);
            for (int i = 0; i <= 10; i++)
            {
                float px = start + i * width / 10;
                Fill(new Rect(px, rect.y + 62, 1, 76), Line);
                GUI.Label(new Rect(px - 10, rect.y + 43, 60, 18), (i * Duration / 10).ToString("0.00"), small);
            }
            Fill(new Rect(start, rect.y + 79, width, 1), Line); Fill(new Rect(start, rect.y + 104, width, 1), Line);
            Fill(new Rect(start, rect.y + 129, width, 1), Line);
            if (Working != null)
                foreach (var key in Working.Keys.Where(k => k.Phase == Phase && (!selectedKeysOnly || k.Target == SelectedTarget)))
                {
                    float px = start + key.Time * width, py = rect.y + (key.Target.StartsWith("Bike:", StringComparison.Ordinal) ? 104 : 79);
                    var diamond = new Rect(px - 6, py - 7, 12, 14); Fill(diamond, key.Target == SelectedTarget ? Color.white : Blue);
                    if (!confirmDiscard && !draftChanged && Event.current.type == EventType.MouseDown && diamond.Contains(Event.current.mousePosition))
                    { Remember(); SelectTarget(key.Target); Time = key.Time; SyncDraft(); keyFrom = key.Time; keyTarget = key.Target; dragMode = 3; Event.current.Use(); break; }
                }
            if (Working != null)
                foreach (var key in Working.Contacts.Where(k => k.Phase == Phase && (!selectedKeysOnly || StudioTargets.Effectors[k.Limb] == SelectedTarget)))
                {
                    var mark = new Rect(start + key.Time * width - 6, rect.y + 123, 12, 12); Fill(mark, key.Mode == StudioContactMode.Free ? new Color(1, .65f, .35f) : Blue);
                    if (!confirmDiscard && !draftChanged && Event.current.type == EventType.MouseDown && mark.Contains(Event.current.mousePosition))
                    { Remember(); SelectTarget(StudioTargets.Effectors[key.Limb]); Time = key.Time; SyncDraft(); keyFrom = key.Time; contactDragLimb = key.Limb; dragMode = 5; Event.current.Use(); break; }
                }
            var e = Event.current;
            if (!confirmDiscard && e.type == EventType.MouseDrag && dragMode == 5)
            {
                float next = Snap((e.mousePosition.x - start) / width);
                var key = Working.Contacts.FirstOrDefault(k => k.Phase == Phase && k.Limb == contactDragLimb && Mathf.Abs(k.Time - keyFrom) < .00001f);
                if (key != null && !Working.Contacts.Any(k => k != key && k.Phase == Phase && k.Limb == key.Limb && Mathf.Abs(k.Time - next) < .00001f))
                { key.Time = keyFrom = Time = next; dirty = true; SyncDraft(); } e.Use();
            }
            if (!confirmDiscard && e.type == EventType.MouseDrag && dragMode == 3)
            {
                float next = Snap((e.mousePosition.x - start) / width);
                if (Working.MoveKey(Phase, keyTarget, keyFrom, next)) { keyFrom = Time = next; dirty = true; SyncDraft(); } e.Use();
            }
            if (!confirmDiscard && e.type == EventType.MouseDown && new Rect(start, rect.y + 44, width, 94).Contains(e.mousePosition))
            { playing = false; Seek((e.mousePosition.x - start) / width); dragMode = 4; e.Use(); }
            if (!confirmDiscard && e.type == EventType.MouseDrag && dragMode == 4) { Seek((e.mousePosition.x - start) / width); e.Use(); }
            Fill(new Rect(start + Time * width, rect.y + 59, 2, 80), Blue);
            if (draftChanged)
            {
                GUI.Label(new Rect(rect.x + 12, rect.y + 139, rect.width - 170, 24), "Unkeyed pose · Set a key to keep it", text);
                if (Button(new Rect(rect.xMax - 145, rect.y + 135, 130, 28), "Discard pose")) SyncDraft();
            }
            else
            {
                float y = rect.y + 143, x = rect.x + 12;
                if (Button(new Rect(x, y, 76, 24), "< Key")) JumpKey(-1);
                if (Button(new Rect(x + 82, y, 76, 24), "Key >")) JumpKey(1);
                if (Button(new Rect(x + 168, y, 38, 24), "< F")) StepFrame(-1);
                if (Button(new Rect(x + 212, y, 38, 24), "F >")) StepFrame(1);
                if (Button(new Rect(x + 260, y, 52, 24), "Start")) { playing = false; Seek(0); }
                if (Button(new Rect(x + 318, y, 52, 24), "End")) { playing = false; Seek(1); }
                if (Button(new Rect(x + 382, y, 120, 24), selectedKeysOnly ? "Selected joint" : "All keys")) selectedKeysOnly = !selectedKeysOnly;
                GUI.Label(new Rect(x + 516, y, rect.width - 540, 24), "Frame " + Mathf.RoundToInt(Time * Duration * FrameRate) + " / " + Mathf.RoundToInt(Duration * FrameRate) + "  |  Arrows: frame  Ctrl+Arrows: key  Home/End", muted);
            }
        }
        private static void DrawConfirmation(float width)
        {
            Fill(new Rect(0, 0, width, 900), new Color(0, 0, 0, .75f));
            Rect box = new(width / 2 - 265, 330, 530, 200); Fill(box, Panel);
            GUI.Label(new Rect(box.x + 24, box.y + 20, 480, 35), "Keep your changes?", title);
            GUI.Label(new Rect(box.x + 24, box.y + 65, 480, 45), "Save this variation before leaving. Applying it in game is a separate step.", muted);
            if (Button(new Rect(box.x + 24, box.y + 135, 145, 38), "Save", true))
            { if (Save()) { var action = pendingAction; confirmDiscard = false; pendingAction = null; action?.Invoke(); } }
            if (Button(new Rect(box.x + 185, box.y + 135, 145, 38), "Discard changes"))
            { dirty = draftChanged = false; var action = pendingAction; confirmDiscard = false; pendingAction = null; action?.Invoke(); }
            if (Button(new Rect(box.x + 346, box.y + 135, 160, 38), "Keep editing")) { confirmDiscard = false; pendingAction = null; }
        }
    }
}
