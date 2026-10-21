using System.Text;
using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    /// <summary>
    /// Visual debugging view for the Crystal Echo Warden.
    /// Visualizes labels, paths, probability zones, and current action.
    /// Fulfills "WardenDebugView: labels, paths, probabilities, current action" from the architecture specification.
    /// </summary>
    [ExecuteAlways]
    public sealed class WardenDebugView : MonoBehaviour
    {
        public WardenBrain brain;
        public WardenPerception perception;
        public WardenWorldModel worldModel;
        public WardenDecisionMaker decisionMaker;
        public WardenMovement movement;
        public WardenMap map;

        public bool showOverlay = true;
        public bool showPaths = true;
        public bool showProbabilityZones = true;
        public bool showWorldLabels = true;

        GUIStyle headerStyle, bodyStyle, boxStyle;

        void Awake()
        {
            BindComponents();
        }

        void OnEnable()
        {
            BindComponents();
        }

        void BindComponents()
        {
            if (brain == null) brain = GetComponent<WardenBrain>();
            if (perception == null) perception = GetComponent<WardenPerception>();
            if (worldModel == null) worldModel = GetComponent<WardenWorldModel>();
            if (decisionMaker == null) decisionMaker = GetComponent<WardenDecisionMaker>();
            if (movement == null) movement = GetComponent<WardenMovement>();
            if (map == null && brain != null) map = brain.map;
        }

        void OnGUI()
        {
            if (!showOverlay || !Application.isPlaying || brain == null) return;

            InitStyles();

            int id = GetEntityId().GetHashCode();
            int offsetIndex = Mathf.Abs(id % 4);
            float xPos = Screen.width - 360;
            float yPos = 12 + offsetIndex * 190;
            Rect panelRect = new Rect(xPos, yPos, 348, 180);

            GUI.Box(panelRect, "", boxStyle);

            var sb = new StringBuilder();
            sb.AppendLine($"<b>WARDEN:</b> {brain.name} ({(brain.Alive ? "ACTIVE" : "DEFEATED")})");
            sb.AppendLine($"<b>ACTION:</b> <color=#00ffff>{brain.Mind.Current}</color> | <b>HEALTH:</b> {brain.Health:0}/{brain.MaximumHealth:0}");
            sb.AppendLine($"<b>REASON:</b> {brain.Mind.Reason}");

            double confidence = worldModel != null ? worldModel.Confidence : (perception?.Belief?.Confidence(Time.time) ?? 0);
            int mostLikely = worldModel != null ? worldModel.MostLikelyZone : (perception?.Belief?.MostLikely ?? -1);
            sb.AppendLine($"<b>CONFIDENCE:</b> {confidence:P0} | <b>LIKELY ZONE:</b> {mostLikely}");
            sb.AppendLine($"<b>EVIDENCE:</b> {perception?.Evidence ?? "None"}");

            if (brain.objective != null)
            {
                sb.AppendLine($"<b>PEDESTAL:</b> {brain.objective.integrity:0}% | <b>DANGER:</b> {brain.objective.Danger:P0}");
            }

            if (movement != null && movement.Path.Length > 0)
            {
                sb.AppendLine($"<b>PATH:</b> Node {brain.Goal} ({movement.Path.Length} wps) | <b>STATUS:</b> {movement.LastResult}");
            }

            GUI.Label(new Rect(panelRect.x + 10, panelRect.y + 8, panelRect.width - 20, panelRect.height - 16), sb.ToString(), bodyStyle);

            if (showWorldLabels && Camera.main != null && brain.Alive)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
                if (screenPos.z > 0 && screenPos.z < 25f)
                {
                    string tag = $"[{brain.Mind.Current}] ({confidence:P0})";
                    GUI.Label(new Rect(screenPos.x - 70, Screen.height - screenPos.y - 20, 140, 24), tag, headerStyle);
                }
            }
        }

        void OnDrawGizmos()
        {
            if (map == null || map.nodes == null || map.nodes.Length == 0) return;

            // 1. Probabilities: draw color-coded belief spheres for each zone
            if (showProbabilityZones)
            {
                var belief = worldModel?.Belief ?? perception?.Belief;
                for (int i = 0; i < map.nodes.Length; i++)
                {
                    float p = (belief != null && i < belief.Count) ? (float)belief[i] : 0f;
                    Gizmos.color = Color.Lerp(new Color(0f, 0.2f, 0.35f, 0.25f), new Color(1f, 0.7f, 0.05f, 0.9f), p);
                    float radius = 0.10f + p * 0.75f;
                    Gizmos.DrawSphere(map.nodes[i], radius);
                }
            }

            // 2. Paths: draw active A* path
            if (showPaths && movement != null && movement.Path != null && movement.Path.Length > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 1; i < movement.Path.Length; i++)
                {
                    int a = movement.Path[i - 1];
                    int b = movement.Path[i];
                    if (map.Graph != null && map.Graph.Valid(a) && map.Graph.Valid(b))
                    {
                        Gizmos.DrawLine(map.nodes[a], map.nodes[b]);
                        Gizmos.DrawWireSphere(map.nodes[b], 0.18f);
                    }
                }
            }

            // 3. Current Action & Goal target
            if (brain != null && brain.Goal >= 0 && map.Graph != null && map.Graph.Valid(brain.Goal))
            {
                Vector3 goalPos = map.nodes[brain.Goal];
                Gizmos.color = brain.Mind.Current switch
                {
                    Intent.Attack => Color.red,
                    Intent.Intercept => new Color(1f, 0.4f, 0f),
                    Intent.Flank => Color.green,
                    Intent.Retreat => new Color(0.8f, 0.2f, 0.9f),
                    Intent.Investigate => Color.yellow,
                    _ => Color.cyan
                };
                Gizmos.DrawWireCube(goalPos, Vector3.one * 0.45f);
                Gizmos.DrawLine(transform.position, goalPos);
            }
        }

        void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                headerStyle.normal.textColor = Color.yellow;
            }

            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    richText = true,
                    wordWrap = true
                };
                bodyStyle.normal.textColor = Color.white;
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
            }
        }
    }
}
