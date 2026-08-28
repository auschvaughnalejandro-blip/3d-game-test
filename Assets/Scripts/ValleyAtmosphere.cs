using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The post-processing stack: everything applied to the finished image AFTER the world has
// been drawn.
//
// This is the single largest visual difference between a project that looks like a
// prototype and one that looks like a product, and almost none of it is about the models.
// Raw output from a renderer is flat and clinical. Real games grade it - crush the blacks,
// warm the highlights, let bright things bleed, darken the corners of the frame.
//
// It is built here in code rather than as a saved asset so there is nothing to configure
// by hand and nothing that can be lost.
public class ValleyAtmosphere : MonoBehaviour
{
    private Volume theVolume;
    private VolumeProfile theProfile;

    void Awake()
    {
        BuildTheVolume();
        AddTonemapping();
        AddColourGrading();
        AddBloom();
        AddVignette();
        AddFilmGrain();
    }

    private void BuildTheVolume()
    {
        // Reuse the Global Volume the URP template already put in the scene if it is
        // there, rather than ending up with two fighting over the same settings.
        theVolume = Object.FindFirstObjectByType<Volume>();
        if (theVolume == null)
        {
            GameObject volumeObject = new GameObject("ValleyAtmosphereVolume");
            theVolume = volumeObject.AddComponent<Volume>();
        }

        theVolume.isGlobal = true;
        theVolume.priority = 100f;

        theProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        theVolume.profile = theProfile;
    }

    // Tonemapping decides how the renderer's raw brightness values get squeezed into what
    // a screen can actually display. Without it, anything bright clips to flat white.
    // ACES is the curve film and most modern games use; it rolls highlights off gently
    // and is most of why a graded image looks "cinematic".
    private void AddTonemapping()
    {
        Tonemapping tonemapping = theProfile.Add<Tonemapping>(true);
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;
    }

    private void AddColourGrading()
    {
        ColorAdjustments grading = theProfile.Add<ColorAdjustments>(true);

        grading.postExposure.overrideState = true;
        grading.postExposure.value = 0.75f;

        // Pushing contrast up separates the rock from the ground, which flat lighting
        // alone never manages.
        grading.contrast.overrideState = true;
        grading.contrast.value = 18f;

        // A slightly warm, slightly desaturated grade. Full saturation reads as cartoon;
        // pulling it back a little reads as photographed.
        grading.colorFilter.overrideState = true;
        grading.colorFilter.value = new Color(1f, 0.97f, 0.92f);

        grading.saturation.overrideState = true;
        grading.saturation.value = -6f;

        // Split toning: cool shadows against warm highlights. This one trick is
        // responsible for an enormous share of how expensive a game looks.
        ShadowsMidtonesHighlights zones = theProfile.Add<ShadowsMidtonesHighlights>(true);
        zones.shadows.overrideState = true;
        zones.shadows.value = new Vector4(0.92f, 0.96f, 1.08f, 0f);
        zones.highlights.overrideState = true;
        zones.highlights.value = new Vector4(1.06f, 1.01f, 0.94f, 0f);
    }

    // Bright things bleeding light into their surroundings. Real camera lenses do this,
    // and its absence is one of the things that makes untouched renderer output look
    // synthetic. It is also what makes the glowing eyes and the shrine actually glow
    // rather than just being brightly coloured.
    private void AddBloom()
    {
        Bloom bloom = theProfile.Add<Bloom>(true);

        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.85f;

        // Only things brighter than this bleed. Set too low and the whole image fogs up.
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.9f;

        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.62f;

        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1f, 0.96f, 0.88f);
    }

    // Darkening towards the corners. It pulls the eye to the middle of the frame, which
    // is where the character is.
    private void AddVignette()
    {
        Vignette vignette = theProfile.Add<Vignette>(true);

        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.20f;

        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;
    }

    // A whisper of grain. Perfectly clean images read as computer-generated; a very small
    // amount of noise reads as captured. Kept low deliberately - grain is easy to overdo.
    private void AddFilmGrain()
    {
        FilmGrain grain = theProfile.Add<FilmGrain>(true);

        grain.type.overrideState = true;
        grain.type.value = FilmGrainLookup.Thin1;

        grain.intensity.overrideState = true;
        grain.intensity.value = 0.18f;
    }

    void OnDestroy()
    {
        // The profile was created in code rather than loaded from disk, so it has to be
        // cleaned up by hand or it leaks every time play mode restarts.
        if (theProfile != null)
        {
            Object.Destroy(theProfile);
        }
    }
}
