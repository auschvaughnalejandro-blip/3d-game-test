using UnityEngine;

// What a swing of a weapon LOOKS like.
//
// Gameplay does not read this at all - the damage, reach, arc and cooldown are separate
// numbers and none of them are decided here. It exists so that the body knows which shape
// to make, exactly the way modelPartName says which mesh to show and weaponName picks the
// sound. A fourth weapon added later names its shape and animates without ProceduralAnimator
// knowing it exists.
public enum SwingShape
{
    // Straight down the line, elbow-driven. The sword.
    Stab,

    // Up over the shoulder and down with the whole body behind it. The hammer.
    Smash,

    // Flat and horizontal, the body turning underneath a held arm. The Warden's Edge.
    Sweep,
}

// One weapon, as pure data. No behaviour at all - the same trick the enemies use, where
// a Grunt and a Darter are one script separated only by numbers.
[System.Serializable]
public class WeaponKind
{
    public string weaponName = "Sword";

    public float damage = 20f;
    public float cooldownSeconds = 0.45f;
    public float reach = 2.6f;

    public float heavyDamageMultiplier = 2.5f;
    public float heavyCooldownSeconds = 0.95f;
    public float heavyReach = 3.2f;
    public float heavyStaminaCost = 20f;

    public float knockback = 7f;

    // How wide the swing is, in degrees, centred on the way the player is facing.
    // Until this existed a swing hit everything within reach in EVERY direction,
    // including things directly behind the player, which was quietly generous and made
    // every weapon feel the same. Narrow is precise; wide clears a circle.
    public float swingArcDegrees = 100f;

    // The piece of the player model this weapon shows. Everything else is hidden.
    public string modelPartName = "Sword";

    // Which of the three swing shapes this weapon makes. See SwingShape above.
    public SwingShape swingShape = SwingShape.Stab;

    // A ranged weapon is not swung. It is drawn, held, and loosed - and the longer it is
    // drawn the harder it hits and the flatter it flies.
    public bool isRanged = false;
    public float secondsToFullDraw = 1.4f;
    public float arrowSpeedAtFullDraw = 42f;
    public float arrowSpeedAtNoDraw = 16f;

    // How far back the string has to be before there is a shot at all. Under this the
    // draw is abandoned and no arrow leaves.
    //
    // This exists because the bow used to be at its most dangerous when tapped. The
    // recovery was a flat cost paid per SHOT while the draw was free, so firing at the
    // fastest the recovery allowed beat aiming by a wide margin - the exact opposite of
    // what a bow is meant to be. A floor under the draw is what removes the tap as a
    // move at all, rather than merely making it weak enough to ignore.
    public float minimumDrawToLoose = 0.35f;

    // Stamina burned per second while the string is held back. The bow was the one
    // weapon in the game outside the stamina economy - no cost to fire, no ammunition,
    // and no competition with the dodge for the same pool. Holding an aim is now a bet
    // against being able to roll out of what is coming.
    public float staminaPerSecondDrawing = 12f;

    // What is left of the player's speed while aiming. Sprinting is refused outright.
    // Without this the player can run backwards faster than anything in the game can
    // follow while holding a full draw, which is the whole of the kiting problem.
    public float movementWhileDrawing = 0.45f;
}

// The two weapons the player carries, and which one is in hand.
//
// The point is that neither is better. The sword swings more than twice as often; the
// hammer hits more than twice as hard with far more reach. Darters retreat after every
// charge, so the sword's speed catches them. Grunts and the Warden commit to long
// wind-ups, so the hammer's slow swing fits in the gap they leave. Choosing wrongly is
// survivable but noticeably worse, which is what makes it a choice.
public class PlayerWeapons : MonoBehaviour
{
    public WeaponKind sword = new WeaponKind();
    public WeaponKind hammer = new WeaponKind();
    public WeaponKind bow = new WeaponKind();
    public WeaponKind wardensEdge = new WeaponKind();

    // Which weapon is in hand: 0 sword, 1 hammer, 2 bow, 3 the Warden's Edge.
    private int weaponInHand = 0;

    // The Edge is not carried into the valley. It is taken off the Warden, and until then
    // it is skipped by the swap so the player never cycles through an empty hand.
    private bool theEdgeHasBeenWon = false;

    // The visible parts, found once at startup so swapping does not search the hierarchy.
    private GameObject[] swordParts;
    private GameObject[] hammerParts;
    private GameObject[] bowParts;
    private GameObject[] edgeParts;

    // Held briefly after a swap so the display can announce it.
    private float announceSecondsRemaining = 0f;

    void Awake()
    {
        SetUpTheWeapons();
    }

    void Start()
    {
        swordParts = FindPartsNamed("Sword");
        hammerParts = FindPartsNamed("Hammer");
        bowParts = FindPartsNamed("Bow");
        edgeParts = FindPartsNamed("Edge");
        ShowOnlyTheWeaponInHand();
    }

    void Update()
    {
        if (announceSecondsRemaining > 0f)
        {
            announceSecondsRemaining = announceSecondsRemaining - Time.deltaTime;
        }

        if (PlayerControl.IsBlocked() == true)
        {
            return;
        }

        if (GameInput.SwapWeaponWasPressed() == true)
        {
            SwapWeapon();
        }
    }

    private void SetUpTheWeapons()
    {
        sword.weaponName = "SWORD";
        sword.damage = 20f;
        sword.cooldownSeconds = 0.45f;
        sword.reach = 2.6f;
        sword.heavyDamageMultiplier = 2.5f;
        sword.heavyCooldownSeconds = 0.95f;
        sword.heavyReach = 3.2f;
        sword.heavyStaminaCost = 20f;
        sword.knockback = 7f;
        sword.modelPartName = "Sword";

        // A point, used as a point. The sword is the fast weapon, and a thrust is the
        // fastest thing a blade can do - there is no arc to travel through, so the
        // animation is as quick as the cooldown says the weapon is.
        sword.swingShape = SwingShape.Stab;

        hammer.weaponName = "HAMMER";
        hammer.damage = 44f;
        // More than twice the sword's cooldown. The hammer is not a strict upgrade; it is
        // a bet that the next opening will be a long one.
        hammer.cooldownSeconds = 1.05f;
        hammer.reach = 3.4f;
        hammer.heavyDamageMultiplier = 1.8f;
        hammer.heavyCooldownSeconds = 1.7f;
        hammer.heavyReach = 4.0f;
        hammer.heavyStaminaCost = 32f;
        hammer.knockback = 16f;
        hammer.modelPartName = "Hammer";

        // Up and down with the whole body behind it. The hammer's case is that it is a
        // bet on a long opening, and a blow that visibly takes a moment to raise is the
        // animation making that bet legible.
        hammer.swingShape = SwingShape.Smash;

        // The answer to the Spitters on the shoulders, who until now could shoot down at
        // the player with nothing that could reach back. Slow enough that using it in a
        // melee is a mistake: a full draw takes longer than a Darter takes to cross the
        // arena.
        bow.weaponName = "BOW";
        bow.isRanged = true;
        bow.damage = 34f;
        bow.cooldownSeconds = 0.35f;
        bow.reach = 60f;
        bow.heavyDamageMultiplier = 1f;
        bow.heavyCooldownSeconds = 0.35f;
        bow.heavyReach = 60f;
        bow.heavyStaminaCost = 0f;
        bow.knockback = 3f;
        bow.modelPartName = "Bow";
        bow.secondsToFullDraw = 1.4f;
        bow.arrowSpeedAtFullDraw = 42f;
        bow.arrowSpeedAtNoDraw = 16f;
        bow.minimumDrawToLoose = 0.35f;
        bow.staminaPerSecondDrawing = 12f;
        bow.movementWhileDrawing = 0.45f;

        // Never used - a bow is loosed, not swung - but left sensible rather than at
        // whatever the default happens to be, the same as swingArcDegrees below.
        bow.swingShape = SwingShape.Stab;

        // The three starting weapons all swing forwards. Roughly a right angle: wide
        // enough to catch a target that has stepped off the centre line, narrow enough
        // that turning to face the right one still matters.
        sword.swingArcDegrees = 100f;
        hammer.swingArcDegrees = 110f;
        // Never used - a bow is aimed, not swung - but left sensible rather than zero.
        bow.swingArcDegrees = 100f;

        // Taken off the Warden. It is straightforwardly better than the sword, and it is
        // meant to be: it is the reward for the hardest fight in the game and the player
        // gets about ninety seconds to enjoy it.
        //
        // What makes it feel different is not the damage, it is the ARC. Two hundred
        // degrees is most of the way around the player, so a single swing clears the ring
        // that the first four rounds spent teaching them to fear.
        wardensEdge.weaponName = "WARDEN'S EDGE";
        wardensEdge.damage = 40f;
        wardensEdge.cooldownSeconds = 0.40f;
        wardensEdge.reach = 5.0f;
        wardensEdge.swingArcDegrees = 200f;
        wardensEdge.heavyDamageMultiplier = 2.2f;
        wardensEdge.heavyCooldownSeconds = 0.9f;
        wardensEdge.heavyReach = 5.8f;
        wardensEdge.heavyStaminaCost = 24f;
        wardensEdge.knockback = 13f;
        wardensEdge.modelPartName = "Edge";

        // Flat and all the way round. This is the one weapon whose animation is carrying
        // a gameplay fact rather than decorating one: the arc really is 200 degrees, and
        // 360 on a heavy, so the body really does have to turn through it. A wheeling
        // overhead swing on a weapon that hits everything behind you would be the
        // animation lying about the reach.
        wardensEdge.swingShape = SwingShape.Sweep;
    }

    // The heavy swing of the Edge goes all the way round. Read by PlayerCombat, which
    // widens the arc rather than this script knowing anything about hit detection.
    public const float EdgeHeavyArcDegrees = 360f;

    // Called by the gem when the player picks it up.
    public void UnlockTheWardensEdge()
    {
        theEdgeHasBeenWon = true;

        // Put straight into the hand. Winning a weapon and then having to find it in a
        // swap cycle is an anticlimax.
        weaponInHand = 3;
        announceSecondsRemaining = 2.4f;
        ShowOnlyTheWeaponInHand();
    }

    public bool TheEdgeHasBeenWon()
    {
        return theEdgeHasBeenWon;
    }

    // Taken back off the player when a run is reset. The Edge is the reward for killing
    // the Warden, and a fresh run that begins with it already in hand has given its own
    // ending away in the first ten seconds.
    public void RelockTheWardensEdge()
    {
        theEdgeHasBeenWon = false;
        weaponInHand = 0;
        announceSecondsRemaining = 0f;
        ShowOnlyTheWeaponInHand();
    }

    private GameObject[] FindPartsNamed(string startsWith)
    {
        Transform[] everything = GetComponentsInChildren<Transform>(true);

        int matches = 0;
        int index = 0;
        while (index < everything.Length)
        {
            if (everything[index].name.StartsWith(startsWith) == true)
            {
                matches = matches + 1;
            }
            index = index + 1;
        }

        GameObject[] found = new GameObject[matches];
        int foundIndex = 0;
        index = 0;
        while (index < everything.Length)
        {
            if (everything[index].name.StartsWith(startsWith) == true)
            {
                found[foundIndex] = everything[index].gameObject;
                foundIndex = foundIndex + 1;
            }
            index = index + 1;
        }
        return found;
    }

    public void SwapWeapon()
    {
        // Sword, hammer, bow, then the Edge if it has been won, then back to the sword.
        weaponInHand = weaponInHand + 1;

        int highestWeapon = 2;
        if (theEdgeHasBeenWon == true)
        {
            highestWeapon = 3;
        }

        if (weaponInHand > highestWeapon)
        {
            weaponInHand = 0;
        }

        GameSound.Play("WeaponSwap", 0.55f);

        announceSecondsRemaining = 1.2f;
        swapsMade = swapsMade + 1;
        ShowOnlyTheWeaponInHand();
    }

    private void ShowOnlyTheWeaponInHand()
    {
        SetPartsVisible(swordParts, weaponInHand == 0);
        SetPartsVisible(hammerParts, weaponInHand == 1);
        SetPartsVisible(bowParts, weaponInHand == 2);
        SetPartsVisible(edgeParts, weaponInHand == 3);
    }

    private void SetPartsVisible(GameObject[] parts, bool visible)
    {
        if (parts == null)
        {
            return;
        }

        int index = 0;
        while (index < parts.Length)
        {
            if (parts[index] != null)
            {
                parts[index].SetActive(visible);
            }
            index = index + 1;
        }
    }

    public WeaponKind WeaponInHand()
    {
        if (weaponInHand == 1)
        {
            return hammer;
        }
        if (weaponInHand == 2)
        {
            return bow;
        }
        if (weaponInHand == 3)
        {
            return wardensEdge;
        }
        return sword;
    }

    // Counted so PlayerAnimator can notice a swap the frame it happens. JustSwapped()
    // cannot be used for this: it stays true for over a second so the HUD can announce
    // the new weapon, which is far longer than the hands take to do the swapping.
    private int swapsMade = 0;

    public int SwapsMade()
    {
        return swapsMade;
    }

    public bool JustSwapped()
    {
        return announceSecondsRemaining > 0f;
    }
}
