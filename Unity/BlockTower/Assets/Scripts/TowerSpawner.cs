using System.Collections.Generic;
using UnityEngine;

/// Builds the block tower at Start, placed at the table-top centre.
/// Layers alternate direction and wood (maple, walnut), three blocks per layer. Every block gets its own
/// thickness within the tolerance, so each game has a different set of loose blocks.
/// Dimensions and mass come from CAD/src/tower_block.py. Units: m, kg.
public class TowerSpawner : MonoBehaviour
{
    /// CAD block: origin at the bottom centre, length along local X
    public GameObject blockPrefab;
    /// The blocks' physics layer ("Blocks"): the only thing a headset hand meets
    public const int BlockLayer = 6;
    /// Wood finish of the even levels (maple, from the bottom) and the odd ones (walnut); null keeps the CAD mesh's own
    public Material blockMaterial, blockMaterialOdd;
    [Min(3)] public int levels = 20;
    /// Blocks side by side in a level; a block is as long as they are wide together
    [Min(2)] public int perLevel = 3;
    /// Block width (28 mm). Block length is perLevel widths
    public float blockWidth = 0.028f;
    /// Nominal block thickness (21 mm), the thickness of the imported mesh
    public float blockThickness = 0.021f;
    /// Max thickness deviation per block (+/-0.3 mm). A thinner block carries no load and slides out easily
    public float thicknessTolerance = 0.0003f;
    /// Hard maple 705 kg/m^3 (even levels), black walnut 610 kg/m^3 (odd levels): rho * 84 x 28 x 21 mm
    public float blockMass = 0.0348f, blockMassOdd = 0.0301f;
    /// Sanded wood on wood
    public float friction = 0.3f;
    /// Hardwood on hardwood, 0.4-0.6 (CAD/checks/tower_mechanics.py section 8)
    public float restitution = 0.5f;
    /// 0 builds a new random tower every game
    public int seed;

    /// Spawned blocks, bottom level first, perLevel per level
    [System.NonSerialized] public readonly List<Rigidbody> blocks = new();
    /// Each block's stacked height in the tower's frame, same order as blocks
    readonly List<float> homeY = new();
    /// A block this far below its stacked height has fallen (half a block width; settling sinks well under 1 mm)
    public float fallDrop = 0.014f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SmallBodyPhysics()
    {
        // Unity's defaults suit metre-sized bodies: a 10 mm contact offset is half a 21 mm block.
        // The TGS solver is set in ProjectSettings (no runtime API); the default PGS lets the tower fall in 1.5 s
        Physics.defaultContactOffset = 0.001f;
        // 20 iterations left the 18-level tower trembling ~120 mm/s and 1.5 mm sunk, like jelly; 60 settled it dead
        // still. About 3 per level plus the table: 64 for 20 levels (CAD/checks/tower_mechanics.py section 9)
        Physics.defaultSolverIterations = 64;
        Physics.defaultSolverVelocityIterations = 4;
        Time.fixedDeltaTime = 1f / 120f;
        // Bounce only real impacts. Below the hand's top speed (0.3 m/s) the stack's own contact corrections
        // bounced too: at 0.15 m/s a 10 mm push fed energy back and burst the tower at 42 m/s
        Physics.bounceThreshold = 1f;
        // wood never springs apart: a solver overlap may separate at hand speed, not the default 10 m/s
        Physics.defaultMaxDepenetrationVelocity = 1f;
    }

    /// A block's length: perLevel widths (84 mm), as wide as the tower
    public float BlockLength => perLevel * blockWidth;

    /// A block's centre off the tower's axis, across (m)
    public float OffAxis(Rigidbody block)
    {
        Vector3 off = block.worldCenterOfMass - transform.position;
        return new Vector2(off.x, off.z).magnitude;
    }

    /// Still in the tower's footprint: centre within 0.4 block lengths (34 mm) of the axis (a block is as long as
    /// the tower is wide)
    public bool InFootprint(Rigidbody block) => OffAxis(block) < 0.4f * BlockLength;
    /// Off the tower: centre just past its half width (0.53 lengths, 45 mm)
    public bool OffTower(Rigidbody block) => OffAxis(block) > 0.533f * BlockLength;
    /// Mostly out: centre 0.8 lengths (67 mm) out, little of it left under the level above
    public bool MostlyOut(Rigidbody block) => OffAxis(block) > 0.8f * BlockLength;

    /// At rest: not moving enough to matter
    public static bool Still(Rigidbody body) => body.linearVelocity.sqrMagnitude < 1e-4f && body.angularVelocity.sqrMagnitude < 0.01f;

    /// A block still standing in the tower (not fallen to a pile)
    public bool Standing(Rigidbody body) => body && body.transform.parent == transform;

    /// The standing block within `radius` of a point, the nearest
    public Rigidbody BlockAt(Vector3 point, float radius)
    {
        Rigidbody best = null;
        float bestDistance = float.MaxValue;
        foreach (var c in Physics.OverlapSphere(point, radius, PlayerCharacter.NotHands, QueryTriggerInteraction.Ignore))
        {
            if (!Standing(c.attachedRigidbody))
                continue;
            float d = Vector3.Distance(c.ClosestPoint(point), point);
            if (d < bestDistance)
                (best, bestDistance) = (c.attachedRigidbody, d);
        }
        return best;
    }

    /// Any block of this game's (standing or piled) within `radius` of a point
    public bool AnyBlockAt(Vector3 point, float radius)
    {
        foreach (var c in Physics.OverlapSphere(point, radius, PlayerCharacter.NotHands, QueryTriggerInteraction.Ignore))
            if (c.attachedRigidbody && blocks.Contains(c.attachedRigidbody))
                return true;
        return false;
    }

    /// A block's place for the log: L(level from the bottom, 0)#(slot)
    public string Label(Rigidbody block)
    {
        int i = blocks.IndexOf(block);
        return i < 0 ? block.name : $"L{i / perLevel}#{i % perLevel}";
    }

    /// How far a point is outside a block (0 inside it)
    public static float Surface(Rigidbody block, Vector3 point)
    {
        var box = block.GetComponent<Collider>();
        return Vector3.Distance(box.ClosestPoint(point), point);
    }

    /// House rule from the official game: never from the top level.
    /// ponytail: also bar the level under an unfinished top once blocks can be stacked back on
    public bool CanTake(Rigidbody block) => Practice || blocks.IndexOf(block) / perLevel < levels - 1;

    /// Practice (the headset menu): one block lying on the table instead of a tower, no rules and no fall, to try
    /// how the hands push and hold
    public static bool Practice;

    /// Game over when the tower has really collapsed: at least this share of the blocks still in it
    /// dropped below their place. A tilt plays on
    [Range(0f, 1f)] public float collapseShare = 0.5f;
    /// Game over when this many blocks fell off the tower onto the table or floor (not counting the one being moved)
    public int fallenLimit = 2;

    /// Game over: fallenLimit blocks fell off, or collapseShare of the blocks still in the tower dropped more than
    /// fallDrop below their stacked height. The moving block and blocks on a pile (no longer children) don't count
    public bool Fallen(Rigidbody moving)
    {
        if (Practice)
            return false;
        int inTower = 0, dropped = 0, off = 0;
        var offList = new System.Text.StringBuilder();
        for (int i = 0; i < blocks.Count; ++i)
        {
            var b = blocks[i];
            if (!b || b == moving || b.transform.parent != transform)
                continue;
            inTower++;
            Vector3 p = b.transform.localPosition;
            if (p.y < homeY[i] - fallDrop)
            {
                dropped++;
                // centre past the tower's half width and below its place: it fell off, wherever it lies
                if (OffTower(b))
                {
                    off++;
                    offList.Append(" ").Append(Label(b));
                }
            }
        }
        if (off < fallenLimit && (inTower == 0 || dropped < collapseShare * inTower))
            return false;
        Debug.Log($"[BT] Game over: {off} blocks fell off ({offList} ), {dropped} of {inTower} dropped");
        return true;
    }

    PhysicsMaterial wood;

    void Start()
    {
        wood = new PhysicsMaterial("Wood")
        {
            dynamicFriction = friction,
            staticFriction = friction,
            bounciness = restitution,
        };
        Build();
    }

    /// Clears every block, wherever it fell or was piled, and stacks a fresh tower (new random thicknesses unless seeded)
    public void Rebuild()
    {
        foreach (var block in blocks)  // piled blocks are no longer children
        {
            if (!block)
                continue;
            block.gameObject.SetActive(false);  // out of this frame's physics; Destroy waits for the frame end
            Destroy(block.gameObject);
        }
        blocks.Clear();
        homeY.Clear();
        Build();
    }

    void Build()
    {
        var rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
        var grainRng = new System.Random();  // looks only: drawing from rng would change a seeded tower
        float levelY = 0f;

        for (int level = 0; level < (Practice ? 1 : levels); ++level)
        {
            bool alongX = level % 2 == 0;
            float thickest = 0f;
            bool odd = level % 2 == 1;
            // Game call: every level has one loose block, the thinnest by at least half the tolerance, so there is
            // always something to find; the others range from -0.5 to +1 tolerance and carry the load
            int loose = rng.Next(perLevel);

            for (int i = 0; i < perLevel; ++i)
            {
                if (Practice && i > 0)
                    break;
                float across = Practice ? 0f : (i - (perLevel - 1) / 2f) * blockWidth;
                float thickness = blockThickness + (i == loose ? -1f : (float)rng.NextDouble() * 1.5f - 0.5f) * thicknessTolerance;
                thickest = Mathf.Max(thickest, thickness);

                var block = Instantiate(blockPrefab, transform);
                block.layer = BlockLayer;
                block.transform.SetLocalPositionAndRotation(
                    alongX ? new Vector3(0f, levelY, across) : new Vector3(across, levelY, 0f),
                    Quaternion.Euler(0f, alongX ? 0f : 90f, 0f));
                block.transform.localScale = new Vector3(1f, thickness / blockThickness, 1f);
                var finish = odd && blockMaterialOdd ? blockMaterialOdd : blockMaterial;
                if (finish)
                {
                    // each block cut from a different spot of the board
                    var grain = new MaterialPropertyBlock();
                    grain.SetVector("_Offset", new Vector4((float)grainRng.NextDouble(), (float)grainRng.NextDouble(), (float)grainRng.NextDouble()));
                    foreach (var r in block.GetComponentsInChildren<MeshRenderer>())
                    {
                        r.sharedMaterial = finish;
                        r.SetPropertyBlock(grain);
                    }
                }

                var box = block.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, blockThickness / 2f, 0f);
                box.size = new Vector3(BlockLength, blockThickness, blockWidth);
                box.sharedMaterial = wood;

                var body = block.AddComponent<Rigidbody>();
                body.mass = (odd ? blockMassOdd : blockMass) * thickness / blockThickness;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // a block dropping off the table moves 33 mm a step and tunnelled through the floor slab
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.Sleep();  // settle quietly; contact wakes it
                blocks.Add(body);
                homeY.Add(levelY);
            }

            // The next level rests on the thickest block; thinner ones are left loose
            levelY += thickest;
        }
    }
}
