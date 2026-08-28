using UnityEngine;

// Orrin, standing still and waiting to be spoken to.
//
// He is deliberately not an animated character. He stands, drifts up and down by a few
// centimetres, and the light he carries breathes. At conversation distance that reads as
// a living person, and it costs nothing next to rigging and animating a robed figure -
// which is a week of work this project does not have.
public class Wizard : MonoBehaviour
{
    public float interactionRadius = 5f;

    // The dungeon Orrin is the one who asks the question. The Orrin waiting in the valley
    // at the end says his piece on his own, so he does not want a prompt hanging over him.
    public bool answersWhenSpokenTo = true;

    // Set false once he has been talked to, so walking past him again does not restart
    // the conversation mid-sentence.
    private bool conversationIsRunning = false;

    private Transform playerTransform;
    private Light carriedLight;

    // Whatever the builder set the staff light to. Breathing is expressed as a fraction
    // of this rather than as an absolute, so changing how bright he is in one place does
    // not silently get overwritten here every frame.
    private float carriedLightRestingIntensity = 1f;
    private Vector3 restingPosition;
    private float secondsAlive = 0f;

    public float bobHeight = 0.09f;
    public float bobSpeed = 1.1f;

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }

        restingPosition = transform.position;
        carriedLight = GetComponentInChildren<Light>();
        if (carriedLight != null)
        {
            carriedLightRestingIntensity = carriedLight.intensity;
        }
    }

    void Update()
    {
        secondsAlive = secondsAlive + Time.deltaTime;

        Drift();

        if (answersWhenSpokenTo == false)
        {
            return;
        }

        // Once the conversation has been opened, the dialogue box owns the player and
        // this script has nothing left to do until it closes.
        if (DialogueBox.ConversationIsOpen() == true)
        {
            conversationIsRunning = true;
            return;
        }
        conversationIsRunning = false;

        if (PlayerIsCloseEnough() == false)
        {
            return;
        }

        if (GameInput.InteractWasPressed() == true)
        {
            SpeakToMe();
        }
    }

    // Start the conversation. Public so the automated play-through can walk up and talk
    // to him the same way a person does.
    public void SpeakToMe()
    {
        OpenTheConversation();
    }

    private void Drift()
    {
        float bobOffset = Mathf.Sin(secondsAlive * bobSpeed) * bobHeight;
        transform.position = restingPosition + Vector3.up * bobOffset;

        if (carriedLight != null)
        {
            // A slow breath rather than a flicker. A flicker reads as a torch; a breath
            // reads as something being held by somebody who is alive.
            float breath = 1f + Mathf.Sin(secondsAlive * 1.7f) * 0.16f;
            carriedLight.intensity = carriedLightRestingIntensity * breath;
        }
    }

    public bool PlayerIsCloseEnough()
    {
        if (playerTransform == null)
        {
            return false;
        }
        if (answersWhenSpokenTo == false)
        {
            return false;
        }
        if (StoryDirector.instance != null
            && StoryDirector.instance.currentAct != StoryDirector.ActInTheDungeon)
        {
            // He has already been agreed with. There is nothing left to ask him.
            return false;
        }

        float distance = Vector3.Distance(playerTransform.position, transform.position);
        return distance <= interactionRadius;
    }

    private void OpenTheConversation()
    {
        if (DialogueBox.instance == null || StoryDirector.instance == null)
        {
            return;
        }

        bool hasAskedBefore = StoryDirector.instance.hasRefusedAtLeastOnce;

        if (hasAskedBefore == false)
        {
            SpeakTheFirstMeeting();
        }
        else
        {
            SpeakTheSecondAsking();
        }
    }

    private void SpeakTheFirstMeeting()
    {
        string orrin = StoryDirector.Orrin;

        DialogueBox.instance.Say(orrin, "You came down here armed. That tells me you already know what is under this valley.");
        DialogueBox.instance.Say(orrin, "The Warden. I put him there. I was younger, and I thought that sealing a thing was the same as solving it.");
        DialogueBox.instance.Say(orrin, "It is not. It is only a way of making it somebody else's afternoon.");
        DialogueBox.instance.Say(orrin, "The seal opens from the outside, and never for the hand that made it. So I cannot go down. But you can.");
        DialogueBox.instance.Ask(orrin,
            "Will you go down and finish what I started?",
            StoryDirector.QuestionFightTheWarden,
            "I will go.",
            "Not today.");
    }

    private void SpeakTheSecondAsking()
    {
        string orrin = StoryDirector.Orrin;

        DialogueBox.instance.Say(orrin, "You have been walking in circles down here for a while now.");
        DialogueBox.instance.Ask(orrin,
            "Have you decided?",
            StoryDirector.QuestionFightTheWarden,
            "I will go.",
            "Still not today.");
    }

    // What the display should show floating over him. Empty means show nothing.
    public string PromptText()
    {
        if (PlayerIsCloseEnough() == false)
        {
            return "";
        }
        if (conversationIsRunning == true)
        {
            return "";
        }
        return "[E]  speak to Orrin";
    }
}
