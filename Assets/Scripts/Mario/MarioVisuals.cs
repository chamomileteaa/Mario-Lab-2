using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MarioController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AnimatorCache))]
public class MarioVisuals : MonoBehaviour
{
    private const float InputDeadzone = 0.01f;
    private const float MinAnimMoveSpeed = 0.2f;

    [Header("Damage Flicker")]
    [SerializeField, Range(0.05f, 1f)] private float invulnerabilityMinAlpha = 0.35f;
    [SerializeField, Min(1f)] private float invulnerabilityFlickerSpeed = 18f;

    [Header("Star Color Cycle")]
    [SerializeField, Min(0.1f)] private float starColorCycleSpeed = 20f;
    [SerializeField] private Color[] starColors =
    {
        Color.yellow,
        Color.cyan,
        Color.magenta,
        Color.green
    };

    [Header("Super Tint")]
    [SerializeField] private Color superTint = new Color(1f, 0.95f, 0.65f, 1f);

    private MarioController marioController;
    private Rigidbody2D body2D;
    private AnimatorCache animatorCache;
    private SpriteFlipper spriteFlipper;
    private SpriteRenderer[] spriteRenderers;
    private Color[] spriteBaseColors;

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
        Anim.TrySetTrigger(grew ? "grow" : "shrink");
    }

    public void PlayDeath()
    {
        Anim.TrySetTrigger("die");
    }

    public void ResetVisuals()
    {
        ApplySpriteVisuals(1f, null);
    }

    private void SyncAnimator()
    {
        var velocity = Body.linearVelocity;
        var inputX = Mario.IsCrouching ? 0f : Mario.MoveInput.x;
        if (Mathf.Abs(inputX) < InputDeadzone) inputX = 0f;

        var absVelocityX = Mathf.Abs(velocity.x);
        if (Mathf.Abs(inputX) > InputDeadzone) absVelocityX = Mathf.Max(absVelocityX, MinAnimMoveSpeed);

        Anim.TrySet("absVelocityX", absVelocityX);
        Anim.TrySet("velocityX", velocity.x);
        Anim.TrySet("inputX", inputX);
        Anim.TrySet("velocityY", velocity.y);
        Anim.TrySet("isGrounded", Mario.IsGrounded);
        Anim.TrySet("isCrouching", Mario.IsCrouching);
        Anim.TrySet("isSkidding", IsSkidding(inputX, velocity.x));
        Anim.TrySet("form", (float)Mario.Form);
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

        Color? tint = null;
        if (Mario.IsStarPowered)
            tint = EvaluateStarTint();
        else if (Mario.IsSuper)
            tint = superTint;
        ApplySpriteVisuals(alpha, tint);
    }

    private Color EvaluateStarTint()
    {
        if (starColors == null || starColors.Length == 0) return Color.white;
        if (starColors.Length == 1) return starColors[0];

        var cycle = Mathf.Repeat(Time.time * Mathf.Max(0.1f, starColorCycleSpeed), starColors.Length);
        var fromIndex = Mathf.FloorToInt(cycle);
        var toIndex = (fromIndex + 1) % starColors.Length;
        var t = cycle - fromIndex;
        return Color.Lerp(starColors[fromIndex], starColors[toIndex], t);
    }

    private void ApplySpriteVisuals(float alpha, Color? tint)
    {
        var renderers = SpriteRenderers;
        EnsureSpriteBaseColors(renderers);
        var tintColor = tint ?? Color.white;

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!renderer) continue;

            var baseColor = spriteBaseColors[i];
            var color = baseColor;
            color.r *= tintColor.r;
            color.g *= tintColor.g;
            color.b *= tintColor.b;
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
