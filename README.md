# Neon Runner

A Unity parkour prototype exploring secondary motion through three independent jiggle-physics implementations: a custom spring-damper solver, a custom Rigidbody-based solution, and a third-party Burst framework.

## Tech Stack

- Unity 6000.0.62f1 with URP 17
- Input System and Cinemachine 3
- Unity Test Framework (148 EditMode tests)

## Quick Start

1. Open `Assets/Scenes/ParkourCity.unity`.
2. Enter Play mode.

Alternatively, open **`Assets/Scenes/JiggleLab.unity`**, a visualization lab that compares solvers on two identical character models. It includes four presets, complete target/limit/force visualization in the Game view, and a tabbed interface with an `F1`/`F2` legend.

Controls: `WASD` to move, `Shift` to run, `Space` to jump, `C` / `Ctrl` to slide or vault, and `E` to blink. Use `F6` / `F7` to toggle jiggle visualization, `[` / `]` to select a chain, and `F8` to switch to the chest camera.

## Project Structure

```
Assets/Scripts/CharacterLogic/            Character movement, animation, camera, HUD, and checkpoints
Assets/Scripts/CharacterLogic/Editor/     Level generation, animation retargeting, and controller-building tools
Assets/Scripts/JigglePhysics/Runtime/     Custom spring-damper solver
Assets/Scripts/JigglePhysics/Demo/        Demos, debug visualization, and HUD
Assets/Scripts/JiggleComparison/
    Backends/OwnSpring/                   Custom spring solver backend
    Backends/OwnRigidbody/                Custom Rigidbody + ConfigurableJoint implementation
    Backends/Framework/                   naelstrof JigglePhysics integration
Assets/Scripts/JigglePhysics/Tests/       EditMode and PlayMode tests
Assets/Scripts/ComponentLogic/            Minimal component framework
```

## Documentation

- [Animation Guide](.docs/AnimationGuide.md)
- [Claude Guide](.docs/Claude_Guide.md)

## Jiggle Physics Implementations

The following costs were measured in `JiggleComparisonLab` on a character with two single-particle chains:

| Implementation | Cost | Features |
|---|---:|---|
| Custom spring solver (`JiggleRig`) | 0.065–0.068 ms/frame | 90 Hz substeps, anchor inertia, 25° cone, 45 mm displacement limit, squash and stretch |
| Rigidbody + ConfigurableJoint | 0.200 ms/frame | PhysX at 50 Hz, kinematic anchor with mass, manually configured limits |
| naelstrof JigglePhysics v16 | 0.113 ms/frame | Burst jobs, JiggleBreasts preset, MIT license |

## Third-Party Licenses

- `Assets/ThirdParty/KayKitAnimations` — Kay Lousberg; see `License.txt`
- `Assets/ThirdParty/KenneyCityCommercial` — Kenney, CC0
- `Assets/ThirdParty/QuaterniusUAL2` — Quaternius, CC0
- `com.gator-dragon-games.jigglephysics` — naelstrof, MIT
- `Assets/Models` — [Anime Girl Rigged Anime model](https://sketchfab.com/3d-models/anime-girl-rigged-anime-model-fbccf5c5a7b244e7ab04fa44da19c621) by [dequeijospizza](https://sketchfab.com/dequeijospizza), licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)
