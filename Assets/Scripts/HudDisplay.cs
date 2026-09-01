using UnityEngine;

// Everything drawn on top of the 3D view: the two bars, the essence count, the shrine
// prompt and the win and death messages.
//
// This draws itself entirely in code rather than from a Canvas built in the editor.
// That means there is nothing to wire up by hand and nothing that can come unlinked,
// which matters a great deal when the scene is being assembled by a script.
public class HudDisplay : MonoBehaviour
{
    private CharacterStats playerStats;
    private ShrineOfEssence theShrine;
    private StyleLens theLens;
    private PlayerHealing playerHealing;
    private PlayerWeapons playerWeapons;
    private RoundDirector theRounds;
    private PlayerCombat playerCombat;
    private StoryDirector theStory;
    private Wizard[] everyWizard;

    // A single white pixel, tinted with GUI.color to draw every coloured rectangle.
    private Texture2D onePlainWhitePixel;

    private GUIStyle bigMessageStyle;
    private GUIStyle normalTextStyle;
    private GUIStyle smallTextStyle;
    private bool stylesHaveBeenBuilt = false;

    private static readonly Color HealthColour = new Color(0.82f, 0.22f, 0.24f);
    private static readonly Color StaminaColour = new Color(0.90f, 0.76f, 0.30f);
    private static readonly Color EmptyBarColour = new Color(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color EssenceColour = new Color(0.42f, 0.95f, 0.84f);
    // Violet, because red, yellow and teal are already spoken for by health, stamina and
    // essence. A fourth bar in a colour already on screen would be read as one of those.
    private static readonly Color SurgeChargingColour = new Color(0.58f, 0.38f, 0.92f);
    private static readonly Color SurgeActiveColour = new Color(0.85f, 0.55f, 1f);

    // The three ailments. Bleeding is deliberately the same red family as the health bar
    // it is draining, so the two read as connected rather than as two unrelated red
    // things that happen to be near each other.
    private static readonly Color BleedingColour = new Color(0.92f, 0.26f, 0.24f);
    private static readonly Color StunnedColour = new Color(0.60f, 0.76f, 0.98f);
    private static readonly Color WeakenedColour = new Color(0.68f, 0.88f, 0.38f);

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerStats = playerObject.GetComponent<CharacterStats>();
        }

        theShrine = Object.FindFirstObjectByType<ShrineOfEssence>();
        theLens = Object.FindFirstObjectByType<StyleLens>();
        theRounds = Object.FindFirstObjectByType<RoundDirector>();
        theStory = Object.FindFirstObjectByType<StoryDirector>();
        // Found once. There are two of them and neither is ever created or destroyed
        // during a run, only switched on and off.
        everyWizard = Object.FindObjectsByType<Wizard>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (playerObject != null)
        {
            playerHealing = playerObject.GetComponent<PlayerHealing>();
            playerWeapons = playerObject.GetComponent<PlayerWeapons>();
            playerCombat = playerObject.GetComponent<PlayerCombat>();
        }

        onePlainWhitePixel = new Texture2D(1, 1);
        onePlainWhitePixel.SetPixel(0, 0, Color.white);
        onePlainWhitePixel.Apply();
    }

    // Styles cannot be built in Start because Unity's GUI system is not ready that
    // early. Building them on the first OnGUI call is the normal way around this.
    private void BuildStylesIfNeeded()
    {
        if (stylesHaveBeenBuilt == true)
        {
            return;
        }

        bigMessageStyle = new GUIStyle(GUI.skin.label);
        bigMessageStyle.fontSize = 44;
        bigMessageStyle.fontStyle = FontStyle.Bold;
        bigMessageStyle.alignment = TextAnchor.MiddleCenter;

        normalTextStyle = new GUIStyle(GUI.skin.label);
        normalTextStyle.fontSize = 19;
        normalTextStyle.fontStyle = FontStyle.Bold;

        smallTextStyle = new GUIStyle(GUI.skin.label);
        smallTextStyle.fontSize = 14;

        stylesHaveBeenBuilt = true;
    }

    void OnGUI()
    {
        BuildStylesIfNeeded();

        if (playerStats == null)
        {
            return;
        }

        // Health bars and essence counts drawn across the title screen would look like a
        // mistake, because they are one.
        if (MainMenu.IsShowing() == true)
        {
            return;
        }

        DrawTheTwoBars();
        DrawTheSurgeMeter();
        DrawTheAilments();
        DrawTheEssenceCount();
        DrawThePotions();
        DrawTheWeapon();
        DrawTheRound();
        DrawTheBossBar();
        DrawTheControlsReminder();
        DrawTheShrinePrompt();
        DrawTheSpeakPrompt();
        DrawTheCrosshair();
        DrawTheBowDraw();
        DrawTheQuiver();
        DrawTheNewWeaponName();
        DrawAnyBigMessage();
    }

    private void DrawTheTwoBars()
    {
        float barLeft = 34f;
        float barWidth = 320f;

        DrawOneBar(barLeft, 30f, barWidth, 24f, playerStats.HealthAsFraction(), HealthColour);
        DrawOneBar(barLeft, 62f, barWidth * 0.8f, 12f, playerStats.StaminaAsFraction(), StaminaColour);

        GUI.color = Color.white;
        GUI.Label(new Rect(barLeft + barWidth + 14f, 28f, 200f, 30f),
            Mathf.CeilToInt(playerStats.currentHealth) + " / " + Mathf.CeilToInt(playerStats.maximumHealth),
            normalTextStyle);
    }

    private void DrawOneBar(float left, float top, float width, float height, float fillFraction, Color fillColour)
    {
        if (fillFraction < 0f)
        {
            fillFraction = 0f;
        }
        if (fillFraction > 1f)
        {
            fillFraction = 1f;
        }

        // A dark border drawn slightly larger than the bar, so the bar stays readable
        // against both a bright sky and dark rock.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(left - 2f, top - 2f, width + 4f, height + 4f), onePlainWhitePixel);

        GUI.color = EmptyBarColour;
        GUI.DrawTexture(new Rect(left, top, width, height), onePlainWhitePixel);

        GUI.color = fillColour;
        GUI.DrawTexture(new Rect(left, top, width * fillFraction, height), onePlainWhitePixel);

        GUI.color = Color.white;
    }

    // The kill-streak meter, sitting under the stamina bar. It is drawn in the same place
    // whether it is filling, empty or spent - a bar that appears and disappears reads as a
    // glitch, and the player cannot learn to watch a thing that is not always there.
    private void DrawTheSurgeMeter()
    {
        PlayerSurge theSurge = PlayerSurge.instance;
        if (theSurge == null)
        {
            return;
        }

        float barLeft = 34f;
        // Kept the same width as the stamina bar above it, and the readout kept in line
        // with the health readout, so the three bars read as one stack rather than three
        // separate things that happen to be near each other.
        float fullBarWidth = 320f;
        float meterWidth = fullBarWidth * 0.8f;
        float readoutLeft = barLeft + fullBarWidth + 14f;

        if (theSurge.SurgeIsActive() == true)
        {
            // While the reward is running the bar stops showing points and shows the time
            // left instead. In those five seconds the only thing worth knowing is how many
            // of them remain, not how the next streak is coming along.
            float secondsLeftAsFraction = 1f;
            if (theSurge.surgeLastsSeconds > 0f)
            {
                secondsLeftAsFraction = theSurge.SurgeSecondsRemaining() / theSurge.surgeLastsSeconds;
            }

            DrawOneBar(barLeft, 80f, meterWidth, 12f, secondsLeftAsFraction, SurgeActiveColour);

            GUI.color = SurgeActiveColour;
            GUI.Label(new Rect(readoutLeft, 74f, 320f, 26f),
                "SURGE  " + theSurge.SurgeSecondsRemaining().ToString("0.0") + "s",
                normalTextStyle);
            GUI.color = Color.white;

            DrawTheSurgeBanner();
            return;
        }

        DrawOneBar(barLeft, 80f, meterWidth, 12f, theSurge.PointsAsFraction(), SurgeChargingColour);

        // Rounded DOWN, so the number never claims a point the meter has already leaked
        // away. Reading "15 / 15" on a bar that is not full would look broken.
        GUI.color = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(readoutLeft, 78f, 320f, 24f),
            "XP  " + Mathf.FloorToInt(theSurge.CurrentPoints())
                + " / " + Mathf.RoundToInt(theSurge.pointsNeededForTheSurge),
            smallTextStyle);
        GUI.color = Color.white;
    }

    // Said out loud in the middle of the screen as well as shown on the bar. Five seconds
    // is short enough that a player watching their feet would otherwise spend the whole
    // reward not knowing they had it.
    private void DrawTheSurgeBanner()
    {
        GUIStyle centred = new GUIStyle(bigMessageStyle);
        centred.alignment = TextAnchor.MiddleCenter;

        // High on the screen, clear of the round banners at 0.30 and the potion bar at
        // 0.62, so nothing important is ever written on top of anything else.
        GUI.color = SurgeActiveColour;
        GUI.Label(new Rect(0f, Screen.height * 0.18f, Screen.width, 50f),
            "POWER SURGE", centred);
        GUI.color = Color.white;
    }

    // What the last creature to hit the player left behind, sitting under the three bars.
    //
    // This is not decoration. Health draining while nothing is attacking, a walk that has
    // gone slow, and a sword that suddenly takes twice as many swings are three of the
    // most convincing-looking bugs this game could have - and a player who cannot see a
    // cause will read all three as broken rather than as an enemy having done something.
    //
    // Unlike the surge meter above it, this row IS allowed to appear and disappear. The
    // surge bar is a thing the player has to learn to watch and so must always be in the
    // same place; an ailment is an event, and an event that left a permanent empty slot
    // on screen would be claiming the player is always about to bleed.
    private void DrawTheAilments()
    {
        PlayerAilments ailments = PlayerAilments.instance;
        if (ailments == null)
        {
            return;
        }

        // Left edge lines up with the three bars. Each chip is drawn in turn and moves
        // the next one along, so one ailment sits on its own at the left rather than
        // leaving gaps where the other two would have been.
        float chipLeft = 34f;
        float chipTop = 100f;

        if (ailments.IsBleeding() == true)
        {
            chipLeft = DrawOneAilmentChip(chipLeft, chipTop, "BLEEDING",
                ailments.BleedSecondsRemaining(), ailments.bleedLastsSeconds, BleedingColour);
        }
        if (ailments.IsStunned() == true)
        {
            chipLeft = DrawOneAilmentChip(chipLeft, chipTop, "STUNNED",
                ailments.StunSecondsRemaining(), ailments.stunLastsSeconds, StunnedColour);
        }
        if (ailments.IsWeakened() == true)
        {
            chipLeft = DrawOneAilmentChip(chipLeft, chipTop, "WEAKENED",
                ailments.WeakenSecondsRemaining(), ailments.weakenLastsSeconds, WeakenedColour);
        }
    }

    // Returns where the NEXT chip should start, so the caller never has to do the sums.
    private float DrawOneAilmentChip(float left, float top, string label,
        float secondsLeft, float secondsTotal, Color colour)
    {
        float chipWidth = 104f;
        float chipHeight = 18f;

        // The chip drains left to right as the ailment runs out, so the player can see
        // how long is left without reading the number.
        float fractionLeft = 1f;
        if (secondsTotal > 0f)
        {
            fractionLeft = secondsLeft / secondsTotal;
        }

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(left - 2f, top - 2f, chipWidth + 4f, chipHeight + 4f), onePlainWhitePixel);

        GUI.color = EmptyBarColour;
        GUI.DrawTexture(new Rect(left, top, chipWidth, chipHeight), onePlainWhitePixel);

        // Dimmed well down, because the label is written on top of it and a full-strength
        // fill behind black-on-bright text is unreadable at this size.
        GUI.color = new Color(colour.r, colour.g, colour.b, 0.45f);
        GUI.DrawTexture(new Rect(left, top, chipWidth * fractionLeft, chipHeight), onePlainWhitePixel);

        GUI.color = colour;
        GUI.Label(new Rect(left + 6f, top + 1f, chipWidth, chipHeight), label, smallTextStyle);
        GUI.color = Color.white;

        // Eight pixels of gap, matched to the border drawn above.
        return left + chipWidth + 8f;
    }

    private void DrawTheEssenceCount()
    {
        if (GameDirector.instance == null)
        {
            return;
        }

        GUI.color = EssenceColour;
        GUI.Label(new Rect(34f, 100f, 400f, 30f),
            "ESSENCE  " + GameDirector.instance.essenceCollected,
            normalTextStyle);
        GUI.color = Color.white;
    }

    private void DrawTheControlsReminder()
    {
        GUI.color = new Color(1f, 1f, 1f, 0.65f);
        GUI.Label(new Rect(34f, Screen.height - 84f, 900f, 24f),
            "WASD move   SHIFT sprint   SPACE jump   CTRL dodge   CLICK attack   HOLD CLICK heavy",
            smallTextStyle);
        GUI.Label(new Rect(34f, Screen.height - 62f, 900f, 24f),
            "Q drink potion   F swap weapon (sword/hammer/bow)   V first person   "
            + "TAB visual style   ESC pause",
            smallTextStyle);
        GUI.color = Color.white;

        // The current style is named in the corner, because during a demonstration the
        // thing being shown off should always be labelled.
        if (theLens != null)
        {
            GUIStyle rightAligned = new GUIStyle(normalTextStyle);
            rightAligned.alignment = TextAnchor.MiddleRight;

            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(Screen.width - 260f, 28f, 226f, 26f),
                "LENS  " + theLens.CurrentStyleName(), rightAligned);
            GUI.color = Color.white;
        }
    }

    // Which round this is, how many enemies are left, and the countdown between rounds.
    private void DrawTheRound()
    {
        if (theRounds == null)
        {
            return;
        }

        GUIStyle centred = new GUIStyle(normalTextStyle);
        centred.alignment = TextAnchor.MiddleCenter;

        GUIStyle bigCentred = new GUIStyle(bigMessageStyle);
        bigCentred.fontSize = 34;

        if (theRounds.allRoundsCleared == true)
        {
            GUI.color = new Color(0.42f, 0.95f, 0.84f);
            GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 60f),
                "THE VALLEY IS YOURS", bigMessageStyle);
            GUI.color = Color.white;
            return;
        }

        // The round banner, held for a few seconds before the enemies arrive.
        if (theRounds.ShowingBanner() == true)
        {
            GUI.color = new Color(1f, 0.92f, 0.6f);
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 50f),
                "ROUND " + theRounds.currentRound + " OF " + theRounds.TotalRounds(), bigCentred);
            GUI.Label(new Rect(0f, Screen.height * 0.30f + 46f, Screen.width, 40f),
                theRounds.CurrentRoundName(), centred);
            GUI.color = Color.white;
            return;
        }

        // Round four clears and then nothing happens on a timer, which is the point - but
        // it also means the player is left standing in an empty arena with no idea the
        // game is waiting on them. The prompt is the only thing that makes it a choice
        // rather than a stall.
        if (theRounds.WaitingForThePortal() == true)
        {
            GUI.color = new Color(0.78f, 0.52f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 50f),
                "THE WAY IS OPEN", bigCentred);

            string directions = "a portal has risen to the north - walk into it";
            if (theRounds.thePortal != null && playerStats != null)
            {
                int paces = Mathf.RoundToInt(Vector3.Distance(
                    playerStats.transform.position,
                    theRounds.thePortal.transform.position));
                directions = "a portal has risen to the north - " + paces + "m away";
            }

            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(0f, Screen.height * 0.30f + 46f, Screen.width, 40f),
                directions, centred);
            GUI.color = Color.white;
            return;
        }

        if (theRounds.InIntermission() == true)
        {
            GUI.color = new Color(0.42f, 0.95f, 0.84f);
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 50f),
                "ROUND " + theRounds.currentRound + " CLEARED", bigCentred);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(0f, Screen.height * 0.30f + 46f, Screen.width, 40f),
                "next round in " + Mathf.CeilToInt(theRounds.SecondsLeftInPhase())
                + "     spend essence at the shrine", centred);
            GUI.color = Color.white;
            return;
        }

        // During the fight it shrinks to a quiet line at the top.
        if (theRounds.WaitingToStart() == false)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(0f, 26f, Screen.width, 26f),
                "ROUND " + theRounds.currentRound + " / " + theRounds.TotalRounds()
                + "     " + theRounds.CurrentRoundName()
                + "     ENEMIES " + theRounds.EnemiesRemaining(), centred);
            GUI.color = Color.white;
        }
    }

    // A wide bar for the Warden, with marks where its behaviour changes, so the player
    // can see a phase coming rather than being surprised by it.
    private void DrawTheBossBar()
    {
        if (theRounds == null || theRounds.IsBossRound() == false)
        {
            return;
        }

        WardenBoss boss = Object.FindFirstObjectByType<WardenBoss>();
        if (boss == null)
        {
            return;
        }

        float barWidth = Screen.width * 0.5f;
        float barLeft = (Screen.width - barWidth) * 0.5f;
        float barTop = 58f;
        float barHeight = 18f;

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(barLeft - 3f, barTop - 3f, barWidth + 6f, barHeight + 6f),
            onePlainWhitePixel);

        GUI.color = new Color(0.10f, 0.08f, 0.12f);
        GUI.DrawTexture(new Rect(barLeft, barTop, barWidth, barHeight), onePlainWhitePixel);

        // The bar itself turns bright while the Warden's core is open, because that is
        // the only window in which arrows are worth firing.
        //
        // The armour is the least visible rule in the fight - an arrow that lands for a
        // third of its damage looks exactly like an arrow that landed - so it has to be
        // said somewhere the player is already looking. They watch this bar to see how
        // the fight is going; it may as well also tell them when to shoot.
        bool coreIsOpen = boss.CoreIsOpen();

        GUI.color = new Color(0.62f, 0.30f, 0.85f);
        if (coreIsOpen == true)
        {
            GUI.color = new Color(1f, 0.72f, 0.30f);
        }
        GUI.DrawTexture(new Rect(barLeft, barTop, barWidth * boss.HealthFraction(), barHeight),
            onePlainWhitePixel);

        // Phase marks at two thirds and one third.
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(barLeft + barWidth * 0.66f, barTop, 2f, barHeight), onePlainWhitePixel);
        GUI.DrawTexture(new Rect(barLeft + barWidth * 0.33f, barTop, 2f, barHeight), onePlainWhitePixel);

        GUIStyle centred = new GUIStyle(smallTextStyle);
        centred.alignment = TextAnchor.MiddleCenter;

        string underTheBar = "THE WARDEN     PHASE " + boss.CurrentPhase();
        GUI.color = new Color(1f, 1f, 1f, 0.9f);

        if (coreIsOpen == true)
        {
            underTheBar = underTheBar + "     CORE EXPOSED";
            GUI.color = new Color(1f, 0.78f, 0.4f);
        }

        GUI.Label(new Rect(0f, barTop + barHeight + 2f, Screen.width, 20f),
            underTheBar, centred);
        GUI.color = Color.white;
    }

    // Potion charges as pips, plus the drink-in-progress bar. Pips rather than a number
    // because the player needs to read "how many left" without stopping to count.
    private void DrawThePotions()
    {
        if (playerHealing == null)
        {
            return;
        }

        float pipLeft = 34f;
        float pipTop = 130f;
        float pipSize = 18f;
        float pipGap = 6f;

        int pipIndex = 0;
        while (pipIndex < playerHealing.maximumCharges)
        {
            Rect where = new Rect(pipLeft + pipIndex * (pipSize + pipGap), pipTop, pipSize, pipSize);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(where.x - 2f, where.y - 2f, where.width + 4f, where.height + 4f),
                onePlainWhitePixel);

            if (pipIndex < playerHealing.chargesLeft)
            {
                GUI.color = new Color(0.45f, 0.85f, 0.45f);
            }
            else
            {
                GUI.color = new Color(0.16f, 0.18f, 0.16f);
            }
            GUI.DrawTexture(where, onePlainWhitePixel);
            pipIndex = pipIndex + 1;
        }

        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(pipLeft + playerHealing.maximumCharges * (pipSize + pipGap) + 8f,
            pipTop - 2f, 160f, 24f), "POTIONS", smallTextStyle);

        // A bar filling while drinking, so the player can see how much longer they are
        // committed for - which is the whole risk of drinking at the wrong moment.
        if (playerHealing.IsDrinking() == true)
        {
            float barWidth = 200f;
            float barLeft = (Screen.width - barWidth) * 0.5f;
            float barTop = Screen.height * 0.62f;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(barLeft - 2f, barTop - 2f, barWidth + 4f, 14f), onePlainWhitePixel);
            GUI.color = new Color(0.45f, 0.85f, 0.45f);
            GUI.DrawTexture(new Rect(barLeft, barTop, barWidth * playerHealing.DrinkProgress(), 10f),
                onePlainWhitePixel);
            GUI.color = Color.white;

            GUIStyle centred = new GUIStyle(smallTextStyle);
            centred.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0f, barTop - 22f, Screen.width, 20f), "DRINKING", centred);
        }

        string refusal = playerHealing.RefusalMessage();
        if (refusal != "")
        {
            GUIStyle centred = new GUIStyle(smallTextStyle);
            centred.alignment = TextAnchor.MiddleCenter;
            centred.fontSize = 17;
            GUI.color = new Color(0.9f, 0.6f, 0.4f);
            GUI.Label(new Rect(0f, Screen.height * 0.58f, Screen.width, 24f), refusal, centred);
            GUI.color = Color.white;
        }
    }

    private void DrawTheWeapon()
    {
        if (playerWeapons == null)
        {
            return;
        }

        WeaponKind weapon = playerWeapons.WeaponInHand();

        GUIStyle rightAligned = new GUIStyle(normalTextStyle);
        rightAligned.alignment = TextAnchor.MiddleRight;

        // Flares briefly on a swap, so the change registers without the player having to
        // look away from the fight.
        if (playerWeapons.JustSwapped() == true)
        {
            GUI.color = new Color(1f, 0.92f, 0.55f);
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
        }

        GUI.Label(new Rect(Screen.width - 260f, 56f, 226f, 26f), weapon.weaponName, rightAligned);
        GUI.color = Color.white;
    }

    // The crosshair. One mark, in the middle of the screen, and the arrow is aimed to
    // arrive exactly there.
    //
    // An earlier version drew a SECOND marker further down showing where the arrow really
    // landed. It was accurate and it was unreadable: two marks that never line up look
    // like a bug rather than like ballistics. The fix was not a better marker, it was
    // aiming the arrow properly - see LaunchDirectionToHit in PlayerCombat.
    private void DrawTheCrosshair()
    {
        if (playerCombat == null || playerCombat.RangedWeaponIsInHand() == false)
        {
            return;
        }

        float middleX = Screen.width * 0.5f;
        float middleY = Screen.height * 0.5f;

        // Four ticks around an empty centre. The gap matters: a solid dot would sit
        // exactly on top of the distant thing being aimed at and hide it.
        Color reticleColour = new Color(1f, 1f, 1f, 0.7f);

        if (playerCombat.ShotWouldNotGetThere() == true)
        {
            // Something is in the way, or the string is not far enough back to carry that
            // distance. Dimmed rather than hidden - a crosshair that vanishes is worse
            // than one that goes quiet, because the player cannot aim with what is not
            // there.
            reticleColour = new Color(0.62f, 0.62f, 0.62f, 0.38f);
        }
        else if (playerCombat.CrosshairIsOnAnEnemy() == true)
        {
            reticleColour = new Color(1f, 0.42f, 0.34f, 0.95f);
        }

        DrawOneCrosshairMark(middleX - 1f, middleY - 14f, 2f, 7f, reticleColour);
        DrawOneCrosshairMark(middleX - 1f, middleY + 7f, 2f, 7f, reticleColour);
        DrawOneCrosshairMark(middleX - 14f, middleY - 1f, 7f, 2f, reticleColour);
        DrawOneCrosshairMark(middleX + 7f, middleY - 1f, 7f, 2f, reticleColour);

        // A dot appears in the middle at full draw. That is the one moment the shot is at
        // its best, and it should be visible without looking away to read the bar.
        if (playerCombat.DrawFraction() >= 1f)
        {
            DrawOneCrosshairMark(middleX - 1.5f, middleY - 1.5f, 3f, 3f, ColourForDraw(1f));
        }
    }

    // Every mark in the crosshair is a plain rectangle with a darker one behind it - the
    // same trick the bars use. Against a bright sky a thin white mark on its own simply
    // vanishes, and a crosshair that disappears over the skyline is worse than no
    // crosshair at all, because the player stops trusting it.
    private void DrawOneCrosshairMark(float left, float top, float width, float height, Color colour)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(left - 1f, top - 1f, width + 2f, height + 2f), onePlainWhitePixel);

        GUI.color = colour;
        GUI.DrawTexture(new Rect(left, top, width, height), onePlainWhitePixel);

        GUI.color = Color.white;
    }

    // Shared by the draw bar and the full-draw dot, so the two are never two different
    // colours describing the same shot.
    private Color ColourForDraw(float drawn)
    {
        // Dull to bright as the string comes back, then green at full draw, so the moment
        // to loose is visible without reading anything.
        if (drawn >= 1f)
        {
            return new Color(0.55f, 1f, 0.65f);
        }

        // Below the minimum there is no shot at all, and the bar says so in red rather
        // than in the same warm colour it uses for a weak but real draw. Those are two
        // different facts and they must not look alike.
        if (playerCombat != null && drawn < playerCombat.MinimumDrawToLoose())
        {
            return new Color(0.85f, 0.28f, 0.24f);
        }

        return Color.Lerp(
            new Color(0.55f, 0.45f, 0.25f),
            new Color(1f, 0.92f, 0.55f),
            drawn);
    }

    // A bar under the crosshair that fills as the bow is drawn.
    //
    // Without this the weapon is unusable. Its whole design is that holding longer shoots
    // harder and flatter, and a player cannot make that trade if the only way to know how
    // far the string is back is to count in their head.
    // How many arrows are left, and how long until they come back.
    //
    // Shown whenever the bow is in hand rather than only while drawing, because the count
    // is what the player is meant to be PLANNING around. A number that only appears once
    // the string is already back is a number that arrives too late to change the decision
    // it exists to inform.
    //
    // Drawn as pips up to a point and as a bare number past it. Twenty pips is a row the
    // eye has to count rather than read, which defeats the purpose - but four left out of
    // twenty is exactly the situation where the count matters most, and a short row of
    // marks reads instantly at a glance.
    private void DrawTheQuiver()
    {
        if (playerCombat == null || playerCombat.RangedWeaponIsInHand() == false)
        {
            return;
        }

        PlayerQuiver quiver = playerCombat.Quiver();
        if (quiver == null)
        {
            return;
        }

        int left = quiver.ArrowsLeft();
        int full = quiver.ArrowsWhenFull();

        float boxWidth = 190f;
        float boxLeft = Screen.width * 0.5f - boxWidth * 0.5f;
        float boxTop = Screen.height * 0.5f + 52f;

        GUIStyle centred = new GUIStyle(smallTextStyle);
        centred.alignment = TextAnchor.MiddleCenter;

        // Amber as it gets low, red at empty. The colour is doing the same job the health
        // bar's does - saying "this is becoming a problem" before it is one.
        Color countColour = new Color(0.92f, 0.88f, 0.72f);
        if (left == 0)
        {
            countColour = new Color(0.92f, 0.32f, 0.28f);
        }
        else if (left <= full / 4)
        {
            countColour = new Color(1f, 0.72f, 0.30f);
        }

        GUI.color = countColour;
        GUI.Label(new Rect(0f, boxTop, Screen.width, 20f),
            "ARROWS  " + left + " / " + full, centred);

        // The refill clock, and only while it is actually running. A timer sitting at
        // zero on a full quiver is a number that means nothing and still asks to be read.
        float secondsLeft = quiver.SecondsUntilRefill();
        if (secondsLeft > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(0f, boxTop + 17f, Screen.width, 20f),
                "refills in " + Mathf.CeilToInt(secondsLeft) + "s", centred);

            // A thin bar under it, because a counting number tells the player how long is
            // left but not how far through they are, and the second question is the one
            // that decides whether to keep shooting or start swinging.
            float barWidth = 120f;
            float barLeft = Screen.width * 0.5f - barWidth * 0.5f;
            float barTop = boxTop + 36f;
            float howFarThrough = 1f - (secondsLeft / quiver.secondsToRefill);

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(barLeft - 1f, barTop - 1f, barWidth + 2f, 5f),
                onePlainWhitePixel);

            GUI.color = new Color(0.55f, 0.70f, 0.95f, 0.9f);
            GUI.DrawTexture(new Rect(barLeft, barTop, barWidth * howFarThrough, 3f),
                onePlainWhitePixel);
        }

        GUI.color = Color.white;
    }

    private void DrawTheBowDraw()
    {
        if (playerCombat == null)
        {
            return;
        }

        float drawn = playerCombat.DrawFraction();

        // The bar is also shown, empty, while the last shot is still being recovered
        // from with the button already held. Otherwise the player holds the button and
        // nothing at all appears for a third of a second, which reads as the bow having
        // stopped working rather than as a weapon that has not finished its last shot.
        bool recovering = playerCombat.IsRecoveringWithTheBowHeld();
        if (drawn <= 0f && recovering == false)
        {
            return;
        }

        float width = 190f;
        float left = Screen.width * 0.5f - width * 0.5f;
        float top = Screen.height * 0.5f + 34f;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(left - 2f, top - 2f, width + 4f, 12f), onePlainWhitePixel);

        GUI.color = EmptyBarColour;
        GUI.DrawTexture(new Rect(left, top, width, 8f), onePlainWhitePixel);

        GUI.color = ColourForDraw(drawn);
        GUI.DrawTexture(new Rect(left, top, width * drawn, 8f), onePlainWhitePixel);

        // The notch where the shot becomes real. Everything left of it is a draw that
        // will slip and lose the arrow, so the bar has to show where the line is rather
        // than leaving the player to find it by failing.
        float minimumDraw = playerCombat.MinimumDrawToLoose();
        if (minimumDraw > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(
                new Rect(left + width * minimumDraw - 1f, top - 2f, 2f, 12f),
                onePlainWhitePixel);
        }

        GUI.color = Color.white;
    }

    private void DrawTheShrinePrompt()
    {
        if (theShrine == null)
        {
            return;
        }

        string confirmation = theShrine.ConfirmationMessage();
        if (confirmation != "")
        {
            GUI.color = EssenceColour;
            GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 60f), confirmation, bigMessageStyle);
            GUI.color = Color.white;
        }

        if (theShrine.PlayerIsCloseEnough() == false)
        {
            return;
        }

        int cost = 3;
        if (GameDirector.instance != null)
        {
            cost = GameDirector.instance.essenceCostPerUpgrade;
        }

        float boxWidth = 460f;
        float boxLeft = (Screen.width - boxWidth) * 0.5f;
        float boxTop = Screen.height - 190f;

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(boxLeft, boxTop, boxWidth, 104f), onePlainWhitePixel);

        GUI.color = EssenceColour;
        GUI.Label(new Rect(boxLeft + 18f, boxTop + 10f, boxWidth, 26f),
            "SHRINE  -  " + cost + " essence per offering", normalTextStyle);

        GUI.color = Color.white;
        GUI.Label(new Rect(boxLeft + 18f, boxTop + 40f, boxWidth, 24f), "[1]  Vitality   +25 max health", smallTextStyle);
        GUI.Label(new Rect(boxLeft + 18f, boxTop + 60f, boxWidth, 24f), "[2]  Strength   +6 attack damage", smallTextStyle);
        GUI.Label(new Rect(boxLeft + 18f, boxTop + 80f, boxWidth, 24f), "[3]  Endurance  +20 max stamina", smallTextStyle);
    }

    // The one prompt that is not about a fight. Drawn in the same place as the shrine's,
    // because the player has already learned to look there for "you can do something
    // here".
    private void DrawTheSpeakPrompt()
    {
        if (everyWizard == null)
        {
            return;
        }

        string prompt = "";
        int index = 0;
        while (index < everyWizard.Length)
        {
            if (everyWizard[index] != null && everyWizard[index].gameObject.activeInHierarchy == true)
            {
                string thisOne = everyWizard[index].PromptText();
                if (thisOne != "")
                {
                    prompt = thisOne;
                }
            }
            index = index + 1;
        }

        if (prompt == "")
        {
            return;
        }

        float boxWidth = 300f;
        float boxLeft = (Screen.width - boxWidth) * 0.5f;
        float boxTop = Screen.height - 190f;

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(boxLeft, boxTop, boxWidth, 40f), onePlainWhitePixel);

        GUIStyle centred = new GUIStyle(normalTextStyle);
        centred.alignment = TextAnchor.MiddleCenter;

        GUI.color = new Color(0.78f, 0.62f, 1f);
        GUI.Label(new Rect(boxLeft, boxTop + 8f, boxWidth, 26f), prompt, centred);
        GUI.color = Color.white;
    }

    // The name of the Warden's Edge, held across the middle of the screen for a few
    // seconds after it is taken. This is the reward for the hardest fight in the game and
    // it deserves more than a line in the corner.
    private void DrawTheNewWeaponName()
    {
        if (WardenGem.SecondsOfNameLeft <= 0f)
        {
            return;
        }

        float opacity = WardenGem.SecondsOfNameLeft;
        if (opacity > 1f)
        {
            opacity = 1f;
        }

        GUI.color = new Color(0.80f, 0.62f, 1f, opacity);
        GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 70f),
            "WARDEN'S EDGE", bigMessageStyle);

        GUI.color = new Color(0.94f, 0.90f, 1f, opacity * 0.85f);
        GUI.Label(new Rect(0f, Screen.height * 0.34f + 58f, Screen.width, 30f),
            "a blade of the Vault's own light", SmallCentred());

        GUI.color = Color.white;
    }

    private void DrawAnyBigMessage()
    {
        if (GameDirector.instance == null)
        {
            return;
        }

        if (GameDirector.instance.theWardenIsDead == true)
        {
            // With a story in the scene this is no longer the end of the game - there is
            // a gem to pick up, a walk home and a conversation still to come, and a
            // caption sitting permanently across the middle of the screen would cover
            // every one of them.
            if (theStory != null)
            {
                return;
            }

            GUI.color = new Color(0.42f, 0.95f, 0.84f);
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 70f), "THE VALLEY IS YOURS", bigMessageStyle);
            GUI.color = Color.white;
            return;
        }

        if (GameDirector.instance.PlayerIsDead() == true)
        {
            // The lead-in to the death screen rather than the whole story of dying. This
            // caption has about two seconds before MainMenu puts YOU ARE DEAD up over the
            // top of it, and this whole method stops drawing the moment it does, because
            // OnGUI above returns early while any menu is showing.
            //
            // It used to add "your essence is kept - spend it at the shrine" underneath,
            // which was true of the old loop where dying silently restarted the round.
            // Death now offers a checkpoint instead, and what comes back is whatever the
            // checkpoint holds, so promising the shrine here would be a lie.
            GUI.color = new Color(0.85f, 0.25f, 0.25f);
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 70f), "YOU DIED", bigMessageStyle);
            GUI.color = Color.white;
        }
    }

    private GUIStyle SmallCentred()
    {
        GUIStyle centred = new GUIStyle(smallTextStyle);
        centred.alignment = TextAnchor.MiddleCenter;
        centred.fontSize = 17;
        return centred;
    }
}
