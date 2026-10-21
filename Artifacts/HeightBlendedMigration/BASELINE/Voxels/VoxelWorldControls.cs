using System;
using System.Globalization;
using UnityEngine;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>运行时参数面板。仅点击生成按钮时重建，不在每次输入时重建。</summary>
    [DisallowMultipleComponent]
    public sealed class VoxelWorldControls : MonoBehaviour
    {
        [SerializeField] private bool showPanel = true;
        private VoxelWorldPreview preview;
        private string sizeX, sizeY, sizeZ, originX, originY, originZ, seed, voxelSize;
        private string message = "";
        private string chunksPerFrame;
        private bool initialized;

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (!initialized)
            {
                preview = GetComponent<VoxelWorldPreview>();
                if (preview == null) return;
                sizeX = preview.WorldSize.x.ToString();
                sizeY = preview.WorldSize.y.ToString();
                sizeZ = preview.WorldSize.z.ToString();
                originX = preview.SampleOrigin.x.ToString();
                originY = preview.SampleOrigin.y.ToString();
                originZ = preview.SampleOrigin.z.ToString();
                seed = preview.Seed.ToString();
                voxelSize = preview.VoxelSize.ToString(CultureInfo.InvariantCulture);
                chunksPerFrame = preview.ChunksPerFrame.ToString();
                initialized = true;
            }

            if (GUI.Button(new Rect(12, 12, 280, 28), showPanel ? "Hide world settings" : "Show world settings"))
                showPanel = !showPanel;
            if (!showPanel) return;
            GUILayout.BeginArea(new Rect(12, 46, 320, 350), "Voxel World", GUI.skin.window);
            GUILayout.Label("Size (voxels): X / Y / Z");
            DrawVector(ref sizeX, ref sizeY, ref sizeZ);
            GUILayout.Label("Seed");
            seed = GUILayout.TextField(seed);
            GUILayout.Label("Sample origin (voxels): X / Y / Z");
            DrawVector(ref originX, ref originY, ref originZ);
            GUILayout.Label("Voxel size (Unity units)");
            voxelSize = GUILayout.TextField(voxelSize);
            GUILayout.Label("Chunks per frame (generation speed)");
            chunksPerFrame = GUILayout.TextField(chunksPerFrame);
            if (GUILayout.Button("Generate / Regenerate")) Regenerate();
            if (GUILayout.Button("Clear"))
            {
                preview.Clear();
                message = "World cleared.";
            }
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private static void DrawVector(ref string x, ref string y, ref string z)
        {
            GUILayout.BeginHorizontal();
            x = GUILayout.TextField(x);
            y = GUILayout.TextField(y);
            z = GUILayout.TextField(z);
            GUILayout.EndHorizontal();
        }

        private void Regenerate()
        {
            if (!int.TryParse(sizeX, out int x) || !int.TryParse(sizeY, out int y) || !int.TryParse(sizeZ, out int z)
                || !int.TryParse(seed, out int worldSeed)
                || !int.TryParse(originX, out int ox) || !int.TryParse(originY, out int oy) || !int.TryParse(originZ, out int oz)
                || !float.TryParse(voxelSize, NumberStyles.Float, CultureInfo.InvariantCulture, out float cellSize)
                || !int.TryParse(chunksPerFrame, out int speed) || speed <= 0)
            {
                message = "Enter integers for size, seed and origin; use . for decimals.";
                return;
            }
            try
            {
                preview.SetChunksPerFrame(speed);
                preview.Generate(new Vector3Int(x, y, z), worldSeed, new Vector3Int(ox, oy, oz), cellSize);
                message = $"Generated {x} x {y} x {z}. Solid: {preview.Volume.SolidCount}";
            }
            catch (ArgumentException)
            {
                message = "World size and voxel size must be positive and finite.";
            }
            catch (OverflowException)
            {
                message = "Size or sample coordinates exceed the supported integer range.";
            }
        }
    }
}
