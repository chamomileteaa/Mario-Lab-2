using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MarioController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AnimatorCache))]
public class MarioVisuals : MonoBehaviour
{
    private const string StarShaderName = "Custom/SpritePaletteStar";

    private const float NesFrameRate = 60f;
    private const int StarPaletteMask = 0x03;

    private const float InputDeadzone = 0.01f;
    private const float MinAnimMoveSpeed = 0.2f;
    private const string GrowTrigger = "grow";
    private const string ShrinkTrigger = "shrink";
    private const string DieTrigger = "die";
    private const string AbsVelocityXParameter = "absVelocityX";
    private const string VelocityXParameter = "velocityX";
    private const string InputXParameter = "inputX";
    private const string VelocityYParameter = "velocityY";
    private const string IsGroundedParameter = "isGrounded";
    private const string IsCrouchingParameter = "isCrouching";
    private const string IsSkiddingParameter = "isSkidding";
    private const string FormParameter = "form";
    private static readonly int StarEnabledId = Shader.PropertyToID("_StarEnabled");
    private static readonly int PaletteIndexId = Shader.PropertyToID("_PaletteIndex");

    [Header("Damage Flicker")]
    [SerializeField, Range(0.05f, 1f)] private float invulnerabilityMinAlpha = 0.35f;
    [SerializeField, Min(1f)] private float invulnerabilityFlickerSpeed = 18f;

    [Header("Star Shader")]
    [SerializeField] private bool useStarPaletteShader = true;
    [SerializeField] private Shader starPaletteShader;

    [Header("Star Palette Cycle")]
    [SerializeField, Min(0f)] private float starSlowPhaseSeconds = 2.25f;
    [SerializeField, Min(1)] private int starFastFramesPerStep = 4;
    [SerializeField, Min(1)] private int starSlowFramesPerStep = 8;

    private MarioController marioController;
    private Rigidbody2D body2D;
    private AnimatorCache animatorCache;
    private SpriteFlipper spriteFlipper;
    private SpriteRenderer[] spriteRenderers;
    private Color[] spriteBaseColors;
    private Material starPaletteMaterial;
    private Material[] originalSpriteMaterials;
    private bool starPaletteApplied;
    private int lastAppliedStarPaletteIndex = -1;

    private MarioController Mario => marioController ? marioController : marioController = GetComponent<MarioController>();
    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);
    private SpriteRenderer[] SpriteRenderers => spriteRenderers != null && spriteRenderers.Length > 0
        ? spriteRenderers
        : spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

    public void RefreshVisualState()
    {
        if (!Mario || Mario.IsDead) return;
        UpdateSpriteDirection();
        SyncAnimator();
        UpdateSpriteVisuals();
    }

    public void PlayFormTransition(bool grew)
    {
        Anim.TrySetTrigger(grew ? GrowTrigger : ShrinkTrigger);
    }

    public void PlayDeath()
    {
        Anim.TrySetTrigger(DieTrigger);
    }

    public void ResetVisuals()
    {
        DisableStarPaletteShader();
        ApplySpriteVisuals(1f);
    }

    private void OnDisable()
    {
        DisableStarPaletteShader();
    }

    private void OnDestroy()
    {
        if (starPaletteMaterial)
            Destroy(starPaletteMaterial);
        starPaletteMaterial = null;
    }

    private void SyncAnimator()
    {
        var velocity = Body.linearVelocity;
        var inputX = Mario.IsCrouching ? 0f : Mario.MoveInput.x;
        if (Mathf.Abs(inputX) < InputDeadzone) inputX = 0f;

        var absVelocityX = Mathf.Abs(velocity.x);
        if (Mathf.Abs(inputX) > InputDeadzone) absVelocityX = Mathf.Max(absVelocityX, MinAnimMoveSpeed);

        Anim.TrySet(AbsVelocityXParameter, absVelocityX);
        Anim.TrySet(VelocityXParameter, velocity.x);
        Anim.TrySet(InputXParameter, inputX);
        Anim.TrySet(VelocityYParameter, velocity.y);
        Anim.TrySet(IsGroundedParameter, Mario.IsGrounded);
        Anim.TrySet(IsCrouchingParameter, Mario.IsCrouching);
        Anim.TrySet(IsSkiddingParameter, IsSkidding(inputX, velocity.x));
        Anim.TrySet(FormParameter, (float)Mario.Form);
    }

    private void UpdateSpriteDirection()
    {
        if (!Flipper) return;
        if (!Mario.IsGrounded) return;
        if (Mathf.Abs(Mario.MoveInput.x) <= InputDeadzone) return;
        Flipper.SetDirection(new Vector2(Mario.MoveInput.x, 0f));
    }

    private void UpdateSpriteVisuals()
    {
        var alpha = 1f;
        if (Mario.IsDamageInvulnerable)
        {
            var pulse = Mathf.PingPong(Time.time * invulnerabilityFlickerSpeed, 1f);
            alpha = Mathf.Lerp(invulnerabilityMinAlpha, 1f, pulse);
        }

        var usedStarShader = Mario.IsStarPowered && TryApplyStarPaletteShader();
        if (!usedStarShader) 
            DisableStarPaletteShader();

        ApplySpriteVisuals(alpha);
    }

    private bool TryApplyStarPaletteShader()
    {
        if (!useStarPaletteShader) return false;

        var shader = ResolveStarShader();
        if (!shader) return false;

        var material = GetOrCreateStarMaterial(shader);
        if (!material) return false;

        var renderers = SpriteRenderers;
        if (renderers == null || renderers.Length == 0) return false;

        if (!starPaletteApplied)
        {
            ApplyStarMaterial(renderers, material);
            material.SetFloat(StarEnabledId, 1f);
            lastAppliedStarPaletteIndex = -1;
        }

        var paletteIndex = EvaluateStarPaletteIndex();
        if (paletteIndex != lastAppliedStarPaletteIndex)
        {
            material.SetFloat(PaletteIndexId, paletteIndex);
            lastAppliedStarPaletteIndex = paletteIndex;
        }

        return true;
    }

    private void ApplyStarMaterial(SpriteRenderer[] renderers, Material material)
    {
        if (!starPaletteApplied || originalSpriteMaterials == null || originalSpriteMaterials.Length != renderers.Length)
        {
            originalSpriteMaterials = new Material[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer) continue;
                originalSpriteMaterials[i] = renderer.sharedMaterial;
            }
        }

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!renderer) continue;
            if (renderer.sharedMaterial != material)
                renderer.sharedMaterial = material;
        }

        starPaletteApplied = true;
    }

    private void DisableStarPaletteShader()
    {
        if (!starPaletteApplied) return;

        if (starPaletteMaterial)
            starPaletteMaterial.SetFloat(StarEnabledId, 0f);

        var renderers = SpriteRenderers;
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!renderer) continue;
            if (originalSpriteMaterials != null && i < originalSpriteMaterials.Length)
                renderer.sharedMaterial = originalSpriteMaterials[i];
        }

        starPaletteApplied = false;
        lastAppliedStarPaletteIndex = -1;
    }

    private Shader ResolveStarShader()
    {
        if (starPaletteShader) return starPaletteShader;
        starPaletteShader = Shader.Find(StarShaderName);
        return starPaletteShader;
    }

    private Material GetOrCreateStarMaterial(Shader shader)
    {
        if (starPaletteMaterial && starPaletteMaterial.shader == shader) return starPaletteMaterial;
        if (starPaletteMaterial) Destroy(starPaletteMaterial);

        starPaletteMaterial = new Material(shader)
        {
            name = "Mario_StarPalette_Runtime"
        };
        starPaletteMaterial.hideFlags = HideFlags.DontSave;
        return starPaletteMaterial;
    }

    private int EvaluateStarPaletteIndex()
    {
        var nesFrame = Mathf.FloorToInt(Time.time * NesFrameRate);
        var useSlowCycle = Mario.StarPowerTimeRemaining <= starSlowPhaseSeconds;
        var framesPerStep = Mathf.Max(1, useSlowCycle ? starSlowFramesPerStep : starFastFramesPerStep);
        return (nesFrame / framesPerStep) & StarPaletteMask;
    }

    private void ApplySpriteVisuals(float alpha)
    {
        var renderers = SpriteRenderers;
        EnsureSpriteBaseColors(renderers);

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!renderer) continue;

            var baseColor = spriteBaseColors[i];
            var color = baseColor;
            color.a = baseColor.a * alpha;
            renderer.color = color;
        }
    }

    private void EnsureSpriteBaseColors(SpriteRenderer[] renderers)
    {
        if (renderers == null) return;
        if (spriteBaseColors != null && spriteBaseColors.Length == renderers.Length) return;

        spriteBaseColors = new Color[renderers.Length];
        for (var i = 0; i < renderers.Length; i++)
            spriteBaseColors[i] = renderers[i] ? renderers[i].color : Color.white;
    }

    private static bool IsSkidding(float inputX, float velocityX)
    {
        if (Mathf.Abs(inputX) <= InputDeadzone) return false;
        if (Mathf.Abs(velocityX) <= InputDeadzone) return false;
        return Mathf.Sign(inputX) != Mathf.Sign(velocityX);
    }
}
