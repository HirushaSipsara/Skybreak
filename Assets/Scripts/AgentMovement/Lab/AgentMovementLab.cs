using System.Collections.Generic;
using Skybreak.AI.Disruptor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.Movement.Lab
{
    /// <summary>
    /// Student 4 movement and animation lab (Docs/TeamGuide/STUDENT_4_AGENT_MOVEMENT.md).
    /// The robot's brain is switched off: this scene drives the motor directly with path arrays, either
    /// from the A* grid (click the floor) or from the scripted test routes, to show path following,
    /// turning, blocked/invalid handling and animation in isolation. Self-building; nothing is shared with the main map.
    /// </summary>
    public sealed class AgentMovementLab : MonoBehaviour
    {
        public GameObject robotPrefab;
        public float walkSpeed = 2.6f, runSpeed = 7.5f;

        public DisruptorNavGrid Grid { get; private set; }
        public GameObject Robot { get; private set; }
        public SmoothPathFollower Motor { get; private set; }
        public AgentAnimationPresenter Presenter { get; private set; }
        public GameObject Door { get; private set; }
        public GameObject Barricade { get; private set; }
        public string Scenario { get; private set; } = "Click the floor to plan an A* route";

        public static readonly Vector3 StartPoint = new Vector3(-8, 0, -8);
        readonly List<Material> materials = new List<Material>();
        readonly List<GameObject> markers = new List<GameObject>();
        LineRenderer pathLine;
        Camera cam;
        GameObject capsule, visual;
        Material floorMat, wallMat, doorMat, markerMat;
        float orbit = 225, zoom = 17, replaceAt = -1;
        int frameCap;
        bool dead;
        GUIStyle style;

        void Awake()
        {
            floorMat = Mat(new Color(.09f, .085f, .08f)); wallMat = Mat(new Color(.3f, .29f, .27f)); doorMat = Mat(new Color(.8f, .45f, .1f));
            markerMat = Mat(new Color(.2f, .9f, 1f));
            Block("Floor", new Vector3(0, -.25f, 0), new Vector3(24, .5f, 24), floorMat);
            Block("Wall N", new Vector3(0, 1.5f, 12), new Vector3(24, 3, .5f), wallMat);
            Block("Wall S", new Vector3(0, 1.5f, -12), new Vector3(24, 3, .5f), wallMat);
            Block("Wall E", new Vector3(12, 1.5f, 0), new Vector3(.5f, 3, 24), wallMat);
            Block("Wall W", new Vector3(-12, 1.5f, 0), new Vector3(.5f, 3, 24), wallMat);
            // An L corner to walk around (guide: "two walls forming a corner").
            Block("Corner wall A", new Vector3(-3, 1.5f, -2), new Vector3(6, 3, .5f), wallMat);
            Block("Corner wall B", new Vector3(-.25f, 1.5f, -5.75f), new Vector3(.5f, 3, 7), wallMat);
            // A dividing wall with a 3 m doorway (the map's door width) and a door leaf (D).
            Block("Divider W", new Vector3(-5.25f, 1.5f, 5), new Vector3(13.5f, 3, .5f), wallMat);
            Block("Divider E", new Vector3(8.25f, 1.5f, 5), new Vector3(7.5f, 3, .5f), wallMat);
            Door = Block("Door (D)", new Vector3(3, 1.5f, 5), new Vector3(3, 3, .3f), doorMat); Door.SetActive(false);
            Barricade = Block("Barricade (B)", new Vector3(6, .6f, -6), new Vector3(2.5f, 1.2f, .6f), doorMat); Barricade.SetActive(false);

            Physics.SyncTransforms();
            var g = new GameObject("Nav grid"); g.SetActive(false); g.transform.SetParent(transform, false);
            Grid = g.AddComponent<DisruptorNavGrid>();
            Grid.origin = new Vector3(-12, 0, -12); Grid.size = new Vector2Int(48, 48); Grid.cellSize = .5f;
            Grid.agentRadius = .65f; Grid.agentHeight = 2.1f; Grid.watchDoorsAndBarricades = false;
            Grid.watched.Add(Door.GetComponent<Collider>()); Grid.watched.Add(Barricade.GetComponent<Collider>());
            g.SetActive(true);

            var camObject = new GameObject("Lab camera"); camObject.tag = "MainCamera";
            cam = camObject.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.03f, .035f, .05f);
            camObject.AddComponent<AudioListener>();
            var key = new GameObject("Key light").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.3f;
            key.color = new Color(1f, .82f, .62f); key.transform.rotation = Quaternion.Euler(50, -40, 0); key.shadows = LightShadows.Soft;
            var rim = new GameObject("Rim light").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = .5f;
            rim.color = new Color(.45f, .8f, 1f); rim.transform.rotation = Quaternion.Euler(30, 140, 0);
            RenderSettings.ambientLight = new Color(.22f, .24f, .3f);

            pathLine = new GameObject("Path line").AddComponent<LineRenderer>();
            pathLine.widthMultiplier = .06f; pathLine.sharedMaterial = markerMat; pathLine.positionCount = 0;
            SpawnRobot();
        }

        void SpawnRobot()
        {
            if (robotPrefab != null) Robot = Instantiate(robotPrefab, StartPoint, Quaternion.identity, transform);
            else
            {
                Robot = new GameObject("Robot (no prefab)"); Robot.transform.position = StartPoint;
                var cc = Robot.AddComponent<CharacterController>(); cc.radius = .55f; cc.height = 2.1f; cc.center = Vector3.up * 1.05f;
                Robot.AddComponent<SmoothPathFollower>(); Robot.AddComponent<AgentAnimationPresenter>();
            }
            Robot.name = "Rift Disruptor (motor only)";
            // Brain off: the lab drives the motor directly. Visual glue keeps working without decisions.
            if (Robot.TryGetComponent<DisruptorBrain>(out var brain)) brain.enabled = false;
            Motor = Robot.GetComponent<SmoothPathFollower>();
            Presenter = Robot.GetComponent<AgentAnimationPresenter>();
            visual = Robot.transform.Find("Visual") != null ? Robot.transform.Find("Visual").gameObject : null;
            capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule); capsule.name = "Capsule stand-in (V)";
            Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(Robot.transform, false); capsule.transform.localPosition = Vector3.up * 1.05f; capsule.transform.localScale = new Vector3(1.1f, 1.05f, 1.1f);
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(capsule.transform, false); nose.transform.localPosition = new Vector3(0, .4f, .5f); nose.transform.localScale = new Vector3(.3f, .15f, .3f);
            capsule.SetActive(visual == null);
        }

        Material Mat(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = c };
            m.SetColor("_BaseColor", c); materials.Add(m); return m;
        }

        GameObject Block(string name, Vector3 position, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(transform, false); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }

        // ------------------------------------------------------------------ scenarios (also used by the automated checks)
        public void Send(Vector3[] waypoints, float speed, string label)
        {
            Scenario = label;
            Motor.SetPath(new PathResult { Status = PathStatus.Complete, Waypoints = waypoints, NavigationVersion = Grid.Version }, speed);
            DrawPath(waypoints);
        }

        public PathResult PlanTo(Vector3 goal, float speed, string label)
        {
            var path = Grid.FindPath(Robot.transform.position, goal);
            Scenario = $"{label}: A* {path.Status}, {path.Waypoints.Length} waypoints, {path.Expanded} cells expanded";
            if (path.HasRoute) { Motor.SetPath(path, speed); DrawPath(path.Waypoints); }
            else { Motor.Stop(); DrawPath(System.Array.Empty<Vector3>()); }
            return path;
        }

        public static readonly Vector3[] ThreePoint = { new Vector3(-8, 0, 0), new Vector3(-1, 0, 2.5f), new Vector3(6, 0, -1) };
        public static readonly Vector3[] CornerCourse = { new Vector3(-8, 0, -1), new Vector3(1.2f, 0, -1), new Vector3(1.2f, 0, -10), new Vector3(-4, 0, -10) };
        public static readonly Vector3[] ThroughDoor = { new Vector3(3, 0, 0), new Vector3(3, 0, 9) };

        public void ResetRobot()
        {
            Motor.Stop(); dead = false; Presenter.SetDead(false);
            var cc = Robot.GetComponent<CharacterController>(); cc.enabled = false;
            Robot.transform.SetPositionAndRotation(StartPoint, Quaternion.identity); cc.enabled = true; Physics.SyncTransforms();
            replaceAt = -1; DrawPath(System.Array.Empty<Vector3>()); Scenario = "Reset";
        }

        public void SetDoor(bool closed) { Door.SetActive(closed); Grid.Refresh(new Bounds(Door.transform.position, Door.transform.localScale)); }
        public void SetBarricade(bool on) { Barricade.SetActive(on); Grid.Refresh(new Bounds(Barricade.transform.position, Barricade.transform.localScale)); }

        public void SetVisual(bool robot) { if (visual != null) visual.SetActive(robot); capsule.SetActive(!robot || visual == null); }

        public void SetFrameCap(int fps) { frameCap = fps; QualitySettings.vSyncCount = 0; Application.targetFrameRate = fps <= 0 ? -1 : fps; }

        void DrawPath(Vector3[] points)
        {
            foreach (var m in markers) Destroy(m);
            markers.Clear();
            pathLine.positionCount = points.Length + 1;
            pathLine.SetPosition(0, Robot.transform.position + Vector3.up * .05f);
            for (int i = 0; i < points.Length; i++)
            {
                bool finite = !float.IsNaN(points[i].x) && !float.IsInfinity(points[i].x);
                pathLine.SetPosition(i + 1, finite ? points[i] + Vector3.up * .05f : Robot.transform.position);
                if (!finite) continue;
                var mk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(mk.GetComponent<Collider>());
                mk.transform.position = points[i] + Vector3.up * .03f; mk.transform.localScale = new Vector3(.25f, .02f, .25f);
                mk.GetComponent<Renderer>().sharedMaterial = markerMat; markers.Add(mk);
            }
        }

        // ------------------------------------------------------------------ input
        void Update()
        {
            var k = Keyboard.current; var mouse = Mouse.current;
            if (k != null)
            {
                bool run = k.leftShiftKey.isPressed;
                float speed = run ? runSpeed : walkSpeed;
                if (k.digit1Key.wasPressedThisFrame) Send(ThreePoint, speed, "1  Three-point route");
                if (k.digit2Key.wasPressedThisFrame) Send(CornerCourse, speed, "2  Corner course (90 degree turns around the L wall)");
                if (k.digit3Key.wasPressedThisFrame) PlanTo(new Vector3(3, 0, 9), speed, "3  Through the doorway");
                if (k.digit4Key.wasPressedThisFrame) Send(new[] { new Vector3(0, 0, 0), new Vector3(float.NaN, 0, 2) }, speed, "4  Invalid path (NaN waypoint)");
                if (k.digit5Key.wasPressedThisFrame) Send(System.Array.Empty<Vector3>(), speed, "5  Empty path (NoPath)");
                if (k.digit6Key.wasPressedThisFrame) { Send(ThreePoint, speed, "6  Path replaced mid-route in 1.5 s"); replaceAt = Time.time + 1.5f; }
                if (k.dKey.wasPressedThisFrame) SetDoor(!Door.activeSelf);
                if (k.bKey.wasPressedThisFrame) SetBarricade(!Barricade.activeSelf);
                if (k.tKey.wasPressedThisFrame) SetFrameCap(frameCap == 0 ? 60 : frameCap == 60 ? 30 : 0);
                if (k.vKey.wasPressedThisFrame) SetVisual(capsule.activeSelf);
                if (k.cKey.wasPressedThisFrame) { Motor.Stop(); Presenter.SetCharging(true); Scenario = "C  Charge telegraph (X releases)"; }
                if (k.xKey.wasPressedThisFrame) { Presenter.SetCharging(false); Presenter.PlayRelease(); Scenario = "X  Release"; }
                if (k.hKey.wasPressedThisFrame) Presenter.PlayHurt();
                if (k.kKey.wasPressedThisFrame) { dead = !dead; if (dead) Motor.Stop(); Presenter.SetDead(dead); Scenario = dead ? "K  Defeated (motor stopped)" : "K  Revived"; }
                if (k.rKey.wasPressedThisFrame) ResetRobot();
                if (k.qKey.isPressed) orbit -= 60 * Time.unscaledDeltaTime;
                if (k.eKey.isPressed) orbit += 60 * Time.unscaledDeltaTime;
            }
            if (replaceAt > 0 && Time.time >= replaceAt) { replaceAt = -1; Send(new[] { Robot.transform.position, new Vector3(-6, 0, 9), new Vector3(8, 0, 9) }, walkSpeed, "6  Replaced: new route starts where the robot stands"); }
            if (mouse != null)
            {
                zoom = Mathf.Clamp(zoom - mouse.scroll.ReadValue().y * .01f, 6, 30);
                if (mouse.leftButton.wasPressedThisFrame && !dead && Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 100) && hit.normal.y > .7f)
                    PlanTo(hit.point, k != null && k.leftShiftKey.isPressed ? runSpeed : walkSpeed, "Click");
            }
            if (Motor.CurrentPath != null && Motor.Report == MotorReport.Moving) pathLine.SetPosition(0, Robot.transform.position + Vector3.up * .05f);
        }

        void LateUpdate()
        {
            Vector3 focus = Robot.transform.position * .6f + Vector3.up * 1.2f;
            cam.transform.position = focus + Quaternion.Euler(42, orbit, 0) * Vector3.back * zoom;
            cam.transform.LookAt(focus);
        }

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            var a = Presenter != null && Presenter.animator != null ? Presenter.animator : null;
            string state = "-";
            if (a != null && a.runtimeAnimatorController != null)
            {
                var info = a.GetCurrentAnimatorStateInfo(0);
                foreach (var s in new[] { "Locomotion", "Charge", "Release", "Hurt", "Dead" }) if (info.IsName(s)) state = s;
            }
            GUI.Box(new Rect(10, 10, 470, 220), "");
            GUI.Label(new Rect(20, 16, 455, 210),
                "<b>AGENT MOVEMENT LAB — Student 4</b>\n" +
                "Click floor: A* route (Shift = run)   1 three-point   2 corners   3 doorway\n4 invalid   5 empty   6 replace mid-route   D door   B barricade\n" +
                "C charge   X release   H hurt   K defeat   V robot/capsule   T frame cap   Q/E orbit   R reset\n\n" +
                $"<b>{Scenario}</b>\n" +
                $"Motor {Motor.Report}{(Motor.LastIssue != "" ? " (" + Motor.LastIssue + ")" : "")}   waypoint {Motor.WaypointIndex + 1}/{(Motor.CurrentPath != null ? Motor.CurrentPath.Waypoints.Length : 0)}\n" +
                $"Speed requested {Motor.RequestedSpeed:0.0}  commanded {Motor.CurrentSpeed:0.0}  measured {Motor.PlanarVelocity.magnitude:0.0} m/s   yaw {Motor.YawRate:0}°/s\n" +
                $"Animator {state}   Speed param {Presenter.AnimatedSpeed:0.0}   rate {Presenter.LocomotionRate(Motor.PlanarVelocity.magnitude):0.00}\n" +
                $"Door {(Door.activeSelf ? "CLOSED" : "open")}   barricade {(Barricade.activeSelf ? "IN ROUTE" : "off")}   nav v{Grid.Version}   cap {(frameCap == 0 ? "none" : frameCap + " fps")}   {1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):0} fps",
                style);
        }

        void OnDestroy() { foreach (var m in materials) if (m != null) Destroy(m); SetFrameCap(0); }
    }
}
