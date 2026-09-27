# Scene authoring

Place UI and other scene-authored objects in the saved Unity hierarchy so they can be edited in the Inspector before Play. Runtime code should bind behavior, update data, and activate existing panels instead of rebuilding their layout. Apply this to new work; convert existing runtime-generated systems when working on them. Dynamic gameplay entities such as spawned enemies/projectiles may still be instantiated or pooled as gameplay requires.

Preserve hand-edited scenes and prefabs. Setup/installation tools must not replace authored layouts. Item icons remain empty unless the user provides or requests assets.

# Action controls

Normal attacks and purification run automatically in their selected mode, aimed with the mouse. Reserve primary-button press/hold/release for charge actions. Keep pause, run-end and mode-change cancellation intact.
