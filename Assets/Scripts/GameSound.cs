using UnityEngine;
using System.Collections.Generic;

// Every sound in the game is played through here.
//
// The same idea as GameInput: one place that knows the messy details, and plainly named
// questions everywhere else. Combat code asks to play "HitEnemy" and never has to know
// there are three recordings of it, where they live, or how loud they should be.
//
// Clips are named by WHAT THEY ARE FOR rather than what they were recorded from, and
// anything ending _0, _1, _2 is an alternative for the same event. One is chosen at
// random each time, which is the single cheapest way to stop a sound that plays forty
// times a minute becoming unbearable.
public static class GameSound
{
    // Loaded once and kept, because loading from Resources mid-fight causes a hitch.
    private static Dictionary<string, List<AudioClip>> clipsByName;

    // A pool of sources, so several sounds can overlap. One source would cut every sound
    // off the moment the next one started.
    private static AudioSource[] sourcePool;
    private static int nextSourceIndex = 0;
    private const int PoolSize = 12;

    private static GameObject soundHolder;

    // Sounds that would otherwise stack into a wall of noise when several enemies do the
    // same thing on the same frame.
    private static Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>();
    private const float SmallestGapBetweenRepeats = 0.04f;

    private static void SetUpIfNeeded()
    {
        if (clipsByName != null && soundHolder != null)
        {
            return;
        }

        clipsByName = new Dictionary<string, List<AudioClip>>();

        AudioClip[] everything = Resources.LoadAll<AudioClip>("Audio");
        int index = 0;
        while (index < everything.Length)
        {
            AudioClip clip = everything[index];

            // "HitEnemy_2" belongs in the "HitEnemy" bucket.
            string baseName = clip.name;
            int underscore = baseName.LastIndexOf('_');
            if (underscore > 0)
            {
                baseName = baseName.Substring(0, underscore);
            }

            if (clipsByName.ContainsKey(baseName) == false)
            {
                clipsByName[baseName] = new List<AudioClip>();
            }
            clipsByName[baseName].Add(clip);

            index = index + 1;
        }

        soundHolder = new GameObject("GameSound");
        Object.DontDestroyOnLoad(soundHolder);

        sourcePool = new AudioSource[PoolSize];
        int poolIndex = 0;
        while (poolIndex < PoolSize)
        {
            AudioSource source = soundHolder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // Flat 2D by default. Positional sound is opt-in through PlayAt.
            source.spatialBlend = 0f;
            sourcePool[poolIndex] = source;
            poolIndex = poolIndex + 1;
        }
    }

    // Plays a sound with no position - interface noises, round banners, the player's own
    // actions, which should sound the same wherever they happen.
    public static void Play(string soundName, float volume)
    {
        AudioClip clip = ChooseAClip(soundName);
        if (clip == null)
        {
            return;
        }

        AudioSource source = NextFreeSource();
        source.transform.localPosition = Vector3.zero;
        source.spatialBlend = 0f;
        source.volume = volume;
        // A little pitch wobble on every sound. Identical pitch is what makes a repeated
        // clip sound like a machine rather than an event.
        source.pitch = Random.Range(0.94f, 1.07f);
        source.clip = clip;
        source.Play();
    }

    // Plays a sound somewhere in the world, so it fades and pans with distance. Used for
    // anything an enemy does, so a Spitter throwing a rock behind you is audible as being
    // behind you.
    public static void PlayAt(string soundName, Vector3 where, float volume)
    {
        AudioClip clip = ChooseAClip(soundName);
        if (clip == null)
        {
            return;
        }

        AudioSource source = NextFreeSource();
        source.transform.position = where;
        source.spatialBlend = 0.85f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 45f;
        source.volume = volume;
        source.pitch = Random.Range(0.94f, 1.07f);
        source.clip = clip;
        source.Play();
    }

    private static AudioClip ChooseAClip(string soundName)
    {
        SetUpIfNeeded();

        if (clipsByName.ContainsKey(soundName) == false)
        {
            return null;
        }

        // Several enemies dying on the same frame should sound like a death, not like a
        // wall of them.
        float now = Time.unscaledTime;
        if (lastPlayedAt.ContainsKey(soundName) == true)
        {
            if (now - lastPlayedAt[soundName] < SmallestGapBetweenRepeats)
            {
                return null;
            }
        }
        lastPlayedAt[soundName] = now;

        List<AudioClip> choices = clipsByName[soundName];
        if (choices.Count == 0)
        {
            return null;
        }
        return choices[Random.Range(0, choices.Count)];
    }

    private static AudioSource NextFreeSource()
    {
        // Round-robin rather than searching for a silent one. With twelve sources the
        // oldest is always the right one to reuse, and it never has to look.
        AudioSource source = sourcePool[nextSourceIndex];
        nextSourceIndex = nextSourceIndex + 1;
        if (nextSourceIndex >= PoolSize)
        {
            nextSourceIndex = 0;
        }
        return source;
    }

    // Entering play mode again reuses the same static fields, so the holder from the
    // previous session has to be forgotten or every sound plays through a destroyed
    // object and is silent.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ForgetEverythingBetweenRuns()
    {
        clipsByName = null;
        sourcePool = null;
        soundHolder = null;
        nextSourceIndex = 0;
        lastPlayedAt = new Dictionary<string, float>();
    }
}
