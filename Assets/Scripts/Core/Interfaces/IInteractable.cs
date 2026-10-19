using UnityEngine;

namespace Skybreak
{
    /// <summary>
    /// Common interaction contract shared by interactive doors, props, and environment mechanisms.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Prompt text displayed to the player when aiming within interaction range (e.g. "[F] Open Door").</summary>
        string Prompt { get; }

        /// <summary>Returns true if the actor is permitted to interact with this object in its current state.</summary>
        bool CanInteract(GameObject actor);

        /// <summary>Performs one deliberate interaction by the actor.</summary>
        void Interact(GameObject actor);
    }
}
