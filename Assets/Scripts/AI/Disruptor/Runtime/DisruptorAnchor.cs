using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    /// <summary>
    /// A hand-placed tactical position ("rift anchor") the Disruptor may strike from or escape to.
    /// Level designers put these at corners, pillars and room edges. They are optional: without them
    /// the Disruptor samples candidate positions in rings around the player and itself.
    /// </summary>
    public sealed class DisruptorAnchor : MonoBehaviour
    {
        public static readonly List<DisruptorAnchor> All = new List<DisruptorAnchor>();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(.75f, .3f, 1f, .8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .1f, .35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.2f);
        }
    }
}
