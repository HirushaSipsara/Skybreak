using Skybreak.Combat;
using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    public enum RiftPart { LeftArm, RightArm, Core }
    public sealed class RiftDamagePart : DamageSink
    {
        public RiftWardenBrain boss;
        public RiftPart part;
        public override Allegiance Team => Allegiance.Hostile;
        public override float Health => boss != null ? boss.PartHealth(part) : 0;
        public override float MaximumHealth => part == RiftPart.Core ? boss.coreMaximum : boss.armMaximum;
        public override Vector3 AimPoint => GetComponent<Collider>().bounds.center;
        public bool Vulnerable => boss != null && boss.CanDamage(part);
        public override void ReceiveDamage(float amount, Transform source) { if (boss != null) boss.DamagePart(part, amount); }
    }
}
