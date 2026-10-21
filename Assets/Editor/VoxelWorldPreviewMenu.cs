using ProceduralGeneration.WorldGen.Voxels;
using UnityEditor;
using UnityEngine;

namespace ProceduralGeneration.WorldGen.Editor
{
    public static class VoxelWorldPreviewMenu
    {
        [MenuItem("GameObject/Procedural Generation/Voxel World Preview", false, 10)]
        private static void Create()
        {
            var target = new GameObject("Voxel World Preview");
            Undo.RegisterCreatedObjectUndo(target, "Create Voxel World Preview");
            var preview = Undo.AddComponent<VoxelWorldPreview>(target);
            preview.Generate();
            Selection.activeGameObject = target;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
