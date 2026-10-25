using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralGeneration.Factory
{
    /// <summary>
    /// 运行时工厂建造入口：网格、俯视相机、Q 旋转、物品栏、放置/删除/查看。
    /// 建造结果直接写入 ECS，因此场景 Authoring 和运行时建造使用同一套模拟组件。
    /// </summary>
    public sealed class FactoryRuntimeBuildController : MonoBehaviour
    {
        private enum DeviceKind { Producer, Conveyor, Receiver }

        private sealed class PlacedDevice
        {
            public Entity Entity;
            public GameObject View;
            public Vector2Int Cell;
            public DeviceKind Kind;
            public byte Rotation;
        }

        [Header("Grid")]
        [Min(1)] public int gridWidth = 24;
        [Min(1)] public int gridHeight = 16;
        [Min(0.1f)] public float cellSize = 1f;
        public Color gridColor = new Color(0.2f, 0.35f, 0.45f, 0.65f);

        [Header("Runtime defaults")]
        public float producerInterval = 1f;
        public float conveyorSpeed = 2f;
        public float receiverConsumeRate = 1f;

        private readonly Dictionary<Vector2Int, PlacedDevice> occupied = new Dictionary<Vector2Int, PlacedDevice>();
        private readonly List<PlacedDevice> devices = new List<PlacedDevice>();
        private readonly Dictionary<int, PlacedDevice> viewLookup = new Dictionary<int, PlacedDevice>();
        private EntityManager entityManager;
        private DeviceKind? selectedKind;
        private byte rotation;
        private PlacedDevice inspected;
        private GameObject gridRoot;
        private Material gridMaterial;

        private void Start()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;
            entityManager = world.EntityManager;
            EnsureTopDownCamera();
            BuildGridVisual();
        }

        private void Update()
        {
            if (!entityManager.IsCreated)
                return;

            if (Input.GetKeyDown(KeyCode.Q))
                rotation = (byte)((rotation + 1) % 4);

            if (Input.GetMouseButtonDown(1))
            {
                DeleteAtMouse();
                return;
            }

            if (!Input.GetMouseButtonDown(0) || IsPointerOverInventory())
                return;

            if (selectedKind.HasValue)
            {
                TryPlaceAtMouse(selectedKind.Value);
            }
            else
            {
                inspected = FindDeviceAtMouse();
            }
        }

        private void OnGUI()
        {
            const float panelHeight = 76f;
            GUI.Box(new Rect(8f, Screen.height - panelHeight - 8f, 390f, panelHeight), "工厂物品栏");
            DrawToolButton(new Rect(18f, Screen.height - panelHeight + 8f, 112f, 28f), "生产器", DeviceKind.Producer);
            DrawToolButton(new Rect(136f, Screen.height - panelHeight + 8f, 112f, 28f), "传送带", DeviceKind.Conveyor);
            DrawToolButton(new Rect(254f, Screen.height - panelHeight + 8f, 112f, 28f), "接收器", DeviceKind.Receiver);
            GUI.Label(new Rect(18f, Screen.height - 32f, 370f, 20f),
                selectedKind.HasValue
                    ? $"已选：{KindName(selectedKind.Value)}  | Q 旋转：{rotation * 90}° | 左键放置，右键删除"
                    : "未选择 | 左键查看器械信息，右键删除");

            if (inspected != null && inspected.View != null)
            {
                GUI.Box(new Rect(8f, 8f, 260f, 92f), "器械信息");
                GUI.Label(new Rect(20f, 32f, 240f, 22f), KindName(inspected.Kind));
                GUI.Label(new Rect(20f, 54f, 240f, 22f), $"网格位置：{inspected.Cell}");
                GUI.Label(new Rect(20f, 76f, 240f, 22f), $"旋转：{inspected.Rotation * 90}°");
            }
        }

        private void DrawToolButton(Rect rect, string label, DeviceKind kind)
        {
            bool active = selectedKind.HasValue && selectedKind.Value == kind;
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(0.35f, 0.8f, 1f) : Color.white;
            if (GUI.Button(rect, active ? label + " ✓" : label))
                selectedKind = active ? (DeviceKind?)null : kind;
            GUI.backgroundColor = old;
        }

        private bool IsPointerOverInventory()
        {
            return Input.mousePosition.y <= 84f;
        }

        private void TryPlaceAtMouse(DeviceKind kind)
        {
            if (!TryGetMouseCell(out Vector2Int cell) || occupied.ContainsKey(cell))
                return;

            PlacedDevice device = new PlacedDevice
            {
                Cell = cell,
                Kind = kind,
                Rotation = rotation,
                Entity = entityManager.CreateEntity(),
                View = CreateView(kind, cell, rotation),
            };

            switch (kind)
            {
                case DeviceKind.Producer:
                    entityManager.AddComponentData(device.Entity, new FactoryProducer
                    {
                        ItemType = 0, ProductionInterval = producerInterval,
                        OutputCapacity = 4, ProductionProgress = 0f, PendingItems = 0,
                    });
                    break;
                case DeviceKind.Conveyor:
                    entityManager.AddComponentData(device.Entity, new FactoryConveyor
                    {
                        Source = Entity.Null, Destination = Entity.Null, Length = cellSize,
                        Speed = conveyorSpeed, ItemSpacing = 0.75f,
                    });
                    entityManager.AddBuffer<FactoryConveyorItem>(device.Entity);
                    break;
                case DeviceKind.Receiver:
                    entityManager.AddComponentData(device.Entity, new FactoryReceiver
                    {
                        ItemType = 0, ConsumeRate = receiverConsumeRate, Capacity = 8,
                        StoredItems = 0, ConsumeProgress = 0f,
                    });
                    break;
            }

            entityManager.AddComponentData(device.Entity, new FactoryViewRef { Root = device.View });
            occupied.Add(cell, device);
            devices.Add(device);
            viewLookup[device.View.GetInstanceID()] = device;
            RebuildConnections();
        }

        private void DeleteAtMouse()
        {
            PlacedDevice device = FindDeviceAtMouse();
            if (device == null)
                return;
            occupied.Remove(device.Cell);
            devices.Remove(device);
            viewLookup.Remove(device.View.GetInstanceID());
            if (entityManager.Exists(device.Entity))
                entityManager.DestroyEntity(device.Entity);
            Destroy(device.View);
            inspected = null;
            RebuildConnections();
        }

        private PlacedDevice FindDeviceAtMouse()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return null;
            Transform current = hit.transform;
            while (current != null)
            {
                if (viewLookup.TryGetValue(current.gameObject.GetInstanceID(), out PlacedDevice device))
                    return device;
                current = current.parent;
            }
            return null;
        }

        private bool TryGetMouseCell(out Vector2Int cell)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance))
            {
                cell = default;
                return false;
            }
            Vector3 point = ray.GetPoint(distance);
            int x = Mathf.FloorToInt(point.x / cellSize + gridWidth * 0.5f);
            int y = Mathf.FloorToInt(point.z / cellSize + gridHeight * 0.5f);
            cell = new Vector2Int(x, y);
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        private GameObject CreateView(DeviceKind kind, Vector2Int cell, byte quarterTurns)
        {
            GameObject view = GameObject.CreatePrimitive(kind == DeviceKind.Conveyor ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            view.name = "Runtime_" + KindName(kind);
            view.transform.position = CellToWorld(cell) + Vector3.up * 0.25f;
            view.transform.localScale = kind == DeviceKind.Conveyor ? new Vector3(0.9f, 0.35f, 0.9f) : new Vector3(0.78f, 0.5f, 0.78f);
            view.transform.rotation = Quaternion.Euler(0f, quarterTurns * 90f, 0f);
            Renderer renderer = view.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = kind == DeviceKind.Producer ? new Color(0.2f, 0.75f, 0.25f) : kind == DeviceKind.Receiver ? new Color(0.2f, 0.4f, 0.9f) : new Color(0.45f, 0.28f, 0.12f);
            return view;
        }

        private void RebuildConnections()
        {
            for (int i = 0; i < devices.Count; i++)
            {
                PlacedDevice belt = devices[i];
                if (belt.Kind != DeviceKind.Conveyor || !entityManager.Exists(belt.Entity))
                    continue;
                Entity source = Entity.Null;
                Entity destination = Entity.Null;
                int2 direction = RotationDirection(belt.Rotation);
                Vector2Int sourceCell = belt.Cell - new Vector2Int(direction.x, direction.y);
                Vector2Int destinationCell = belt.Cell + new Vector2Int(direction.x, direction.y);
                if (occupied.TryGetValue(sourceCell, out PlacedDevice sourceDevice) && sourceDevice.Kind == DeviceKind.Producer)
                    source = sourceDevice.Entity;
                if (occupied.TryGetValue(destinationCell, out PlacedDevice destinationDevice) && destinationDevice.Kind == DeviceKind.Receiver)
                    destination = destinationDevice.Entity;
                FactoryConveyor conveyor = entityManager.GetComponentData<FactoryConveyor>(belt.Entity);
                conveyor.Source = source;
                conveyor.Destination = destination;
                entityManager.SetComponentData(belt.Entity, conveyor);
            }
        }

        private void BuildGridVisual()
        {
            gridRoot = new GameObject("FactoryRuntimeGrid");
            gridMaterial = new Material(Shader.Find("Sprites/Default")) { color = gridColor };
            float left = -gridWidth * cellSize * 0.5f;
            float bottom = -gridHeight * cellSize * 0.5f;
            for (int x = 0; x <= gridWidth; x++) CreateGridLine(new Vector3(left + x * cellSize, 0.01f, bottom), new Vector3(left + x * cellSize, 0.01f, -bottom));
            for (int y = 0; y <= gridHeight; y++) CreateGridLine(new Vector3(left, 0.01f, bottom + y * cellSize), new Vector3(-left, 0.01f, bottom + y * cellSize));
        }

        private void CreateGridLine(Vector3 start, Vector3 end)
        {
            GameObject lineObject = new GameObject("GridLine");
            lineObject.transform.SetParent(gridRoot.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.material = gridMaterial;
            line.positionCount = 2;
            line.startWidth = 0.015f;
            line.endWidth = 0.015f;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void EnsureTopDownCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            camera.transform.position = new Vector3(0f, Mathf.Max(gridWidth, gridHeight), 0f);
            // Camera looks along local -Z, therefore -90° X points from +Y toward the grid.
            camera.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(gridWidth, gridHeight) * 0.55f;
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3((cell.x + 0.5f - gridWidth * 0.5f) * cellSize, 0f, (cell.y + 0.5f - gridHeight * 0.5f) * cellSize);
        }

        private static int2 RotationDirection(byte value)
        {
            switch (value % 4)
            {
                case 1: return new int2(0, 1);
                case 2: return new int2(-1, 0);
                case 3: return new int2(0, -1);
                default: return new int2(1, 0);
            }
        }

        private static string KindName(DeviceKind kind)
        {
            return kind == DeviceKind.Producer ? "生产器" : kind == DeviceKind.Conveyor ? "传送带" : "接收器";
        }
    }
}
