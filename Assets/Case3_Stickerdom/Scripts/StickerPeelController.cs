using UnityEngine;

/// <summary>
/// Drives the Custom/StickerPeelURP shader without creating a unique material
/// for every sticker. Attach this component to a SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class StickerPeelController : MonoBehaviour
{
    public enum PeelCorner
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight
    }

    [Header("Peel")]
    [SerializeField] private PeelCorner corner = PeelCorner.BottomRight;
    [Range(0f, 1f)] [SerializeField] private float progress;
    [Tooltip("Rotates the inward peel direction. Zero aims from the chosen corner to the centre.")]
    [Range(-60f, 60f)] [SerializeField] private float angleOffsetDegrees;
    [Tooltip("1.6 fully removes a diagonal corner peel. Increase it for shallower directions.")]
    [Range(0.01f, 2f)] [SerializeField] private float travel = 1.6f;

    [Header("Optional playback")]
    [SerializeField] private bool playOnEnable;
    [Min(0.01f)] [SerializeField] private float duration = 0.65f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private static readonly int PeelProgressId = Shader.PropertyToID("_PeelProgress");
    private static readonly int PeelCornerId = Shader.PropertyToID("_PeelCorner");
    private static readonly int PeelDirectionId = Shader.PropertyToID("_PeelDirection");
    private static readonly int TravelId = Shader.PropertyToID("_Travel");

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock properties;
    private bool isPlaying;
    private float elapsed;

    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp01(value);
            ApplyProperties();
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        properties = new MaterialPropertyBlock();
        ApplyProperties();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            properties ??= new MaterialPropertyBlock();
            ApplyProperties();
        }
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        elapsed += Time.deltaTime;
        progress = easing.Evaluate(Mathf.Clamp01(elapsed / duration));
        ApplyProperties();

        if (elapsed >= duration)
            isPlaying = false;
    }

    /// <summary>Starts the peel from the selected corner.</summary>
    public void Play()
    {
        elapsed = 0f;
        progress = 0f;
        isPlaying = true;
        ApplyProperties();
    }

    /// <summary>Stops the current animation and restores the intact sticker.</summary>
    public void ResetPeel()
    {
        isPlaying = false;
        Progress = 0f;
    }

    /// <summary>Stops the animation and makes the sticker fully peeled.</summary>
    public void FinishPeel()
    {
        isPlaying = false;
        Progress = 1f;
    }

    private void ApplyProperties()
    {
        if (spriteRenderer == null)
            return;

        Vector2 cornerUv = corner switch
        {
            PeelCorner.BottomLeft => new Vector2(0f, 0f),
            PeelCorner.BottomRight => new Vector2(1f, 0f),
            PeelCorner.TopLeft => new Vector2(0f, 1f),
            PeelCorner.TopRight => new Vector2(1f, 1f),
            _ => new Vector2(1f, 0f)
        };

        Vector2 inwardDirection = (new Vector2(0.5f, 0.5f) - cornerUv).normalized;
        float angle = angleOffsetDegrees * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(
            inwardDirection.x * Mathf.Cos(angle) - inwardDirection.y * Mathf.Sin(angle),
            inwardDirection.x * Mathf.Sin(angle) + inwardDirection.y * Mathf.Cos(angle)
        );

        spriteRenderer.GetPropertyBlock(properties);
        properties.SetFloat(PeelProgressId, progress);
        properties.SetVector(PeelCornerId, cornerUv);
        properties.SetVector(PeelDirectionId, direction);
        properties.SetFloat(TravelId, travel);
        spriteRenderer.SetPropertyBlock(properties);
    }
}
