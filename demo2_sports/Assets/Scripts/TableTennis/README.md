# VR Table Tennis Trainer

Add `VRTableTennisTrainer` to a scene object, then assign its Ball Prefab, Ball Spawn, Table Surface collider, optional Floor Collision Root, Player Serve Zone, Opponent Target Zone, Net trigger collider, Target Zone, and optional 3D TextMeshPro Score Text, Misses Text, and Serve Number Text fields in the Inspector. `Initial Delay` controls startup, `Delay Between Serves` controls the cooldown after a ball ends, `Start Delay After Buzzer` controls how long after the buzzer the ball appears, `Serves Per Round` controls the countdown cycle for the serve display, and the serve margin settings keep legal first bounces away from the net and table edges. `Serve Net Clearance` rejects generated serve arcs that do not clear the assigned net trigger by the requested margin. Under Target, `Easy`, `Medium`, and `Hard` scale the assigned target cylinder on x/z only, making the landing spot larger or smaller without changing its height. The trainer moves that target to a new random location on the opponent half at startup and immediately after each score or miss so the player can read the next landing spot during the buzzer and delay. Assign an AudioSource plus Start Buzzer, Foul Buzzer, Fail Buzzer, and Success Ding clips under Feedback Audio. Foul plays when the trainer registers a let or other foul, failure plays when an unscored ball retires, and success plays when a point is registered.

On the ball prefab, add a spatial AudioSource and assign it to `TrainingBall.Impact Audio Source`. Set suitable 3D rolloff distances, then assign separate Table And Floor Impact and Paddle Impact clips. The script forces Spatial Blend to 3D, applies a short cooldown to prevent contact chatter, and assigns a runtime physics material from the `TrainingBall` bounce settings so the ball rebounds consistently even if the prefab has no collider material set.

Keep a non-trigger collider on the table top for physical bounce and assign that same collider to the trainer as Table Surface. If your floor uses several colliders under one parent, assign that parent transform as Floor Collision Root so the trainer can register a miss as soon as the active ball hits the floor while still letting the ball finish bouncing for audio. Add thin trigger colliders just above each half as children of the table, attach `TableZone`, select Player or Opponent on each component, and assign those two triggers to the trainer as Player Serve Zone and Opponent Target Zone. Assign the net trigger collider to the trainer as Net Surface, assign a cylinder or other target object with a trigger collider and `TargetZone` to the trainer, and add `OpponentScoreZone` to a trigger beyond the opponent's end. These triggers move with the table, so scoring has no world-position dependency. Keep the table zones and target shallow so entry represents a bounce rather than a ball flying overhead. A point is finalized only after a paddle-hit ball enters the target and opponent table zones, avoids the net, and then enters the opponent score zone.

## Controls

- XR: the headset and right controller are read through Unity XR tracking. Stand still at the near end of the table.
- The paddle transform is driven by an Input System `TrackedPoseDriver` bound to the right XR controller.

## Paddle model

Put `TrackedPaddle`, `TrackedPoseDriver`, a kinematic Rigidbody, and a paddle collider on your paddle root. Assign the Rigidbody, collider, and a Center Of Mass transform placed at the controller/grip rotation pivot. Configure the pose driver's position and rotation actions for the right controller. Ball impacts use the XR runtime's reported `deviceVelocity` and `deviceAngularVelocity`, including the rotational contact velocity `ω × r`.

The machine serves continuously. Return the orange ball over the net so its first opponent-side bounce lands in the green target. The board records attempts and successful targets.

Serve fouls can be ignored by the trainer. A serve that clips the net before it becomes playable is treated as a let and does not increment attempts or misses. Terminal balls are no longer destroyed immediately, so scored, missed, or fouled balls can finish bouncing and playing audio while the next serve is already active.

## Physics

All dimensions and mass use SI units: a 40 mm / 2.7 g ball and regulation placement around a 2.74 x 1.525 x 0.76 m table. Simulation runs at 120 Hz with continuous ball collision detection. The ball model applies quadratic aerodynamic drag, Magnus force from spin, gradual spin decay, and rubber-paddle tangential grip.

The launch velocity, spin, restitution, drag, and Magnus constants are exposed together in the scripts for easy tuning against a particular headset/controller latency profile.

OpenXR 1.18 is included. The first time you target a headset platform, enable **OpenXR** for that platform under **Project Settings > XR Plug-in Management**, then select the interaction profile for your controller. This one-time checkbox creates Unity's platform-specific loader assets.
