using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.AI.Disruptor.Lab
{
    /// <summary>
    /// Self-building test arena for the Rift Disruptor. Touches nothing outside its own hierarchy, so it
    /// cannot affect Student 1's main map. Controls are shown on screen.
    /// </summary>
    public sealed class DisruptorLab : MonoBehaviour
    {
        public bool threeDisruptors;
        public DisruptorNavGrid Grid { get; private set; }
        public DisruptorLabPlayer Player { get; private set; }
        public readonly List<DisruptorBrain> Disruptors = new List<DisruptorBrain>();
        public GameObject Door { get; private set; }

        public static readonly Vector3 PlayerSpawn = new Vector3(-10, 0, -10);
        // All in the open arena (the side room is x > 5.7, z > 5).
        static readonly Vector3[] Spawns = { new Vector3(11, 0, -11), new Vector3(12, 0, -1), new Vector3(-11, 0, 11) };
        static readonly Vector3[] Anchors =
        {
            new Vector3(-9, 0, 6), new Vector3(-4, 0, 7), new Vector3(3, 0, -7), new Vector3(9, 0, -7), new Vector3(-3, 0, -3.5f),
            new Vector3(3.5f, 0, 3), new Vector3(-12, 0, -2), new Vector3(12, 0, 2), new Vector3(9, 0, 10),
        };

        readonly List<Material> materials = new List<Material>();
        Material floor, wall, accent;
        GUIStyle style;

        void Start()
        {
            floor = Mat("Disruptor lab floor", new Color(.05f, .06f, .08f));
            wall = Mat("Disruptor lab wall", new Color(.2f, .23f, .28f));
            accent = Mat("Disruptor lab door", new Color(1f, .5f, .1f));

            Block("Floor", new Vector3(0, -.3f, 0), new Vector3(33, .6f, 33), floor);
            Block("North wall", new Vector3(0, 2, 16), new Vector3(33, 4, 1), wall);
            Block("South wall", new Vector3(0, 2, -16), new Vector3(33, 4, 1), wall);
            Block("East wall", new Vector3(16, 2, 0), new Vector3(1, 4, 33), wall);
            Block("West wall", new Vector3(-16, 2, 0), new Vector3(1, 4, 33), wall);
            Block("Central pillar", new Vector3(0, 1.75f, 0), new Vector3(3, 3.5f, 3), wall);
            Block("Cover west", new Vector3(-7, 1, 3), new Vector3(1.2f, 2, 4), wall);
            Block("Cover east", new Vector3(6, 1, -4), new Vector3(4, 2, 1.2f), wall);
            // Side room in the north-east corner: an L wall with a doorway closed by a toggle door.
            Block("Side room wall south", new Vector3(10.5f, 1.75f, 5), new Vector3(10, 3.5f, .6f), wall);
            Block("Side room wall west A", new Vector3(5.7f, 1.75f, 8), new Vector3(.6f, 3.5f, 6), wall);
            Block("Side room wall west B", new Vector3(5.7f, 1.75f, 14), new Vector3(.6f, 3.5f, 2), wall);
            Door = Block("Side room door (F2)", new Vector3(5.7f, 1.75f, 12), new Vector3(.5f, 3.5f, 2), accent);
            Door.SetActive(false);

            // Hand-placed anchors beside cover; the Disruptor also samples its own candidates.
            foreach (var p in Anchors)
            {
                var anchor = new GameObject("Rift anchor").AddComponent<DisruptorAnchor>();
                anchor.transform.SetParent(transform, false); anchor.transform.position = p;
            }

            Physics.SyncTransforms();
            var gridObject = new GameObject("Disruptor nav grid"); gridObject.SetActive(false);   // configure before Awake scans
            gridObject.transform.SetParent(transform, false);
            Grid = gridObject.AddComponent<DisruptorNavGrid>();
            Grid.origin = new Vector3(-16, 0, -16); Grid.size = new Vector2Int(64, 64); Grid.cellSize = .5f;
            Grid.watched.Add(Door.GetComponent<Collider>());
            gridObject.SetActive(true);

            var playerObject = new GameObject("Lab player"); playerObject.layer = 8; playerObject.tag = "Player";
            var capsule = playerObject.AddComponent<CharacterController>(); capsule.height = 1.8f; capsule.radius = .35f; capsule.center = Vector3.up * .9f; capsule.minMoveDistance = 0;
            Player = playerObject.AddComponent<DisruptorLabPlayer>();
            var cam = new GameObject("Lab player camera"); cam.tag = "MainCamera"; cam.transform.SetParent(playerObject.transform, false); cam.transform.localPosition = Vector3.up * 1.6f;
            Player.eye = cam.AddComponent<Camera>(); Player.eye.nearClipPlane = .05f; Player.eye.fieldOfView = 75;
            Player.eye.clearFlags = CameraClearFlags.SolidColor; Player.eye.backgroundColor = new Color(.02f, .02f, .05f);
            cam.AddComponent<AudioListener>();

            var sun = new GameObject("Lab light").AddComponent<Light>(); sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new Color(.3f, .32f, .4f);
            gameObject.AddComponent<DisruptorDebugOverlay>();
            ResetRound();
        }

        Material Mat(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = name, color = c };
            m.SetColor("_BaseColor", c); materials.Add(m); return m;
        }

        GameObject Block(string name, Vector3 position, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(transform, false); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public void ResetRound()
        {
            foreach (var d in Disruptors) if (d != null) { d.gameObject.SetActive(false); Destroy(d.gameObject); }
            Disruptors.Clear();
            Player.ResetPlayer(PlayerSpawn, 45);
            int count = threeDisruptors ? 3 : 1;
            for (int i = 0; i < count; i++) Spawn(Spawns[i]);
        }

        public DisruptorBrain Spawn(Vector3 position)
        {
            var go = new GameObject("Rift Disruptor " + (Disruptors.Count + 1)); go.layer = 8;
            go.transform.SetParent(transform, false); go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(PlayerSpawn - position);
            var body = go.AddComponent<CharacterController>(); body.radius = .5f; body.height = 1.8f; body.center = Vector3.up * .9f; body.stepOffset = .25f;
            go.AddComponent<Skybreak.Movement.BasicPathFollower>();
            var brain = go.AddComponent<DisruptorBrain>(); brain.grid = Grid; brain.player = Player.transform; brain.playerVitals = Player;
            go.AddComponent<DisruptorTelegraph>().brain = brain;
            Disruptors.Add(brain);
            return brain;
        }

        public void SetDoorClosed(bool closed)
        {
            Door.SetActive(closed);
            Grid.Refresh(new Bounds(Door.transform.position, Door.transform.localScale));   // immediate; the watcher would catch it within 0.25 s
        }

        void Update()
        {
            var k = Keyboard.current; if (k == null) return;
            if (k.rKey.wasPressedThisFrame) ResetRound();
            if (k.f2Key.wasPressedThisFrame) SetDoorClosed(!Door.activeSelf);
            if (k.f3Key.wasPressedThisFrame) { threeDisruptors = !threeDisruptors; ResetRound(); }
        }

        void OnGUI()
        {
            if (Player == null || !Player.acceptInput) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            GUI.Box(new Rect(12, 12, 380, 130), "");
            string s = "RIFT DISRUPTOR LAB — Student 4\nWASD move · Mouse look · Shift sprint · Space jump · Click fire\n" +
                       "R reset · F2 side-room door · F3 one/three Disruptors · F8 AI overlay\n" +
                       $"Health {Player.Health:0} · Door {(Door.activeSelf ? "CLOSED" : "OPEN")} · Disruptors {Disruptors.FindAll(d => d != null && d.Alive).Count}/{Disruptors.Count}";
            if (!Player.Alive) s += "\nArc down — press R";
            GUI.Label(new Rect(22, 18, 366, 124), s, style);
        }

        void OnDestroy() { foreach (var m in materials) if (m != null) Destroy(m); }
    }
}
