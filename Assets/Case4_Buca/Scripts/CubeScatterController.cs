using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CubeScatterController : MonoBehaviour
{
    [Header("Pieces")]
    [SerializeField, Min(0.001f)] private float cubeMass = 0.15f;
    [SerializeField, Min(0f)] private float settleLinearDamping = 0.18f;
    [SerializeField, Min(0f)] private float settleAngularDamping = 0.22f;
    [SerializeField] private bool triggerOnce = true;

    [Header("Scatter")]
    [SerializeField, Min(0f)] private float radialVelocity = 4.5f;
    [SerializeField, Min(0f)] private float upwardVelocity = 2.1f;
    [Tooltip("Disc'in geliş yönünün dağılıma etkisi.")]
    [SerializeField, Range(0f, 1f)] private float directionalInfluence = 0.35f;
    [SerializeField, Range(0f, 1f)] private float randomSpread = 0.22f;
    [SerializeField, Min(0f)] private float angularVelocity = 10f;
    [SerializeField, Min(0.01f)] private float maximumCubeSpeed = 8f;
    [SerializeField] private AnimationCurve distanceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.45f);

    [Header("Shockwave Timing")]
    [Tooltip("Temas noktasından her bir dünya birimi uzaklık için eklenecek gecikme.")]
    [SerializeField, Min(0f)] private float waveDelayPerUnit = 0.025f;
    [SerializeField, Min(0f)] private float randomDelay = 0.012f;

    [Header("Progressive Slowdown")]
    [Tooltip("Scatter başladıktan sonra yavaşlamanın devreye girmesine kadar geçecek süre.")]
    [SerializeField, Min(0f)] private float freeScatterDuration = 0.1f;
    [Tooltip("Bu mesafenin içinde ekstra yavaşlama uygulanmaz.")]
    [SerializeField, Min(0f)] private float slowdownStartRadius = 2f;
    [Tooltip("Yavaşlatma kuvvetinin tam değere ulaştığı merkez mesafesi.")]
    [SerializeField, Min(0.01f)] private float slowdownFullStrengthRadius = 3.5f;
    [Tooltip("Tam güçte dışarı yönlü hızdan saniyede eksiltilecek miktar.")]
    [SerializeField, Min(0f)] private float outwardDeceleration = 35f;
    [Tooltip("Mesafeye göre yavaşlama şiddeti. 0 ters kuvvet üretmez; yalnızca mevcut hızı azaltır.")]
    [SerializeField] private AnimationCurve distanceSlowdownCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Yavaşlama bölgesindeki XZ hızına uygulanan ek smooth damping.")]
    [SerializeField, Min(0f)] private float planarDamping = 1f;
    [Tooltip("Güvenlik sınırı. Aşılırsa yalnızca dışarı yönlü hız sıfırlanır.")]
    [SerializeField, Min(0.01f)] private float hardLimitRadius = 4.5f;

    [Header("Disc")]
    [Tooltip("Scatter ilk kez tetiklendiğinde disc hızının korunacak oranı.")]
    [SerializeField, Range(0f, 1f)] private float discSpeedRetention = 0.85f;

    [Header("Impact Color")]
    [Tooltip("Her küp kendi scatter hareketine başladığı anda bu renk akışını oynatır.")]
    [SerializeField] private Gradient impactColorGradient = CreateDefaultImpactGradient();
    [Tooltip("Küp scatter kuvvetini aldıktan kaç saniye sonra renk geçişinin başlayacağı.")]
    [SerializeField, Min(0f)] private float colorTransitionStartDelay = 0.1f;
    [SerializeField, Min(0.01f)] private float colorTransitionDuration = 0.75f;
    [SerializeField] private AnimationCurve colorTransitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly List<Rigidbody> cubeBodies = new();
    private readonly Dictionary<Rigidbody, Renderer[]> renderersByBody = new();
    private readonly Dictionary<Rigidbody, Coroutine> colorAnimations = new();
    private MaterialPropertyBlock colorProperties;
    private bool hasTriggered;
    private bool isScattering;
    private Vector3 slowdownCenter;
    private float scatterStartedTime;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public float DiscSpeedRetention => discSpeedRetention;

    private readonly struct ScatterEntry
    {
        public readonly Rigidbody Body;
        public readonly float Distance;
        public readonly float Delay;

        public ScatterEntry(Rigidbody body, float distance, float delay)
        {
            Body = body;
            Distance = distance;
            Delay = delay;
        }
    }

    private void Awake()
    {
        colorProperties = new MaterialPropertyBlock();
        CacheCubeBodies();
        ConfigureCubeBodies();

        if (distanceFalloff == null || distanceFalloff.length == 0)
            distanceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.45f);

        if (distanceSlowdownCurve == null || distanceSlowdownCurve.length == 0)
            distanceSlowdownCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (impactColorGradient == null)
            impactColorGradient = CreateDefaultImpactGradient();

        if (colorTransitionEase == null || colorTransitionEase.length == 0)
            colorTransitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void FixedUpdate()
    {
        if (!hasTriggered)
            return;

        var slowdownIsActive = Time.time >= scatterStartedTime + freeScatterDuration;
        var maximumSpeedSqr = maximumCubeSpeed * maximumCubeSpeed;
        foreach (var body in cubeBodies)
        {
            if (body == null)
                continue;

            if (slowdownIsActive)
                ApplyProgressiveSlowdown(body);

            if (body.linearVelocity.sqrMagnitude > maximumSpeedSqr)
                body.linearVelocity = body.linearVelocity.normalized * maximumCubeSpeed;
        }
    }

    public bool TryScatter(Vector3 impactPoint, Vector3 discVelocity)
    {
        if (isScattering || (triggerOnce && hasTriggered))
            return false;

        if (cubeBodies.Count == 0)
            CacheCubeBodies();

        if (cubeBodies.Count == 0)
            return false;

        UpdateSlowdownCenter();
        scatterStartedTime = Time.time;
        hasTriggered = true;
        isScattering = true;
        StartCoroutine(ScatterRoutine(impactPoint, discVelocity));
        return true;
    }

    private IEnumerator ScatterRoutine(Vector3 impactPoint, Vector3 discVelocity)
    {
        var entries = new List<ScatterEntry>(cubeBodies.Count);
        var maximumDistance = 0.001f;

        foreach (var body in cubeBodies)
        {
            if (body == null)
                continue;

            var planarOffset = body.worldCenterOfMass - impactPoint;
            planarOffset.y = 0f;
            var distance = planarOffset.magnitude;
            maximumDistance = Mathf.Max(maximumDistance, distance);
            var delay = distance * waveDelayPerUnit + Random.Range(0f, randomDelay);
            entries.Add(new ScatterEntry(body, distance, delay));
        }

        entries.Sort((left, right) => left.Delay.CompareTo(right.Delay));

        var elapsedDelay = 0f;
        foreach (var entry in entries)
        {
            var waitDuration = entry.Delay - elapsedDelay;
            if (waitDuration > 0f)
                yield return new WaitForSeconds(waitDuration);

            elapsedDelay = entry.Delay;
            ApplyScatterVelocity(entry.Body, impactPoint, discVelocity, entry.Distance / maximumDistance);
        }

        isScattering = false;
    }

    private void ApplyScatterVelocity(
        Rigidbody body,
        Vector3 impactPoint,
        Vector3 discVelocity,
        float normalizedDistance)
    {
        if (body == null)
            return;

        var radialDirection = body.worldCenterOfMass - impactPoint;
        radialDirection.y = 0f;

        var discDirection = discVelocity;
        discDirection.y = 0f;
        if (discDirection.sqrMagnitude > 0.0001f)
            discDirection.Normalize();

        if (radialDirection.sqrMagnitude > 0.0001f)
            radialDirection.Normalize();
        else
            radialDirection = discDirection.sqrMagnitude > 0f ? discDirection : transform.forward;

        var randomDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        if (randomDirection.sqrMagnitude > 0.0001f)
            randomDirection.Normalize();
        else
            randomDirection = radialDirection;

        var directedScatter = radialDirection + discDirection * directionalInfluence;
        var horizontalDirection = Vector3.Lerp(directedScatter.normalized, randomDirection, randomSpread).normalized;
        var strength = Mathf.Max(0f, distanceFalloff.Evaluate(Mathf.Clamp01(normalizedDistance)));
        var verticalVariation = Random.Range(0.86f, 1.14f);
        var scatterVelocity = horizontalDirection * (radialVelocity * strength) +
                              Vector3.up * (upwardVelocity * verticalVariation);

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.AddForce(scatterVelocity, ForceMode.VelocityChange);
        body.AddTorque(Random.onUnitSphere * angularVelocity, ForceMode.VelocityChange);
        body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maximumCubeSpeed);
        body.WakeUp();
        StartColorTransition(body);
    }

    private void CacheCubeBodies()
    {
        cubeBodies.Clear();
        cubeBodies.AddRange(GetComponentsInChildren<Rigidbody>(true));
        renderersByBody.Clear();

        foreach (var body in cubeBodies)
        {
            if (body == null)
                continue;

            renderersByBody[body] = body.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void UpdateSlowdownCenter()
    {
        var centerSum = Vector3.zero;
        var validBodyCount = 0;

        foreach (var body in cubeBodies)
        {
            if (body == null)
                continue;

            centerSum += body.worldCenterOfMass;
            validBodyCount++;
        }

        slowdownCenter = validBodyCount > 0 ? centerSum / validBodyCount : transform.position;
    }

    private void ApplyProgressiveSlowdown(Rigidbody body)
    {
        var offsetFromCenter = body.worldCenterOfMass - slowdownCenter;
        offsetFromCenter.y = 0f;
        var distanceFromCenter = offsetFromCenter.magnitude;
        if (distanceFromCenter <= slowdownStartRadius || distanceFromCenter <= 0.0001f)
            return;

        var outwardDirection = offsetFromCenter / distanceFromCenter;
        var velocity = body.linearVelocity;
        var planarVelocity = new Vector3(velocity.x, 0f, velocity.z);

        var normalizedDistance = Mathf.InverseLerp(
            slowdownStartRadius,
            slowdownFullStrengthRadius,
            distanceFromCenter);
        var slowdownStrength = Mathf.Clamp01(distanceSlowdownCurve.Evaluate(normalizedDistance));

        if (planarDamping > 0f && slowdownStrength > 0f)
            planarVelocity *= Mathf.Exp(-planarDamping * slowdownStrength * Time.fixedDeltaTime);

        var outwardSpeed = Vector3.Dot(planarVelocity, outwardDirection);
        if (outwardSpeed > 0f)
        {
            var slowedOutwardSpeed = Mathf.MoveTowards(
                outwardSpeed,
                0f,
                outwardDeceleration * slowdownStrength * Time.fixedDeltaTime);
            planarVelocity += outwardDirection * (slowedOutwardSpeed - outwardSpeed);
        }

        if (distanceFromCenter > hardLimitRadius)
        {
            var clampedPosition = slowdownCenter + outwardDirection * hardLimitRadius;
            var currentPosition = body.position;
            body.position = new Vector3(clampedPosition.x, currentPosition.y, clampedPosition.z);

            var remainingOutwardSpeed = Vector3.Dot(planarVelocity, outwardDirection);
            if (remainingOutwardSpeed > 0f)
                planarVelocity -= outwardDirection * remainingOutwardSpeed;
        }

        body.linearVelocity = new Vector3(planarVelocity.x, velocity.y, planarVelocity.z);
    }

    private void StartColorTransition(Rigidbody body)
    {
        if (body == null)
            return;

        if (colorAnimations.TryGetValue(body, out var activeAnimation) && activeAnimation != null)
            StopCoroutine(activeAnimation);

        colorAnimations[body] = StartCoroutine(AnimateBodyColor(body));
    }

    private IEnumerator AnimateBodyColor(Rigidbody body)
    {
        if (colorTransitionStartDelay > 0f)
            yield return new WaitForSeconds(colorTransitionStartDelay);

        var elapsed = 0f;
        SetBodyColor(body, impactColorGradient.Evaluate(0f));

        while (elapsed < colorTransitionDuration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / colorTransitionDuration);
            var easedProgress = Mathf.Clamp01(colorTransitionEase.Evaluate(progress));
            SetBodyColor(body, impactColorGradient.Evaluate(easedProgress));
            yield return null;
        }

        SetBodyColor(body, impactColorGradient.Evaluate(1f));
        colorAnimations.Remove(body);
    }

    private void SetBodyColor(Rigidbody body, Color color)
    {
        if (!renderersByBody.TryGetValue(body, out var renderers))
            return;

        colorProperties ??= new MaterialPropertyBlock();

        foreach (var bodyRenderer in renderers)
        {
            if (bodyRenderer == null)
                continue;

            colorProperties.Clear();
            bodyRenderer.GetPropertyBlock(colorProperties);
            colorProperties.SetColor(BaseColorId, color);
            colorProperties.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(colorProperties);
        }
    }

    private void ConfigureCubeBodies()
    {
        foreach (var body in cubeBodies)
        {
            if (body == null)
                continue;

            body.mass = cubeMass;
            body.useGravity = true;
            body.linearDamping = settleLinearDamping;
            body.angularDamping = settleAngularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    private void OnValidate()
    {
        cubeMass = Mathf.Max(0.001f, cubeMass);
        maximumCubeSpeed = Mathf.Max(0.01f, maximumCubeSpeed);
        freeScatterDuration = Mathf.Max(0f, freeScatterDuration);
        slowdownStartRadius = Mathf.Max(0f, slowdownStartRadius);
        slowdownFullStrengthRadius = Mathf.Max(slowdownStartRadius + 0.01f, slowdownFullStrengthRadius);
        outwardDeceleration = Mathf.Max(0f, outwardDeceleration);
        planarDamping = Mathf.Max(0f, planarDamping);
        hardLimitRadius = Mathf.Max(slowdownFullStrengthRadius + 0.01f, hardLimitRadius);
        colorTransitionStartDelay = Mathf.Max(0f, colorTransitionStartDelay);
        colorTransitionDuration = Mathf.Max(0.01f, colorTransitionDuration);
    }

    private static Gradient CreateDefaultImpactGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color32(255, 217, 40, 255), 0f),
                new GradientColorKey(new Color32(255, 133, 27, 255), 0.3f),
                new GradientColorKey(new Color32(242, 56, 56, 255), 0.65f),
                new GradientColorKey(new Color32(142, 68, 255, 255), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        return gradient;
    }
}
