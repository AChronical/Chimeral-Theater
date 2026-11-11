# Chimeral Theater

**A film set for one person, in VR.**

![alt](img/preview.gif)

Making a film, an animation or any staged scene usually takes a crew: actors, a set, a motion capture stage, or the time to keyframe every movement by hand. Chimeral Theater is an attempt to put all of that within reach of one person with a consumer VR headset.

It's built on two ideas:

- **VR as motion capture.** A headset already tracks your head and hands. Instead of animating a character, you perform it, and your movement becomes the recording.
- **VR as the set.** You don't direct from outside the scene. You stand in it, play against the other characters, pick things up and move around them, the way you would on a real set. Because everything is digital, nothing is fixed: any performance can be redone, recast with a different character or moved into a different scene.

The goal is for the output to be anything visual: films, animation, previs, storyboards, or reference for drawing.

> **Status:** early prototype. Today it covers the performance side: recording, layering and recasting characters, with physics props. Cameras and export are next. Built in Unity 6 for PC VR (tested on Meta Quest via OpenXR).

## Features

- **Play every role yourself.** A *take* is a scene made of *acts*, one per character. Record yourself as one character, then record the next while your earlier performances play back around you, so you can react to yourself.
- **Recast at any time.** Recordings store only tracking data, so any avatar can be assigned to an act after it's recorded.
- **Bring your own characters.** Drop a rigged `.glb`/`.gltf` model into the Files tab and convert it to an avatar. Bones from Mixamo, Unity Humanoid, VRoid/VRM, Rigify and Biped rigs are mapped automatically, the body follows your head, the arms are solved with IK, and the avatar is scaled to your height.
- **Physics props.** Any model can also become a prop. Grab, carry and throw props with the grip button. Grips are recorded and replayed, so a prop thrown in one act can be caught in the next. Props reset after every take.
- **Never take the headset off.** A dashboard on your hand has Films, Files and Inventory tabs for recording, importing and assigning everything.

## Using it

1. Press the left primary button to open the dashboard.
2. In **Films**, start a new take and record an act. A countdown gives you time to get into place.
3. Record another act. Everything recorded so far plays back while you perform.
4. In **Files**, convert your own models into avatars or props. They appear in **Inventory**.
5. Assign an avatar to any act, or to yourself, and play the take back.

| Input | Action |
|---|---|
| Left stick | Move |
| Right stick | Turn |
| Left primary button | Open/close dashboard |
| Right trigger | Click in the dashboard |
| Grip (either hand) | Grab/release props |

## UniRes

Data is saved with **[UniRes](https://github.com/AChronical/UniRes)**, a Godot-inspired resource and serialization package for Unity (included as a submodule).

| Extension | Contents |
|---|---|
| `.chact` | One recorded act: poses and grip actions |
| `.chavi` | An avatar: mesh, skeleton and materials |
| `.chprop` | A prop: mesh and materials |

## Roadmap

[Developement Status](https://chimeral-theater.notion.site/Prototype-Scoping-accc5f43138a83f28c4b014b56bc7704?source=copy_link)