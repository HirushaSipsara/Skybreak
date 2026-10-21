using System.Collections.Generic;
using UnityEngine;
using Skybreak.Descent;
using Skybreak.LocalCombatLab;

namespace Skybreak.Player
{
    [DefaultExecutionOrder(250)]
    public sealed class ArcImportedPresentation : MonoBehaviour
    {
        public Transform modelRoot, carbine, rightGrip, leftGrip, magazine;
        DescentCharacterController arc;
        LocalCombatDriver combat;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        Renderer[] oldBody, newBody, gunBody;
        Vector3 previous, magazineRest;
        float phase, pace;

        void Start()
        {
            arc = GetComponent<DescentCharacterController>();
            combat = GetComponent<LocalCombatDriver>();
            previous = transform.position;
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>())
            {
                bones[t.name] = t;
                rest[t] = t.localRotation;
            }
            newBody = modelRoot.GetComponentsInChildren<Renderer>();
            gunBody = carbine.GetComponentsInChildren<Renderer>();
            var originals = new List<Renderer>();
            foreach (var r in arc.visual.GetComponentsInChildren<Renderer>())
                if (!r.transform.IsChildOf(modelRoot) && !r.transform.IsChildOf(carbine)) originals.Add(r);
            oldBody = originals.ToArray();
            if (magazine != null) magazineRest = magazine.localPosition;
        }

        void LateUpdate()
        {
            if (arc == null || modelRoot == null) return;
            foreach (var r in oldBody) if (r != null) r.enabled = false;
            bool firstPerson=combat!=null&&combat.IsFirstPerson;
            bool visible = !firstPerson&&(arc.viewCamera == null || Vector3.Distance(arc.viewCamera.transform.position, transform.position + Vector3.up * 1.7f) > 1.1f);
            foreach (var r in newBody) r.enabled = visible;
            foreach (var r in gunBody) r.enabled = firstPerson||visible;
            foreach (var pair in rest) pair.Key.localRotation = pair.Value;
            Vector3 movement = transform.position - previous; previous = transform.position; movement.y = 0;
            float speed = Time.deltaTime > 0 && movement.magnitude < 2 ? movement.magnitude / Time.deltaTime : 0;
            pace = Mathf.Lerp(pace, Mathf.Clamp01(speed / 4.7f), 1 - Mathf.Exp(-12 * Time.deltaTime));
            phase += Time.deltaTime * (speed > 6 ? 13 : 9) * pace;
            float swing = Mathf.Sin(phase) * 28 * pace;
            RotateWorld("Thigh.L", swing); RotateWorld("Thigh.R", -swing);
            RotateWorld("Shin.L", Mathf.Max(0, -swing) * .6f);
            RotateWorld("Shin.R", Mathf.Max(0, swing) * .6f);
            if (combat != null && combat.Feedback != null && combat.Feedback.Weapon != null)
            {
                var carrier = combat.Feedback.Weapon.transform;
                if (carbine.parent != carrier)
                {
                    carbine.SetParent(carrier, false);
                    carbine.localPosition = Vector3.zero;
                    carbine.localRotation = Quaternion.identity;
                    carbine.localScale = Vector3.one;
                }
                foreach (var r in carrier.GetComponentsInChildren<Renderer>())
                    if (!r.transform.IsChildOf(carbine)) r.enabled = false;
                SolveArm("L", leftGrip.position, -transform.right - Vector3.up);
                SolveArm("R", rightGrip.position, transform.right - Vector3.up);
                var direction = combat.AimPoint - bones["Head"].position;
                float pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                RotateWorld("Head", -Mathf.Clamp(pitch, -45, 45) * .55f);
                if (magazine != null && combat.Magazine != null)
                {
                    float motion = combat.Magazine.IsReloading ? Mathf.Sin(combat.Magazine.ReloadProgress * Mathf.PI) : 0;
                    magazine.localPosition = magazineRest + Vector3.down * (.26f * motion);
                    magazine.localRotation = Quaternion.Euler(-10 * motion, 0, -20 * motion);
                }
            }
        }
        void RotateWorld(string name, float angle)
        {
            if (bones.TryGetValue(name, out var bone)) bone.rotation = Quaternion.AngleAxis(angle, transform.right) * bone.rotation;
        }
        void SolveArm(string side, Vector3 grip, Vector3 hint)
        {
            var upper = bones["UpperArm." + side];
            var lower = bones["Forearm." + side];
            var hand = bones["Hand." + side];
            Vector3 target = grip - carbine.forward * (.065f * arc.visual.lossyScale.y);
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
            Vector3 delta = target - upper.position;
            if (delta.sqrMagnitude < .0001f) return;
            Vector3 direction = delta.normalized;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            float along = (a * a + distance * distance - b * b) / (2 * distance);
            var bend = Vector3.ProjectOnPlane(hint, direction).normalized;
            var elbow = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            hand.rotation = Quaternion.FromToRotation(hand.up, carbine.forward) * hand.rotation;
        }
    }
}
