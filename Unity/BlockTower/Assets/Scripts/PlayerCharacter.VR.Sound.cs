using UnityEngine;

/// A bare hand has no vibration motor: a fingertip meeting a block buzzes once at the fingertip instead, so the
/// player hears where the finger stopped (the hand feels nothing there)
public partial class PlayerCharacter
{
    /// How loud the buzz is (0-1)
    public float touchBuzzVolume = 0.6f;
    /// A fingertip off the block at least this long buzzes again on the next touch, s: tracking jitter on a resting
    /// finger breaks contact for a step or two and would buzz every time
    const float BuzzRearm = 0.08f;

    /// Buzzes when a fingertip comes onto a block (not the one it holds), once per touch
    void TouchBuzz(VrHand h, Tip t, Rigidbody touched)
    {
        bool on = touched && touched != h.held;
        if (on && !t.touching && Time.time - t.offAt > BuzzRearm)
        {
            if (!h.buzz)
            {
                h.buzz = new GameObject($"{h.node} Buzz").AddComponent<AudioSource>();
                h.buzz.spatialBlend = 1f;  // from the fingertip
                h.buzz.minDistance = 0.1f;
                h.buzz.dopplerLevel = 0f;
                h.buzz.playOnAwake = false;
            }
            h.buzz.transform.position = t.ball.transform.position;
            h.buzz.PlayOneShot(SynthSound.Buzz(), touchBuzzVolume);
        }
        if (!on && t.touching)
            t.offAt = Time.time;
        t.touching = on;
    }
}
