using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralGeneration.Factory
{
    /// <summary>把生产器的生产进度转换为待输出物品。</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(FactoryConveyorSystem))]
    public partial class FactoryProducerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (RefRW<FactoryProducer> producerRef in
                     SystemAPI.Query<RefRW<FactoryProducer>>())
            {
                ref FactoryProducer producer = ref producerRef.ValueRW;
                if (producer.ProductionInterval <= 0f || producer.OutputCapacity <= 0)
                    continue;

                producer.ProductionProgress += deltaTime;
                while (producer.ProductionProgress >= producer.ProductionInterval &&
                       producer.PendingItems < producer.OutputCapacity)
                {
                    producer.ProductionProgress -= producer.ProductionInterval;
                    producer.PendingItems++;
                }

                // 输出槽满时暂停累计，避免生产进度无限增长。
                if (producer.PendingItems >= producer.OutputCapacity)
                    producer.ProductionProgress = 0f;
            }
        }
    }

    /// <summary>移动传送带物品，并在入口/出口执行容量和类型检查。</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FactoryProducerSystem))]
    public partial class FactoryConveyorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            ComponentLookup<FactoryProducer> producers = GetComponentLookup<FactoryProducer>(false);
            ComponentLookup<FactoryReceiver> receivers = GetComponentLookup<FactoryReceiver>(false);

            foreach (var (conveyorRef, items) in
                     SystemAPI.Query<RefRO<FactoryConveyor>, DynamicBuffer<FactoryConveyorItem>>())
            {
                FactoryConveyor conveyor = conveyorRef.ValueRO;
                DynamicBuffer<FactoryConveyorItem> buffer = items;
                if (conveyor.Length <= 0f || conveyor.Speed < 0f || conveyor.ItemSpacing <= 0f)
                    continue;

                // 从路径末端向入口移动，保证前面的物品先占据空间。
                for (int index = buffer.Length - 1; index >= 0; index--)
                {
                    FactoryConveyorItem item = buffer[index];
                    float nextDistance = conveyor.Length;
                    if (index < buffer.Length - 1)
                        nextDistance = buffer[index + 1].Distance - conveyor.ItemSpacing;

                    item.Distance = math.min(
                        item.Distance + conveyor.Speed * deltaTime,
                        nextDistance);
                    item.Distance = math.max(0f, item.Distance);

                    if (index == items.Length - 1 && item.Distance >= conveyor.Length)
                    {
                        bool delivered = false;
                        if (receivers.HasComponent(conveyor.Destination))
                        {
                            FactoryReceiver receiver = receivers[conveyor.Destination];
                            if (receiver.ItemType == item.ItemType &&
                                receiver.StoredItems < receiver.Capacity)
                            {
                                receiver.StoredItems++;
                                receivers[conveyor.Destination] = receiver;
                                delivered = true;
                            }
                        }

                        if (delivered)
                        {
                            buffer.RemoveAt(index);
                            continue;
                        }

                        item.Distance = conveyor.Length;
                    }

                    buffer[index] = item;
                }

                // 入口采用“尝试接收”：失败时生产器保留成品，阻塞自然向上游传播。
                if (!producers.HasComponent(conveyor.Source))
                    continue;

                FactoryProducer source = producers[conveyor.Source];
                bool enoughHeadSpace = buffer.Length == 0 ||
                                       buffer[0].Distance >= conveyor.ItemSpacing;
                if (source.PendingItems > 0 && enoughHeadSpace)
                {
                    buffer.Insert(0, new FactoryConveyorItem
                    {
                        ItemType = source.ItemType,
                        Distance = 0f,
                    });
                    source.PendingItems--;
                    producers[conveyor.Source] = source;
                }
            }
        }
    }

    /// <summary>接收器消费库存，释放容量后允许传送带继续输入。</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FactoryConveyorSystem))]
    public partial class FactoryReceiverSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (RefRW<FactoryReceiver> receiverRef in
                     SystemAPI.Query<RefRW<FactoryReceiver>>())
            {
                ref FactoryReceiver receiver = ref receiverRef.ValueRW;
                if (receiver.ConsumeRate <= 0f || receiver.StoredItems <= 0)
                    continue;

                receiver.ConsumeProgress += receiver.ConsumeRate * deltaTime;
                while (receiver.ConsumeProgress >= 1f && receiver.StoredItems > 0)
                {
                    receiver.ConsumeProgress -= 1f;
                    receiver.StoredItems--;
                }
            }
        }
    }
}


