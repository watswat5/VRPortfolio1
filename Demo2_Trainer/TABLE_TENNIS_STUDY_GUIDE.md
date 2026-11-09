# VR Table Tennis Trainer – Study Guide

This guide covers the **concepts** and **architecture** of the 12-script table-tennis system for VR. Focus on understanding *what each script does* and *how they connect*—not specific math.

---

## System Overview

The trainer simulates a table-tennis rally between a human player and an AI opponent. The system:
- Generates valid serves with preview arrows
- Tracks ball state through the rally (serve, awaiting hit, traveling, scored/faulted)
- Applies physics (drag, spin, bounce)
- Scores points and fouls
- Switches between "normal practice" and "AI opponent" modes
- Moves a target zone after each rally (bonus scoring)

---

## Architecture Map

```
VRTableTennisTrainer (Main Coordinator)
  ├─ ServeController (Serve timing & generation)
  │  ├─ TrainingBall (Ball lifecycle & physics)
  │  └─ TargetZone (Bonus trigger)
  ├─ RallyScorekeeper (Score display & audio)
  ├─ PaddleButton × 3 (Mode control buttons)
  ├─ TableCourt (Court geometry & collision classification)
  ├─ TrackedPaddle (Player's physical paddle)
  ├─ AIPaddleController (AI opponent paddle)
  └─ PaddleHitResponder × 2 (Hit response for both paddles)
```

---

## Script-by-Script Guide

### 1. **VRTableTennisTrainer** – Master Orchestrator

**What it does:**
- Central coordinator that wires all subsystems together
- Manages mode switching (Normal Practice ↔ AI Opponent)
- Owns serve scheduling loop, score counters, and display updates
- Routes active-ball events from ServeController and TrainingBall into scoring logic

**Key concepts:**
- **Mode:** Two trainer states with different responses (scores vs. volleys)
- **Active ball:** Only one ball is "active" at a time; others are cleaned up
- **Event routing:** Ball events trigger trainer callbacks that update score, audio, and target placement

**Interface to other scripts:**
- **Receives:** Button presses (reset, mode change)
- **Sends:** Mode events to AIPaddleController, ball launch commands to TrainingBall
- **Publishes:** ModeChanged event

---

### 2. **ServeController** – Serve Orchestration

**What it does:**
- Manages the serve loop: timing, ball spawn, trajectory preview, and target placement
- Generates legal serve velocities (low-arc, net-clear ballistic paths)
- Scales and repositions the target zone for difficulty
- Maintains the active ball reference

**Key concepts:**
- **Ballistic solve:** Uses math to find launch velocity from spawn to a bounce point
- **Net clearance check:** Before committing to a serve, verifies the ball clears the net at the right height
- **Preview arrow:** Visual trajectory line drawn before serve (can be disabled)
- **Target difficulty:** Easy (large), Medium (normal), Hard (small) adjust the target zone size
- **Serve margins:** Keeps first-bounce landing inside legal boundaries with safety margins

**Flow:**
1. Prepare serve → instantiate ball, calculate trajectory, show preview
2. Wait delay → play start buzzer
3. Launch → disable preview, set ball velocity and spin

---

### 3. **TrainingBall** – Rally State Machine

**What it does:**
- Implements the entire rally state machine (Serving → Awaiting Hit → Traveling → Scored/Faulted)
- Applies physics each frame (aerodynamic drag, spin-induced Magnus force)
- Routes collision events (paddle, table, net, floor) to the appropriate state handlers
- Tracks bonus target entry

**Key concepts:**
- **State machine:** Ball progresses through 8 states; each state determines what collisions mean
- **Rally outcomes:**
  - **Scored:** Ball landed on the correct side after a legal bounce
  - **Faulted:** Ball hit net or landed wrong side during serve/travel
  - **Retired:** Ball timed out or fell off the world
- **Aerodynamics:** Drag slows the ball down; spin (Magnus force) curves its path
- **Bounce debounce:** Ignores multiple rapid bounce events on the same surface within a time window
- **Terminal state:** Once scored/faulted/retired, the ball ignores further collisions
- **Delayed destruction:** Ball waits for audio to finish before being destroyed

**State flow example:**
```
Serving 
  → (bounces on player side) → AwaitingPlayerHit 
  → (player hits) → TravelingToOpponent 
  → (bounces on opponent side) → AwaitingOpponentHit 
  → (opponent hits) → TravelingToPlayer
  → (bounces on player side) → AwaitingPlayerHit again ...
  → (second bounce on player side without hit) → Scored (player wins)
```

---

### 4. **PaddleHitResponder** – Physics-Based Hit Response

**What it does:**
- Detects paddle-ball collisions and computes outgoing velocity and spin
- Uses shared physics for both player and AI paddles
- Validates that a hit is legal (correct state and side)
- Notifies listeners (haptic feedback, AI tracking) when a legal hit occurs

**Key concepts:**
- **Motion source:** Gets paddle velocity from an `IPaddleMotionSource` (TrackedPaddle or AIPaddleController)
- **Contact geometry:** Calculates the exact point and normal at ball contact
- **Response formula:** 
  - Separates incoming velocity into normal and tangential components
  - Applies restitution (bounciness) to normal component
  - Applies friction to tangential component
  - Adds spin based on paddle angular motion
- **Approach speed check:** If ball is already moving away, it's a "physics-resolved" hit (already separated)
- **Legal validation:** Ball must be in AwaitingPlayerHit or AwaitingOpponentHit state

---

### 5. **TrackedPaddle** – Player's Real Paddle

**What it does:**
- Converts XR hand tracking into simulated paddle motion
- Reads tracked hand pose every frame and measures velocity/angular velocity
- Applies those velocities to the Rigidbody for continuous collision detection
- Triggers haptic feedback when hitting the ball

**Key concepts:**
- **Velocity from pose:** Calculates linear velocity as (new position − old position) / time step
- **Angular velocity from rotation:** Converts rotation delta into axis + angle, then rad/s
- **Clamped speeds:** Enforces maximum tracking speed so hand jerks don't create impossible velocities
- **Rigidbody drive:** Uses .linearVelocity property to move the body (rather than teleporting)
- **Continuous collision detection:** Required to not miss small, fast-moving targets
- **Haptic feedback:** On legal hit, sends pulse to the right-hand controller

---

### 6. **AIPaddleController** – AI Opponent

**What it does:**
- Predicts when/where an incoming ball can be intercepted
- Moves the AI paddle toward intercept position with realistic acceleration limits
- After hitting, chooses either an accurate return or intentionally flawed shot
- Enables/disables itself based on trainer mode

**Key concepts:**
- **Reaction time:** AI waits N milliseconds before starting to predict (difficulty setting)
- **Intercept prediction:**
  - Samples the ball's physics (velocity, current position)
  - Projects forward in time to find when ball reaches the paddle's reachable zone
  - Accounts for a bounce if the ball is still traveling toward opponent
  - Constrains intercept to a reachable box (lateral, vertical, depth limits)
- **Constrained motion:**
  - Maximum move speed and acceleration (not instant teleport)
  - Moves toward target position within these limits
  - Uses angular acceleration to rotate paddle toward aim angle
- **Error chance:** Randomly decides to hit accurately or inaccurately based on required speed
  - High speed requirement → higher error chance (stressed AI)
  - Low speed → mostly accurate
- **Return velocity:** Generates a valid ballistic arc to the opponent zone, or illegal aim if "inaccurate"

---

### 7. **Ballistics** – Stateless Projectile Helpers

**What it does:**
- Solves for launch velocity given origin, target, and speed (low-arc formula)
- Checks whether a projectile path clears a vertical obstacle

**Key concepts:**
- **Low-arc solution:** Given start, end, and launch speed, finds the launch angle that reaches the target
  - Two solutions exist (high and low arc); low-arc is chosen
  - Uses quadratic equation to solve
- **Obstacle clearance:** Samples ball height at the net and verifies it meets clearance threshold

**Used by:**
- ServeController (generate legal serves)
- AIPaddleController (generate legal returns)

---

### 8. **TableCourt** – Court Geometry & Collision Classifier

**What it does:**
- Centralizes court collider references (table surface, net, floor, player/opponent zones)
- Provides queries to classify collisions: Is this net? Floor? Which table side?

**Key concepts:**
- **Table zones:** Trigger colliders that mark player-side and opponent-side halves
- **Contact classification:**
  - Is collider the net? → contact is a foul
  - Is collider the floor or under-table? → end rally
  - Is collider the table surface? → which side? (determines bounce location)

**Used by:**
- TrainingBall (classify collisions in OnCollisionEnter)
- VRTableTennisTrainer (validate scene setup)
- AIPaddleController (navigate court geometry)

---

### 9. **TableZone** – Logical Side Marker

**What it does:**
- Simple trigger collider that declares "this is player side" or "opponent side"
- No physics; just bounds and identity

**Used by:**
- TableCourt (to query which side a contact point is on)
- Serve targeting (bounce in player zone, return to opponent zone)

---

### 10. **TargetZone** – Bonus Trigger

**What it does:**
- Trigger collider that awards a temporary bonus flag on the active ball
- When the ball enters, calls `ball.EnterTargetZone()`

**Key concepts:**
- **Bonus flag:** Once set, persists until rally outcome (only counts if player wins rally)
- **Repositioning:** ServeController moves this zone after every rally

**Scoring impact:**
- Player wins rally + hit target = +1 bonus point (separate counter)
- Player wins rally + no target = standard point

---

### 11. **PaddleButton** – In-World Mode Control

**What it does:**
- Physics-driven button with realistic spring return
- Watches depression (how far down it's pushed)
- Emits "Pressed" event when depression crosses threshold

**Key concepts:**
- **Spring physics:** SpringJoint pulls button back up; friction is minimal
- **Press detection:** Hysteresis (only fires once per press, even if held)
- **Audio:** Plays click sound when pressed
- **Three buttons:** Reset, Normal Practice, AI Opponent (used by VRTableTennisTrainer)

---

### 12. **RallyScorekeeper** – Score Display & Audio

**What it does:**
- Maintains score counters (score, misses, bonuses, attempts/volleys)
- Updates UI text displays (scoreboard labels, values)
- Plays feedback audio (success ding, miss buzz, foul buzz)
- Switches label text based on mode (SCORE vs. HOME, MISSES vs. AWAY)

**Key concepts:**
- **Mode-aware labels:** Normal Practice shows "SCORE/MISSES"; AI mode shows "HOME/AWAY"
- **Bonus tracking:** Separate counter incremented when player scores with target
- **Attempt counting:** Increments when serve is first hit (not on serve itself)
- **Audio events:**
  - Point for player → success ding or bonus ding
  - Point for opponent → fail buzz
  - Foul → foul buzz

---

## Key Flows & Scenarios

### Scenario 1: Player Serves and Scores a Point

1. **Trainer loop** → "time to serve"
2. **ServeController.PrepareServe()** → spawn ball, generate legal velocity, show preview arrow
3. **ServeController.LaunchPreparedServe()** → ball.Launch(velocity, spin)
4. **TrainingBall** state = Serving; applies physics each frame
5. **Player hits button** → PaddleButton.Pressed event
6. **TrackedPaddle** is actively providing motion to PaddleHitResponder
7. **PaddleHitResponder.OnCollisionEnter()** detects paddle-ball contact
   - Checks: is ball in AwaitingPlayerHit state? Yes → **legal hit**
   - Updates ball velocity/spin
   - Fires LegalHit event (haptics trigger here)
   - Calls ball.RegisterPaddleHit(Player) → ball state = TravelingToOpponent
8. **TrainingBall.OnCollisionEnter()** → ball bounces on opponent's side
   - State now = AwaitingOpponentHit
9. **Player doesn't hit again** → ball sits on opponent side
10. **TrainingBall.FixedUpdate()** → timeout elapsed or second bounce detected
    - Calls trainer.RegisterPoint(Player, targetBonus)
    - State = Scored
11. **VRTableTennisTrainer.RegisterPoint()** → RallyScorekeeper.RegisterPoint(Player)
12. **RallyScorekeeper** → score++, play success ding, refresh UI
13. **ServeController.MoveTargetZone()** → place new target for next rally

---

### Scenario 2: AI Opponent Plays (AI Mode)

1. Ball is traveling toward opponent
2. **AIPaddleController.FixedUpdate()** → detects ball is incoming
3. Reaction time elapses
4. **CommitInterceptPrediction()** → estimate intercept point and time
5. Set targetPosition and targetRotation
6. **DriveTowardTarget()** each frame → move paddle incrementally toward intercept
7. Ball arrives at intercept location
8. **PaddleHitResponder** detects collision
9. **AIPaddleController.HandleLegalHit()** → decide accuracy
10. Generate return velocity (accurate or illegal) and set ball.linearVelocity
11. Ball now travels back to player

---

### Scenario 3: Foul (Net Hit)

1. Ball traveling at net height
2. **TrainingBall.OnCollisionEnter()** → court.IsNetCollider(collider) = true
3. **HandleNetContact()** → RegisterFoul(LastHitter)
4. Ball state = Faulted; terminalStateTime = now
5. Trainer routes foul to scorekeeper
6. If player hit the net → opponent scores. If serve net → replay.

---

## Physics Concepts (No Math, Concepts Only)

- **Drag:** Air resistance slows the ball proportionally to speed
- **Magnus force:** Spinning objects experience lift perpendicular to spin axis and velocity
- **Restitution:** Bounce "bounciness"; 0 = dead, 1 = perfectly elastic
- **Friction:** Tangential energy loss on bounce
- **Continuous collision detection:** Checks for collisions along the full path each frame (not just start/end positions)

---

## Testing/Quiz Tips

1. **Understand the state machine:** Trace a ball through 3–4 states (Serving → AwaitingPlayerHit → TravelingToOpponent → AwaitingOpponentHit)
2. **Know the roles:** Each script has one main responsibility (e.g., ServeController = serves, PaddleHitResponder = collisions)
3. **Follow an event:** Pick a mode button press or ball collision; trace the function calls
4. **Modes matter:** AI Opponent mode is completely different from Normal Practice (AI has prediction/motion overhead)
5. **Scoring rules:** Faults end rallies differently than misses; target bonus only counts on wins

---

## Glossary

| Term | Meaning |
|------|---------|
| **StateSpace** | The 8 ball states (Serving, AwaitingPlayerHit, TravelingToOpponent, etc.) |
| **Intercept** | Where the AI predicts it can contact an incoming ball |
| **Reachability** | Constrained zone the AI paddle can move into |
| **Serve validity** | Bounce on player side + net clearance + land in opponent zone |
| **Rally outcome** | Scored, Faulted, or Retired |
| **Motion source** | Interface (TrackedPaddle or AIPaddleController) that provides paddle velocity |
| **Terminal state** | Ball is in Scored, Faulted, or Retired and ignores further collisions |
| **Bounce debounce** | Small time window to ignore duplicate bounce events on same surface |

---

## Recommended study order for the quiz:

1. **VRTableTennisTrainer** – understand the big picture
2. **TrainingBall** – understand the state machine
3. **ServeController & AIPaddleController** – understand serve and intercept concepts
4. **PaddleHitResponder & TableCourt** – understand collision routing
5. **TrackedPaddle** – understand hand tracking
6. **Everything else** – filler details

Good luck on your quiz!
