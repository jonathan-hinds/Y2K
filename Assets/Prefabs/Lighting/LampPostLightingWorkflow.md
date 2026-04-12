# Lamp Post Lighting Workflow

1. Drag `LampPostLit` from `Assets/Prefabs/Lighting` into the scene.
2. Expand the prefab instance and move `LightAnchor` if you want to change where the bulb sits on the model.
3. Edit the `Lamp Light` component to tune color, intensity, range, and bake mode.
4. Bake lighting after placement so the lamps stay cheap at runtime.

Recommended defaults for a lot of street lights:

- Keep the light type as `Point`.
- Keep shadows `Off`.
- Keep bake type `Baked` unless you specifically want dynamic characters lit in real time.
- Keep range tight so pools of light do not overlap too much.
- Use the shared profile when possible so all lamps stay consistent.
