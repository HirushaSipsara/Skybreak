using UnityEngine;
using UnityEngine.Rendering;

namespace Skybreak.AI
{
    // Reusable unlit cues; no real-time lights or per-shot object/material allocation.
    [DefaultExecutionOrder(200)]
    public sealed class CombatSignals : MonoBehaviour
    {
        public LineRenderer line;
        public LineRenderer ring;
        public Transform FollowMuzzle { get; set; }
        public bool AnchorShotOrigin { get; set; }
        Material beamMaterial, ringMaterial;
        Transform muzzle, impact;
        float pulseEnd, pulseDuration;
        bool pulsing;
        Material CueMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.white); return material;
        }
        void EnsureLine()
        {
            if (line == null)
            {
                var go = new GameObject("Shot tracer"); go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>(); line.positionCount = 2; line.useWorldSpace = true;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            }
            if (beamMaterial == null) { beamMaterial = CueMaterial(); line.sharedMaterial = beamMaterial; }
        }
        Transform Flash(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(transform, false); go.layer = 8;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = beamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            go.SetActive(false); return go.transform;
        }
        public void Beam(Vector3 from, Vector3 to, Color colour)
        {
            EnsureLine(); beamMaterial.SetColor("_BaseColor", colour);
            FollowMuzzle = null;
            pulsing = false; if (muzzle != null) muzzle.gameObject.SetActive(false); if (impact != null) impact.gameObject.SetActive(false);
            line.widthMultiplier = .045f; line.enabled = true; line.startColor = line.endColor = colour;
            line.SetPosition(0, from); line.SetPosition(1, to);
        }
        public void Shot(Vector3 from, Vector3 to, Color colour, float duration = .13f)
        {
            Beam(from, to, colour);
            if (muzzle == null) { muzzle = Flash("Muzzle pulse"); impact = Flash("Impact pulse"); }
            muzzle.position = from; impact.position = to; muzzle.gameObject.SetActive(true); impact.gameObject.SetActive(true);
            muzzle.localScale = Vector3.one * .18f; impact.localScale = Vector3.one * .13f;
            line.widthMultiplier = .085f; pulseDuration = Mathf.Max(.02f, duration); pulseEnd = Time.time + pulseDuration; pulsing = true;
        }
        public void Circle(Vector3 centre, float radius, Color colour)
        {
            if (ringMaterial == null) { ringMaterial = CueMaterial(); ring.sharedMaterial = ringMaterial; }
            ringMaterial.SetColor("_BaseColor", colour); ring.enabled = true; ring.startColor = ring.endColor = colour;
            for (int i = 0; i <= 48; i++) { float a = i * Mathf.PI * 2 / 48; ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius, .06f, Mathf.Sin(a) * radius)); }
        }
        void Update()
        {
            if (!pulsing) return;
            float remaining = Mathf.Clamp01((pulseEnd - Time.time) / pulseDuration);
            if (remaining <= 0) { Clear(); return; }
            line.widthMultiplier = Mathf.Lerp(.02f, .085f, remaining);
            muzzle.localScale = Vector3.one * Mathf.Lerp(.035f, .18f, remaining);
            impact.localScale = Vector3.one * Mathf.Lerp(.03f, .13f, remaining);
        }
        void LateUpdate()
        {
            if (!pulsing || FollowMuzzle == null) return;
            if(!AnchorShotOrigin)line.SetPosition(0,FollowMuzzle.position);
            muzzle.position=FollowMuzzle.position;
        }
        public void Clear()
        {
            pulsing = false; if (line != null) line.enabled = false; if (ring != null) ring.enabled = false;
            if (muzzle != null) muzzle.gameObject.SetActive(false); if (impact != null) impact.gameObject.SetActive(false);
        }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (beamMaterial != null) Destroy(beamMaterial); if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
