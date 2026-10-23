from pathlib import Path
p=Path('Assets/Scripts/WorldGen/Voxels/VoxelVolume.cs')
s=p.read_text(encoding='utf-8-sig').replace('''    public sealed class VoxelVolume
    {
        public int Width''','''    public sealed class VoxelVolume
    {
        public const long MaxVoxelCount = 16000000;
        public int Width''').replace('''            Width = width;
            Height = height;
            Depth = depth;
            solid = new bool[checked(width * height * depth)];''','''            long voxelCount = (long)width * height * depth;
            if (voxelCount > MaxVoxelCount)
                throw new ArgumentException($"Voxel volume has {voxelCount:N0} cells; maximum for full-volume preview is {MaxVoxelCount:N0}. Reduce world size or use streaming chunks.", nameof(width));
            Width = width;
            Height = height;
            Depth = depth;
            solid = new bool[checked((int)voxelCount)];''')
p.write_text(s,encoding='utf-8',newline='\r\n')
p=Path('Assets/Scenes/SampleScene.unity'); s=p.read_text(encoding='utf-8-sig').replace('worldSize: {x: 1280, y: 384, z: 1280}','worldSize: {x: 128, y: 160, z: 128}').replace('chunksPerFrame: 2','chunksPerFrame: 1'); p.write_text(s,encoding='utf-8',newline='\r\n')
print('APPLIED')
