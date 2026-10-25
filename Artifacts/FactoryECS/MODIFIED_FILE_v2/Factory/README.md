# Factory ECS 基础组件

本目录提供最小可运行链路：

```text
FactoryProducer → FactoryConveyor → FactoryReceiver
```

## 在 SubScene 中使用

1. 创建三个 GameObject，分别挂载 `FactoryProducerAuthoring`、`FactoryConveyorAuthoring`、`FactoryReceiverAuthoring`。
2. 在 `FactoryConveyorAuthoring` 中把 `source` 指向生产器，`destination` 指向接收器。
3. 三个对象放进同一个 SubScene，Baking 后会得到 Entity。
4. 运行时由固定步系统自动生产、移动、接收和消费物品。

## 表现层

`FactoryPresentationSystem` 会读取每个实体上的 `FactoryViewRef`：生产器有待输出物品时变绿，接收器按库存从蓝色变红色，传送带下生成黄色小球显示路径中的物品。它是调试用表现层，不参与模拟逻辑。请给三个 Authoring GameObject 放置一个可见的 Cube/Sphere Renderer，系统会更新其颜色；传送带上的小球由系统按需创建。

参数含义：

- 生产器：`productionInterval` 是生产一个物品的秒数，`outputCapacity` 是输出槽容量。
- 传送带：`speed` 是空间移动速度，`length` 是路径长度，`itemSpacing` 是物品最小间距。
- 接收器：`capacity` 是库存容量，`consumeRate` 是每秒消费数量。

传送带入口采用尝试接收：入口没有间距时生产器保留成品；出口接收器满时，物品停在路径末端，阻塞向上游传播。

这是学习用的单路径版本，暂未包含分流器、多输入配方、动态拓扑重建和渲染表现。

## 运行时建造交互

在场景中创建一个空 GameObject，挂载 `FactoryRuntimeBuildController`。运行时它会自动创建俯视相机和网格，并提供简易物品栏：

- 点击物品栏按钮选中生产器、传送带或接收器；再次点击取消选择。
- 选中器械后，Q 键按 90° 旋转，左键点击网格放置，右键删除。
- 未选中器械时，左键点击已有器械查看网格位置和方向信息。
- 运行时创建的对象直接写入 `FactoryProducer`、`FactoryConveyor`、`FactoryReceiver` ECS 组件，传送带会连接相邻方向上的生产器和接收器。

这是验证交互和 ECS 链路的最小实现，正式版本可把 OnGUI 物品栏替换为 UGUI，并把实体创建改成 ECB 建造命令系统。
