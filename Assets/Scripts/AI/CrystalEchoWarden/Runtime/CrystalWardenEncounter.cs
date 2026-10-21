using UnityEngine;
using Skybreak.Combat;
using Skybreak.Descent;
using Skybreak.LocalCombatLab;
using UnityEngine.InputSystem;

namespace Skybreak.Warden
{
    // Scene wiring stays outside Sachini's independent decision and search algorithms.
    public sealed class CrystalWardenEncounter : MonoBehaviour
    {
        public DescentCharacterController player;
        public WardenMap map;
        public WardenNoiseBus noise;
        public WardenBrain[] wardens;
        public WardenObjective[] objectives;
        Vector3[] spawn;
        Quaternion[] rotations;
        float decoyReady;
        void Start()
        {
            var health=player.GetComponent<DamageSink>();
            if(health==null){Debug.LogError("Crystal Wardens need the player's damage adapter.",this);enabled=false;return;}
            var driver=player.GetComponent<LocalCombatDriver>();
            if(driver!=null)driver.noiseBus=noise;
            var footsteps=player.GetComponent<WardenFootsteps>();
            if(footsteps==null)footsteps=player.gameObject.AddComponent<WardenFootsteps>();
            footsteps.bus=noise;
            spawn=new Vector3[wardens.Length];rotations=new Quaternion[wardens.Length];
            for(int i=0;i<wardens.Length;i++)
            {
                spawn[i]=wardens[i].transform.position;rotations[i]=wardens[i].transform.rotation;
                var senses=wardens[i].GetComponent<WardenPerception>();
                senses.target=health;senses.map=map;senses.noise=noise;
            }
        }
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.qKey.wasPressedThisFrame&&Cursor.lockState==CursorLockMode.Locked)
            {
                var camera=Camera.main;
                if(camera!=null)TryDecoy(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)));
            }
        }
        public bool TryDecoy(Ray aim)
        {
            if(!Skybreak.Player.MissionFlowController.InputAllowed)return false;
            var health=player.GetComponent<DamageSink>();
            if(health==null||!health.Alive||Time.time<decoyReady)return false;
            decoyReady=Time.time+3;
            Vector3 destination=aim.GetPoint(12);
            if(Physics.Raycast(aim,out var hit,12,map.environmentMask,QueryTriggerInteraction.Ignore))
                destination=hit.point-aim.direction*.1f;
            noise.Emit(destination,22,WardenNoiseKind.Decoy);
            return true;
        }
        public void ResetEncounter()
        {
            if(spawn==null)return;
            decoyReady=0;noise.Clear();foreach(var objective in objectives)objective.ResetState();
            for(int i=0;i<wardens.Length;i++)
            {
                var controller=wardens[i].GetComponent<CharacterController>();controller.enabled=false;
                wardens[i].transform.SetPositionAndRotation(spawn[i],rotations[i]);
                wardens[i].ResetForEncounter();
            }
            Physics.SyncTransforms();map.Refresh();
        }
    }
}
