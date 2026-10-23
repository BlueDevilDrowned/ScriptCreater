# 最小 DensityFunction 库

这是面向学习的 Minecraft 风格密度函数移植，不追求 Minecraft 1.20.1 的数值一致性。

## 最小用法

```csharp
using ProceduralGeneration.WorldGen.Density;

IDensityFunction mountains = DensityFunctions.Noise(123, 0.01, 1.0);
IDensityFunction height = DensityFunctions.Add(
    DensityFunctions.Multiply(mountains, 40.0),
    DensityFunctions.YClampedGradient(0, 160, 1.0, -1.0));

var point = new DensityContext(worldX, worldY, worldZ);
double value = height.Sample(point);
bool solid = value > 0.0;
```

## 与 Minecraft 的对应关系

| 本库 | Minecraft 1.20.1 概念 |
|---|---|
| `IDensityFunction` | `DensityFunction` |
| `Constant`、`Noise` | 基础节点 |
| `Add`、`Multiply`、`Clamp` | 组合节点 |
| `RangeChoice` | 条件选择节点 |
| `YClampedGradient` | 垂直渐变 |
| `CacheOnce` | 简化缓存 |
| `WorldFields` | `NoiseRouter` 的语义输出集合 |

`WorldFields.CreateLearningDefaults(seed)` 提供一套可运行的学习默认值。Biome、含水层、矿脉和地形系统可以读取同一个 `WorldFields`，但各自使用不同字段。
