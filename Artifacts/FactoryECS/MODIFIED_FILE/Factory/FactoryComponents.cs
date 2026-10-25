using Unity.Entities;

namespace ProceduralGeneration.Factory
{
    /// <summary>生产器：按 ProductionInterval 产生 ItemType，并把成品暂存在 PendingItems。</summary>
    public struct FactoryProducer : IComponentData
    {
        public int ItemType;
        public float ProductionInterval;
        public float ProductionProgress;
        public int OutputCapacity;
        public int PendingItems;
    }

    /// <summary>传送带路径：一条连续路径的运行时聚合实体。</summary>
    public struct FactoryConveyor : IComponentData
    {
        public Entity Source;
        public Entity Destination;
        public float Length;
        public float Speed;
        public float ItemSpacing;
    }

    /// <summary>接收器：拥有输入容量，并按 ConsumeRate 消耗库存。</summary>
    public struct FactoryReceiver : IComponentData
    {
        public int ItemType;
        public float ConsumeRate;
        public int Capacity;
        public int StoredItems;
        public float ConsumeProgress;
    }

    /// <summary>传送带中的物品。Distance 从路径起点开始计算。</summary>
    [InternalBufferCapacity(8)]
    public struct FactoryConveyorItem : IBufferElementData
    {
        public int ItemType;
        public float Distance;
    }
}
