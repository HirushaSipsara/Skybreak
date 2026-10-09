using UnityEngine;

namespace Skybreak.World
{
    /// <summary>Visual power/proximity response. Does not teleport Arc or complete an objective.</summary>
    [DisallowMultipleComponent]
    public sealed class FinalTeleportVisual : MonoBehaviour
    {
        [SerializeField] Renderer energyField;
        [SerializeField] Renderer crystal;
        [SerializeField] Transform player;
        [SerializeField] bool powered = true;
        [SerializeField, Min(1)] float approachDistance = 12;
        [SerializeField, Min(.1f)] float fullPowerDistance = 4;
        [SerializeField] Color crystalEmission = new Color(.12f, .75f, 1) * .6f;

        static readonly int EnergyId = Shader.PropertyToID("_Energy");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock fieldBlock;
        MaterialPropertyBlock crystalBlock;
        float approach;

        public bool IsPowered => powered;
        public float ApproachStrength => approach;

        void Awake()
        {
            fieldBlock = new MaterialPropertyBlock();
            crystalBlock = new MaterialPropertyBlock();
            if (player == null)
            {
                var arc = FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>();
                if (arc != null) player = arc.transform;
            }
            ApplyVisual(0, Time.time);
        }

        void Update()
        {
            float target = 0;
            if (powered && player != null)
            {
                var delta = player.position - transform.position;
                delta.y = 0;
                target = 1 - Mathf.InverseLerp(fullPowerDistance,
                    Mathf.Max(fullPowerDistance + .1f, approachDistance), delta.magnitude);
            }
            approach = Mathf.MoveTowards(approach, target, Time.deltaTime * .65f);
            ApplyVisual(approach, Time.time);
        }

        /// <summary>Call from encounter/quest logic when the reactor return point becomes available.</summary>
        public void SetPowered(bool value)
        {
            powered = value;
            ApplyVisual(approach, Time.time);
        }

        void ApplyVisual(float proximity, float time)
        {
            if (fieldBlock == null) fieldBlock = new MaterialPropertyBlock();
            if (crystalBlock == null) crystalBlock = new MaterialPropertyBlock();
            if (energyField != null)
            {
                energyField.enabled = powered;
                energyField.GetPropertyBlock(fieldBlock);
                fieldBlock.SetFloat(EnergyId, powered ? Mathf.Lerp(.6f, 1.15f, proximity) : 0);
                energyField.SetPropertyBlock(fieldBlock);
            }
            if (crystal != null)
            {
                float pulse = .94f + .12f * Mathf.Sin(time * 1.8f);
                float strength = powered ? Mathf.Lerp(.92f, 1.2f, proximity) * pulse : .18f;
                crystal.GetPropertyBlock(crystalBlock);
                crystalBlock.SetColor(EmissionId, crystalEmission * strength);
                crystal.SetPropertyBlock(crystalBlock);
            }
        }

        void OnDisable()
        {
            // Avoid leaving transient runtime overrides in the Editor when Play Mode ends.
            if (energyField != null) { energyField.SetPropertyBlock(null); energyField.enabled = powered; }
            if (crystal != null) crystal.SetPropertyBlock(null);
        }
    }
}
