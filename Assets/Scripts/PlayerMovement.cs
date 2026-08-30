using UnityEngine;

// Walking, sprinting and the dodge-roll.
// The dodge is the important one: it costs stamina, and stamina is the only thing
// stopping the player from spamming it. That single rule is what makes the combat feel
// tense instead of mashy.
public class PlayerMovement : MonoBehaviour
{
    public float walkingSpeed = 5.5f;
    public float sprintingSpeed = 8.5f;

    [Header("Dodge roll")]
    public float dodgeSpeed = 16f;
    public float dodgeLastsSeconds = 0.35f;
    public float dodgeCostsStamina = 30f;

    [Header("Jumping")]
    // Height reached is speed squared over twice gravity. Gravity here is 22, not the
    // real world's 9.8, so the arithmetic is unforgiving: 4.6 m/s reached barely half a
    // metre and read as a stumble rather than a jump.
    //
    // 7.3 m/s clears about 1.2 m - comfortably more than half the player's 1.77 m - and
    // is what the Warden's ground shockwave is tuned against.
    // 9.5 clears 2.05 m against gravity of 22, and the player is 1.77 m tall - so the
    // jump now goes over their own head with a little room to spare. At 7.3 it cleared
    // 1.21 m, which is chest height and reads as a hop rather than a jump.
    public float jumpSpeed = 9.5f;
    // Steering is cut once airborne. A jump that can be steered freely is just a second
    // dodge; one that commits you to an arc is a decision.
    public float airControlFraction = 0.6f;

    [Header("Falling")]
    public float gravityStrength = 22f;

    // How fast the character model spins to face the way it is running.
    public float turningSpeed = 14f;

    private CharacterController bodyController;
    private CharacterStats ownStats;
    private OrbitCamera theCamera;

    private float verticalSpeed = 0f;
    private float dodgeSecondsRemaining = 0f;
    // True from the frame a jump starts until the controller reports ground again.
    private bool isAirborne = false;
    private Vector3 dodgeDirection = Vector3.zero;

    // The last place the player was genuinely standing on solid ground.
    //
    // Remembered so that if they ever do end up in empty space they can be put back
    // somewhere that is solid BY DEFINITION - in whichever room they happened to be in,
    // the valley or the Vault or Orrin's cellar - without this script having to know
    // which room that is or where its floor begins.
    private Vector3 lastPlaceStoodSafely = Vector3.zero;
    private bool haveStoodSomewhereSafe = false;

    // Read by PlayerCombat so the player cannot swing a sword mid-roll.
    public bool IsCurrentlyDodging()
    {
        return dodgeSecondsRemaining > 0f;
    }

    // Read by PlayerHealing, which refuses to let a potion be drunk in mid-air.
    public bool IsAirborne()
    {
        return isAirborne;
    }

    // How far through the roll we are, from 0 at the start to 1 at the end, or -1 when
    // there is no roll happening.
    //
    // Read by PlayerAnimator so the dodge animation runs on the same clock as the dodge
    // itself. Deriving it here rather than timing it again in the animator is what stops
    // the two drifting apart - an animation that outlasts its own invulnerability window
    // is the game lying to the player about when they were safe.
    public float DodgeProgress()
    {
        if (dodgeSecondsRemaining <= 0f || dodgeLastsSeconds <= 0f)
        {
            return -1f;
        }

        return 1f - (dodgeSecondsRemaining / dodgeLastsSeconds);
    }

    // Which way the roll is going, in world space. The animator needs it to decide which
    // side to throw the body over.
    public Vector3 DodgeDirection()
    {
        return dodgeDirection;
    }

    // The jump itself, separated from the key that asks for it. Returns false when
    // refused, which is either mid-air or mid-roll.
    public bool TryToJump()
    {
        if (bodyController.isGrounded == false || isAirborne == true)
        {
            return false;
        }
        if (dodgeSecondsRemaining > 0f)
        {
            return false;
        }

        verticalSpeed = jumpSpeed;
        isAirborne = true;
        GameSound.Play("Jump", 0.4f);
        return true;
    }

    // How fast the player is currently moving up or down. Read by tests, and later by
    // the Warden's shockwave to decide whether the player was in the air when it passed.
    public float VerticalSpeed()
    {
        return verticalSpeed;
    }

    void Awake()
    {
        bodyController = GetComponent<CharacterController>();
        ownStats = GetComponent<CharacterStats>();
        theCamera = Camera.main.GetComponent<OrbitCamera>();
    }

    void Update()
    {
        if (ownStats.isDead == true)
        {
            return;
        }

        // Standing still while somebody talks to you. Gravity is deliberately still
        // applied below via the dodge and movement paths being skipped entirely - the
        // player is on the ground in both places a conversation can happen, so there is
        // nothing to fall.
        if (PlayerControl.IsBlocked() == true)
        {
            return;
        }

        if (dodgeSecondsRemaining > 0f)
        {
            ContinueTheDodge();
            return;
        }

        HandleNormalMovement();
    }

    private void ContinueTheDodge()
    {
        dodgeSecondsRemaining = dodgeSecondsRemaining - Time.deltaTime;

        ApplyGravity();

        Vector3 movementThisFrame = dodgeDirection * dodgeSpeed;
        movementThisFrame.y = verticalSpeed;
        bodyController.Move(movementThisFrame * Time.deltaTime);
    }

    private void HandleNormalMovement()
    {
        float sidewaysInput = GameInput.SidewaysAxis();
        float forwardInput = GameInput.ForwardAxis();

        // Movement is expressed relative to where the camera is looking, so pressing W
        // always means away from the camera no matter which way the player is facing.
        Quaternion cameraFacing = Quaternion.Euler(0f, theCamera.CurrentYawDegrees(), 0f);
        Vector3 desiredDirection = cameraFacing * new Vector3(sidewaysInput, 0f, forwardInput);

        if (desiredDirection.sqrMagnitude > 1f)
        {
            desiredDirection = desiredDirection.normalized;
        }

        // Jump first: it is the cheaper action and should win if both are pressed on the
        // same frame. Dodging in mid-air is refused outright.
        if (GameInput.JumpWasPressed() == true)
        {
            TryToJump();
        }

        bool wantsToDodge = GameInput.DodgeWasPressed();
        if (wantsToDodge == true
            && isAirborne == false
            && bodyController.isGrounded == true
            && desiredDirection.sqrMagnitude > 0.01f)
        {
            bool couldAffordIt = ownStats.TrySpendStamina(dodgeCostsStamina);
            if (couldAffordIt == true)
            {
                dodgeSecondsRemaining = dodgeLastsSeconds;
                dodgeDirection = desiredDirection.normalized;
                GameSound.Play("Dodge", 0.5f);
                return;
            }
        }

        bool isSprinting = GameInput.SprintIsHeld() && desiredDirection.sqrMagnitude > 0.01f;
        float speedThisFrame = walkingSpeed;
        if (isSprinting == true)
        {
            speedThisFrame = sprintingSpeed;
        }

        // A kill streak makes the player faster on foot. It is applied to walking and
        // sprinting but deliberately NOT to the dodge: the roll's distance is tuned
        // against the reach of all three enemy attack shapes, and a longer one would
        // quietly break the timing of every fight in the game.
        speedThisFrame = speedThisFrame * PlayerSurge.MovementSpeedMultiplierNow();

        ApplyGravity();

        // Steering authority drops away once the feet leave the ground.
        if (bodyController.isGrounded == false)
        {
            speedThisFrame = speedThisFrame * airControlFraction;
        }

        Vector3 movementThisFrame = desiredDirection * speedThisFrame;
        movementThisFrame.y = verticalSpeed;
        bodyController.Move(movementThisFrame * Time.deltaTime);

        TurnToFaceDirectionOfTravel(desiredDirection);
    }

    // Holds the player on top of the floor, and puts them back if they ever get off it.
    //
    // The player had NO protection of any kind against this before. An enemy that went
    // through the floor was a round that could not be finished; a player who went through
    // it was the end of the run - falling for ever in empty space with nothing to land on,
    // no ground to walk back to, and nothing in the game watching for it. The only way out
    // was the pause menu.
    //
    // The reason it happens is the same one it happens to the enemies for, and the comment
    // on EnemyBrain.LateUpdate explains it: the floors are imported meshes, a non-convex
    // mesh collider is a one-sided sheet, and there is nothing behind it.
    //
    // Run in LateUpdate rather than at the end of Update because Update returns early in
    // several places - while dead, and while a conversation is open - and the floor should
    // hold the player up in those states just as much as in any other.
    void LateUpdate()
    {
        if (bodyController == null || bodyController.enabled == false)
        {
            return;
        }

        float floorHeight;
        bool overAMeshFloor =
            ValleyBuilder.TryFindFloorUnder(transform.position, out floorHeight);

        // Measured at the feet, because the controller is centred on the body.
        float feetHeight = transform.position.y - bodyController.height * 0.5f;

        // Six-tenths of a metre of slack, for the same reason the enemies get it: on a
        // sculpted slope the floor directly under the middle of a capsule sits a little
        // above the point the capsule is really resting on.
        bool hasBeenPushedThroughTheFloor =
            overAMeshFloor == true && feetHeight < floorHeight - 0.6f;

        // Lifted straight back on top, where they already were.
        //
        // This is tried BEFORE the teleport below, and it is what almost every case turns
        // out to be. Getting that order the wrong way round is worth more than it sounds:
        // the automated play-through walks the player into the portal by standing them on
        // the position of the portal itself, which is set a little under the terrain, and
        // with the teleport going first the player was flung sixty metres back down the
        // valley to the start once a frame for as long as they stood there. Lifting them
        // the metre they are actually short is invisible, and leaves them where they were
        // going.
        if (hasBeenPushedThroughTheFloor == true)
        {
            Vector3 backOnTop = transform.position;
            backOnTop.y = floorHeight + bodyController.height * 0.5f;

            // Switched off across the move, or the controller fights it and drags the
            // player back down to where it believes they should be.
            bodyController.enabled = false;
            transform.position = backOnTop;
            bodyController.enabled = true;

            verticalSpeed = 0f;
            return;
        }

        // Gone entirely: no floor anywhere above them to be put back on top of, and a long
        // way down with it. This is the one case a lift cannot answer, and the only one
        // worth a teleport.
        bool hasFallenOutOfTheWorld =
            overAMeshFloor == false
            && (transform.position.y < -40f
                || ValleyBuilder.IsUnderneathTheValley(transform.position) == true);

        if (hasFallenOutOfTheWorld == true && haveStoodSomewhereSafe == true)
        {
            Debug.LogWarning("The player ended up out of the world at "
                + transform.position + " with no floor above them, and was put back on the"
                + " last solid ground they stood on, at " + lastPlaceStoodSafely + ".");
            TeleportTo(lastPlaceStoodSafely);
            return;
        }

        // Standing on something, on the right side of it. Worth remembering.
        //
        // Recorded from LateUpdate, so it is the position AFTER everything has moved the
        // player this frame - which is the position that was actually safe.
        if (bodyController.isGrounded == true && hasFallenOutOfTheWorld == false)
        {
            lastPlaceStoodSafely = transform.position;
            haveStoodSomewhereSafe = true;
        }
    }

    private void TurnToFaceDirectionOfTravel(Vector3 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion wantedRotation = Quaternion.LookRotation(desiredDirection);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            wantedRotation,
            turningSpeed * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (bodyController.isGrounded == true)
        {
            // Rising counts as airborne even though the controller may still report
            // ground on the very first frame of a jump, so the flag is only cleared once
            // the upward push has actually gone.
            if (verticalSpeed <= 0f)
            {
                isAirborne = false;

                // A small downward push rather than zero keeps the controller pressed
                // into the ground, which stops it reporting "not grounded" every other
                // frame on slopes and stairs.
                verticalSpeed = -2f;
            }
        }
        else
        {
            verticalSpeed = verticalSpeed - gravityStrength * Time.deltaTime;
        }
    }

    public void TeleportTo(Vector3 whereTo)
    {
        // The controller has to be switched off for a frame, otherwise it fights the
        // teleport and drags the player back to where it thinks they should be.
        bodyController.enabled = false;
        transform.position = whereTo;
        bodyController.enabled = true;

        verticalSpeed = 0f;
        dodgeSecondsRemaining = 0f;
        isAirborne = false;
    }
}
