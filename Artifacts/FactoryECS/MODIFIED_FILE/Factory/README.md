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

参数含义：

- 生产器：`productionInterval` 是生产一个物品的秒数，`outputCapacity` 是输出槽容量。
- 传送带：`speed` 是空间移动速度，`length` 是路径长度，`itemSpacing` 是物品最小间距。
- 接收器：`capacity` 是库存容量，`consumeRate` 是每秒消费数量。

传送带入口采用尝试接收：入口没有间距时生产器保留成品；出口接收器满时，物品停在路径末端，阻塞向上游传播。

这是学习用的单路径版本，暂未包含分流器、多输入配方、动态拓扑重建和渲染表现。
