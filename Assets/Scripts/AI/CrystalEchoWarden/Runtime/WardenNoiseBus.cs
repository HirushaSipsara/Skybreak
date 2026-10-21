using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.Warden
{
    public enum WardenNoiseKind { Footstep, Gunshot, Decoy, Pedestal }
    public sealed class WardenNoiseBus : MonoBehaviour
    {
        public readonly struct Noise
        {
            public readonly long Id;
            public readonly Vector3 Origin;
            public readonly float Loudness,Time;
            public readonly WardenNoiseKind Kind;
            public Noise(long id,Vector3 p,float volume,WardenNoiseKind kind){Id=id;Origin=p;Loudness=volume;Time=UnityEngine.Time.time;Kind=kind;}
        }
        readonly List<Noise> events=new List<Noise>(128);
        public IReadOnlyList<Noise> Events=>events;
        public long LatestId {get;private set;}
        public void Emit(Vector3 source,float loudness,WardenNoiseKind kind)
        {
            if(loudness<=0||float.IsNaN(loudness)||float.IsInfinity(loudness))return;
            if(events.Count==128)events.RemoveAt(0);
            events.Add(new Noise(++LatestId,source,loudness,kind));
        }
        public void Clear(){events.Clear();}
        void OnDisable(){events.Clear();}
    }
}
