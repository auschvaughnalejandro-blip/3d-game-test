using UnityEngine;

// One visual style. This is pure data - a description of how the valley should LOOK.
// There is no behaviour in here at all, which is the entire point: a style cannot break
// the game, because a style cannot do anything. The worst a bad one can be is ugly.
[System.Serializable]
public class VisualStyle
{
    public string styleName = "Natural";

    // When true this style hands every object back the material it was built with,
    // instead of repainting it. That is how the detailed procedural rock survives: the
    // natural style is not a repaint at all, it is the absence of one.
    public bool keepsTheOriginalMaterials = false;

    // Unlit throws away all shading and draws flat colour. That single switch is most of
    // the difference between something that looks like a rendered world and something
    // that looks like a diagram.
    public bool drawsFlatWithoutLighting = false;

    // How each object's own colour is transformed before being drawn.
    // 0 keeps it, 1 drains it to grey, 2 pushes it to full saturation.
    public int colourTreatment = 0;
    public float brightnessMultiplier = 1f;

    public Color skyAndFogColour = new Color(0.62f, 0.63f, 0.60f);
    public float fogStartDistance = 45f;
    public float fogEndDistance = 230f;

    public Color sunColour = Color.white;
    public float sunIntensity = 1.35f;
    public bool sunCastsShadows = true;

    public Color ambientColour = new Color(0.40f, 0.43f, 0.48f);
}

// Swaps the whole valley between visual styles at the press of a key.
//
// This is the part that is not just another small RPG. The geometry, the enemies, the
// numbers and every line of gameplay code stay exactly the same - only the way it is
// drawn changes. Content and appearance are separate things, and both can be authored.
public class StyleLens : MonoBehaviour
{
    private VisualStyle[] availableStyles;
    private int currentStyleIndex = 0;

    // The colour each renderer was given when the valley was built. Every style is
    // computed from these, never from whatever is on screen right now, so cycling
    // through the styles repeatedly never drifts or degrades.
    private Renderer[] everyRenderer;
    private Color[] colourEachRendererStartedWith;
    // The exact material each object was built with, kept so the natural style can put
    // it back rather than approximating it.
    private Material[] materialEachRendererStartedWith;

    private Light theSun;
    private Camera theCamera;

    void Start()
    {
        BuildTheStyleList();
        RememberEveryOriginalColour();

        theSun = Object.FindFirstObjectByType<Light>();
        theCamera = Camera.main;

        ApplyStyle(0);
    }

    void Update()
    {
        if (GameInput.StyleChangeWasPressed() == true)
        {
            int nextStyle = currentStyleIndex + 1;
            if (nextStyle >= availableStyles.Length)
            {
                nextStyle = 0;
            }
            ApplyStyle(nextStyle);
        }
    }

    private void BuildTheStyleList()
    {
        availableStyles = new VisualStyle[4];

        // 1. NATURAL - an ordinary lit 3D world. The baseline.
        VisualStyle natural = new VisualStyle();
        natural.styleName = "NATURAL";
        // The natural style hands everything back its built material, which is what keeps
        // the procedural rock detail intact.
        natural.keepsTheOriginalMaterials = true;
        natural.drawsFlatWithoutLighting = false;
        natural.colourTreatment = 0;
        natural.brightnessMultiplier = 1f;
        natural.skyAndFogColour = new Color(0.62f, 0.63f, 0.60f);
        natural.fogStartDistance = 45f;
        natural.fogEndDistance = 230f;
        natural.sunColour = new Color(1f, 0.95f, 0.86f);
        natural.sunIntensity = 1.35f;
        natural.sunCastsShadows = true;
        natural.ambientColour = new Color(0.40f, 0.43f, 0.48f);
        availableStyles[0] = natural;

        // 2. NOIR - colour drained out, hard light, heavy black haze.
        VisualStyle noir = new VisualStyle();
        noir.styleName = "NOIR";
        noir.drawsFlatWithoutLighting = false;
        noir.colourTreatment = 1;
        noir.brightnessMultiplier = 1.1f;
        noir.skyAndFogColour = new Color(0.05f, 0.05f, 0.06f);
        noir.fogStartDistance = 12f;
        noir.fogEndDistance = 90f;
        noir.sunColour = Color.white;
        noir.sunIntensity = 2.2f;
        noir.sunCastsShadows = true;
        noir.ambientColour = new Color(0.06f, 0.06f, 0.08f);
        availableStyles[1] = noir;

        // 3. NEON - flat unlit colour pushed to full saturation against black. No
        //    shading at all, so everything reads as a glowing cut-out shape.
        VisualStyle neon = new VisualStyle();
        neon.styleName = "NEON";
        neon.drawsFlatWithoutLighting = true;
        neon.colourTreatment = 2;
        neon.brightnessMultiplier = 1.25f;
        neon.skyAndFogColour = new Color(0.03f, 0.01f, 0.07f);
        neon.fogStartDistance = 30f;
        neon.fogEndDistance = 160f;
        neon.sunColour = Color.white;
        neon.sunIntensity = 1f;
        neon.sunCastsShadows = false;
        neon.ambientColour = Color.white;
        availableStyles[2] = neon;

        // 4. CHALK - flat pale colour on white. The same valley as a technical drawing.
        VisualStyle chalk = new VisualStyle();
        chalk.styleName = "CHALK";
        chalk.drawsFlatWithoutLighting = true;
        chalk.colourTreatment = 1;
        chalk.brightnessMultiplier = 1.9f;
        chalk.skyAndFogColour = new Color(0.94f, 0.93f, 0.90f);
        chalk.fogStartDistance = 25f;
        chalk.fogEndDistance = 140f;
        chalk.sunColour = Color.white;
        chalk.sunIntensity = 1f;
        chalk.sunCastsShadows = false;
        chalk.ambientColour = Color.white;
        availableStyles[3] = chalk;
    }

    // Reading Material.color asks for a property called "_Color", which the standard
    // shaders have and the two custom ones in this project do not - they expose
    // "_BaseColor" and "_Tint" instead. Asking blindly threw an error for every single
    // rock and every character, every time play started. This asks what the material
    // actually has before reading it.
    private Color ReadColourOf(Material material)
    {
        if (material == null)
        {
            return Color.grey;
        }
        if (material.HasProperty("_BaseColor") == true)
        {
            return material.GetColor("_BaseColor");
        }
        if (material.HasProperty("_Tint") == true)
        {
            return material.GetColor("_Tint");
        }
        if (material.HasProperty("_Color") == true)
        {
            return material.GetColor("_Color");
        }
        return Color.grey;
    }

    private void RememberEveryOriginalColour()
    {
        everyRenderer = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        colourEachRendererStartedWith = new Color[everyRenderer.Length];
        materialEachRendererStartedWith = new Material[everyRenderer.Length];

        int rendererIndex = 0;
        while (rendererIndex < everyRenderer.Length)
        {
            Material builtWith = everyRenderer[rendererIndex].material;
            materialEachRendererStartedWith[rendererIndex] = builtWith;
            colourEachRendererStartedWith[rendererIndex] = ReadColourOf(builtWith);
            rendererIndex = rendererIndex + 1;
        }
    }

    public void ApplyStyle(int styleIndex)
    {
        currentStyleIndex = styleIndex;
        VisualStyle style = availableStyles[styleIndex];

        RepaintEveryObject(style);
        SetTheWorldLighting(style);
        TellCharactersTheirColoursChanged();
    }

    private void RepaintEveryObject(VisualStyle style)
    {
        if (style.keepsTheOriginalMaterials == true)
        {
            PutTheOriginalMaterialsBack();
            return;
        }

        Shader shaderToUse = Shader.Find("Universal Render Pipeline/Lit");
        if (style.drawsFlatWithoutLighting == true)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader != null)
            {
                shaderToUse = unlitShader;
            }
        }

        int rendererIndex = 0;
        while (rendererIndex < everyRenderer.Length)
        {
            Renderer oneRenderer = everyRenderer[rendererIndex];
            if (oneRenderer != null)
            {
                Color startingColour = colourEachRendererStartedWith[rendererIndex];
                Color finalColour = TreatTheColour(startingColour, style);

                Material restyled = new Material(shaderToUse);
                restyled.color = finalColour;

                if (restyled.HasProperty("_BaseColor") == true)
                {
                    restyled.SetColor("_BaseColor", finalColour);
                }
                if (restyled.HasProperty("_Smoothness") == true)
                {
                    restyled.SetFloat("_Smoothness", 0.1f);
                }

                oneRenderer.material = restyled;
                oneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            rendererIndex = rendererIndex + 1;
        }
    }

    private void PutTheOriginalMaterialsBack()
    {
        int rendererIndex = 0;
        while (rendererIndex < everyRenderer.Length)
        {
            if (everyRenderer[rendererIndex] != null
                && materialEachRendererStartedWith[rendererIndex] != null)
            {
                everyRenderer[rendererIndex].material = materialEachRendererStartedWith[rendererIndex];
            }
            rendererIndex = rendererIndex + 1;
        }
    }

    private Color TreatTheColour(Color startingColour, VisualStyle style)
    {
        Color treated = startingColour;

        if (style.colourTreatment == 1)
        {
            // Drain to grey using the standard perceptual weights, so a red and a blue
            // of the same apparent brightness end up the same grey.
            float grey = startingColour.r * 0.299f + startingColour.g * 0.587f + startingColour.b * 0.114f;
            treated = new Color(grey, grey, grey);
        }
        else if (style.colourTreatment == 2)
        {
            float hue = 0f;
            float saturation = 0f;
            float value = 0f;
            Color.RGBToHSV(startingColour, out hue, out saturation, out value);

            // Anything nearly grey gets a floor put under its saturation, otherwise the
            // rocks stay dull and only the characters light up.
            float boostedSaturation = saturation + 0.65f;
            if (boostedSaturation > 1f)
            {
                boostedSaturation = 1f;
            }
            float boostedValue = value * 0.9f + 0.35f;
            if (boostedValue > 1f)
            {
                boostedValue = 1f;
            }

            treated = Color.HSVToRGB(hue, boostedSaturation, boostedValue);
        }

        treated = treated * style.brightnessMultiplier;
        treated.a = 1f;
        return treated;
    }

    private void SetTheWorldLighting(VisualStyle style)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = style.skyAndFogColour;
        RenderSettings.fogStartDistance = style.fogStartDistance;
        RenderSettings.fogEndDistance = style.fogEndDistance;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = style.ambientColour;

        if (theSun != null)
        {
            theSun.color = style.sunColour;
            theSun.intensity = style.sunIntensity;
            if (style.sunCastsShadows == true)
            {
                theSun.shadows = LightShadows.Soft;
            }
            else
            {
                theSun.shadows = LightShadows.None;
            }
        }

        if (theCamera != null)
        {
            // A solid background rather than the default sky, so the horizon matches the
            // fog instead of showing a blue sky behind a black-and-white world.
            theCamera.clearFlags = CameraClearFlags.SolidColor;
            theCamera.backgroundColor = style.skyAndFogColour;
        }
    }

    // The hit-flash remembers what colour a character is meant to return to. Restyling
    // hands every character a brand new material, so those remembered colours have to be
    // taken again or the first hit after a style change flashes to the wrong colour.
    private void TellCharactersTheirColoursChanged()
    {
        CharacterStats[] everyCharacter = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        int characterIndex = 0;
        while (characterIndex < everyCharacter.Length)
        {
            everyCharacter[characterIndex].RecacheOriginalColours();
            characterIndex = characterIndex + 1;
        }
    }

    public string CurrentStyleName()
    {
        if (availableStyles == null)
        {
            return "";
        }
        return availableStyles[currentStyleIndex].styleName;
    }
}
