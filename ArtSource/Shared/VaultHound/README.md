# Vault Hound teammate package

This package contains the Vault Hound character model, mechanical rig, eight animation clips, textures, and previews. The construction is AI-assisted Blender work based on `References/VaultHound_Concept.png`. The actual model is shown in `Previews/VaultHound_ThreeQuarter.png`.

It is an art handoff. The hunting AI, chamber trigger, Unity prefab, Animator Controller, audio, and map placement remain integration work for the assigned teammate.

## Open the model

Use Blender 5.1.2 or a compatible newer version.

- `Source/VaultHound_Editable.blend`: separate named armor and mechanism pieces, weighted to the rig. Use this file to modify the model.
- `Source/VaultHound_Rigged.blend`: joined skinned mesh with the same rig and clips. Use this file to inspect the export representation.
- `Exports/VaultHound_Animated.fbx`: skinned model with all eight animation takes.
- `Exports/VaultHound.glb`: static posed reference model with embedded materials, textures, and skeleton. This GLB does not contain animation clips.
- `Textures/`: base color and emission atlas images, also packed into the Blender sources.
- `Previews/`: front, side, three-quarter, lunge, and a looping run GIF.
- `Evidence/ModelReport.json`: measured geometry, dimensions, and clip frame ranges.
- `Evidence/ExportValidation.json`: results from reopening the Blender source and importing the exported FBX.

The model uses metres, Blender Z up, and Blender -Y forward. Its root is at ground level between the feet. The measured bounds are approximately 1.00 m wide, 1.89 m long, and 1.15 m high. It has 6,192 triangles, 19 bones, and one material. These counts come from `Evidence/ModelReport.json`.

The atlas UVs deliberately share solid-color swatches. They are not a unique UV layout for painting detailed textures or baking lightmaps. Every vertex is rigidly assigned to one bone, which suits the separate mechanical plates. The rig has no IK controls or automatic foot planting.

## Inspect the animations

Select `VaultHound_Rig` in Blender, open the Dope Sheet in Action Editor mode, select a clip, and scrub its frame range. The source also holds each clip on a muted NLA track. Keep other tracks muted when previewing an action.

| Clip | Duration | Loop |
| --- | ---: | --- |
| Idle | 2.00 seconds | Yes |
| Walk | 1.00 second | Yes |
| Run | 0.67 seconds | Yes |
| Alert | 1.00 second | No |
| Sniff | 2.00 seconds | Yes |
| Lunge | 0.80 seconds | No |
| Hit | 0.40 seconds | No |
| Death | 1.50 seconds | No |

All clips are in place. Movement code must move the actor; the lunge animation does not advance the gameplay root or apply damage. These are basic mechanical animation clips. Tune locomotion speed, foot contact, attack timing, and the defeat pose during gameplay integration.

## Suggested Unity import

These settings are a handoff procedure, not a claim of a completed Unity import.

1. Copy the FBX and the two PNG textures into your own model folder, preserving the files already used by the project.
2. Select a Generic rig and create its Avatar from this model. Check the imported scale and forward direction in a small test scene before placing it in the map.
3. Create one URP Lit material. Assign `VaultHound_BaseColor.png` as Base Map. Enable Emission and assign `VaultHound_Emission.png`; start with an orange emission multiplier around 1.8, then review under the team's actual lighting. Metallic 0.45 and Smoothness 0.62 match the source material's intended response.
4. Remap the FBX material slot to that URP material. Unity does not reproduce Blender's material nodes automatically.
5. Inspect the imported takes. Their names are prefixed with `VaultHound_Rig|`. Enable looping for the four clips marked Yes above and disable it for the others. Leave root motion disabled.
6. Build an Animator Controller and connect it to the agent's actual movement and combat state. Damage belongs to gameplay logic, not to repeatedly playing the animation.
7. Fit collision and navigation clearance to the full imported model. Test the snout, feet, and rear stabilizer at the real chamber doors and around cover.

## Proposed chamber encounter

Use two instances of the same Hound prefab, one behind each chamber door. Both instances use the same agent code.

The encounter controller counts only living enemies assigned to the first area. When that count reaches zero, it opens the chamber doors once. Each Hound waits for its doorway to become traversable, leaves the chamber, and investigates the last reported combat area.

The teammate's brain should use sight and remembered observations. When one Hound sees the player, it can share that sighting with its partner. One pursues while the other evaluates a reachable alternative approach. If sight is lost, search around the last sighting instead of reading the hidden player's current position. A dead partner switches the survivor to solo pursuit. A closed or blocked route requires replanning or a search fallback.

Exclude Relay, unreleased chamber enemies, second-area enemies, and the boss from the first-area clear counter. Reset the release latch only when restarting the encounter.

## Rebuild and checks

`Source/build_vault_hound.py` creates the model in a separate scene. For an isolated rebuild, run Blender in background mode with `--factory-startup --python` followed by the script path. It writes the two Blender files, FBX, GLB, atlas images, and model report into this package folder.

`Source/render_previews.py` renders the saved rigged model. `Source/validate_exports.py` checks source skinning and animation deformation, then reimports the FBX and checks its mesh, bone, vertex, and clip counts. Export validation passed; see `Evidence/ExportValidation.json` for the specific checks. Unity import, gameplay, chamber clearance, and runtime performance have not been tested for this model.

The original concept image is generated artwork. Model and script edits made by the receiving teammate should be documented as their actual contribution.
