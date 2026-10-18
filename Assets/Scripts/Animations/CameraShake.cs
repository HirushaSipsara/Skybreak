using UnityEngine;

namespace Skybreak.Presentation
{
    // Trauma-based camera shake layered on top of whatever moved the camera this frame.
    // Runs after the player's camera rig and removes its own offset if nothing else moves the camera,
    // so offsets never accumulate. Uses unscaled time, so it keeps working during hit-stop and slow motion.
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        public float maxOffset = .3f, maxAngle = 2.6f, frequency = 22f, recovery = 1.6f;
        public float Trauma { get; private set; }

        Vector3 basePosition, shakenPosition; Quaternion baseRotation, shakenRotation; bool shaken;
        float seed;

        public static CameraShake ForMainCamera()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var s = cam.GetComponent<CameraShake>();
            return s != null ? s : cam.gameObject.AddComponent<CameraShake>();
        }

        /// <summary>Adds trauma (0..1), reduced with distance from the camera to the source.</summary>
        public void Add(float amount, Vector3 source, float falloffDistance = 30f)
        {
            float d = Vector3.Distance(transform.position, source);
            float k = 1 - Mathf.Clamp01(d / Mathf.Max(1, falloffDistance));
            Trauma = Mathf.Clamp01(Trauma + amount * k * k);
        }

        void Awake() { seed = Random.value * 100; }

        void LateUpdate()
        {
            // If nothing repositioned the camera since our last shake, undo our own offset first.
            if (shaken && transform.position == shakenPosition && transform.rotation == shakenRotation)
                transform.SetPositionAndRotation(basePosition, baseRotation);
            shaken = false;
            if (Trauma <= 0) return;
            float t = Time.unscaledTime * frequency, s = Trauma * Trauma;
            Vector3 offset = new Vector3(Noise(t, 0), Noise(t, 1), Noise(t, 2) * .4f) * maxOffset * s;
            Vector3 angles = new Vector3(Noise(t, 3), Noise(t, 4), Noise(t, 5)) * maxAngle * s;
            basePosition = transform.position; baseRotation = transform.rotation;
            transform.SetPositionAndRotation(basePosition + baseRotation * offset, baseRotation * Quaternion.Euler(angles));
            shakenPosition = transform.position; shakenRotation = transform.rotation; shaken = true;
            Trauma = Mathf.Max(0, Trauma - recovery * Time.unscaledDeltaTime);
        }

        float Noise(float t, int channel) => Mathf.PerlinNoise(seed + channel * 31.7f, t) * 2 - 1;

        void OnDisable()
        {
            if (shaken && transform.position == shakenPosition) transform.SetPositionAndRotation(basePosition, baseRotation);
            shaken = false; Trauma = 0;
        }
    }
}
