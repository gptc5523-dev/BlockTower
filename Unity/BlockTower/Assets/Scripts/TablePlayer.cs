using UnityEngine;

/// Someone at the table taking turns at the tower (PlayerCharacter, mouse or headset). TowerGame keeps the turns
/// and scores over these
public abstract class TablePlayer : MonoBehaviour
{
    /// 0..3: the seat colour (name tag, shirt) and score row; -1 for the editor's or the headset's own player
    [System.NonSerialized] public int seat = -1;
    /// Blocks taken this round
    [System.NonSerialized] public int taken;

    public abstract string DisplayName { get; }
    /// Played on this device (false: another Quest's, drawn from what it sends)
    public virtual bool Local => true;
    /// The block this player last moved: the fall check lets it lie where it fell
    public abstract Rigidbody Moving { get; }
    /// The tower fell: let go
    public abstract void TowerFell();
    /// A new tower is being stacked: drop whatever is held
    public abstract void NewRound();

    static MaterialPropertyBlock tintBlock;

    /// Multiplies the block's colour by `hue` (white puts it back), keeping its per-block grain offset
    protected static void Tint(Component target, Color hue)
    {
        tintBlock ??= new MaterialPropertyBlock();
        foreach (var r in target.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(tintBlock);
            tintBlock.SetColor("_Color", r.sharedMaterial.GetColor("_Color") * hue);
            r.SetPropertyBlock(tintBlock);
        }
    }
}
