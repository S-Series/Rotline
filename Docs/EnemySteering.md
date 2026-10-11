# Enemy distance bands

Enemy definitions select one of five range categories. The values below are starting points; tune `Minimum Distance` and `Maximum Distance` on each `EnemyDefinition` to match its collider and attack range. The asset context menu **Apply Range Band Distance Preset** copies the category defaults when wanted.

| Category | Inspector value | Default distance |
| --- | --- | --- |
| 근접 | Melee | 1.0–1.5 |
| 근거리 | Close | 2.0–3.0 |
| 중거리 | Medium | 4.0–6.0 |
| 원거리 | Long | 6.0–8.0 |
| 저격 | Sniper | 9.0–12.0 |

The current slime and Chaser use Melee distance-band movement, Shooter uses Medium, and Boss uses Long. Close and Sniper are ready for later enemy definitions. Hopper retains its hop movement, which ignores the distance band while hopping.

`EnemyTargeting` computes approach or retreat outside the distance band. `EnemySteering` adds weak local separation from nearby Enemy colliders and checks the navigation grid before moving. `DistanceBandMovementRuntime` applies the result through `EnemyMovementMotor`. Enemies stop approaching inside the band, but may shift gently to separate. There is no slot reservation or orbit behavior.

Enemy–Enemy collision is disabled in the Physics 2D layer matrix. Enemy–World remains enabled. Enemy–Player is currently disabled for crowd movement; `EnemyActor` evaluates melee range or collider contact and deals damage through `PlayerHealth` instead. Player attacks still query Enemy colliders independently of the collision matrix.

For a melee enemy, set `Melee Attack Range` at least as large as `Maximum Distance` if it should hit while stopped. Ranged enemies can use zero to retain contact damage only on physical overlap. `Separation Radius` and `Separation Weight` control crowd spreading without Rigidbody repulsion. The separation query uses a fixed 24-collider buffer; dense crowds may need a spatial index if profiling shows this query becoming expensive.
