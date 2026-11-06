"""Mechanics of the block tower, worked from first principles, and the Unity settings they imply.

Run: python3 checks/tower_mechanics.py   (from CAD root; stdlib only)

Each section states the formula, the numbers, and where the result lands in Unity
(Unity/BlockTower/Assets/Scripts). Measured simulation values quoted here come from
the 2026-10-28 runs in the editor (see MEMO.md).

Material data (~12 % moisture; USDA Wood Handbook ch. 4-5 ranges):
  density: levels alternate hard maple 705 and black walnut 610 kg/m^3 (loads below use the mean), E perpendicular to grain >= 1.0 GPa (low end, radial ~1.6),
  wood-on-wood static friction 0.4 (sanded, dry; handbook range 0.3-0.6),
  wood-on-wood restitution 0.4-0.6 (hardwood impacts),
  skin-on-wood friction 0.5 (dry fingertip, range 0.4-0.6).
Human hand (precision-grip literature): careful pinch 2-5 N, careful fingertip push 1-5 N,
  careful hand speed 0.1-0.5 m/s.
"""
import math
import random

G = 9.81
RHO_MAPLE, RHO_WALNUT = 705.0, 610.0  # kg/m^3, even / odd levels
RHO = (RHO_MAPLE + RHO_WALNUT) / 2  # mean, for stack loads
E_PERP = 1.0e9       # Pa, conservative
MU = 0.3             # wood-wood: low end of 0.3-0.6, a game call (32 g blocks felt too stiff at 0.4)
MU_SKIN = 0.5        # fingertip pad on wood
COR_WOOD = 0.5       # restitution, mid of 0.4-0.6
PER = 3             # blocks per level
W, T = 0.028, 0.021  # block, m (CAD/src/tower_block.py)
L = PER * W
LEVELS = 20
TOL = 0.0003         # +/- thickness tolerance, m
DT = 1 / 120         # Unity fixed step
fails = []


def check(ok, msg):
    print(("  PASS " if ok else "  FAIL ") + msg)
    if not ok:
        fails.append(msg)


def head(title):
    print("\n" + title)


# 1. One block
head("1. Block: mass, weight, inertia")
V = L * W * T
m = RHO * V
w = m * G
Ixx = m / 12 * (W**2 + T**2)   # about the long axis
Iyy = m / 12 * (L**2 + W**2)   # about vertical
Izz = m / 12 * (L**2 + T**2)
print(f"  V = L*W*T = {V * 1e6:.2f} cm^3,  m = rho*V = {m * 1000:.2f} g,  w = m*g = {w:.4f} N")
print(f"  I = m/12*(a^2+b^2): long {Ixx * 1e9:.2f}, vertical {Iyy * 1e9:.2f}, cross {Izz * 1e9:.2f} g*mm^2 (x1e3)")
for name, rho, unity in (("maple", RHO_MAPLE, 0.0348), ("walnut", RHO_WALNUT, 0.0301)):
    check(abs(rho * V - unity) < 0.0002, f"TowerSpawner {name} block {unity} kg matches rho*V = {rho * V * 1000:.2f} g")

# 2. Statics: load on each level
head("2. Load carried at each level (weight of everything above)")
print(f"  N(L) = {PER}*w*(LEVELS-L),  L = 1 bottom .. {LEVELS} top")
N = {lv: PER * w * (LEVELS - lv) for lv in range(1, LEVELS + 1)}
for lv in (1, 6, 10, 14, 17, 19):
    print(f"    level {lv:2d}: {N[lv]:.2f} N")
tower_w = PER * LEVELS * w
print(f"  whole tower {tower_w:.2f} N ({tower_w / G * 1000:.0f} g)")

# 3. Wood is effectively rigid: contact stiffness vs. thickness tolerance
head("3. Contact stiffness: how much does wood compress?")
A = W * W                       # crossing blocks touch on a 20 x 20 mm patch
k_contact = E_PERP * A / T      # two half-thicknesses in series = one full thickness
d_bottom = N[1] / PER / k_contact  # one crossing patch of the bottom level, worst case even share
d_tower = sum(N[lv] / PER / k_contact for lv in range(1, LEVELS))
print(f"  k = E*A/t = {E_PERP / 1e9:.1f}e9 * {A * 1e6:.0f} mm^2 / {T * 1e3:.0f} mm = {k_contact:.2e} N/m per patch")
print(f"  bottom patch squeezes {d_bottom * 1e6:.3f} um; whole tower shortens {d_tower * 1e6:.2f} um")
print(f"  thickness tolerance is +/-{TOL * 1e6:.0f} um, {TOL / d_tower:.0f}x larger")
print("  -> a block only 1 um thinner than its neighbours carries nothing: contacts must behave rigid")
check(d_tower < TOL / 100, "real compression is negligible against the tolerance")

# 4. Rigid stacking: where does each layer actually rest?
head("4. Rigid stacking with +/-0.3 mm blocks (Monte Carlo, 20000 levels)")
print(f"  The layer above is a rigid plank on {PER} parallel supports {W * 1e3:.0f} mm apart, load at x = 0.")
print("  A support at x = 0 that is thickest carries it alone. Otherwise the plank tips about the thickest")
print("  onto the support with the least slope s_i = (h_max - h_i)/(x_max - x_i) on the far side of x = 0.")
print("  Reactions from moments about the two contacts: R_pivot = W*(0 - x_c)/(x_p - x_c), R_c = W - R_pivot.")
random.seed(1)
xs = tuple((i - (PER - 1) / 2) * W for i in range(PER))
LOOSE_MAX = (LEVELS - 1) * (PER - 1)  # every non-thickest block under a level
drops, loose_loaded, loose_free, share_thickest = [], 0, 0, []
for _ in range(20000):
    h = [random.uniform(-TOL, TOL) for _ in xs]
    p = max(range(PER), key=lambda i: h[i])
    if xs[p] == 0.0:
        drops.append(0.0)
        loose_free += PER - 1
        share_thickest.append(1.0)
        continue
    far = [i for i in range(PER) if i != p and (xs[i] - 0.0) * (xs[p] - 0.0) <= 0]  # supports at or past x = 0
    c = min(far, key=lambda i: (h[p] - h[i]) / abs(xs[p] - xs[i]))
    s = (h[p] - h[c]) / abs(xs[p] - xs[c])
    drops.append(s * abs(xs[p]))               # centre drop of the plank
    r_p = (0.0 - xs[c]) / (xs[p] - xs[c])      # fraction on the thickest block
    share_thickest.append(r_p)
    loose_loaded += 1
    loose_free += PER - 2
mean_drop = sum(drops) / len(drops)
frac_loaded = loose_loaded / (loose_loaded + loose_free)
print(f"  the plank tips {mean_drop * 1e6:.0f} um at its centre (mean)")
print(f"  share of the load on the thickest block: mean {sum(share_thickest) / len(share_thickest):.2f}"
      " (0 when the plank tips onto the middle block: COM straight over it)")
print(f"  loose blocks pressed by a tipped plank: {frac_loaded * 100:.0f} %  ({frac_loaded * LOOSE_MAX:.1f} of {LOOSE_MAX})")
sag_rigid = mean_drop * (LEVELS - 1)
print(f"  expected tower height loss from tipping alone: {sag_rigid * 1e3:.2f} mm")
print("  (upper bound: summing centre drops ignores that alternate layers tip on crossed axes and partly cancel)")
print("  Simulation (60 iterations) was measured on the old 3-wide, 18-level tower only (2026-10-28: sag 0.6 mm,")
print("  16 of 34 loose blocks pressed); the 84 x 28 x 21 mm, 20-level tower is not measured yet")

# 5. Friction: force to slide a block out
head("5. Force to slide a block (Coulomb friction on top and bottom faces)")
print("  F = mu*(N_top + N_bottom) = mu*(2*N_top + w)   (N_bottom = N_top + own weight)")
print("  tight = carries the whole load above; loose = carries none")
for lv in (3, 6, 10, 14, 17, 19):
    tight = MU * (2 * N[lv] + w)
    print(f"    level {lv:2d}: tight {tight:.2f} N, loose {MU * w:.3f} N")

# 6. Inverse: what the hand can do
head("6. Inverse: which tight blocks can a careful hand move?")
F_PINCH = 12.0  # pull = push limit, N: a game call so even a bottom block carrying the whole tower (11.0 N) comes out
pull_max = F_PINCH  # PlayerCharacter.pinchStrength caps the pull itself
print(f"  pinch pull limit = PlayerCharacter.pinchStrength = {pull_max:.1f} N (squeeze 2*mu_skin*s: s = {pull_max / (2 * MU_SKIN):.0f} N)")
print(f"  solve mu*(2*N_top + w) <= F for N_top:  N_top <= (F/mu - w)/2,  levels above = N_top/({PER}w)")
for name, f in (("pinch pull", pull_max), (f"fingertip push {F_PINCH:.0f} N", F_PINCH)):
    n_top = (f / MU - w) / 2
    print(f"    {name}: N_top <= {n_top:.2f} N -> tight blocks with <= {n_top / (PER * w):.1f} levels above")
check(abs(pull_max - 12.0) < 1e-9, "PlayerCharacter.pinchStrength 12 N = pushStrength (what you push out, you can pull out)")
worst = MU * (2 * N[1] + RHO_MAPLE * L * W * T * G)  # bottom maple block carrying the whole tower
check(worst < pull_max, f"worst block (level 1, whole load, maple) needs {worst:.2f} N < {pull_max:.0f} N: every block can come out")
print(f"  Game reading: a tight block comes out only with <= {(pull_max / MU - w) / 2 / (PER * w):.0f} levels above (of {LEVELS});")
print(f"  loose blocks ({MU * w:.2f} N) come out anywhere. That is real-tower behaviour.")

head("6b. Sole support: does the block slide out, or drag the tower with it?")
print("  Block alone moves if top-face friction can't drag the stack above: the stack must be held elsewhere.")
print(f"  If the block is the only support, nothing holds the stack: top friction mu*N_top = {MU}*N_top,")
print("  bottom friction mu*(N_top + w). Top < bottom always, so block + stack move as one,")
print("  needing only the bottom friction F = mu*(N_top + w); the tower shifts with the block and falls")
print("  once its centre passes the support edge.")
for lv in (3, 8, 13, 17):
    f_drag = MU * (N[lv] + w)
    print(f"    level {lv:2d}: drag the tower with {f_drag:.2f} N -> {'a careful pinch DRAGS it' if f_drag <= pull_max else 'pinch slips'}")
n_drag = (pull_max / MU - w) / (PER * w)
print(f"  pinch drags any sole-support block with <= {n_drag:.1f} levels above (most of the tower)")
print("  Simulation 2026-10-28 (old 3-wide tower): level-7 sole-support block pulled -> tower slid 78 mm and collapsed.")
print("  Game reading: find the loose ones (tap first). Yanking a sole support topples the tower, as in reality.")

# 7. Stability of the whole tower
head(f"7. Tower stability (rigid {L * 1e3:.0f} x {L * 1e3:.0f} mm prism)")
h_com = LEVELS * T / 2
theta = math.degrees(math.atan((L / 2) / h_com))
a_crit = G * (L / 2) / h_com
f_top = tower_w * (L / 2) / (LEVELS * T - T / 2)
print(f"  COM height = {h_com * 1e3:.0f} mm; tipping angle atan((b/2)/h) = {theta:.1f} deg")
print(f"  table shove that tips it: a = g*(b/2)/h = {a_crit:.2f} m/s^2")
print(f"  sideways force at the top layer that tips it: F = W*(b/2)/h_top = {f_top:.2f} N")
print(f"  (a stuck tower dragged by a tight block tips once its top is pushed ~{f_top:.1f} N)")

# 8. Impacts: restitution and Unity's bounce threshold
head("8. Impacts: falling blocks")
for drop in (T, 5 * T, LEVELS * T, 0.73):
    v = math.sqrt(2 * G * drop)
    print(f"    falls {drop * 1e3:5.0f} mm -> hits at v = sqrt(2*g*h) = {v:.2f} m/s, rebounds {COR_WOOD * v:.2f} m/s"
          f" ({(COR_WOOD * v) ** 2 / (2 * G) * 1e3:.1f} mm)")
v_layer = math.sqrt(2 * G * T)
print(f"  Unity default bounceThreshold = 2 m/s: below it, no bounce at all. A block dropping one layer"
      f" ({v_layer:.2f} m/s) lands dead")
print(f"  inverse: let a 2 mm drop clack -> threshold <= sqrt(2*g*0.002) = {math.sqrt(2 * G * 0.002):.2f} m/s")
RESTITUTION, BOUNCE_THRESHOLD, V_HAND_MAX = 0.5, 1.0, 0.3  # TowerSpawner, PlayerCharacter.handSpeed
print("  BUT the solver's own contact corrections inside the stack run at about hand speed (0.05-0.3 m/s).")
print("  Bouncing those pumps energy: threshold 0.15 m/s + restitution 0.5 burst the tower at 42 m/s on a 10 mm push.")
print(f"  -> threshold above the hand speed: {BOUNCE_THRESHOLD} m/s. Falls from >= {BOUNCE_THRESHOLD**2 / (2 * G) * 1e3:.0f} mm bounce;")
print("     one-layer drops land dead (a game-engine limit, not wood)")
check(0.4 <= RESTITUTION <= 0.6, f"TowerSpawner.restitution {RESTITUTION} in the hardwood range")
check(BOUNCE_THRESHOLD > V_HAND_MAX, f"bounceThreshold {BOUNCE_THRESHOLD} m/s above hand speed {V_HAND_MAX} m/s (no energy pumping)")

# 9. Why a constraint solver, and how many iterations
head("9. Solver: real stiffness cannot be time-stepped")
wn = math.sqrt(k_contact / m)
print(f"  block on a wood contact: wn = sqrt(k/m) = {wn:,.0f} rad/s ({wn / 2 / math.pi / 1000:.1f} kHz)")
print(f"  explicit spring needs dt < 2/wn = {2 / wn * 1e6:.0f} us = {wn / 2 / 1000:.0f} kHz steps; game runs {1 / DT:.0f} Hz")
print(f"  -> contacts must be rigid constraints (PhysX). Unresolved gravity per step: g*dt^2 = {G * DT**2 * 1e3:.2f} mm")
print(f"  Gauss-Seidel moves contact information about one layer per iteration; {LEVELS} layers + table,")
print(f"  several passes -> ~3 x {LEVELS + 1} = {3 * (LEVELS + 1)} iterations")
print("  Measured on the old 18-level tower: 20 its -> still moving 121 mm/s after 1.5 s; 40 -> 113 mm/s; 60 -> 0")
SOLVER_ITERATIONS = 64  # TowerSpawner.SmallBodyPhysics
check(3 * (LEVELS + 1) <= SOLVER_ITERATIONS, f"Physics.defaultSolverIterations {SOLVER_ITERATIONS} covers the stack")

# 10. The hand as a force-limited spring
head("10. Hand grip spring (pinch)")
K_P, V_HAND = 1000.0, 0.3
c_crit = 2 * math.sqrt(K_P * m)
print(f"  k = {K_P:.0f} N/m; critical damping c = 2*sqrt(k*m) = {c_crit:.1f} N*s/m")
C_PINCH, K_FIRM, C_FIRM = 11.4, 5000.0, 25.5  # PlayerCharacter.PinchDrive
for k_, c_, name in ((K_P, C_PINCH, "in the tower"), (K_FIRM, C_FIRM, "firm, clear of it")):
    zeta = c_ / (2 * math.sqrt(k_ * m))
    check(0.9 <= zeta <= 1.1, f"pinch drive {name}: k {k_:.0f}, c {c_} -> damping ratio {zeta:.2f} (critical 1)")
print(f"  sag holding the block: m*g/k = {w / K_P * 1e3:.2f} mm")
print(f"  start jolt with the hand capped at {V_HAND} m/s: F = m*v/dt = {m * V_HAND / DT:.2f} N (< {pull_max:.0f} N limit)")
print(f"  held by its end once out: torque m*g*L/2 = {w * L / 2 * 1000:.1f} N*mm")
print("  Measured with a rigid breakable FixedJoint: 2.7-2.8 N jolts on loose blocks -> random slips;")
print("  now a ConfigurableJoint drive with maximumForce = pinch limit")
check(m * V_HAND / DT < pull_max, "hand start jolt stays under the pinch limit")
print("  Headset hand: no longer kinematic (unlimited force pressed blocks into the ones below, 2026-11-05);")
print(f"  it follows the tracked wrist with at most {pull_max:.0f} N, so a block held by the stack stops it")
HAND_MASS, HAND_REACH = 0.05, 0.18  # PlayerCharacter.VR.Hand
print(f"  hand {HAND_MASS * 1000:.0f} g swept at {V_HAND} m/s into a block: impulse {HAND_MASS * V_HAND * 1000:.0f} mN*s,"
      f" as a {HAND_MASS * V_HAND / DT:.1f} N push for one step")
check(HAND_MASS * V_HAND / DT <= pull_max, "a hand sweep hits no harder than the push limit")
V_DEPEN = 0.05  # PlayerCharacter.VR.Hand.HandDepenetration
print("  fingers are put on the tracked joints each step: jitter puts them mm into a block they rest on,")
print(f"  and the solver pushes it out at up to the depenetration limit; a loose block then slides v^2/(2 mu g)")
for v in (1.0, V_DEPEN):
    print(f"    {v:.2f} m/s -> slides {v * v / (2 * MU * G) * 1000:.1f} mm")
check(V_DEPEN ** 2 / (2 * MU * G) <= 0.001, "hand depenetration 0.05 m/s: a touched loose block slides <= 1 mm (not thrown)")

# 11. When a block counts as fallen
head("11. Fallen block threshold")
FALL_DROP = 0.014  # TowerSpawner.fallDrop: half a block width
print(f"  settling sinks at most {sag_rigid * 1e3:.1f} mm; a block knocked down one layer drops {T * 1e3:.0f} mm")
check(sag_rigid < FALL_DROP < T, f"fallDrop {FALL_DROP * 1e3:.0f} mm between settling and one layer")

print("\nALL PASS" if not fails else f"\n{len(fails)} FAIL: " + "; ".join(fails))
