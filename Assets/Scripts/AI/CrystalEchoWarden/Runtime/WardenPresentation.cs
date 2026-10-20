using UnityEngine;

namespace Skybreak.Warden
{
    public sealed class WardenPresentation : MonoBehaviour
    {
        public WardenBrain brain;
        public Transform visual;
        public Renderer core;
        public LineRenderer beam;
        MaterialPropertyBlock properties;
        float beamOff;
        void Start()
        {
            properties=new MaterialPropertyBlock();
            if(brain!=null)brain.Fired+=Shot;
        }
        void Shot(Vector3 from,Vector3 to)
        {
            if(beam==null)return;
            beam.SetPosition(0,from);beam.SetPosition(1,to);beam.enabled=true;beamOff=Time.time+.12f;
        }
        void Update()
        {
            if(brain==null)return;
            // Skeletal clips own the pose; presentation handles only light and pulse effects.
            if(core!=null)
            {
                Color color=!brain.Alive?Color.gray:brain.WindingUp?new Color(1,.35f,.03f):new Color(.02f,.8f,1);
                core.GetPropertyBlock(properties);properties.SetColor("_BaseColor",color);properties.SetColor("_EmissionColor",color*(brain.WindingUp?3:.8f));core.SetPropertyBlock(properties);
            }
            if(beam!=null&&Time.time>=beamOff)beam.enabled=false;
        }
        void OnDestroy(){if(brain!=null)brain.Fired-=Shot;}
    }
}
