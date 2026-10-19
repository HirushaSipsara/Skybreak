using UnityEngine;

namespace Skybreak.Warden
{
    public sealed class WardenObjective : MonoBehaviour
    {
        public float integrity=100;
        public WardenNoiseBus noise;
        public Transform crystalVisual;
        float alarmAt=-100;
        public bool Available=>isActiveAndEnabled&&integrity>0;
        public float Danger=>Available?Mathf.Clamp01(1-(Time.time-alarmAt)/8):0;
        public void ResetState()
        {
            integrity=100;alarmAt=-100;
            if(crystalVisual!=null)crystalVisual.gameObject.SetActive(true);
        }
        public void Disturb(float damage)
        {
            if(!Available||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage))return;
            integrity=Mathf.Max(0,integrity-damage);alarmAt=Time.time;
            if(noise!=null)noise.Emit(transform.position,24,WardenNoiseKind.Pedestal);
            if(crystalVisual!=null)crystalVisual.gameObject.SetActive(Available);
        }
    }
}
