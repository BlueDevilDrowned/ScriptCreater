using System;
using ProceduralGeneration.WorldGen.Density;
using ProceduralGeneration.WorldGen.Voxels;
using UnityEditor;
using UnityEngine;

namespace ProceduralGeneration.WorldGen.Editor
{
    /// <summary>独立的 XZ 参数地图，不生成网格，也不改写世界场。</summary>
    public sealed class WorldFieldMapWindow : EditorWindow
    {
        [SerializeField] private int seed = 12345;
        [SerializeField] private Vector2Int origin = new Vector2Int(-512, -512);
        [SerializeField] private Vector2Int span = new Vector2Int(1024, 1024);
        [SerializeField] private int resolution = 256;
        [SerializeField] private bool showReferenceP;
        private XZFieldMap map;
        private int sampledSeed;
        private Texture2D[] textures;
        private Texture2D legend;
        private Vector2 scroll;
        private int selectedX, selectedZ;
        private string error;

        [MenuItem("Window/Procedural Generation/XZ Parameter Maps")]
        public static void Open()
        {
            var window = GetWindow<WorldFieldMapWindow>("XZ 参数地图");
            window.minSize = new Vector2(360, 440);
            window.Show();
        }

        private void OnEnable() { wantsMouseMove = true; }
        private void OnDisable() { ReleaseTextures(); }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("世界场 · XZ 参数地图", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("C/E/当前山脊直接读取 WorldFields。蓝色低、浅色中、橙色高；色阶固定，不按局部最值拉伸。地图向右为 +X，向上为 +Z。", MessageType.Info);
            seed = EditorGUILayout.IntField("世界种子", seed);
            origin = EditorGUILayout.Vector2IntField("起点 X / Z（体素）", origin);
            span = EditorGUILayout.Vector2IntField("范围 X / Z（体素数）", span);
            resolution = EditorGUILayout.IntPopup("每张图分辨率", resolution, new[] { "128", "256", "512" }, new[] { 128, 256, 512 });
            showReferenceP = EditorGUILayout.Toggle("第三张显示参考 P", showReferenceP);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新地图")) RefreshMaps();
            if (GUILayout.Button("读取选中预览范围")) ReadPreview();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);

            if (map != null)
            {
                EditorGUILayout.LabelField($"已采样 Seed={sampledSeed} · X=[{map.OriginX}, {map.GetX(map.Resolution - 1)}] · Z=[{map.OriginZ}, {map.GetZ(map.Resolution - 1)}]", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("修改上方种子或范围后点击刷新。读数对应已采样的地图。", EditorStyles.miniLabel);
                bool horizontal = position.width >= 780;
                float side = Mathf.Clamp(horizontal ? (position.width - 65) / 3 : position.width - 42, 96, 512);
                if (horizontal) EditorGUILayout.BeginHorizontal();
                DrawMap(XZFieldMap.Field.Continents, "C · 大陆性", side);
                DrawMap(XZFieldMap.Field.Erosion, "E · 侵蚀度", side);
                DrawMap(showReferenceP ? XZFieldMap.Field.PeaksAndValleys : XZFieldMap.Field.Ridges,
                    showReferenceP ? "P · 峰谷（参考）" : "R · 当前山脊 |noise|", side);
                if (horizontal) EditorGUILayout.EndHorizontal();
                if (showReferenceP)
                    EditorGUILayout.HelpBox("P = -3 × (||R| - 2/3| - 1/3)。这里只用于对照；当前地形仍使用 R=|noise|，没有接入 P。", MessageType.Info);
                EditorGUILayout.LabelField($"采样点 X={map.GetX(selectedX)}, Z={map.GetZ(selectedZ)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"C={map.GetValue(XZFieldMap.Field.Continents, selectedX, selectedZ):F5}    E={map.GetValue(XZFieldMap.Field.Erosion, selectedX, selectedZ):F5}");
                EditorGUILayout.LabelField($"R={map.GetValue(XZFieldMap.Field.Ridges, selectedX, selectedZ):F5}    参考 P={map.GetValue(XZFieldMap.Field.PeaksAndValleys, selectedX, selectedZ):F5}");
            }
            else EditorGUILayout.HelpBox("点击刷新地图。默认观察 1024×1024 体素，以便看清大尺度变化。也可选中 Voxel World Preview 后读取它的种子与范围。", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void ReadPreview()
        {
            var preview = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<VoxelWorldPreview>();
            if (preview == null) { error = "请先选中带有 VoxelWorldPreview 的场景对象。"; return; }
            seed = preview.Seed;
            origin = new Vector2Int(preview.SampleOrigin.x, preview.SampleOrigin.z);
            span = new Vector2Int(preview.WorldSize.x, preview.WorldSize.z);
            RefreshMaps();
        }

        private void RefreshMaps()
        {
            try
            {
                var next = new XZFieldMap(WorldFields.CreateLearningDefaults(seed), origin.x, origin.y, span.x, span.y, resolution);
                ReleaseTextures();
                map = next;
                sampledSeed = seed;
                selectedX = selectedZ = map.Resolution / 2;
                textures = new Texture2D[4];
                for (int field = 0; field < 4; field++)
                {
                    var pixels = new Color32[resolution * resolution];
                    for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                    {
                        double value = map.GetValue((XZFieldMap.Field)field, x, z);
                        float t = (float)(field == 2 ? value : (value + 1) * 0.5);
                        pixels[z * resolution + x] = Palette(t);
                    }
                    textures[field] = MakeTexture(resolution, resolution, pixels);
                }
                var ramp = new Color32[128];
                for (int i = 0; i < ramp.Length; i++) ramp[i] = Palette(i / 127f);
                legend = MakeTexture(128, 1, ramp);
                error = null;
            }
            catch (ArgumentException exception) { error = exception.Message; }
        }

        private static Color Palette(float t)
        {
            var low = new Color32(26, 75, 154, 255);
            var mid = new Color32(239, 242, 238, 255);
            var high = new Color32(201, 70, 43, 255);
            return t <= 0.5f ? Color.Lerp(low, mid, t * 2) : Color.Lerp(mid, high, t * 2 - 1);
        }

        private static Texture2D MakeTexture(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawMap(XZFieldMap.Field field, string title, float side)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(side));
            GUILayout.Label(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, textures[(int)field], ScaleMode.StretchToFill);
            if (rect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDown))
            {
                selectedX = Mathf.Clamp((int)((Event.current.mousePosition.x - rect.x) / rect.width * map.Resolution), 0, map.Resolution - 1);
                selectedZ = map.Resolution - 1 - Mathf.Clamp((int)((Event.current.mousePosition.y - rect.y) / rect.height * map.Resolution), 0, map.Resolution - 1);
                Repaint();
            }
            float px = rect.x + (selectedX + 0.5f) / map.Resolution * rect.width;
            float py = rect.y + (map.Resolution - selectedZ - 0.5f) / map.Resolution * rect.height;
            EditorGUI.DrawRect(new Rect(px - 1, rect.y, 3, rect.height), new Color(0, 0, 0, 0.6f));
            EditorGUI.DrawRect(new Rect(rect.x, py - 1, rect.width, 3), new Color(0, 0, 0, 0.6f));
            EditorGUI.DrawRect(new Rect(px, rect.y, 1, rect.height), Color.white);
            EditorGUI.DrawRect(new Rect(rect.x, py, rect.width, 1), Color.white);
            Rect bar = GUILayoutUtility.GetRect(side, 10, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(bar, legend, ScaleMode.StretchToFill);
            GUILayout.Label(field == XZFieldMap.Field.Ridges ? "0                 0.5                 1" : "−1                  0                  +1", EditorStyles.miniLabel);
            GUILayout.Label($"采样最值 {map.Minimum(field):F3} ～ {map.Maximum(field):F3}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void ReleaseTextures()
        {
            if (textures != null) foreach (var texture in textures) if (texture != null) DestroyImmediate(texture);
            if (legend != null) DestroyImmediate(legend);
            textures = null;
            legend = null;
            map = null;
        }
    }
}
