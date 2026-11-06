using UnityEngine;

/// The sounds made in code, no sound files
public static class SynthSound
{
    const int Rate = 48000;
    static AudioClip click, buzz;

    static AudioClip Make(string name, float seconds, System.Func<float, float> sample)
    {
        var data = new float[Mathf.RoundToInt(Rate * seconds)];
        for (int i = 0; i < data.Length; i++)
            data[i] = sample(i / (float)Rate);
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// A wooden click: a few ms of a 1.8 kHz knock
    public static AudioClip Click()
    {
        if (click)
            return click;
        var rng = new System.Random(7);
        return click = Make("Start Click", 0.025f, t =>
        {
            float knock = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.7f + ((float)rng.NextDouble() * 2f - 1f) * 0.3f;
            return 0.6f * knock * Mathf.Exp(-t / 0.004f);
        });
    }

    /// 40 ms of a vibration motor: a 220 Hz hum (with its 2nd and 3rd harmonics, the headset speakers are thin below
    /// 200 Hz), 1 ms rise, 10 ms fall
    public static AudioClip Buzz()
    {
        if (buzz)
            return buzz;
        return buzz = Make("Touch Buzz", 0.04f, t =>
        {
            float w = 2f * Mathf.PI * 220f * t;
            float hum = Mathf.Sin(w) + 0.5f * Mathf.Sin(2f * w) + 0.25f * Mathf.Sin(3f * w);
            return 0.55f * hum * Mathf.Min(1f, t / 0.001f) * Mathf.Exp(-t / 0.01f);
        });
    }
}
