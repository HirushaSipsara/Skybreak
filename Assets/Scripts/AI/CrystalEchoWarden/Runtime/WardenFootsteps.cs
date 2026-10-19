using UnityEngine;

namespace Skybreak.Warden
{
    // Optional adapter for the existing player: measured motion, not keyboard guesses.
    public sealed class WardenFootsteps : MonoBehaviour
    {
        public WardenNoiseBus bus;
        public float walkLoudness=9,sprintLoudness=18,sprintThreshold=6;
        CharacterController body;
        Vector3 previous;
        float nextStep;
        void OnEnable(){previous=transform.position;body=GetComponent<CharacterController>();}
        void Update()
        {
            Vector3 delta=transform.position-previous;previous=transform.position;delta.y=0;
            if(Time.deltaTime<=0||delta.magnitude>2)return; // Respawn/teleport is not a footstep.
            float speed=delta.magnitude/Time.deltaTime;
            if(bus!=null&&speed>.6f&&(body==null||body.isGrounded)&&Time.time>=nextStep)
            {
                bus.Emit(transform.position,speed>sprintThreshold?sprintLoudness:walkLoudness,WardenNoiseKind.Footstep);
                nextStep=Time.time+(speed>sprintThreshold?.3f:.5f);
            }
        }
    }
}
