# sim-demo

Unity simulator demo of a camera-and-arm home robot that recognizes wall switches and operates them as part of a proactive routine. It walks through the whole process, from detection to learning, in a modern house scene.

## Process

| Stage | What happens |
|---|---|
| S1 | Detect candidate switches in the robot camera feed |
| S2 | Map each switch to a device and verify it with a test pulse |
| S3 | Propose a routine to the user (auto-approved in auto mode) |
| S4 | Trigger the routine and resolve intent |
| S5 | Pick a control channel (wireless first, physical press as fallback) |
| S6 | Safety guard check, then arm path planning and the press |
| S7 | Update channel success estimates from the outcome |

## Run

- Unity 6000.5.4f1, built-in render pipeline, Mono
- Open the project and press Play. The scene builds itself at runtime (`Assets/Scripts/Bootstrap.cs`).
- Editor menu `CLOiD > Setup project` regenerates materials and the scene.
- Headless builds: `-executeMethod Setup.Build` (macOS) and `Setup.BuildWin` (Windows) write to `Builds/`.

## Controls

| Key | Action |
|---|---|
| W A S D / arrows | Drive the robot (Shift to run) |
| Click on the floor | Walk there with A* path finding |
| Space | Start or stop the auto demo |
| R / G | Run the evening / goodnight routine |
| H | Inject a hazard (a person near the switch) |
| C / F / V | Cycle camera / overview / robot POV |
| Q / E, right mouse | Orbit camera |
| T | Reset the world |
| Esc | Cancel the current mission |

## Layout

- `Assets/Scripts` runtime code: house, robot rig, perception and process director, HUD, cameras
- `Assets/Editor` project setup, build and play-mode automation
- `Assets/Resources` FBX models and baked materials
- `BlenderSrc` Blender scripts that generate the models (`lib.py` exports FBX) and the source `.blend`

## Skills and standards

`skills/` is a git submodule of [harness](https://github.com/sweg9/harness). It holds the shared standards and skills for this project. Claude Code finds the skills through `.claude/skills`.

```bash
git clone --recurse-submodules <this repo>
git submodule update --init            # in a repo that is already cloned
git submodule update --remote skills   # move to the latest skills, then commit
```
