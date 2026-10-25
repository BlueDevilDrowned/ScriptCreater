using Unity.Entities;
using UnityEngine;

namespace ProceduralGeneration.Factory
{
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
