using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace ProceduralGeneration.Factory
{
    /// <summary>
    /// 把 ECS 状态同步到 Authoring GameObject 的轻量调试表现层。
    /// 该系统只负责显示，不参与生产、运输和容量判定。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FactoryPresentationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<FactoryViewRef>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int index = 0; index < entities.Length; index++)
                UpdateView(entities[index]);
        }

        private void UpdateView(Entity entity)
        {
            FactoryViewRef view = EntityManager.GetComponentData<FactoryViewRef>(entity);
            GameObject root = view.Root.Value;
            if (root == null)
                return;

            if (EntityManager.HasComponent<FactoryProducer>(entity))
            {
                FactoryProducer producer = EntityManager.GetComponentData<FactoryProducer>(entity);
                SetColor(root, producer.PendingItems > 0
                    ? new Color(0.2f, 0.9f, 0.25f)
                    : new Color(0.25f, 0.45f, 0.25f));
                return;
            }

            if (EntityManager.HasComponent<FactoryReceiver>(entity))
            {
                FactoryReceiver receiver = EntityManager.GetComponentData<FactoryReceiver>(entity);
                float fill = receiver.Capacity <= 0
                    ? 0f
                    : Mathf.Clamp01((float)receiver.StoredItems / receiver.Capacity);
                SetColor(root, Color.Lerp(new Color(0.15f, 0.3f, 0.9f),
                    new Color(0.95f, 0.2f, 0.15f), fill));
                return;
            }

            if (!EntityManager.HasComponent<FactoryConveyor>(entity))
                return;

            FactoryConveyor conveyor = EntityManager.GetComponentData<FactoryConveyor>(entity);
            DynamicBuffer<FactoryConveyorItem> items =
                EntityManager.GetBuffer<FactoryConveyorItem>(entity);
            SetColor(root, new Color(0.35f, 0.25f, 0.12f));

            for (int index = 0; index < items.Length; index++)
            {
                Transform itemView = GetOrCreateItemView(root.transform, index);
                float normalized = conveyor.Length <= 0f
                    ? 0f
                    : Mathf.Clamp01(items[index].Distance / conveyor.Length);
                itemView.localPosition = new Vector3(
                    Mathf.Lerp(-0.5f, 0.5f, normalized),
                    0.35f,
                    0f);
                itemView.gameObject.SetActive(true);
            }

            for (int index = items.Length; index < root.transform.childCount; index++)
            {
                Transform child = root.transform.GetChild(index);
                if (child.name.StartsWith("FactoryItemVisual_"))
                    child.gameObject.SetActive(false);
            }
        }

        private static void SetColor(GameObject root, Color color)
        {
            Renderer renderer = root.GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        private static Transform GetOrCreateItemView(Transform root, int index)
        {
            string name = "FactoryItemVisual_" + index;
            Transform existing = root.Find(name);
            if (existing != null)
                return existing;

            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = name;
            item.transform.SetParent(root, false);
            item.transform.localScale = Vector3.one * 0.18f;
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(1f, 0.75f, 0.1f);
            return item.transform;
        }
    }
}
