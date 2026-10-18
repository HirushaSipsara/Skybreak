using System;
using UnityEngine;

namespace Skybreak.AI
{
    // Editor: polls only this known file once per second for workbench changes.
    // Build: reads the packaged Resources TextAsset once, with no web server required.
    public static class AgentTuning
    {
        static AgentTuningData current;
        static float checkAt;
#if UNITY_EDITOR
        static DateTime lastWrite;
#endif
        public static int Revision { get; private set; }
        public static string Status { get; private set; } = "Defaults";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { current = null; checkAt = 0; Revision = 0;
#if UNITY_EDITOR
            lastWrite = default;
#endif
        }
        public static AgentTuningData Current
        {
            get
            {
                if (current == null) { current = new AgentTuningData(); Reload(); }
#if UNITY_EDITOR
                if (Time.realtimeSinceStartup >= checkAt) {
                    checkAt = Time.realtimeSinceStartup + 1;
                    var path = System.IO.Path.Combine(Application.dataPath, "Resources/AI/AgentTuning.json");
                    if (System.IO.File.Exists(path) && System.IO.File.GetLastWriteTimeUtc(path) != lastWrite) Reload();
                }
#endif
                return current;
            }
        }
        public static bool Reload()
        {
            try {
                string json;
#if UNITY_EDITOR
                var path = System.IO.Path.Combine(Application.dataPath, "Resources/AI/AgentTuning.json");
                json = System.IO.File.ReadAllText(path);
                lastWrite = System.IO.File.GetLastWriteTimeUtc(path);
#else
                var asset = Resources.Load<TextAsset>("AI/AgentTuning");
                if (asset == null) throw new Exception("AgentTuning resource missing");
                json = asset.text;
#endif
                var value = JsonUtility.FromJson<AgentTuningData>(json);
                if (value == null || !value.Valid()) throw new Exception("Unsupported schema or out-of-range tuning values");
                current = value; Revision++; Status = "Shared configuration loaded"; return true;
            }
            catch (Exception e) { if(current == null)current=new AgentTuningData(); Status="Keeping last valid tuning: "+e.Message; Debug.LogWarning(Status); return false; }
        }
    }
}
