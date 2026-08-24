using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class StickerStarTrailVfx : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(-2f, 1f, 0f);

    [Header("Particle Visual")]
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private Material material;
    [SerializeField, ColorUsage(true, true)] private Color color = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField, Min(0f)] private float emissionRate = 18f;
    [SerializeField, Min(0)] private int initialBurstCount = 2;
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.45f, 0.75f);
    [SerializeField] private Vector2 sizeRange = new Vector2(0.24f, 0.5f);
    [SerializeField, Min(0f)] private float spawnRadius = 0.38f;
    [SerializeField] private Vector2 driftSpeedRange = new Vector2(-0.12f, 0.12f);

    [Header("Rendering")]
    [Tooltip("Sticker sorting order değerinden çıkarılır.")]
    [SerializeField, Min(1)] private int sortingOrderOffset = 1;

    public ParticleSystem Particles => particles;

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
        var particleRenderer = GetComponent<ParticleSystemRenderer>();
        if (particleRenderer == null)
            return;

        particleRenderer.sortingLayerID = sortingLayerId;
        particleRenderer.sortingOrder = stickerSortingOrder - Mathf.Max(1, sortingOrderOffset);
    }

    public void Play()
    {
        EnsureParticles();
        if (particles == null)
            return;

        particles.Clear(true);
        particles.Play(true);
        if (initialBurstCount > 0)
            particles.Emit(initialBurstCount);
    }

    public void Stop(bool clearImmediately)
    {
        EnsureParticles();
        if (particles == null)
            return;

        var stopBehavior = clearImmediately
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        particles.Stop(true, stopBehavior);
    }

    private void ApplyVisualSettings()
    {
        EnsureParticles();
        transform.localPosition = localOffset;

        if (particles == null)
            return;

        var lifetimeMin = Mathf.Max(0.01f, Mathf.Min(lifetimeRange.x, lifetimeRange.y));
        var lifetimeMax = Mathf.Max(lifetimeMin, Mathf.Max(lifetimeRange.x, lifetimeRange.y));
        var sizeMin = Mathf.Max(0.01f, Mathf.Min(sizeRange.x, sizeRange.y));
        var sizeMax = Mathf.Max(sizeMin, Mathf.Max(sizeRange.x, sizeRange.y));
        var driftMin = Mathf.Min(driftSpeedRange.x, driftSpeedRange.y);
        var driftMax = Mathf.Max(driftSpeedRange.x, driftSpeedRange.y);

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.maxParticles = 80;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, emissionRate);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0f, spawnRadius);
        shape.radiusThickness = 1f;

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // Unity bütün eksenlerin aynı curve modunda olmasını ister.
        velocity.x = new ParticleSystem.MinMaxCurve(driftMin, driftMax);
        velocity.y = new ParticleSystem.MinMaxCurve(driftMin, driftMax);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.2f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.12f),
                new GradientAlphaKey(color.a, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.72f, 0.82f),
            new Keyframe(1f, 0f));
        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

        var particleRenderer = GetComponent<ParticleSystemRenderer>();
        if (particleRenderer == null)
            return;

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        if (material != null)
            particleRenderer.sharedMaterial = material;
    }

    private void EnsureParticles()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();
    }
}
