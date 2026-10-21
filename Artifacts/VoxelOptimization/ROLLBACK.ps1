param([Parameter(Mandatory=$true)][string]$TargetCopy)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$name = Split-Path -Leaf $TargetCopy
if ($name -eq 'ROLLBACK_COPY_VoxelWorldPreview.cs') { Copy-Item (Join-Path $root 'BASELINE_VoxelWorldPreview.cs') $TargetCopy -Force }
elseif ($name -eq 'ROLLBACK_COPY_VoxelMeshBuilder.cs') { Copy-Item (Join-Path $root 'BASELINE_VoxelMeshBuilder.cs') $TargetCopy -Force }
else { throw "Target must be a rollback copy file." }
