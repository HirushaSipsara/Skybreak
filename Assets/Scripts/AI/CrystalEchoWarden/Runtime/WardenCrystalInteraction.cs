using UnityEngine;
using Skybreak.Combat;

namespace Skybreak.Warden
{
    public sealed class WardenCrystalInteraction : MonoBehaviour, IInteractable
    {
        public WardenObjective objective;
        public string Prompt=>"[F] Disturb guarded crystal";
        public bool CanInteract(GameObject actor)
        {
            var health=actor!=null?actor.GetComponent<DamageSink>():null;
            return objective!=null&&objective.Available&&health!=null&&health.Team==Allegiance.Player;
        }
        public void Interact(GameObject actor){if(CanInteract(actor))objective.Disturb(1);}
    }
}
