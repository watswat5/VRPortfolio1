# VR Table Tennis Trainer

Add `VRTableTennisTrainer` to a scene object, then assign its Ball Prefab, Ball Spawn, and optional 3D TextMeshPro Status Text fields in the Inspector. `Initial Delay` controls startup, `Delay Between Serves` controls the cooldown after a ball ends, and `Start Delay After Buzzer` controls how long after the buzzer the ball appears. Assign an AudioSource plus Start Buzzer, Fail Buzzer, and Success Ding clips under Feedback Audio. Failure plays when an unscored ball retires, and success plays when a point is registered.

On the ball prefab, add a spatial AudioSource and assign it to `TrainingBall.Impact Audio Source`. Set suitable 3D rolloff distances, then assign separate Table And Floor Impact and Paddle Impact clips. The script forces Spatial Blend to 3D and applies a short cooldown to prevent contact chatter.

Keep a non-trigger collider on the table top for physical bounce. Add thin trigger colliders just above each half as children of the table, attach `TableZone`, and select Player or Opponent on each component. Add `TargetZone` to the desired bounce area, `NetZone` to the net, and `OpponentScoreZone` to a trigger beyond the opponent's end. These triggers move with the table, so scoring has no world-position dependency. Keep the table zones and target shallow so entry represents a bounce rather than a ball flying overhead. A point is finalized only after a paddle-hit ball enters the target and opponent table zones, avoids the net, and then enters the opponent score zone.

## Controls

- XR: the headset and right controller are read through Unity XR tracking. Stand still at the near end of the table.
- The paddle transform is driven by an Input System `TrackedPoseDriver` bound to the right XR controller.

## Paddle model

Put `TrackedPaddle`, `TrackedPoseDriver`, a kinematic Rigidbody, and a paddle collider on your paddle root. Assign the Rigidbody, collider, and a Center Of Mass transform placed at the controller/grip rotation pivot. Configure the pose driver's position and rotation actions for the right controller. Ball impacts use the XR runtime's reported `deviceVelocity` and `deviceAngularVelocity`, including the rotational contact velocity `ω × r`.

The machine serves continuously. Return the orange ball over the net so its first opponent-side bounce lands in the green target. The board records attempts and successful targets.

## Physics

All dimensions and mass use SI units: a 40 mm / 2.7 g ball and regulation placement around a 2.74 x 1.525 x 0.76 m table. Simulation runs at 120 Hz with continuous ball collision detection. The ball model applies quadratic aerodynamic drag, Magnus force from spin, gradual spin decay, and rubber-paddle tangential grip.

The launch velocity, spin, restitution, drag, and Magnus constants are exposed together in the scripts for easy tuning against a particular headset/controller latency profile.

OpenXR 1.18 is included. The first time you target a headset platform, enable **OpenXR** for that platform under **Project Settings > XR Plug-in Management**, then select the interaction profile for your controller. This one-time checkbox creates Unity's platform-specific loader assets.
