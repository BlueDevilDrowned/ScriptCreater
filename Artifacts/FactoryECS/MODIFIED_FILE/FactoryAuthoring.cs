using Unity.Entities;
using UnityEngine;

namespace ProceduralGeneration.Factory
{
    public sealed class FactoryProducerAuthoring : MonoBehaviour
    {
        [Min(0)] public int itemType;
        [Min(0.01f)] public float productionInterval = 1f;
        [Min(1)] public int outputCapacity = 4;
    }

    public sealed class FactoryProducerBaker : Baker<FactoryProducerAuthoring>
    {
        public override void Bake(FactoryProducerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new FactoryProducer
            {
                ItemType = authoring.itemType,
                ProductionInterval = authoring.productionInterval,
                OutputCapacity = authoring.outputCapacity,
                ProductionProgress = 0f,
                PendingItems = 0,
            });
        }
    }

    public sealed class FactoryConveyorAuthoring : MonoBehaviour
    {
        public GameObject source;
        public GameObject destination;
        [Min(0.01f)] public float length = 4f;
        [Min(0f)] public float speed = 2f;
        [Min(0.01f)] public float itemSpacing = 1f;
    }

    public sealed class FactoryConveyorBaker : Baker<FactoryConveyorAuthoring>
    {
        public override void Bake(FactoryConveyorAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new FactoryConveyor
            {
                Source = authoring.source == null
                    ? Entity.Null
                    : GetEntity(authoring.source, TransformUsageFlags.None),
                Destination = authoring.destination == null
                    ? Entity.Null
                    : GetEntity(authoring.destination, TransformUsageFlags.None),
                Length = authoring.length,
                Speed = authoring.speed,
                ItemSpacing = authoring.itemSpacing,
            });
            AddBuffer<FactoryConveyorItem>(entity);
        }
    }

    public sealed class FactoryReceiverAuthoring : MonoBehaviour
    {
        [Min(0)] public int itemType;
        [Min(0f)] public float consumeRate = 1f;
        [Min(1)] public int capacity = 8;
    }

    public sealed class FactoryReceiverBaker : Baker<FactoryReceiverAuthoring>
    {
        public override void Bake(FactoryReceiverAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new FactoryReceiver
            {
                ItemType = authoring.itemType,
                ConsumeRate = authoring.consumeRate,
                Capacity = authoring.capacity,
                StoredItems = 0,
                ConsumeProgress = 0f,
            });
        }
    }
}
