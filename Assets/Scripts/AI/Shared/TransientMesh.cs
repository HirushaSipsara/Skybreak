using UnityEngine;
namespace Skybreak.AI
{
    public sealed class TransientMesh : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
