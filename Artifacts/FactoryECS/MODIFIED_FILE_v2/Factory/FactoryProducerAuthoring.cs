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
            AddComponent(entity, new FactoryViewRef { Root = authoring.gameObject });
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
}
