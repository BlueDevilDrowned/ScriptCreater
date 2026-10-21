using System;
using ProceduralGeneration.WorldGen.Density;
using ProceduralGeneration.WorldGen.Voxels;
using UnityEditor;
using UnityEngine;

public static class VoxelVerification
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Debug.Log("PASS: " + name); }
    public static void Run()
    {
        try
        {
            var volume = new VoxelVolume(128, 128, 128);
            volume.Fill(WorldFields.CreateLearningDefaults(12345).FinalDensity);
            var mesh = VoxelMeshBuilder.BuildWithJobs(volume, 0.5f);
            Check(volume.SolidCount > 0 && volume.SolidCount < 128 * 128 * 128, "default solids and air");
            Check(mesh.vertexCount > 0, "job mesh vertices");
            Debug.Log($"DEFAULT solid={volume.SolidCount} vertices={mesh.vertexCount} faces={mesh.vertexCount / 4}");
            UnityEngine.Object.DestroyImmediate(mesh);
            var go = new GameObject("Runtime Job Preview");
            var preview = go.AddComponent<VoxelWorldPreview>();
            preview.Generate(new Vector3Int(12, 10, 8), -123, new Vector3Int(-100, 75, 34), 0.25f);
            Check(preview.WorldSize == new Vector3Int(12, 10, 8), "runtime size");
            Check(preview.Seed == -123 && preview.SampleOrigin == new Vector3Int(-100, 75, 34), "runtime seed and origin");
            Check(preview.VoxelSize == 0.25f && go.transform.childCount == 1 && go.transform.GetChild(0).childCount == 1 && go.transform.GetChild(0).GetChild(0).GetComponent<MeshFilter>().sharedMesh != null, "runtime voxel size and chunk mesh");
            Check(go.GetComponent<VoxelWorldControls>() != null, "runtime controls");
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("VOXEL_JOB_VERIFICATION_SUCCESS");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
