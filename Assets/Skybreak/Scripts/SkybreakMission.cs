using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak
{
    public sealed class SkybreakMission : MonoBehaviour
    {
        public ArcController arc;
        public Transform sled;
        public Light[] districtLights;
        public Renderer towerCore;
        public float escortSpeed = 2.1f;
        public float transferSeconds = 12f;

        readonly Vector3[] route = {
            new Vector3(0, 0, 12), new Vector3(6, 0, 12),
            new Vector3(6, 0, 0), new Vector3(0, 0, 0),
            new Vector3(0, 0, -15), new Vector3(0, 0, -28)
        };
        int routeIndex;
        float charge;
        public enum MissionStage { Recover, Escort, Activate, Defend, Complete }
        public MissionStage Stage { get; private set; } = MissionStage.Recover;
        public float TransferProgress => Mathf.Clamp01(charge / transferSeconds);

        void Start()
        {
            if (districtLights == null) return;
            foreach (var lamp in districtLights) if (lamp != null) lamp.enabled = false;
        }

        bool Contested()
        {
            foreach (var target in FindObjectsByType<SecurityTarget>(FindObjectsSortMode.None))
                if (Vector3.Distance(target.transform.position, sled.position) < 4f) return true;
            return false;
        }

        void Update()
        {
            if (arc == null || sled == null || Keyboard.current == null) return;
            Vector3 player = arc.transform.position;
            bool press = Keyboard.current.fKey.wasPressedThisFrame;
            if (Stage == MissionStage.Recover && press && Vector3.Distance(player, sled.position) < 3.5f)
            {
                Stage = MissionStage.Escort;
                return;
            }
            if (Stage == MissionStage.Escort)
            {
                if (Vector3.Distance(player, sled.position) > 13f || Contested()) return;
                Vector3 destination = route[routeIndex];
                sled.position = Vector3.MoveTowards(sled.position, destination, escortSpeed * Time.deltaTime);
                if (Vector3.Distance(sled.position, destination) < .08f)
                {
                    routeIndex++;
                    if (routeIndex >= route.Length) Stage = MissionStage.Activate;
                }
            }
            if (Stage == MissionStage.Activate && press && Vector3.Distance(player, new Vector3(0, 0, -30)) < 3.8f)
                Stage = MissionStage.Defend;
            if (Stage == MissionStage.Defend)
            {
                if (!Contested()) charge += Time.deltaTime;
                if (charge >= transferSeconds)
                {
                    Stage = MissionStage.Complete;
                    if (districtLights != null)
                        foreach (var lamp in districtLights) if (lamp != null) lamp.enabled = true;
                    if (towerCore != null) towerCore.material.color = new Color(.6f, 1f, .83f);
                }
            }
        }

        void OnGUI()
        {
            if (arc == null || sled == null) return;
            string objective = Stage switch
            {
                MissionStage.Recover => "01 / RECOVER THE POWER CORE",
                MissionStage.Escort => "02 / ESCORT THROUGH THE MARKET",
                MissionStage.Activate => "03 / PRESS F AT THE TOWER SOCKET",
                MissionStage.Defend => "03 / PROTECT THE POWER TRANSFER",
                _ => "CITY ONLINE / MISSION COMPLETE"
            };
            string hint = Stage switch
            {
                MissionStage.Recover => "Approach the orange core sled and press F.",
                MissionStage.Escort => Contested() ? "Security robot close to the sled. Clear it!" : "Stay within 13 m of the sled to keep it moving.",
                MissionStage.Activate => "Stand near the tower and press F.",
                MissionStage.Defend => "Transfer " + Mathf.RoundToInt(TransferProgress * 100) + "% — clear nearby robots.",
                _ => "You and Relay brought power back to the district."
            };
            GUI.Box(new Rect(18, 18, 355, 72), "SKYBREAK : LAST LIGHT\n" + objective + "\n" + hint);
            if (Stage == MissionStage.Recover && Vector3.Distance(arc.transform.position, sled.position) < 3.5f)
                GUI.Box(new Rect(Screen.width / 2f - 125, Screen.height - 125, 250, 35), "F  /  RELEASE THE CORE");
            if (Stage == MissionStage.Activate && Vector3.Distance(arc.transform.position, new Vector3(0, 0, -30)) < 3.8f)
                GUI.Box(new Rect(Screen.width / 2f - 125, Screen.height - 125, 250, 35), "F  /  ACTIVATE THE TOWER");
        }
    }
}