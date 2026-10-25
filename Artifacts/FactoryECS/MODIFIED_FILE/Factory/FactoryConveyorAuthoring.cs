using Unity.Entities;
using UnityEngine;

namespace ProceduralGeneration.Factory
{
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
}
