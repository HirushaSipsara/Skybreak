# Unity integration

The lab is the reference for component wiring. Integrating into the team's final scene requires its real player, gun, map and objective references; this has not been performed or Play Mode tested here.

## Scene services and geometry

Create one scene object with `WardenMap` and one `WardenNoiseBus`. Supply 1–256 world-space navigation nodes and unique undirected connections. Nodes represent safe hover-centre positions; the lab uses Y=1. Edge cost is Euclidean distance plus a nonnegative extra cost. Call `Build()` after assigning arrays from code. Inspector-assigned arrays are built in `Awake()`.

Keep all actor colliders and actor children on layer 8, matching the project's `DamageSink` convention. Walls, doors and cover need non-trigger environment colliders on layers included by `environmentMask`. Verify the Physics collision matrix permits actor/environment and intended actor/actor contact. The default mask excludes layer 8, so actors cannot permanently close navigation edges or provide environmental cover.

The map checks a sphere of radius 0.7 m at each node plus a 0.2 m upward offset and casts along every edge. This approximates clearance for the lab's hovering controller. Validate all passages and the full capsule against actual level geometry; larger models, low ceilings, slopes or stacked floors need adjusted graph authoring and collision checks. This is a waypoint navigation system, not automatic navigation-mesh generation.

Door changes are detected every 0.4 s. For immediate updates, change the collider, call `Physics.SyncTransforms()`, then `map.Refresh()`. The graph version changes only when a link changes. Movement replans when its destination or graph version changes and retries blocked routes after a delay. It never teleports across a blocked edge. Temporarily crowded agents stop/retry; this implementation does not provide cooperative path reservations or guaranteed multi-agent deadlock resolution.

## Warden object

Create a root with `CharacterController`, `WardenMotor`, `WardenPerception`, `WardenBrain` and `WardenPresentation`. The lab uses a controller radius 0.62 m, height 1.5 m, centre `(0,0.2,0)`, and speed 2.8 m/s. Instantiate the model as a child, with its forward axis aligned to Unity +Z. Verify the FBX orientation and dimensions after import.

Assign the shared map to brain, motor and perception. Assign the noise bus and actual player `DamageSink` to perception; assign the protected `WardenObjective` to the brain. Assign visual root, resonance-core renderer and pulse `LineRenderer` to presentation. Set URP materials using the lab's cyan, amber and metal mapping as a starting point. Save a reusable prefab in Unity after verifying its appearance; bind scene services and target references on each instance. No prebuilt Unity prefab is claimed in this delivery.

Visual bobbing, core colours and beams are separate from the AI. Removing presentation must leave decisions and damage functional. The model has rigid parts rather than a skeletal animation rig.

## Player combat and sound

The actual player's health adapter must derive from the existing `Skybreak.Combat.DamageSink`, report `Allegiance.Player`, and provide its health, maximum health, damage method and an aim point inside its collider. The Warden itself is a hostile `DamageSink`; route gun hits through `GetComponentInParent<DamageSink>()`. A controller that damages only the old `SecurityTarget` class will not damage this enemy until its hit handling is connected.

Emit a noise when a shot is fired, including misses:

```csharp
noiseBus.Emit(muzzle.position, 26, WardenNoiseKind.Gunshot);
```

Attach `WardenFootsteps` to the moving player and assign its bus. It emits based on actual horizontal displacement and ignores airborne movement and large teleport deltas. Adjust walking/sprinting thresholds to match the final controller. Do not also emit duplicate footstep events from animation unless you remove the adapter.

The noise bus carries world events, but only the sensor simulation uses the source position to calculate received amplitude. The brain gets a probability map, not that hidden position. All emitters currently use known loudness categories. Footsteps, decoys and pedestal noise are treated as possible intrusion evidence; identifying friendly or unrelated noise is not implemented.

## Crystal and other agents

Attach `WardenObjective` to the real pedestal, assign the bus and crystal visual, and call `Disturb(damage)` from the interaction that removes or damages it. The alarm reveals objective danger, not the player's current hidden location. At zero integrity it becomes unavailable and the Warden falls back to patrol/search around its home area. `ResetState()` restores the lab's 100 integrity and clears the alarm.

Use the shared combat contract with the other team agents. Each Warden owns its own brain, belief and path, while graph and event bus are scene services. Running four Wardens is a preliminary load scenario; the assignment requires the four **distinct student agents** together. Run that scenario in the final team map before claiming group stability. Keep player damage, events and scene setup outside the pure C# core assembly.
