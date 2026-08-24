using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(TrailRenderer))]
public class StickerTravelTrailVfx : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(-2f, 1f, 0f);

    [Header("Trail Visual")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Material material;
    [SerializeField] private Color color = new Color(1f, 0.92f, 0.62f, 0.6f);
    [SerializeField, Min(0.01f)] private float width = 1f;
    [SerializeField, Range(0f, 1f)] private float endWidthMultiplier = 0.12f;
    [SerializeField, Min(0.01f)] private float lifetime = 0.35f;
    [SerializeField, Min(0.001f)] private float minVertexDistance = 0.05f;
    [SerializeField, Min(0)] private int cornerVertices = 4;
    [SerializeField, Min(0)] private int capVertices = 4;

    [Header("Rendering")]
    [Tooltip("Sticker sorting order değerinden çıkarılır.")]
    [SerializeField, Min(1)] private int sortingOrderOffset = 1;

    public TrailRenderer Renderer => trailRenderer;
    public float ReleaseDelay
    {
        get
        {
            EnsureRenderer();
            return trailRenderer != null ? Mathf.Max(0.01f, trailRenderer.time) : 0.01f;
        }
    }

    private void Awake()
    {
        ApplyVisualSettings();
        Stop(true);
    }

    private void OnValidate()
    {
        ApplyVisualSettings();
    }

    public void ConfigureSorting(int sortingLayerId, int stickerSortingOrder)
    {
        EnsureRenderer();
        if (trailRenderer == null)
            return;

        trailRenderer.sortingLayerID = sortingLayerId;
        trailRenderer.sortingOrder = stickerSortingOrder - Mathf.Max(1, sortingOrderOffset);
    }

    public void AttachTo(Transform owner)
    {
        transform.SetParent(owner, false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;
    }

    public void Play()
    {
        EnsureRenderer();
        if (trailRenderer == null)
            return;

        trailRenderer.Clear();
        trailRenderer.emitting = true;
    }

    public void Stop(bool clearImmediately)
    {
        EnsureRenderer();
        if (trailRenderer == null)
            return;

        trailRenderer.emitting = false;
        if (clearImmediately)
            trailRenderer.Clear();
    }

    private void ApplyVisualSettings()
    {
        EnsureRenderer();
        transform.localPosition = localOffset;

        if (trailRenderer == null)
            return;

        trailRenderer.time = Mathf.Max(0.01f, lifetime);
        trailRenderer.startWidth = Mathf.Max(0.01f, width);
        trailRenderer.endWidth = Mathf.Max(0.01f, width) * Mathf.Clamp01(endWidthMultiplier);
        trailRenderer.minVertexDistance = Mathf.Max(0.001f, minVertexDistance);
        trailRenderer.startColor = color;

        var transparentEndColor = color;
        transparentEndColor.a = 0f;
        trailRenderer.endColor = transparentEndColor;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.numCornerVertices = Mathf.Max(0, cornerVertices);
        trailRenderer.numCapVertices = Mathf.Max(0, capVertices);
        trailRenderer.generateLightingData = false;
        trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;

        if (material != null)
            trailRenderer.sharedMaterial = material;
    }

    private void EnsureRenderer()
    {
        if (trailRenderer == null)
            trailRenderer = GetComponent<TrailRenderer>();
    }
}
