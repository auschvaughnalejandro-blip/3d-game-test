using UnityEngine;

// Orrin talking the player through the fight.
//
// Every line here is triggered by something the game was already doing. Nothing in this
// file changes how anything works - it only notices and comments. That is deliberate: a
// tutorial that has to be wired into the combat code is a tutorial that breaks the combat
// code, and this one can be deleted entirely without the game noticing.
//
// It watches rather than being told. Polling a handful of public values once a frame is
// far easier to follow than threading callbacks through six other scripts, and at this
// scale it costs nothing measurable.
public class CoachLines : MonoBehaviour
{
    private RoundDirector theRounds;
    private GameDirector theDirector;
    private WardenBoss theWarden;
    private StyleLens theLens;

    // What the world was being drawn as last time we looked.
    private string lensWeLastSaw = "";
    private int timesTheLensHasChanged = 0;

    // What has already been said. A hint that repeats is worse than no hint at all.
    private bool saidRoundOne = false;
    private bool saidRoundTwo = false;
    private bool saidRoundThree = false;
    private bool saidRoundFour = false;
    private bool saidFirstEssence = false;
    private bool saidShrine = false;
    private bool saidSpitters = false;
    private bool saidPortal = false;
    private bool saidWardenPhaseTwo = false;
    private bool saidWardenPhaseThree = false;
    private bool saidWardenDead = false;

    // Round lines wait for the banner to clear, so the hint is not competing with
    // "THE PACK" written across the middle of the screen.
    private float secondsUntilQueuedLine = 0f;
    private string queuedLine = "";

    private int roundWeLastSaw = 0;

    void Start()
    {
        theRounds = Object.FindFirstObjectByType<RoundDirector>();
        theDirector = GameDirector.instance;
        theLens = Object.FindFirstObjectByType<StyleLens>();
        if (theLens != null)
        {
            lensWeLastSaw = theLens.CurrentStyleName();
        }
    }

    // Re-armed when a run is reset. Every one of these is a said-it-once latch, so
    // without this a second run is played in total silence - the coaching reads as
    // having been removed rather than as already spent.
    public void ResetForANewRun()
    {
        saidRoundOne = false;
        saidRoundTwo = false;
        saidRoundThree = false;
        saidRoundFour = false;
        saidFirstEssence = false;
        saidShrine = false;
        saidSpitters = false;
        saidPortal = false;
        saidWardenPhaseTwo = false;
        saidWardenPhaseThree = false;
        saidWardenDead = false;

        queuedLine = "";
        secondsUntilQueuedLine = 0f;
        roundWeLastSaw = 0;
        timesTheLensHasChanged = 0;
    }

    void Update()
    {
        if (DialogueBox.instance == null)
        {
            return;
        }

        ReleaseAnyQueuedLine();

        WatchTheLens();
        WatchTheRounds();
        WatchTheEssence();
        WatchForSpitters();
        WatchThePortal();
        WatchTheWarden();
    }

    // ------------------------------------------------------------------------
    // Saying things
    // ------------------------------------------------------------------------

    private void SayIn(float seconds, string line)
    {
        queuedLine = line;
        secondsUntilQueuedLine = seconds;
    }

    private void SayNow(string line)
    {
        DialogueBox.instance.Murmur(StoryDirector.Orrin, line);
    }

    private void ReleaseAnyQueuedLine()
    {
        if (queuedLine == "")
        {
            return;
        }

        secondsUntilQueuedLine = secondsUntilQueuedLine - Time.deltaTime;
        if (secondsUntilQueuedLine > 0f)
        {
            return;
        }

        SayNow(queuedLine);
        queuedLine = "";
    }

    // ------------------------------------------------------------------------
    // The lens
    // ------------------------------------------------------------------------

    // Orrin noticing that the player has just changed what the world is made of.
    //
    // This is the one thing in the project that nothing else on the market does, and
    // until now it was a debug key that silently repainted the screen. Having a character
    // react to it is what turns it from a graphics option into something the world knows
    // about - which is a far better thirty seconds in front of an investor than a button
    // that changes the art.
    private void WatchTheLens()
    {
        if (theLens == null)
        {
            return;
        }

        string lensNow = theLens.CurrentStyleName();
        if (lensNow == lensWeLastSaw)
        {
            return;
        }
        lensWeLastSaw = lensNow;
        timesTheLensHasChanged = timesTheLensHasChanged + 1;

        // He only comments while the player is down in the dungeon with him. Doing it
        // mid-fight would be noise, and by then the player already knows what the key
        // does.
        if (StoryDirector.instance == null
            || StoryDirector.instance.currentAct != StoryDirector.ActInTheDungeon)
        {
            return;
        }

        if (timesTheLensHasChanged == 1)
        {
            SayNow("Ah - you found it. That is a lens. The valley did not change; only the way it is being drawn did.");
        }
        else if (timesTheLensHasChanged == 2)
        {
            SayNow("Keep going. There are four of them, and none is more true than the others.");
        }
        else if (timesTheLensHasChanged == 4)
        {
            SayNow("I made every one of these. It is the only thing I am still proud of. Press TAB whenever you like - it costs nothing.");
        }
    }

    // ------------------------------------------------------------------------
    // The rounds
    // ------------------------------------------------------------------------

    private void WatchTheRounds()
    {
        if (theRounds == null)
        {
            return;
        }

        int round = theRounds.currentRound;

        if (round != roundWeLastSaw)
        {
            roundWeLastSaw = round;
            SpeakForRound(round);
        }

        // The gap after round one is the only chance the player gets to be told what the
        // shrine is for while they are not being chased.
        if (saidShrine == false && round == 1 && theRounds.IsBetweenRounds() == true)
        {
            saidShrine = true;
            SayNow("That pillar behind you is a shrine. Stand near it and spend what you have: 1 for vigour, 2 for strength, 3 for wind.");
        }
    }

    private void SpeakForRound(int round)
    {
        if (round == 1 && saidRoundOne == false)
        {
            saidRoundOne = true;
            // The banner runs three and a half seconds, so this lands just as the ring
            // of Grunts starts closing.
            SayIn(4.0f, "Grunts. Slow, heavy, and they do not flinch. The HAMMER breaks them - press F to change weapon.");
        }
        else if (round == 2 && saidRoundTwo == false)
        {
            saidRoundTwo = true;
            SayIn(4.0f, "Darters. Once one commits to a charge it cannot turn. Step ASIDE. Never backwards.");
        }
        else if (round == 3 && saidRoundThree == false)
        {
            saidRoundThree = true;
            SayIn(4.0f, "I am raising the stone for you. Put it between yourself and whatever is throwing.");
        }
        else if (round == 4 && saidRoundFour == false)
        {
            saidRoundFour = true;
            SayIn(4.0f, "All of them at once. There is no lesson left in this one. Only what you kept.");
        }
    }

    // ------------------------------------------------------------------------
    // Essence
    // ------------------------------------------------------------------------

    private void WatchTheEssence()
    {
        if (saidFirstEssence == true)
        {
            return;
        }
        if (theDirector == null)
        {
            theDirector = GameDirector.instance;
            return;
        }

        if (theDirector.essenceCollected > 0)
        {
            saidFirstEssence = true;
            SayNow("That light is essence. Take all of it. It buys what you will not survive without.");
        }
    }

    // ------------------------------------------------------------------------
    // Spitters, and the bow that answers them
    // ------------------------------------------------------------------------

    private void WatchForSpitters()
    {
        if (saidSpitters == true)
        {
            return;
        }

        // Only checked while a round is actually running, and only from round three,
        // which is the first round that has any. Scanning every frame from the start
        // would be wasted work.
        if (theRounds == null || theRounds.currentRound < 3)
        {
            return;
        }

        EnemyBrain[] everyEnemy = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
        int index = 0;
        while (index < everyEnemy.Length)
        {
            if (everyEnemy[index].displayName == "Spitter")
            {
                saidSpitters = true;
                SayNow("Spitters, up on the high stone. No blade reaches them. Take the BOW - hold to draw, release to loose, and aim ABOVE them, not at them.");
                return;
            }
            index = index + 1;
        }
    }

    // ------------------------------------------------------------------------
    // The portal and the Warden
    // ------------------------------------------------------------------------

    private void WatchThePortal()
    {
        if (saidPortal == true || theRounds == null)
        {
            return;
        }
        if (theRounds.thePortal == null || theRounds.thePortal.IsOpen() == false)
        {
            return;
        }

        saidPortal = true;
        SayNow("That is the Vault. He is behind it. Go, or do not - but I cannot follow you in.");
    }

    private void WatchTheWarden()
    {
        if (theWarden == null)
        {
            theWarden = Object.FindFirstObjectByType<WardenBoss>();
        }

        if (theWarden != null)
        {
            int phase = theWarden.CurrentPhase();

            if (phase >= 2 && saidWardenPhaseTwo == false)
            {
                saidWardenPhaseTwo = true;
                SayNow("He is hurt, and hurt has made him clever. He throws now. Keep stone at your shoulder.");
            }
            if (phase >= 3 && saidWardenPhaseThree == false)
            {
                saidWardenPhaseThree = true;
                SayNow("He is calling them to him. Kill HIM. Let the rest come.");
            }
        }

        if (saidWardenDead == false && theDirector != null && theDirector.theWardenIsDead == true)
        {
            saidWardenDead = true;
            SayNow("It is done. Take what is in his chest - it was never his to begin with.");
        }
    }
}
