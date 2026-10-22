param([Parameter(Mandatory=$true)][string]$TargetCopy)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ((Split-Path -Leaf $TargetCopy) -eq 'ROLLBACK_COPY_VoxelVolume.cs') { Copy-Item (Join-Path $root 'BASELINE_VoxelVolume.cs') $TargetCopy -Force } else { throw 'Target must be rollback copy.' }
