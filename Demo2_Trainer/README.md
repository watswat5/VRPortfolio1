# Demo 2 — Trainer

## Description

This standalone VR table-tennis trainer serves balls to a player using a tracked paddle. Normal Practice emphasizes return consistency and target accuracy, while AI Opponent mode adds a computer-controlled paddle and rally scoring.

## What it demonstrates

- Direct mapping of controller motion to a physics paddle.
- Ball flight with gravity, drag, bounce response, and spin/Magnus-force behavior.
- Serve, rally, fault, scoring, target-bonus, and ball-retirement state handling.
- Normal Practice and AI Opponent modes selected through physical in-world buttons.
- Visual and spatial-audio feedback for contact, points, misses, and mode changes.

## Controls

- Move the tracked hand holding the paddle to strike the ball.
- Hit the **Normal Practice** or **AI Opponent** in-world button with the paddle to change mode.
- Hit the **Reset** button to clear the current scores and restart serving.
- Aim for marked target zones to earn accuracy bonuses in practice.

## Install and run

1. Download the APK from the linked GitHub Release.
2. Enable developer mode on the Quest and install it with `adb install -r <downloaded-apk>` or another approved sideloading tool.
3. Launch the app from **Unknown Sources** in the Quest library.

For development, open this folder with Unity `6000.3.22f1` and load `Assets/Scenes/SampleScene.unity`.

## Links

- Video walkthrough: **https://youtu.be/deG_jVfxxmo**
- Quest APK release: **https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission**

## Sources and credits

| Item | Source/license |
| --- | --- |
| Ball and table standards | The [ITTF Handbook](https://www.ittf.com/handbook/) was used for the 40 mm, 2.7 g ball; the 2.74 m × 1.525 m × 0.76 m table; and the 15.25 cm net. The ball's `0.88` bounciness approximates the lower end of the ITTF ball test's 24–26 cm rebound from a 30.5 cm drop. |
| Simulation tuning | Air density is `1.225 kg/m³`, based on NASA's [standard sea-level atmosphere](https://www.grc.nasa.gov/www/k-12/airplane/atmosmet.html), and the spherical drag coefficient is `0.47`, based on NASA's [shape-effects comparison](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/shape-effects-on-drag/). Magnus strength, serve speed and spin ranges, surface friction, and spin decay were gameplay-tuned rather than measured values. |
| Table and net models | Original work by Christopher Antepenko, built to the ITTF dimensions cited above. |
| Scoreboard | AI-generated. |
| Fonts | Unity default fonts supplied with the project packages. |
| Cinder-block and drop-ceiling textures | AI-generated for this project with OpenAI image generation through Codex; seamless processing and the matching cinder-block normal map were produced locally. |
| Unity XR/OpenXR packages | Unity package dependencies are recorded in `Packages/manifest.json`|

### Audio assets

| Local file | Source/license |
| --- | --- |
| `freesound_community-money-machine-sound-104463.wav` | [“Money Machine sound” by Pablofonsvice, distributed through freesound_community on Pixabay](https://pixabay.com/sound-effects/film-special-effects-money-machine-sound-104463/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `miss.wav` | [“BUZZER OR WRONG ANSWER” by eritnhut1992 on Pixabay](https://pixabay.com/sound-effects/film-special-effects-buzzer-or-wrong-answer-20582/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `ball.wav` | [“Ping Pong Ball” by BrendanSound12, distributed through freesound_community on Pixabay](https://pixabay.com/sound-effects/film-special-effects-ping-pong-ball-100074/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `bonus.wav` | [“Crowd Cheering” by VoiceBosch on Pixabay](https://pixabay.com/sound-effects/people-crowd-cheering-198411/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `score.wav` | [“ding” by Daronoxus, distributed through freesound_community on Pixabay](https://pixabay.com/sound-effects/film-special-effects-ding-101492/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `paddle.wav` | Edited excerpt from [“ping pong” by bruno.auzet, distributed through freesound_community on Pixabay](https://pixabay.com/sound-effects/film-special-effects-ping-pong-64516/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `freesound_community-referee-whistle-blow-gymnasium-6320.mp3` | [“Referee Whistle Blow Gymnasium”](https://pixabay.com/sound-effects/film-special-effects-referee-whistle-blow-gymnasium-6320/) — [Pixabay Content License](https://pixabay.com/service/license-summary/). |
| `button_click.wav` | From Kenney's [UI Audio](https://kenney.nl/assets/ui-audio) pack, released under [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). |

## Known issues

- Balls still on the table can sometimes affect new serves if they remain on the table.
- Sometimes the buttons will get stuck down and can't be pressed again.
