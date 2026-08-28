# Pathfinding: the mathematics

How an enemy works out that it must walk *around* the crate rather than into it.

This is the theory behind the navigation added to One Valley. Unity's NavMesh does all of
it for us, but every part of that black box is one of the ideas below, and knowing which
is which is the difference between configuring it and guessing at it.

---

## 1. The problem, stated properly

Let $F$ be the **free space**: the walkable ground with the obstacles removed. For two
points $a, b \in F$, define

$$d_F(a,b) \;=\; \inf \left\{\, \operatorname{length}(\gamma) \;:\; \gamma \subset F,\; \gamma(0)=a,\; \gamma(1)=b \,\right\}$$

That is: of all the ways to get from $a$ to $b$ **without leaving the free space**, the
length of the shortest. This is a real distance function — it is symmetric, non-negative,
and obeys the triangle inequality — and it is emphatically **not** the straight-line
distance.

Straight-line distance says the player is 4 m away, through the crate.
$d_F$ says 11 m, around it.

A curve achieving that infimum is a **geodesic** of the free space. "Geodesic" usually
gets attached to curved surfaces, but the definition is the same in any metric space: the
locally-shortest path available. Here the curvature is zero and it is the *holes* that
make the geometry interesting.

An enemy that walks straight at the player is minimising the wrong metric. That is the
entire bug, stated in one sentence.

---

## 2. Why no amount of tuning fixes local steering

The natural first attempt is a **potential field**. Build a scalar function

$$U(\mathbf{x}) \;=\; \underbrace{k_a \lVert \mathbf{x} - \mathbf{x}_{\text{player}}\rVert^2}_{\text{pulled toward the player}} \;+\; \underbrace{\sum_{i} \frac{k_r}{\lVert \mathbf{x} - \mathbf{o}_i \rVert^2}}_{\text{pushed off the obstacles}}$$

and walk downhill: $\dot{\mathbf{x}} = -\nabla U$.

It is cheap, it needs no memory, and it fails.

Gradient descent stops wherever $\nabla U = \mathbf{0}$. There is such a point at the goal
— but there are **also** such points anywhere the pull forward exactly cancels the push
back, which is precisely what happens in front of a wide flat obstacle. The enemy arrives
at the crate, attraction and repulsion balance, and it stalls. Nudge the constants and the
stall point moves; it does not disappear.

```
            player
              ×
    ┌───────────────────┐
    │                   │   <-  pull is +x, push is -x,
    │       CRATE       │       they cancel exactly here
    └───────────────────┘
              ●  <- enemy, stuck, ∇U = 0
```

This is structural, not a tuning failure. The choice *"left or right around this crate"*
cannot be made from the gradient at the enemy's feet, because both directions look
identical there. It depends on where the crate **ends**, which is information that exists
metres away.

> Steering is local. Pathfinding is a search over global structure.
> No amount of tuning turns one into the other.

Everything below is about acquiring that global information cheaply.

---

## 3. The theorem that makes the problem finite

The space of curves from $a$ to $b$ is infinite-dimensional. We cannot search it. But we
do not have to, because of this:

> **Theorem.** In a polygonal free space, a shortest path is a *polyline* whose interior
> vertices are all **convex corners of obstacles**.

**Proof.** Suppose the shortest path bends at some point $p$ that is not touching an
obstacle. Since $p$ is in the open free space, some disc $D$ of radius $\varepsilon$
around $p$ lies entirely in the free space. The path enters $D$ at $u$ and leaves at $v$.
Replace the portion between them with the straight segment $uv$ — which stays inside $D$,
hence inside the free space, and by the triangle inequality is *strictly* shorter unless
the path was already straight there. So the path was not shortest. Contradiction.

Therefore every bend touches an obstacle. And a bend in the middle of a flat obstacle
*edge* can be short-cut the same way. So bends occur **only at corners**, and only at
corners where the obstacle actually blocks the shortcut — that is, corners that are convex
as seen from the obstacle. $\blacksquare$

The physical statement is nicer. Stretch a **rubber band** from the enemy to the player
and let it snap taut. It runs straight until something stops it, wraps that corner, and
runs straight again. The shortest path is the taut band.

```
    ●────────────╮
    enemy        │  ← runs straight until the corner blocks it
        ┌────────┴──┐
        │   CRATE   │
        └───────────┘
                 ╰──────× player
```

The consequence is enormous: **the answer is a finite sequence of corners.** We have gone
from searching a space of curves to searching a graph.

---

## 4. Visibility graphs — mechanising the theorem directly

The theorem says the path only ever visits corners, so build a graph of exactly those:

- **Nodes** — the start, the goal, and every convex obstacle corner.
- **Edges** — join two nodes when the straight segment between them crosses no obstacle
  (they can "see" each other).
- **Weights** — plain Euclidean length of that segment.

Then run a shortest-path search on the graph. The result is **exactly optimal**, not an
approximation, because by the theorem the true geodesic is guaranteed to be one of the
paths in this graph.

One necessary refinement: the enemy is a body, not a point. If you path a point, the
enemy's shoulders clip the corner. The fix is to **inflate every obstacle by the enemy's
radius $r$** before building the graph — formally the Minkowski sum $O \oplus D_r$ of the
obstacle with a disc of radius $r$. The enemy can then be treated as a point, and the
corners it rounds sit $r$ away from the real ones.

Cost: $n$ corners gives $O(n^2)$ candidate edges, each needing a visibility test. Fine for
tens of corners, hopeless for thousands. It is exact and simple; it does not scale.

---

## 5. Navigation meshes — decomposing the free space

The scalable alternative inverts the idea. Rather than enumerating the corners of the
obstacles, cover the **free space itself** with convex cells — in practice, triangles.

Convexity is the whole point:

> Inside a convex region, the straight segment between any two points stays inside the
> region.

So *within* one cell, movement is trivially safe and needs no thought. All the difficulty
is reduced to the question of **which cells to cross, in what order**.

That gives a graph again — the **dual graph** of the mesh. One node per triangle, an edge
between triangles that share a wall. Searching it yields a **channel**: an ordered corridor
of triangles from start to goal.

```
   ┌───────────────────────────┐
   │ ╲  1  │  3  ╱ │           │
   │  ╲────┼────╱  │  CRATE    │      channel: 1 → 2 → 3 → 5
   │ 2 ╲   │   ╱ 4 │           │
   │────╲──┼──╱────┼───────────┤
   │  5  ╲ │ ╱  6  │           │
   └───────────────────────────┘
```

A channel is not yet a path. It is the *homotopy class* of the answer — it says which side
of the crate to pass, which is exactly the global information a potential field could never
supply. Turning it into an actual path is §7.

An honest caveat: searching the dual graph using distances between triangle centroids does
**not** provably select the corridor containing the global optimum. In practice the
difference is negligible; formally, the visibility graph is exact and this is not.

---

## 6. Searching the graph: Dijkstra, then A\*

Both methods leave us with a weighted graph and a shortest-path query.

**Dijkstra's algorithm** repeatedly expands the unvisited node with the smallest known
distance from the start. It is correct because edge weights are non-negative: once a node
is expanded, no later route can beat the one already found. Its weakness is that it expands
outward in all directions equally — it has no idea where the goal is.

**A\*** fixes that by adding an estimate of the remaining distance. It expands the node
minimising

$$f(n) \;=\; g(n) \;+\; h(n)$$

where $g(n)$ is the known cost from the start and $h(n)$ is a guess at the cost to the
goal. Set $h = 0$ and A\* *is* Dijkstra; the better $h$ is, the more directly the search
drives at the goal.

The guess cannot be arbitrary. Two properties matter:

**Admissible** — $h$ never overestimates: $h(n) \le d_F(n,\text{goal})$ for all $n$.
Straight-line distance qualifies, and the reason is exactly the observation we started
with: obstacles can only ever make the real path *longer* than the straight line, so

$$h(n) = \lVert n - \text{goal} \rVert_2 \;\le\; d_F(n, \text{goal})$$

**Consistent** — $h(n) \le w(n,m) + h(m)$ for every edge. Euclidean distance satisfies
this automatically, because it obeys the triangle inequality. Consistency implies
admissibility and additionally guarantees that once a node is expanded its $g$ is already
optimal, so nothing is ever re-expanded.

**Why admissibility gives optimality.** Let $C^*$ be the true optimal cost, and suppose A\*
is about to expand the goal via a path costing $g(\text{goal}) > C^*$. Take any optimal
path; since it is not yet fully expanded, some node $n$ on it is still on the open list,
reached optimally, so $g(n) + h(n) \le g(n) + d_F(n,\text{goal}) = C^*$. Then

$$f(n) \;\le\; C^* \;<\; g(\text{goal}) \;=\; f(\text{goal})$$

so A\* would have expanded $n$ first, and never popped the suboptimal goal. Contradiction —
the returned path is optimal. $\blacksquare$

That proof is why the straight-line heuristic is not a heuristic in the loose sense. It is a
provable lower bound, and A\* is exact because of it.

---

## 7. The funnel algorithm — pulling the string taut

A channel of triangles gives a wide corridor. Naively walking centroid-to-centroid produces
a drunk, zig-zagging path that visibly hugs invisible triangle boundaries. We want the taut
rubber band of §3, restricted to the corridor.

The **funnel algorithm** (also "string pulling", or the Simple Stupid Funnel Algorithm)
computes it in a single linear pass.

Maintain three things:

- an **apex** — the last point the path is confirmed to pass through;
- a **left ray** and a **right ray** from the apex, forming a wedge (the *funnel*) of
  directions still reachable without bending.

Then walk the corridor's shared edges in order. For each new edge, with left endpoint $L$
and right endpoint $R$:

1. If $L$ narrows the wedge (lies inside it), tighten the left ray onto $L$. Same for $R$
   on the right.
2. If tightening one side would push it **past the other side**, the funnel has closed. The
   path cannot continue straight — it is physically blocked. So the opposite side's vertex
   becomes a **corner of the final path**: emit it, move the apex there, and restart the
   funnel from that apex.

Step 2 is the taut-string theorem, implemented. The rays crossing is the geometric signature
of "the band has caught on a corner."

```
      apex ●─────────────── left ray
            ╲    funnel
             ╲──────────── right ray

   left and right cross  ⇒  band snags  ⇒  apex jumps to that corner
```

Each vertex enters and leaves the funnel once, so the whole thing is $O(k)$ in the corridor
length. The output is the exact shortest path **within that corridor** — genuinely optimal,
not smoothed or approximated.

---

## 8. What the terrain's curvature actually costs

Everything so far assumed a flat plane. One Valley's ground is a heightfield, so here is the
honest differential geometry.

The surface is $z = h(x,y)$. Parametrise by $(x,y)$; the tangent vectors are
$\mathbf{r}_x = (1,0,h_x)$ and $\mathbf{r}_y = (0,1,h_y)$. The induced metric — the first
fundamental form — is

$$g \;=\; \begin{pmatrix} 1 + h_x^2 & h_x h_y \\ h_x h_y & 1 + h_y^2 \end{pmatrix}$$

and the length of a path across the terrain is

$$L[\gamma] \;=\; \int \sqrt{g_{ij}\, \dot{x}^i \dot{x}^j}\; dt$$

Curves that make $L$ stationary satisfy the geodesic equation

$$\ddot{x}^k + \Gamma^k_{ij}\, \dot{x}^i \dot{x}^j = 0$$

with the Christoffel symbols $\Gamma^k_{ij}$ built from derivatives of $g$.

**And you should never integrate that.** Two reasons, both practical:

**First**, on a graph, minimising that integral is *exactly* the same as weighting each edge
by its **3D length rather than its 2D length**. The continuous geodesic is precisely the
limit of the discrete problem as the mesh refines. All that beautiful machinery collapses to
"measure the edges in 3D" — one change, no ODE solver.

**Second, and more interestingly, the true geodesic is not even what we want.** A creature
walking uphill is slower than one walking downhill. The cost of traversing a direction
depends on *which way you are going*, not merely on where you are. A metric with that
property is **Finsler**, not Riemannian: the cost function $F(\mathbf{x}, \mathbf{v})$
depends on the direction $\mathbf{v}$ and is not symmetric under $\mathbf{v} \to -\mathbf{v}$.

Finsler geometry in the continuum is genuinely hard. On a graph it is free: give the edge
$u \to v$ a different weight from $v \to u$. Dijkstra and A\* never assumed symmetry.

So terrain curvature enters as an **edge weight**, never as a different algorithm. Unity
expresses the same idea as *area costs* and a *slope limit* — a coarse discretisation of
exactly this.

---

## 9. Obstacles that move

Pillars rise between rounds and the Warden shatters them mid-fight. A path computed against
last second's world may now run through a wall, or detour around one that no longer exists.

Two mechanisms handle this.

**Carving.** A moving obstacle cuts a hole in the mesh at its current position, and the
affected cells are re-triangulated locally. Only the neighbourhood is rebuilt, not the whole
mesh — which is what makes it affordable every frame.

**Replanning.** Any agent whose path crosses the changed region recomputes. The cheap and
robust version is simply to re-run the search periodically — every few hundred milliseconds
rather than every frame, since a stale path is wrong by centimetres, not metres. (The
sophisticated alternative is incremental repair, D\* Lite and relatives, which reuse the
previous search tree. Overkill here.)

There is a subtlety worth knowing: an obstacle that carves the mesh **shut** can strand an
agent inside solid geometry, with no valid cell beneath it. Robust systems detect that and
push the agent to the nearest valid point rather than letting it fall through the world.

---

## 10. Where each idea lives in Unity

Unity's navigation is this document with different names.

| The maths | Unity's name |
|---|---|
| Convex decomposition of free space (§5) | the **NavMesh**, produced by baking |
| Minkowski inflation by body radius (§4) | agent **Radius** |
| Traversability constraints on the metric (§8) | **Slope Limit**, **Step Height** |
| Direction-dependent edge weights (§8) | **Area costs** |
| Dual-graph A\* (§6) | `CalculatePath` / `SetDestination` internals |
| Funnel / string pulling (§7) | `NavMeshPath.corners` |
| Local carving for movers (§9) | `NavMeshObstacle` with **Carve** enabled |
| Reciprocal collision avoidance between agents | **Obstacle Avoidance** quality |

The last row is the one piece **not** covered above. Agents avoiding *each other* is a
different problem from avoiding scenery: it is a many-body, real-time, symmetric negotiation
rather than a static search, usually solved with reciprocal velocity obstacles (RVO). Each
agent picks a velocity outside the cone of velocities that would lead to a collision, and
takes responsibility for half the avoidance, trusting the other to take the rest — which is
what stops two agents endlessly mirroring each other. It matters here because round four
puts thirteen enemies on the field at once.

---

## 11. Costs at a glance

| | Build | Query | Exact? |
|---|---|---|---|
| Potential field | — | $O(1)$ | **No** — gets stuck (§2) |
| Visibility graph | $O(n^2 \log n)$ | $O(n^2)$ | Yes |
| NavMesh + A\* + funnel | once, offline | $O(k \log k)$ in cells visited | Optimal within its corridor |

$n$ = obstacle corners, $k$ = cells the search touches. The NavMesh wins because $k$ depends
on how far apart the two points are, not on how complicated the world is.

---

## Summary

1. "Walk around the obstacle" means minimising **geodesic distance in the free space**, not
   Euclidean distance.
2. No **local** rule can do this, because the left-or-right decision depends on information
   that is not present at the agent's feet.
3. Shortest paths are **taut strings**, bending only at obstacle corners — which turns an
   infinite search into a finite graph search.
4. Either enumerate the corners (**visibility graph**) or decompose the free space
   (**NavMesh**) to obtain that graph.
5. Search it with **A\***, whose straight-line heuristic is a provable lower bound and
   therefore returns genuinely optimal paths.
6. Convert the resulting corridor into a real path with the **funnel algorithm**.
7. Terrain slope and one-way costs are **edge weights**, not a harder algorithm.
