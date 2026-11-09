# Demo 3 — Procedure

## Description

This VR procedure teaches the ordered assembly of a mechanical mine prop. The player inserts and winds a spring, places and fastens a cover, installs a pressure plate, tests its force, and stamps the completed assembly. Stage displays and interaction feedback communicate progress and errors.

## What it demonstrates

- A validated, ordered procedure with `done` and `failed` outcomes.
- Two-handed object handling and snap/socket placement.
- Constrained circular thumbstick motion for winding a spring and tightening screws.
- Physical-button, pressure-plate, impact-audio, and stage-display feedback.

## Controls and procedure

1. Grab the spring and place it into its socket.
2. Grab the winding key and rotate the thumbstick in circles until the spring is wound five turns.
3. Place the cover in its socket.
4. Insert all three screws, grab each screw handle, and rotate the thumbstick in circles for three turns.
5. Place the pressure plate.
6. Press the plate with enough force to seat it without exceeding the detonation threshold.
7. Use the stamp to complete the procedure.

Use the controller trigger to grab or activate objects as indicated in VR. At any time, you can press the red or green button to reset the procedure.

## Install and run

1. Download the APK from the linked GitHub Release.
2. Enable developer mode on the Quest and install it with `adb install -r <downloaded-apk>` or another approved sideloading tool.
3. Launch the app from **Unknown Sources** in the Quest library.

For development, open this folder with Unity `6000.3.22f1` and load `Assets/Scenes/SampleScene.unity`.

## Links

- Video walkthrough: **https://youtu.be/cTN1ua7tM90**
- Quest APK release: **https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission**

## Sources and credits

| Item | Source/license |
| --- | --- |
| Assembly sequence, winding counts, force thresholds, and mechanical behavior | Fictional, cartoon "Landmine," inspired by ACME and with arbitrary steps and constraints. |
| 3D models | All models are either AI-generated or original work by Christopher Antepenko. |
| Textures (Including motivational cat) | AI-generated for this portfolio with OpenAI image generation through Codex. The ceiling is shared with Demo 2. |
| Unity XR/OpenXR packages | Unity package dependencies are recorded in `Packages/manifest.json`|

### Audio assets

| Local file | Source/license |
| --- | --- |
| `universfield-hammer-steel-impact-454390.mp3` | [“Hammer Steel Impact” by Universfield on Pixabay](https://pixabay.com/sound-effects/hammer-steel-impact-454390/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `Assume All Risks.mp3` | [“Assume All Risks”](https://suno.com/song/fc7209f1-5c69-4c8f-ba97-b86e98e9c5f8), created on my Suno Basic (free) account (`onomatopoeticpostpunk282`). Under [Suno’s free-plan rights](https://help.suno.com/en/articles/9601601) and [Terms of Service](https://about.suno.com/terms), Suno retains ownership and permits personal, non-commercial use; the track has no commercial-use rights. |
| `click.wav` | [“Click Button” by Universfield on Pixabay](https://pixabay.com/sound-effects/film-special-effects-click-button-140881/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `dragon-studio-glass-breaking-504033.mp3` | [“Glass Breaking” by DRAGON-STUDIO on Pixabay](https://pixabay.com/sound-effects/household-glass-breaking-504033/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `squeak.wav` | Original procedural screw-ratchet click generated for this project with `tools/generate_procedure_audio.py`; no third-party recording or sample is used. The legacy filename is retained to preserve its Unity asset reference. |

## Known issues

- Hammer hitting can be janky. You should hold the assembly with one hand to prevent it from flying off.
