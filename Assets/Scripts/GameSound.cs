using UnityEngine;
using System.Collections.Generic;

// Every sound in the game is played through here.
//
// The same idea as GameInput: one place that knows the messy details, and plainly named
// questions everywhere else. Combat code asks to play "HitFlesh" and never has to know
// there are three recordings of it, where they live, how loud they should be, or that a
// Warden slam happening at the same moment will quietly get out of its way.
//
// Clips are named by WHAT THEY ARE FOR rather than what they were recorded from, and
// anything ending _0, _1, _2 is an alternative for the same event. One is chosen at
// random each time, which is the single cheapest way to stop a sound that plays forty
// times a minute becoming unbearable.
//
// ----------------------------------------------------------------------------------
// What changed, and why, because two of these were real bugs rather than polish
// ----------------------------------------------------------------------------------
//
// EVERY SOUND USED TO GET THE SAME TREATMENT. One pitch range of 0.94 to 1.07 for the
// whole game. That range is right for a footstep and wrong for everything with weight -
// a Warden slam pitched UP is a smaller Warden, and the boss's biggest move was randomly
// shrinking itself every other swing. Sounds now carry a profile saying how much they
// are allowed to move and in which direction.
//
// POSITIONAL AUDIO DID NOT POSITION. PlayAt has always asked for spatialBlend 0.85, but
// every clip in Resources/Audio was STEREO, and Unity will not pan a stereo clip in 3D -
// it plays both channels flat. So "a Spitter throwing a rock behind you is audible as
// being behind you", promised in this file's own comment, had never once been true. The
// clips are mono now (Tools/build_audio.py writes mono, and the older .ogg files have
// forceToMono set in their .meta), and the line is finally honest.
//
// BIG SOUNDS NOW MAKE ROOM. A slam briefly pushes everything that starts after it down
// a few decibels. This is most of what makes an impact feel heavy - not the loudness of
// the hit but the hole it leaves around itself. Turning a sound up cannot do this,
// because everything else is still there underneath it.
public static class GameSound
{
    // ------------------------------------------------------------------------
    // Mixing
    //
    // These stand in for an AudioMixer asset. A real mixer is a binary Unity asset that
    // cannot be written or reviewed by hand, and everything the game actually needs
    // from one - group volumes and ducking - is a few lines here where it can be read.
    // ------------------------------------------------------------------------

    public const int CategoryWorld = 0;      // impacts, creatures, the valley
    public const int CategoryPlayer = 1;     // the player's own body and weapons
    public const int CategoryInterface = 2;  // menus, banners, pickups

    public static float masterVolume = 1f;

    private static float[] volumePerCategory = new float[] { 1f, 1f, 1f };

    public static void SetCategoryVolume(int category, float volume)
    {
        if (category < 0 || category >= volumePerCategory.Length)
        {
            return;
        }
        volumePerCategory[category] = Mathf.Clamp01(volume);
    }

    // ------------------------------------------------------------------------
    // A sound's profile
    //
    // Everything the game knows about one named sound beyond the recordings themselves.
    // Anything not described below falls back to DefaultProfile, so adding a clip to
    // Resources/Audio still works with no code at all - it simply gets ordinary
    // treatment until someone decides it deserves better.
    // ------------------------------------------------------------------------

    private class SoundProfile
    {
        public float volumeTrim = 1f;

        // How far the pitch is allowed to wander. Identical pitch is what makes a
        // repeated clip sound like a machine rather than an event - but the amount that
        // helps is not the same for every sound, and for heavy things it is nearly zero.
        public float lowestPitch = 0.94f;
        public float highestPitch = 1.07f;

        public int category = CategoryWorld;

        // Sounds that would otherwise stack into a wall of noise when several creatures
        // do the same thing on the same frame.
        public float smallestGapSeconds = 0.04f;

        // Positional falloff. A Warden is heard across the whole arena; a footstep is
        // not, and giving them the same range is why small sounds used to crowd the mix
        // from places the player could not see.
        public float fullVolumeWithin = 4f;
        public float silentBeyond = 45f;

        // A sound that clears space around itself. Zero for almost everything.
        public float ducksOthersForSeconds = 0f;
        public float ducksOthersTo = 1f;
    }

    private static SoundProfile DefaultProfile = new SoundProfile();

    private static Dictionary<string, SoundProfile> profilesByName;

    // ------------------------------------------------------------------------
    // Loaded state
    // ------------------------------------------------------------------------

    // Loaded once and kept, because loading from Resources mid-fight causes a hitch.
    private static Dictionary<string, List<AudioClip>> clipsByName;

    // A pool of sources, so several sounds can overlap. One source would cut every sound
    // off the moment the next one started. Sixteen rather than the original twelve: the
    // creatures each have their own voice now, so a fight genuinely has more going on.
    private static AudioSource[] sourcePool;
    private static int nextSourceIndex = 0;
    private const int PoolSize = 16;

    private static GameObject soundHolder;

    private static Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>();

    // While this time has not passed, ordinary sounds start quieter.
    private static float duckingUntilTime = 0f;
    private static float duckingToVolume = 1f;

    // ------------------------------------------------------------------------
    // Set-up
    // ------------------------------------------------------------------------

    private static void SetUpIfNeeded()
    {
        if (clipsByName != null && soundHolder != null)
        {
            return;
        }

        LoadEveryClip();
        DescribeEverySound();
        BuildTheSourcePool();
    }

    private static void LoadEveryClip()
    {
        clipsByName = new Dictionary<string, List<AudioClip>>();

        AudioClip[] everything = Resources.LoadAll<AudioClip>("Audio");
        int index = 0;
        while (index < everything.Length)
        {
            AudioClip clip = everything[index];

            // "HitFlesh_2" belongs in the "HitFlesh" bucket.
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
    }

    private static void BuildTheSourcePool()
    {
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

    // ------------------------------------------------------------------------
    // What every sound is
    //
    // Read this as a table. The numbers that matter most are the pitch pair - whether a
    // sound is allowed to move, and how far - because that is what decides whether a
    // creature sounds alive or mechanical, and whether a heavy thing stays heavy.
    // ------------------------------------------------------------------------

    private static void Describe(string name, float volumeTrim, float lowestPitch, float highestPitch,
        int category, float smallestGapSeconds, float fullVolumeWithin, float silentBeyond)
    {
        SoundProfile profile = new SoundProfile();
        profile.volumeTrim = volumeTrim;
        profile.lowestPitch = lowestPitch;
        profile.highestPitch = highestPitch;
        profile.category = category;
        profile.smallestGapSeconds = smallestGapSeconds;
        profile.fullVolumeWithin = fullVolumeWithin;
        profile.silentBeyond = silentBeyond;
        profilesByName[name] = profile;
    }

    private static void MakeItClearSpace(string name, float forSeconds, float pushOthersDownTo)
    {
        if (profilesByName.ContainsKey(name) == false)
        {
            return;
        }
        profilesByName[name].ducksOthersForSeconds = forSeconds;
        profilesByName[name].ducksOthersTo = pushOthersDownTo;
    }

    private static void DescribeEverySound()
    {
        profilesByName = new Dictionary<string, SoundProfile>();

        // -- The Grunt. A big slow body; its voice may wander widely, because a creature
        //    that says exactly the same thing every time is the clearest possible tell
        //    that it is a recording.
        Describe("GruntWindUp", 1.00f, 0.90f, 1.12f, CategoryWorld, 0.10f, 6f, 34f);
        Describe("GruntSwing", 0.85f, 0.88f, 1.10f, CategoryWorld, 0.05f, 5f, 26f);
        Describe("GruntHurt", 1.00f, 0.86f, 1.16f, CategoryWorld, 0.05f, 6f, 32f);
        Describe("GruntDeath", 1.00f, 0.90f, 1.10f, CategoryWorld, 0.08f, 8f, 40f);

        // -- The Darter. Small and fast, so it sits high and moves the most of anything.
        Describe("DarterWindUp", 0.85f, 0.92f, 1.14f, CategoryWorld, 0.06f, 5f, 28f);
        Describe("DarterLunge", 1.00f, 0.88f, 1.18f, CategoryWorld, 0.05f, 6f, 34f);
        Describe("DarterHurt", 0.95f, 0.85f, 1.22f, CategoryWorld, 0.04f, 5f, 30f);
        Describe("DarterDeath", 1.00f, 0.90f, 1.14f, CategoryWorld, 0.08f, 7f, 36f);

        // -- The Spitter. The only enemy that can hit without reaching, so its wind-up
        //    deliberately carries further than anything else its size.
        Describe("SpitterWindUp", 1.00f, 0.92f, 1.12f, CategoryWorld, 0.08f, 8f, 44f);
        Describe("SpitterThrow", 0.95f, 0.90f, 1.14f, CategoryWorld, 0.05f, 7f, 40f);
        Describe("SpitterHurt", 0.95f, 0.86f, 1.18f, CategoryWorld, 0.04f, 5f, 30f);
        Describe("SpitterDeath", 1.00f, 0.90f, 1.12f, CategoryWorld, 0.08f, 7f, 36f);

        // -- The Warden. Nearly no pitch movement at all, and this is the important
        //    line in the whole table. Four tonnes of stone does not vary in pitch
        //    between swings; anything that does is not four tonnes. Everything else in
        //    the game is allowed to be lively, and the boss is deliberately not.
        Describe("WardenWindUp", 1.00f, 0.98f, 1.02f, CategoryWorld, 0.15f, 14f, 70f);
        Describe("WardenImpact", 1.00f, 0.97f, 1.03f, CategoryWorld, 0.06f, 16f, 80f);
        Describe("WardenHurt", 0.80f, 0.94f, 1.06f, CategoryWorld, 0.05f, 10f, 50f);
        Describe("WardenDeath", 1.00f, 1.00f, 1.00f, CategoryWorld, 0.20f, 22f, 95f);
        Describe("WardenPhase", 1.00f, 0.99f, 1.01f, CategoryWorld, 0.20f, 20f, 90f);
        Describe("WardenEnrage", 1.00f, 1.00f, 1.00f, CategoryWorld, 0.20f, 22f, 95f);
        Describe("WardenLeapLaunch", 0.90f, 0.97f, 1.03f, CategoryWorld, 0.10f, 12f, 60f);
        Describe("WardenLeapLand", 1.00f, 0.97f, 1.03f, CategoryWorld, 0.10f, 18f, 85f);
        Describe("WardenShockwave", 1.00f, 0.97f, 1.03f, CategoryWorld, 0.10f, 16f, 80f);
        Describe("WardenSummon", 0.95f, 0.98f, 1.02f, CategoryWorld, 0.15f, 16f, 75f);
        Describe("WardenStep", 0.55f, 0.95f, 1.05f, CategoryWorld, 0.09f, 10f, 46f);

        // -- The player's blows, split by what was struck rather than by what struck it.
        Describe("HitFlesh", 1.00f, 0.90f, 1.12f, CategoryPlayer, 0.03f, 8f, 40f);
        Describe("HitStone", 1.00f, 0.92f, 1.10f, CategoryPlayer, 0.03f, 8f, 40f);
        Describe("KillingBlow", 0.80f, 0.94f, 1.08f, CategoryPlayer, 0.05f, 10f, 48f);
        Describe("SwordWhiff", 0.70f, 0.92f, 1.12f, CategoryPlayer, 0.04f, 4f, 20f);
        Describe("HammerWhiff", 0.75f, 0.90f, 1.10f, CategoryPlayer, 0.04f, 4f, 22f);

        // -- The bow, which had no sounds at all until now.
        Describe("BowNock", 0.55f, 0.94f, 1.08f, CategoryPlayer, 0.04f, 3f, 14f);
        Describe("BowDraw", 0.60f, 0.96f, 1.06f, CategoryPlayer, 0.10f, 3f, 14f);
        Describe("BowRelease", 0.85f, 0.94f, 1.08f, CategoryPlayer, 0.04f, 4f, 22f);
        Describe("ArrowFlyBy", 0.50f, 0.90f, 1.14f, CategoryPlayer, 0.04f, 3f, 18f);
        Describe("ArrowHitFlesh", 0.90f, 0.92f, 1.12f, CategoryPlayer, 0.03f, 7f, 36f);
        Describe("ArrowHitStone", 0.70f, 0.92f, 1.14f, CategoryPlayer, 0.03f, 6f, 30f);

        // -- The player's own body.
        Describe("Footstep", 0.30f, 0.88f, 1.14f, CategoryPlayer, 0.05f, 3f, 12f);
        Describe("PlayerLand", 0.55f, 0.90f, 1.10f, CategoryPlayer, 0.06f, 4f, 18f);
        Describe("Heartbeat", 0.70f, 1.00f, 1.00f, CategoryPlayer, 0.30f, 3f, 10f);

        // -- Status effects, none of which made any sound before.
        Describe("BleedTick", 0.35f, 0.92f, 1.10f, CategoryPlayer, 0.20f, 3f, 12f);
        Describe("Stunned", 0.70f, 0.98f, 1.02f, CategoryPlayer, 0.30f, 3f, 12f);
        Describe("Weakened", 0.60f, 0.96f, 1.05f, CategoryPlayer, 0.30f, 3f, 12f);

        // -- The world.
        Describe("SurgeActivate", 0.85f, 0.98f, 1.03f, CategoryPlayer, 0.20f, 6f, 30f);
        Describe("GemShatter", 0.90f, 0.97f, 1.04f, CategoryWorld, 0.15f, 10f, 55f);
        Describe("StoryBeat", 0.75f, 1.00f, 1.00f, CategoryInterface, 0.30f, 8f, 45f);

        // -- The clips that were already here and are still doing their original job.
        Describe("Jump", 0.70f, 0.94f, 1.08f, CategoryPlayer, 0.06f, 3f, 16f);
        Describe("Dodge", 0.80f, 0.94f, 1.08f, CategoryPlayer, 0.06f, 3f, 16f);
        Describe("PlayerHurt", 1.00f, 0.92f, 1.10f, CategoryPlayer, 0.05f, 3f, 14f);
        Describe("PotionDrink", 0.90f, 0.96f, 1.06f, CategoryPlayer, 0.10f, 3f, 14f);
        Describe("WeaponSwap", 0.75f, 0.94f, 1.08f, CategoryPlayer, 0.05f, 3f, 14f);
        Describe("PillarRise", 0.90f, 0.94f, 1.08f, CategoryWorld, 0.06f, 6f, 36f);
        Describe("PillarBreak", 1.00f, 0.92f, 1.10f, CategoryWorld, 0.05f, 8f, 44f);
        Describe("RockThrow", 0.85f, 0.92f, 1.10f, CategoryWorld, 0.05f, 6f, 38f);
        Describe("RockImpact", 0.90f, 0.90f, 1.12f, CategoryWorld, 0.04f, 6f, 36f);
        Describe("BarrierMove", 0.95f, 0.97f, 1.04f, CategoryWorld, 0.10f, 8f, 45f);
        Describe("PortalOpen", 0.95f, 0.97f, 1.04f, CategoryWorld, 0.15f, 8f, 45f);
        Describe("EssencePickup", 0.70f, 0.94f, 1.12f, CategoryInterface, 0.03f, 3f, 14f);
        Describe("ShrineBuy", 0.85f, 0.96f, 1.06f, CategoryInterface, 0.10f, 3f, 14f);
        Describe("RoundStart", 0.90f, 1.00f, 1.00f, CategoryInterface, 0.30f, 3f, 14f);
        Describe("RoundCleared", 0.90f, 1.00f, 1.00f, CategoryInterface, 0.30f, 3f, 14f);
        Describe("UiClick", 0.70f, 0.98f, 1.03f, CategoryInterface, 0.02f, 3f, 12f);

        // -- And the handful of sounds big enough to be given room.
        //
        //    Only the Warden's heaviest moves and the player's death-blow qualify. Duck
        //    too many things and the mix pumps audibly, which is far worse than no
        //    ducking at all - the fix stops sounding like weight and starts sounding
        //    like a fault.
        MakeItClearSpace("WardenImpact", 0.34f, 0.55f);
        MakeItClearSpace("WardenLeapLand", 0.40f, 0.50f);
        MakeItClearSpace("WardenDeath", 0.90f, 0.42f);
        MakeItClearSpace("WardenEnrage", 0.70f, 0.50f);
        MakeItClearSpace("WardenPhase", 0.55f, 0.55f);
        MakeItClearSpace("WardenShockwave", 0.30f, 0.62f);
        MakeItClearSpace("PillarBreak", 0.18f, 0.72f);
    }

    // ------------------------------------------------------------------------
    // Playing
    // ------------------------------------------------------------------------

    // Plays a sound with no position - interface noises, round banners, the player's own
    // actions, which should sound the same wherever they happen.
    public static void Play(string soundName, float volume)
    {
        PlayInternal(soundName, volume, Vector3.zero, false, 0f);
    }

    // The same, with the pitch decided by the caller rather than rolled.
    //
    // This is how one recording becomes several creatures. A single club impact played
    // at 0.7 and at 1.35 is a big animal and a small one, and it costs nothing.
    public static void Play(string soundName, float volume, float pitch)
    {
        PlayInternal(soundName, volume, Vector3.zero, false, pitch);
    }

    // Plays a sound somewhere in the world, so it fades and pans with distance. Used for
    // anything an enemy does, so a Spitter throwing a rock behind you is audible as being
    // behind you.
    public static void PlayAt(string soundName, Vector3 where, float volume)
    {
        PlayInternal(soundName, volume, where, true, 0f);
    }

    public static void PlayAt(string soundName, Vector3 where, float volume, float pitch)
    {
        PlayInternal(soundName, volume, where, true, pitch);
    }

    // A creature's own voice, chosen by which creature is asking.
    //
    // EnemyBrain is one script for every enemy in the game, so it cannot name its sounds
    // literally - it asks for "Hurt" and the creature it belongs to decides which Hurt
    // that is. A voice with no clips of its own falls back to the Grunt's, so a creature
    // added later is never silent while its sounds are still being made.
    public static void PlayCreature(string voiceName, string whatHappened, Vector3 where, float volume)
    {
        SetUpIfNeeded();

        string wanted = voiceName + whatHappened;
        if (clipsByName.ContainsKey(wanted) == false)
        {
            wanted = "Grunt" + whatHappened;
        }

        PlayInternal(wanted, volume, where, true, 0f);
    }

    private static void PlayInternal(string soundName, float volume, Vector3 where,
        bool positional, float pitchOrZeroToRoll)
    {
        SetUpIfNeeded();

        SoundProfile profile = ProfileFor(soundName);

        AudioClip clip = ChooseAClip(soundName, profile);
        if (clip == null)
        {
            return;
        }

        float finalVolume = volume * profile.volumeTrim
            * volumePerCategory[profile.category] * masterVolume;

        // A sound that clears space is never itself pushed down - otherwise two slams in
        // quick succession would each quieten the other and the boss would get smaller
        // the harder it hit.
        if (profile.ducksOthersForSeconds <= 0f && Time.unscaledTime < duckingUntilTime)
        {
            finalVolume = finalVolume * duckingToVolume;
        }

        float pitch = pitchOrZeroToRoll;
        if (pitch <= 0f)
        {
            pitch = Random.Range(profile.lowestPitch, profile.highestPitch);
        }

        AudioSource source = NextFreeSource();

        if (positional == true)
        {
            source.transform.position = where;
            source.spatialBlend = 0.85f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = profile.fullVolumeWithin;
            source.maxDistance = profile.silentBeyond;
        }
        else
        {
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
        }

        source.volume = finalVolume;
        source.pitch = pitch;
        source.clip = clip;
        source.Play();

        if (profile.ducksOthersForSeconds > 0f)
        {
            BeginDucking(profile.ducksOthersForSeconds, profile.ducksOthersTo);
        }
    }

    // Two sounds on the same instant, the second laid over the first as an accent.
    //
    // A kill is not a different event from a hit - it is a hit that finished something,
    // and it should sound like the hit it actually was with something added. Replacing
    // the sound outright makes the killing blow feel like a different weapon.
    public static void PlayWithAccent(string soundName, string accentName, Vector3 where,
        float volume, float accentVolume)
    {
        PlayAt(soundName, where, volume);
        PlayAt(accentName, where, accentVolume);
    }

    private static void BeginDucking(float forSeconds, float toVolume)
    {
        float until = Time.unscaledTime + forSeconds;

        // Nothing is currently ducking, so this sound simply sets it.
        //
        // Testing this FIRST is what makes the rest correct. The depth left over from a
        // duck that has already finished is stale, and a gentle sound arriving after a
        // heavy one had ended would otherwise inherit the heavy one's depth and push the
        // whole mix down for no reason.
        if (Time.unscaledTime >= duckingUntilTime)
        {
            duckingUntilTime = until;
            duckingToVolume = toVolume;
            return;
        }

        // Already ducking. The deepest duck asked for wins and the longest one sets the
        // clock - letting a later, gentler sound overwrite a heavy one mid-slam is how
        // ducking starts to pump audibly.
        if (until > duckingUntilTime)
        {
            duckingUntilTime = until;
        }
        if (toVolume < duckingToVolume)
        {
            duckingToVolume = toVolume;
        }
    }

    private static SoundProfile ProfileFor(string soundName)
    {
        if (profilesByName != null && profilesByName.ContainsKey(soundName) == true)
        {
            return profilesByName[soundName];
        }
        return DefaultProfile;
    }

    private static AudioClip ChooseAClip(string soundName, SoundProfile profile)
    {
        if (clipsByName.ContainsKey(soundName) == false)
        {
            return null;
        }

        // Several enemies dying on the same frame should sound like a death, not like a
        // wall of them.
        float now = Time.unscaledTime;
        if (lastPlayedAt.ContainsKey(soundName) == true)
        {
            if (now - lastPlayedAt[soundName] < profile.smallestGapSeconds)
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
        // Round-robin rather than searching for a silent one. With sixteen sources the
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
        profilesByName = null;
        sourcePool = null;
        soundHolder = null;
        nextSourceIndex = 0;
        lastPlayedAt = new Dictionary<string, float>();
        duckingUntilTime = 0f;
        duckingToVolume = 1f;
        masterVolume = 1f;
        volumePerCategory = new float[] { 1f, 1f, 1f };
    }
}
