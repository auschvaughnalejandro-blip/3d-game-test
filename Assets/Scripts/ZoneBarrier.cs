using UnityEngine;

// A slab of rock that grinds up out of the valley floor to seal one zone off from the
// next, and sinks again when the fight moves on.
//
// This is what makes the five rounds feel like five different places rather than five
// waves in the same field. The valley already has three distinct spaces built into it;
// the barriers are what let a round hand the player exactly one of them.
public class ZoneBarrier : MonoBehaviour
{
    public float moveSeconds = 1.5f;

    private Vector3 raisedPosition;
    private Vector3 buriedPosition;

    // Where it is heading: 1 for raised, 0 for buried.
    private float wantedState = 0f;
    private float currentState = 0f;

    private Collider ownCollider;

    void Awake()
    {
        raisedPosition = transform.position;

        float height = 8f;
        Renderer ownRenderer = GetComponent<Renderer>();
        if (ownRenderer != null)
        {
            height = ownRenderer.bounds.size.y;
        }
        buriedPosition = raisedPosition + Vector3.down * (height + 1f);

        ownCollider = GetComponent<Collider>();

        // Starts buried. Nothing is sealed until a round says so.
        transform.position = buriedPosition;
        currentState = 0f;
        wantedState = 0f;
        UpdateCollider();
    }

    public void Raise()
    {
        if (wantedState < 1f)
        {
            GameSound.PlayAt("BarrierMove", transform.position, 0.7f);
        }
        wantedState = 1f;
    }

    public void Sink()
    {
        wantedState = 0f;
    }

    // Puts it in place with no animation, for setting a round up before play resumes.
    public void SnapTo(bool raised)
    {
        if (raised == true)
        {
            wantedState = 1f;
            currentState = 1f;
            transform.position = raisedPosition;
        }
        else
        {
            wantedState = 0f;
            currentState = 0f;
            transform.position = buriedPosition;
        }
        UpdateCollider();
    }

    void Update()
    {
        if (Mathf.Abs(currentState - wantedState) < 0.001f)
        {
            return;
        }

        float step = Time.deltaTime / moveSeconds;
        if (currentState < wantedState)
        {
            currentState = currentState + step;
            if (currentState > wantedState)
            {
                currentState = wantedState;
            }
        }
        else
        {
            currentState = currentState - step;
            if (currentState < wantedState)
            {
                currentState = wantedState;
            }
        }

        // Eased at both ends, so a wall this size never looks like it is on a lift.
        float eased = currentState * currentState * (3f - 2f * currentState);
        transform.position = Vector3.Lerp(buriedPosition, raisedPosition, eased);

        UpdateCollider();
    }

    // The collider is switched off once the slab is mostly underground, so a buried
    // barrier cannot trip the player up on ground that looks completely clear.
    private void UpdateCollider()
    {
        if (ownCollider == null)
        {
            return;
        }
        ownCollider.enabled = currentState > 0.15f;
    }

    public bool IsRaised()
    {
        return currentState > 0.9f;
    }
}
