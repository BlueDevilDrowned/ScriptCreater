# DensityFunction 方法解读

这组代码是一个简化的 Minecraft 风格密度函数库。它没有复制 Minecraft 1.20.1 的全部实现，而是保留了最重要的结构：

```text
坐标上下文
    -> DensityFunction 节点
    -> 节点组合
    -> 有语义的世界场
    -> 地形、Biome、流体、矿脉系统读取
```

所有函数都返回一个 `double`。这个数可以表示高度、温度、湿度、地形密度或其他连续世界数据。

## 1. `DensityContext`

文件：`DensityContext.cs`

```csharp
public readonly struct DensityContext
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
}
```

它表示一次采样的位置。

`X`、`Y`、`Z` 是世界方块坐标，而不是区块内坐标。使用世界坐标可以保证相邻区块在边界处继续采样同一个连续函数。

它是 `readonly struct`，含义是：

- 创建成本低；
- 创建后坐标不可修改；
- 可以安全地作为一次采样的输入；
- 不保存任何生成状态。

构造方法：

```csharp
new DensityContext(x, y, z)
```

## 2. `IDensityFunction`

文件：`IDensityFunction.cs`

```csharp
public interface IDensityFunction
{
    double Sample(in DensityContext context);
}
```

这是整个库的核心抽象。每一个节点都必须实现 `Sample`。

可以把它理解为：

```text
一个坐标 -> 一个数值
```

例如：

```text
常量函数：任意坐标都返回 3
噪声函数：不同坐标返回不同值
温度函数：返回该位置的温度场值
最终密度：返回该位置是固体还是空气的依据
```

该接口没有直接规定这个数的语义。语义由使用它的系统决定：

```text
Biome 系统把它当作气候参数
地形系统把它当作密度
含水层系统把它当作流体倾向
矿脉系统把它当作矿脉形状
```

## 3. `DensityFunctions`

文件：`DensityFunctions.cs`

这是一个工厂类。它隐藏具体实现类，让调用者只组合接口，不需要直接 `new` 内部节点。

### `Constant(double value)`

```csharp
DensityFunctions.Constant(2.0)
```

创建一个始终返回相同值的函数。

用途：

- 提供偏移量；
- 提供乘数；
- 作为条件分支的结果；
- 构造测试函数。

### `Noise(int seed, double scale, double amplitude)`

```csharp
DensityFunctions.Noise(123, 0.01, 1.0)
```

创建确定性的三维 Value Noise。

- `seed`：决定随机格点值；
- `scale`：决定坐标采样频率；
- `amplitude`：决定输出范围大小。

相同的 `seed` 和坐标会得到相同结果。改变 `seed` 会得到另一张噪声场。

这里的噪声是学习版 Value Noise，并不是 Minecraft 1.20.1 使用的完整 `NormalNoise` / `BlendedNoise`。

### `Add`、`Subtract`、`Multiply`

```csharp
var result = DensityFunctions.Add(a, b);
var difference = DensityFunctions.Subtract(a, b);
var scaled = DensityFunctions.Multiply(a, 2.0);
```

它们把两个函数组合成一个新函数。采样时先分别采样子函数，再执行运算：

```text
result(x,y,z) = a(x,y,z) + b(x,y,z)
```

注意：组合发生在函数构造阶段，真正的数值计算发生在 `Sample` 阶段。

### `Min`、`Max`、`Abs`

```csharp
DensityFunctions.Min(a, b)
DensityFunctions.Max(a, b)
DensityFunctions.Abs(a)
```

它们分别用于：

- 选择两个世界场中较小的值；
- 选择较大的值；
- 将双向变化转换为距离或强度。

例如：

```csharp
var ridge = DensityFunctions.Abs(noise);
```

这会把正负噪声都变成正值，适合表示“距离山脊中心的强度”或矿脉形状。

### `Clamp`

```csharp
DensityFunctions.Clamp(input, -1.0, 1.0)
```

把输入限制在闭区间 `[min, max]`：

```text
小于 min -> min
大于 max -> max
中间值   -> 原值
```

它常用于防止某一层噪声影响过大，也可以把任意值转换成有限范围内的控制参数。

### `RangeChoice`

```csharp
DensityFunctions.RangeChoice(
    input,
    0.2, 0.8,
    inRange,
    outOfRange);
```

先采样 `input`，再判断：

```text
0.2 <= input < 0.8 -> 采样 inRange
其他情况          -> 采样 outOfRange
```

`minInclusive` 是包含的，`maxExclusive` 是不包含的。

它类似 Minecraft 的条件密度函数，可用于：

- 在某个大陆性范围内提高地形；
- 只在指定高度或噪声范围内生成内容；
- 根据一个世界场切换两套地形公式。

### `YClampedGradient`

```csharp
DensityFunctions.YClampedGradient(0, 160, 1.0, -1.0)
```

根据 `Y` 坐标在线性插值，超出范围后固定在端点：

```text
Y <= fromY -> fromValue
Y >= toY   -> toValue
中间       -> 线性插值
```

它常用来表达垂直趋势，例如：

- 越接近世界底部越容易生成固体；
- 越高密度越低；
- 在某个高度范围内逐渐过渡到海洋或空气。

### `CacheOnce`

```csharp
DensityFunctions.CacheOnce(input)
```

缓存上一次坐标的结果。如果下一次采样坐标相同，就直接返回缓存值。

它适合一个函数在同一个坐标被多个组合节点重复读取的情况。当前实现是最小缓存，只保存一个坐标；它不是区块级缓存，也不是线程安全的共享缓存。

## 4. 内部节点类

这些类是 `internal`，调用者通常通过 `DensityFunctions` 创建它们。

### `ConstantFunction`

保存一个 `value`。`Sample` 忽略坐标，直接返回该值。

### `UnaryFunction`

保存一个输入函数和一个一元操作：

```csharp
operation(input.Sample(context))
```

目前 `Abs` 使用它。以后可以复用它实现 `Negate`、平方、平滑曲线等节点。

### `BinaryFunction`

保存左右两个输入和一个二元操作：

```csharp
operation(left.Sample(context), right.Sample(context))
```

`Add`、`Subtract`、`Multiply`、`Min`、`Max` 都复用同一个实现。这体现了“节点结构”和“运算规则”分离。

### `ClampFunction`

先采样输入，再执行两次比较，把结果压缩到指定范围。构造时会检查 `min <= max`。

### `RangeChoiceFunction`

先采样条件函数，只计算被选中的分支。它不会同时计算 `inRange` 和 `outOfRange`，因此条件分支也可以用于减少不必要的计算。

### `YGradientFunction`

只读取 `context.Y`，不依赖 X 和 Z。它使用线性插值产生垂直方向的控制场。

### `CacheOnceFunction`

保存：

```text
上一次坐标
上一次结果
是否已有缓存
```

坐标变化后才重新计算。由于它有可变缓存，不应把同一个实例无保护地放到多个并发线程之间共享。

### `ValueNoiseFunction`

它的采样流程是：

```text
世界坐标 * scale
    -> 找到包围采样点的整数格点
    -> 为 8 个格点生成确定性随机值
    -> 使用 Smooth 曲线计算插值权重
    -> X、Y、Z 三个方向三线性插值
    -> 乘以 amplitude
```

`Lattice` 使用种子和整数格点坐标计算哈希，不保存随机状态，因此不会因为采样顺序变化而改变结果。

内部方法：

- `FloorToInt`：向下取整，得到格点坐标；
- `Smooth`：使用 `t*t*(3-2*t)` 平滑插值权重；
- `Lerp`：线性插值；
- `Lattice`：为格点生成 `[-1, 1]` 范围内的确定性值。

## 5. `WorldFields`

文件：`WorldFields.cs`

`WorldFields` 对应 Minecraft `NoiseRouter` 的简化版本。它不把所有结果都叫作 `FinalDensity`，而是保存多组有语义的函数：

| 字段 | 用途建议 |
|---|---|
| `Temperature` | 生物群系温度 |
| `Vegetation` | 生物群系植被参数 |
| `Continents` | 大陆、海洋和宏观地形 |
| `Erosion` | 地形侵蚀程度 |
| `Depth` | 地形深度或垂直形状 |
| `Ridges` | 山脊、山谷或矿脉形状 |
| `FinalDensity` | 判断固体、空气和地下空间 |
| `FluidFloodedness` | 地下空间的含水倾向 |
| `FluidSpread` | 流体传播相关参数 |
| `Lava` | 熔岩倾向 |
| `VeinToggle` | 矿脉是否出现 |
| `VeinRidged` | 矿脉形状 |
| `VeinGap` | 矿脉空隙 |

构造函数要求前 7 个主要世界场存在。后面的流体和矿脉字段可以省略，省略时使用 `Constant(0)` 作为默认值。

### `CreateLearningDefaults(int seed)`

这是一个可运行的学习版世界场配置。它做了以下事情：

```text
创建大陆噪声
创建侵蚀噪声
创建山脊噪声
根据大陆噪声构造山地项
加入高频细节噪声
加入 Y 方向渐变
组合成 FinalDensity
为流体和矿脉创建独立噪声场
```

当前学习版最终地形密度大致是：

```text
depth = verticalGradient(y) + terrainOffset(x,z)
jaggedTerm = terrainJaggedness(x,z) * HalfNegative(jaggedNoise(x,z))
rawDensity = 4 * QuarterNegative((depth + jaggedTerm) * terrainFactor(x,z))
           + base3DNoise(x,y,z)
finalDensity = VerticalSlide(rawDensity)
```

`VerticalSlide` 对应 Minecraft 密度函数中的上下边界滑动：底部逐渐趋向固体目标值，顶部逐渐趋向空气目标值，中间区域保留原始密度。它仍然是学习版参数，并不是原版全部 `NoiseRouter` 的精确复刻。

## 6. 一次采样的完整过程

下面的代码：

```csharp
var mountains = DensityFunctions.Noise(123, 0.01, 1.0);
var height = DensityFunctions.Add(
    DensityFunctions.Multiply(mountains, 40.0),
    DensityFunctions.YClampedGradient(0, 160, 1.0, -1.0));

var point = new DensityContext(x, y, z);
double value = height.Sample(point);
bool solid = value > 0.0;
```

执行顺序是：

```text
height.Sample(point)
  -> 采样 Multiply
     -> 采样 mountains
        -> 采样 ValueNoise
     -> 采样 Constant(40)
     -> 相乘
  -> 采样 YClampedGradient
  -> 相加
  -> 得到 density
```

最后由调用者解释这个值：

```text
density > 0 -> 固体
density <= 0 -> 空气或流体
```

## 7. 与 Minecraft 源码的对应关系

| 本项目 | Minecraft 1.20.1 |
|---|---|
| `IDensityFunction` | `DensityFunction` |
| `DensityFunctions` | `DensityFunctions` 工具和节点工厂 |
| `ValueNoiseFunction` | 简化的噪声节点，不等价于 `NormalNoise` |
| `WorldFields` | `NoiseRouter` |
| `FinalDensity` | `NoiseRouter.finalDensity()` |
| `Temperature` 到 `Ridges` | `Climate.Sampler` 使用的气候函数 |
| `FluidFloodedness`、`FluidSpread`、`Lava` | `Aquifer` 使用的流体函数 |
| `VeinToggle`、`VeinRidged`、`VeinGap` | `OreVeinifier` 使用的矿脉函数 |

重要区别：Minecraft 还包含函数引用、Codec、注册表、cell 插值、区块缓存、Blending 和更复杂的噪声实现；本库暂时只保留最适合学习组合思想的部分。

## 8. 推荐学习顺序

1. 先阅读 `IDensityFunction` 和 `DensityContext`，理解统一输入输出。
2. 使用 `Constant`、`Noise`、`Add`、`Multiply` 构造一个简单地形。
3. 使用 `Clamp` 和 `RangeChoice` 学习限制与条件分支。
4. 使用 `YClampedGradient` 理解垂直控制场。
5. 阅读 `ValueNoiseFunction.Sample`，理解格点、平滑和三线性插值。
6. 阅读 `WorldFields.CreateLearningDefaults`，观察多个世界场如何共享同一个函数库。
7. 再实现 Biome、Aquifer 和 OreVeinifier，让它们分别读取对应字段。

## 9. 当前实现的边界

- `ValueNoiseFunction` 只是确定性三维 Value Noise，不是 Minecraft 的精确噪声算法。
- `CacheOnce` 只缓存一个坐标，不适合替代区块级缓存。
- 节点目前没有序列化和资源文件加载。
- `WorldFields` 的默认参数是教学参数，不保证生成 Minecraft 风格的完整地形。
- 所有节点返回 `double`，方块写入、Biome 选择和流体决策仍应由上层系统完成。

## 10. 当前加入的 `CubicSplineFunction`

文件：`CubicSplineFunction.cs`、`CubicSplinePoint.cs`

当前默认世界场已经使用第一阶段的 Minecraft 风格样条：

```text
Continents -> Offset
Erosion    -> Factor
Ridges     -> Jaggedness
```

然后组合为：

```text
depth = verticalGradient + offset
baseTerrain = 4 * QuarterNegative((depth + jaggedness * HalfNegative(jaggedNoise)) * factor)
finalDensity = VerticalSlide(baseTerrain + base3DNoise)
```

`CubicSplinePoint` 保存三个量：

```csharp
new CubicSplinePoint(location, value, derivative)
```

- `location`：输入世界场值位于什么位置；
- `value`：该位置对应的输出；
- `derivative`：曲线经过该点时的斜率。

`CubicSplineFunction.Sample` 会先采样 `coordinate`，查找相邻控制点，再执行 Hermite 三次插值。输入超出控制点范围时，使用端点斜率线性外推。

当前已支持控制点的 value 引用另一个函数，包括嵌套样条。大陆性、侵蚀度、山脊度及它们派生的样条参数使用 XZ 二维输入；同一列共享这些参数。最新实现、目的、源码差异与验证记录见同目录《世界生成变更记录.md》。
