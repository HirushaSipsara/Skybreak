using UnityEngine;

namespace Skybreak.Descent
{
    public sealed class DescentGate : MonoBehaviour
    {
        public string encounter;
        public Transform[] leaves;
        public bool closed;
        public void SetClosed(bool value)
        {
            closed = value;
            if (leaves == null) return;
            foreach (Transform leaf in leaves)
            {
                Vector3 p = leaf.localPosition; p.y = value ? 2.15f : 6.2f; leaf.localPosition = p;
                foreach (Collider collider in leaf.GetComponentsInChildren<Collider>()) collider.enabled = value;
            }
        }
    }
}
