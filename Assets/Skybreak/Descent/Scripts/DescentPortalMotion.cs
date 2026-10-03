using UnityEngine;
namespace Skybreak.Descent
{
    public sealed class DescentPortalMotion : MonoBehaviour
    {
        public Transform innerRing;
        public Transform[] crystals;
        void Update()
        {
            if (innerRing != null) innerRing.Rotate(0,0,18 * Time.deltaTime,Space.Self);
            if (crystals != null) foreach (var crystal in crystals) if (crystal != null) crystal.Rotate(0,22 * Time.deltaTime,0,Space.Self);
        }
    }
}
