# 密度到体素 Mesh：同步学习版

## 使用

1. 在场景中新建空对象，添加 `VoxelWorldPreview` 组件。
2. 保留默认尺寸 `128,128,128`、体素边长 `0.5`、种子 `12345`。
3. 点击 Play 自动生成，或在组件菜单中选择 **Generate Voxel World** 在编辑器内生成。
4. 在 Scene 窗口选中对象按 F 查看全貌。Game 窗口需要将相机对准世界。
5. 修改参数后再次 Generate；选择 **Clear Voxel World** 清除生成数据与网格。

## 运行时调整

进入 Play 后，Game 窗口左上角显示参数面板，可调整 Size（体素数量）、Seed、Sample origin（整数体素坐标偏移）和 Voxel size。点击 **Generate / Regenerate** 同步重建；点击 **Clear** 清除世界。支持负数 Seed 与偏移。非正尺寸或非法输入会显示提示。

运行时只需给场景中的空对象添加 `VoxelWorldPreview`，显示面板的 `VoxelWorldControls` 会自动添加。运行前可在 Inspector 设置初始参数；勾选 Generate On Start 时进入 Play 自动生成。

脚本调用：

```csharp
preview.Generate(new Vector3Int(128, 128, 128), 42, new Vector3Int(-64, 0, -64), 0.5f);
```

Seed 和采样偏移改变采样内容；尺寸决定采样区域大小；Voxel size 只改变显示比例。

也可以通过菜单 **GameObject > Procedural Generation > Voxel World Preview** 创建对象并生成。
默认创建灰色 Lit 材质，需要场景灯光；也可以手动赋予材质。

## 数据流

`WorldFields.FinalDensity -> VoxelVolume.Fill -> VoxelMeshBuilder.Build -> MeshFilter`

- 密度 `>0` 为固体，`<=0` 为空气。
- 一个体素占据 `[x,x+1] × [y,y+1] × [z,z+1]`，生成顶点时乘 `0.5`。
- 默认显示范围为本地坐标 `(0,0,0)` 到 `(64,64,64)`。
- `Sample Origin` 是密度采样的整数体素坐标偏移，不改变网格的本地位置。
- 物体 Transform 可以移动整个可视化区域，不改变采样结果。
- `VoxelVolume` 保存方块数据，每个位置只采样一次。构建 Mesh 时读取数组判断邻居。
- 区域外视为空气，包含底面和侧边封口。
- 每个暴露面单独追加 4 顶点、6 索引；法线固定为该面的外方向。
- X/Z 按 `16×16` 切成子区块，Y 覆盖当前世界全高；每个子区块一个 Mesh、一个子对象。一次 Job 计算全世界的面可见性，再按子区块分配结果，避免重复采样和重复判面。区块数量为 `ceil(width/16) × ceil(depth/16)`。
- 根对象只负责管理，实际 Mesh 位于 `Voxel Chunks XZ16` 下的子对象；每个子对象都有自己的 `MeshFilter` 和 `MeshRenderer`。

## 当前范围

当前生成仍是同步流程，可能在生成时暂停一帧。默认使用 Job System 并行计算每个体素的暴露面；`JobHandle.Complete()` 后主线程创建和上传 Mesh。密度采样仍在主线程，下一步再将密度函数改成 Burst 可用的数据结构。

当前 Job 的边界很明确：`VoxelVolume.Fill` 先完成密度采样，`VoxelFaceVisibilityJob` 只并行处理 `solid -> 六面可见性位掩码`，`VoxelMeshBuilder` 再在主线程把位掩码转换为 Unity Mesh。这样可以先验证 Job 数据流，且不会改变现有密度函数结果。

`Use Jobs For Mesh` 可关闭以对比原始逐体素判定版本。当前没有协程、贪心合并、碰撞体或流体。
只使用 FinalDensity，不把气候、矿脉字段直接当作材料规则。
目前密度库是简化 Value Noise，不是 Minecraft 的精确复刻。
复杂参数可能产生大量暴露面；默认 128³ 包含 2,097,152 次密度采样。
生成时控制台记录实体数量、面数、采样耗时和总耗时，后续据此判断优化重点。
