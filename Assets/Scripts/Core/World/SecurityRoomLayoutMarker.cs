using UnityEngine;

namespace Skybreak.Handbuilt
{
    // Authoring guide only. It has no gameplay trigger, door collider or AI logic.
    public sealed class SecurityRoomLayoutMarker : MonoBehaviour
    {
        public string description;
        public Vector3 footprint = new Vector3(8, .02f, 8);
        public Vector3 centreOffset;
        public Color guideColor = new Color(1, .55f, .2f);

        void OnDrawGizmos()
        {
            var old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = guideColor;
            Gizmos.DrawWireCube(centreOffset, footprint);
            Gizmos.matrix = old;
#if UNITY_EDITOR
            UnityEditor.Handles.color = guideColor;
            UnityEditor.Handles.Label(transform.TransformPoint(centreOffset + Vector3.up * .2f), description);
#endif
        }
    }
}
