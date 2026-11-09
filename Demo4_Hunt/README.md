# Demo 4 — Hunt

## Description

This VR scavenger hunt places the player in a roadside desert environment filled with interactive props and traffic hazards. The player explores the location, finds collectible coins, manipulates environmental objects, and uses the coins with the scene's vending-machine interaction.

## What it demonstrates

- Exploration using configurable smooth movement or teleport locomotion.
- Snap/smooth turning, seated/standing height modes, vignette comfort settings, and view reset.
- Grab interactions for props including a ladder, dumpster lids, and a lighter.
- Coin collection, return, and vending-machine slot interactions.
- Spawned traffic, collision death/respawn behavior, ambient audio, and spatial sound effects.

## Controls

- Use the left joystick to rotate, and the right joystick to move.
- Press B to open the options menu.
- Swap rotation style by pressing Y, or change it in the options menu.
- Swap movement style by pressing X, or change it in the options menu.
- Grabbing objects uses the grip buttons on each hand.
- For teleportation, the joystick sets the amplitude; use the trigger to confirm and teleport.
- Holding A will strike a lighter for a portable source of light.

## Install and run

1. Download the APK from the linked GitHub Release.
2. Enable developer mode on the Quest and install it with `adb install -r <downloaded-apk>` or another approved sideloading tool.
3. Launch the app from **Unknown Sources** in the Quest library.

For development, open this folder with Unity `6000.3.22f1` and load `Assets/Scenes/SampleScene.unity`.

## Links

- Video walkthrough: **https://youtu.be/pia2PfLQ0FQ**
- Quest APK release: **https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission**

## Sources and credits

| Item | Source/license |
| --- | --- |
| Free Low Poly Desert Pack | [“Free Low Poly Desert Pack” by 2C 23 Space Robots and Counting...](https://assetstore.unity.com/packages/3d/environments/free-low-poly-desert-pack-106709), licensed under the [Standard Unity Asset Store EULA](https://unity.com/legal/as-terms). |
| Vending machine, vehicles, props, materials, textures, and skybox | AI-generated. See the portfolio [AI tooling notes](../README.md#ai-tooling). |
| Traffic speed/spawn behavior and comfort-setting values | Chosen through playtesting or exposed to the player through the options menu.|
| Unity XR/OpenXR packages | Unity package dependencies are recorded in `Packages/manifest.json`|

### Audio assets

The following Pixabay downloads are used under the [Pixabay Content License](https://pixabay.com/service/license-summary/).

| Local file | Creator and source |
| --- | --- |
| `freesound_community-money-machine-sound-104463.wav` | [“Money Machine sound” by Pablofonsvice, distributed through freesound_community](https://pixabay.com/sound-effects/film-special-effects-money-machine-sound-104463/) |
| `soundreality-lighter-2-410607.mp3` | [“Lighter 2” by SoundReality](https://pixabay.com/sound-effects/lighter-2-410607/) |
| `sumaga123-coin-drop-432151.mp3` | [“Coin drop” by sumaga123](https://pixabay.com/sound-effects/film-special-effects-coin-drop-432151/) |
| `universfield-click-button-140881.mp3` | [“Click Button” by Universfield](https://pixabay.com/sound-effects/film-special-effects-click-button-140881/) |
| `traffic/virtual_vibes-thud-impact-sound-sfx-379990.mp3` | [“Thud Impact Sound SFX” by Virtual_Vibes](https://pixabay.com/sound-effects/film-special-effects-thud-impact-sound-sfx-379990/) |
| `ambiance/dragon-studio-cicada-buzzing-331499.mp3` | [“Cicada Buzzing” by DRAGON-STUDIO](https://pixabay.com/sound-effects/nature-cicada-buzzing-331499/) |
| `ambiance/dragon-studio-desert-sand-rustling-482891.mp3` | [“Desert Sand Rustling” by DRAGON-STUDIO](https://pixabay.com/sound-effects/desert-sand-rustling-482891/) |
| `ambiance/freesound_community-owl-hooting-48028.mp3` | [“Owl Hooting,” distributed through freesound_community](https://pixabay.com/sound-effects/owl-hooting-48028/) |
| `ambiance/lazychillzone-owl-hooting-223549.mp3` | [“owl hooting” by LazyChillZone](https://pixabay.com/sound-effects/nature-owl-hooting-223549/) |
| `ambiance/places_atmosphere_nature-suono-delle-cicale-383838.mp3` | [“Suono delle cicale” by places_atmosphere_nature](https://pixabay.com/sound-effects/suono-delle-cicale-383838/) |
| `traffic/highway_engine_constant_loop.wav` | AI-generated engine noise; the only AI-generated sound in Demo 4. |
| `button_down.wav`, `button_up.wav` | [“Button Press” by DRAGON-STUDIO](https://pixabay.com/sound-effects/film-special-effects-button-press-382713/) |
| `egg_splat.wav` | [“Splat Sounds” by XpMonster](https://pixabay.com/sound-effects/film-special-effects-splat-sounds-419584/) |

## Known issues

- There is a cliff with a large gap in the vertices that lets the skybox through. Bonus points if you can find it.
- If your play area is not centered on you, you may spawn inside another object.
