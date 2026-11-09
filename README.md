# Christopher Antepenko — VR Portfolio 1

![Headshot](portfolio-assets/ds_headshot.jpg)

## Bio

Chris Antepenko is a Computer Science student at the University of Georgia graduating in Fall 2026. His interests include electronics, computers, game development, cooking, cybersecurity, and artificial intelligence. He has professional experience in food service, engineering, and consulting, and works with Java, C++, C, C#, Python, and Arduino. His projects include the award-winning Ban-Jovi portable synthesizer, a JavaFX CHIP-8 emulator, and published research on microtransactions and gambling in video games. After graduation, he plans to pursue a graduate degree in cybersecurity or AI.

## Projects

| Project | What it demonstrates | Video | Experience |
| --- | --- | --- | --- |
| [Demo 1 — Ride](Demo1_Ride/) | A browser-oriented carousel ride with animated horses, multiple viewpoints, a loop counter, and day/night lighting. | [Video walkthrough](https://youtu.be/Zm-wzBWX1gk) | [Play online](https://dimma-dome.com/demo1/) · [Submission build](https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission) |
| [Demo 2 — Trainer](Demo2_Trainer/) | A VR table-tennis trainer with tracked paddle physics, ball aerodynamics, practice and AI-opponent modes, scoring, targets, and spatial audio. | [Video walkthrough](https://youtu.be/deG_jVfxxmo) | [Submission APK](https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission) |
| [Demo 3 — Procedure](Demo3_Procedure/) | A guided VR assembly procedure using socketed parts, constrained winding and fastening, ordered validation, and success/failure feedback. | [Video walkthrough](https://youtu.be/cTN1ua7tM90) | [Submission APK](https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission) |
| [Demo 4 — Hunt](Demo4_Hunt/) | A VR scavenger hunt in a roadside environment with collectible coins, interactive props, traffic hazards, and configurable comfort controls. | [Video walkthrough](https://youtu.be/pia2PfLQ0FQ) | [Submission APK](https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission) |

## Submission downloads

The [Submission release](https://github.com/ugavrclass2026/portfolio-1-watswat5/releases/tag/Submission) contains the Quest APKs for Demos 2, 3, and 4, plus a downloadable WebGL ZIP for Demo 1. Demo 1 can also be [played online](https://dimma-dome.com/demo1/).

## AI tooling

**Assets and audio.** I used AI to create most of the 3D assets and textures, along with some sound effects. In Demo 4, all custom visual assets except the Free Low Poly Desert Pack were AI-generated; the engine noise was its only AI-generated sound. I used GPT 6 Astra for many of the 3D models. In one prompt, it created and animated the vending machine and its components for Demo 4. I then wired up the buttons and display. Additionally, as an easter egg, I had Suno AI create a song from an employment waiver to play during Demo 3.

**Scripting workflow.** I used the Codex desktop app and CLI, primarily with GPT 5.6 Sol. I explored ideas in chat until the mechanics were clear, then either wrote the code myself or had Codex implement it. I tested and reviewed each script to check that it behaved as intended. For minor edits, I used the copilot plugin within VS Code.

**What I learned.** Codex often kept code that no longer served the design, adding compatibility layers instead of simplifying an existing script. To keep the implementation manageable, I learned to describe the intended behavior plainly and explicitly ask it to remove redundant or stale code.

## Repository layout

Each demo is a self-contained Unity project created with Unity `6000.3.22f1`. Generated Unity folders, local builds, and APK files are excluded by the root `.gitignore`. Quest APKs belong in GitHub Releases rather than in this repository.
