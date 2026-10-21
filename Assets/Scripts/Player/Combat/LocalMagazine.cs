using UnityEngine;

namespace Skybreak.LocalCombatLab
{
    public sealed class LocalMagazine : MonoBehaviour
    {
        public int capacity = 20, startingReserve = 80;
        public float reloadDuration = 2.2f, shotInterval = .22f;
        public int Rounds { get; private set; } = 20;
        public int Reserve { get; private set; } = 80;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.Clamp01((Time.time-reloadStarted)/reloadDuration) : 0;
        public int ReloadsCompleted { get; private set; }
        float nextShot, reloadStarted, reloadEnds;
        public bool TryFire()
        {
            Advance();
            if (IsReloading || Time.time < nextShot || Rounds <= 0) return false;
            Rounds--; nextShot = Time.time + shotInterval; return true;
        }
        public bool BeginReload()
        {
            if (IsReloading || Rounds >= capacity || Reserve <= 0) return false;
            IsReloading = true; reloadStarted = Time.time; reloadEnds = Time.time + reloadDuration; return true;
        }
        void Update() { Advance(); }
        public void Advance()
        {
            if (!IsReloading || Time.time < reloadEnds) return;
            int transfer = Mathf.Min(capacity-Rounds, Reserve);
            Rounds += transfer; Reserve -= transfer; IsReloading = false; ReloadsCompleted++;
        }
        public void ResetMagazine()
        {
            Rounds=capacity;Reserve=startingReserve;IsReloading=false;nextShot=0;reloadStarted=reloadEnds=0;
        }
    }
}
