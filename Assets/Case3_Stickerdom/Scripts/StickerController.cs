using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class StickerController : MonoBehaviour
{
    private const string DefaultPeelThresholdSfxId = "StickerdomSFX_1";
    private const string DefaultPlacementCompleteSfxId = "StickerdomSFX_2";

    [Header("Sticker")]
    [SerializeField] private StickerType stickerType;

    [Header("Interaction")]
    [Tooltip("Boş bırakılırsa Main Camera kullanılır.")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private LayerMask clickLayers = ~0;

    [Header("Peel And Move")]
    [SerializeField] private StickerPeelController peelController;
    [SerializeField, Range(0f, 1f)] private float peeledProgress = 0.668f;
    [SerializeField, Min(0.01f)] private float peelDuration = 0.55f;
    [SerializeField] private Ease peelEase = Ease.InOutSine;
    [Tooltip("Hareketi peel bitmeden kaç saniye önce başlatır. 0 değerinde animasyonlar tam ardışık çalışır.")]
    [SerializeField, Min(0f)] private float moveStartOverlap = 0.06f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.75f;
    [SerializeField] private Ease moveEase = Ease.InOutSine;
    [SerializeField, Min(0.01f)] private float restoreDuration = 0.45f;
    [SerializeField] private Ease restoreEase = Ease.InOutSine;

    [Header("Travel VFX")]
    [SerializeField] private string travelTrailPoolId = "Sticker Travel Trail";
    [SerializeField] private string travelStarPoolId = "Sticker Star Trail";
    [Tooltip("Sticker holder'a bu dünya mesafesi kadar yaklaştığında trail emission kapanır. 0 değeri holder'a kadar sürdürür.")]
    [SerializeField, Min(0f)] private float travelTrailStopDistance = 0.75f;

    [Header("Arrival Scale Pop")]
    [SerializeField, Min(1f)] private float arrivalScaleMultiplier = 1.12f;
    [Tooltip("Scale pop'u peel restore bitmeden kaç saniye önce başlatır. 0 değeri tam bitişi bekler.")]
    [SerializeField, Min(0f)] private float arrivalScaleStartOverlap = 0.12f;
    [SerializeField, Min(0.01f)] private float arrivalScaleUpDuration = 0.12f;
    [SerializeField] private Ease arrivalScaleUpEase = Ease.OutBack;
    [SerializeField, Min(0.01f)] private float arrivalScaleReturnDuration = 0.14f;
    [SerializeField] private Ease arrivalScaleReturnEase = Ease.InOutSine;

    [Header("Arrival VFX")]
    [SerializeField] private string arrivalVfxPoolId = "AttachBurst";

    [Header("Audio")]
    [SerializeField] private string peelThresholdSfxId = DefaultPeelThresholdSfxId;
    [SerializeField, Range(0f, 1f)] private float peelSfxProgressThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float peelSfxVolume = 1f;
    [SerializeField] private string placementCompleteSfxId = DefaultPlacementCompleteSfxId;
    [Tooltip("Holder SFX'ini peel restore bitmeden kaç saniye önce çalar. 0 değeri tam bitişi bekler.")]
    [SerializeField, Min(0f)] private float placementCompleteSfxStartOverlap = 0.12f;
    [SerializeField, Range(0f, 1f)] private float placementCompleteSfxVolume = 1f;

    private Collider2D clickCollider;
    private StickerHolder targetHolder;
    private Sequence placementSequence;
    private bool isPlacing;
    private bool hasPlayedPeelThresholdSfx;
    private StickerTravelTrailVfx travelTrailVfx;
    private StickerStarTrailVfx travelStarVfx;
    private Tween travelTrailReleaseTween;
    private Tween travelStarReleaseTween;

    public StickerType StickerType => stickerType;
    public bool IsPlacing => isPlacing;

    private void Awake()
    {
        if (peelController == null)
            peelController = GetComponent<StickerPeelController>();

        EnsureClickCollider();
        FindMatchingHolder();
    }

    private void Update()
    {
        if (isPlacing)
        {
            StopTravelTrailWhenCloseToHolder();
            return;
        }

        if (!TryGetTapPosition(out var screenPosition))
            return;

        if (WasThisStickerTapped(screenPosition))
            PlaceInMatchingHolder();
    }

    private void OnDisable()
    {
        placementSequence?.Kill();
        placementSequence = null;
        isPlacing = false;
        StopTravelTrail(true);
    }

    /// <summary>
    /// Tap dışında başka bir sistemden de aynı yerleştirme akışını başlatır.
    /// </summary>
    public void PlaceInMatchingHolder()
    {
        if (isPlacing)
            return;

        if (peelController == null)
        {
            Debug.LogWarning($"{name} üzerinde StickerPeelController bulunamadı.", this);
            return;
        }

        if (targetHolder == null || targetHolder.StickerType != stickerType)
            FindMatchingHolder();

        if (targetHolder == null)
        {
            Debug.LogWarning($"{name} için {stickerType} tipinde StickerHolder bulunamadı.", this);
            return;
        }

        isPlacing = true;
        hasPlayedPeelThresholdSfx = false;
        clickCollider.enabled = false;
        peelController.ResetPeel();
        StopTravelTrail(true);

        var peelTween = DOTween.To(
                () => peelController.Progress,
                SetPeelProgressAndCheckSfx,
                peeledProgress,
                peelDuration)
            .SetEase(peelEase);
        var moveTween = transform.DOMove(targetHolder.transform.position, moveDuration)
            .SetEase(moveEase);
        var restoreTween = DOTween.To(
                () => peelController.Progress,
                value => peelController.Progress = value,
                0f,
                restoreDuration)
            .SetEase(restoreEase);

        var restingScale = transform.localScale;

        var moveStartTime = Mathf.Max(0f, peelDuration - moveStartOverlap);
        var restoreStartTime = moveStartTime + moveDuration;
        var restoreEndTime = restoreStartTime + restoreDuration;
        var trailStartTime = Mathf.Min(peelDuration, restoreStartTime);
        var scaleStartTime = Mathf.Max(
            restoreStartTime,
            restoreEndTime - arrivalScaleStartOverlap);
        var scaleReturnStartTime = scaleStartTime + arrivalScaleUpDuration;
        var placementCompleteSfxTime = Mathf.Max(
            restoreStartTime,
            restoreEndTime - placementCompleteSfxStartOverlap);

        placementSequence?.Kill();
        placementSequence = DOTween.Sequence()
            .Insert(0f, peelTween)
            .Insert(moveStartTime, moveTween)
            .InsertCallback(trailStartTime, StartTravelTrail)
            .InsertCallback(restoreStartTime, () => StopTravelTrail(false))
            .Insert(restoreStartTime, restoreTween)
            .InsertCallback(placementCompleteSfxTime, PlayPlacementCompleteSfx)
            .InsertCallback(scaleStartTime, PlayArrivalVfx)
            .Insert(scaleStartTime, transform.DOScale(
                    restingScale * arrivalScaleMultiplier,
                    arrivalScaleUpDuration)
                .SetEase(arrivalScaleUpEase))
            .Insert(scaleReturnStartTime, transform.DOScale(restingScale, arrivalScaleReturnDuration)
                .SetEase(arrivalScaleReturnEase))
            .OnComplete(CompletePlacement)
            .OnKill(() => placementSequence = null);
    }

    private void CompletePlacement()
    {
        transform.position = targetHolder.transform.position;
        peelController.Progress = 0f;
        StopTravelTrail(false);
        isPlacing = false;
        placementSequence = null;
    }

    private void PlayPlacementCompleteSfx()
    {
        PlaySfx(
            GetSfxIdOrDefault(placementCompleteSfxId, DefaultPlacementCompleteSfxId),
            placementCompleteSfxVolume);
    }

    private void PlayArrivalVfx()
    {
        if (PoolManager.Instance == null)
            return;

        if (!string.IsNullOrWhiteSpace(arrivalVfxPoolId))
        {
            PoolManager.Instance.PlayVfx(
                arrivalVfxPoolId,
                transform.position,
                transform.rotation);
        }

    }

    private void SetPeelProgressAndCheckSfx(float value)
    {
        peelController.Progress = value;

        if (hasPlayedPeelThresholdSfx || value < peelSfxProgressThreshold)
            return;

        hasPlayedPeelThresholdSfx = true;
        PlaySfx(
            GetSfxIdOrDefault(peelThresholdSfxId, DefaultPeelThresholdSfxId),
            peelSfxVolume);
    }

    private void PlaySfx(string sfxId, float volume)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlaySfx(sfxId, volume);
    }

    private static string GetSfxIdOrDefault(string configuredId, string defaultId)
    {
        return string.IsNullOrWhiteSpace(configuredId) ? defaultId : configuredId;
    }

    private void FindMatchingHolder()
    {
        targetHolder = null;
        var holders = FindObjectsByType<StickerHolder>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var holder in holders)
        {
            if (holder.StickerType != stickerType)
                continue;

            targetHolder = holder;
            return;
        }
    }

    private void EnsureClickCollider()
    {
        clickCollider = GetComponent<Collider2D>();
        if (clickCollider != null)
            return;

        var spriteRenderer = GetComponent<SpriteRenderer>();
        var boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.size = spriteRenderer.sprite.bounds.size;
        boxCollider.offset = spriteRenderer.sprite.bounds.center;
        clickCollider = boxCollider;
    }

    private void AcquireTravelTrail()
    {
        if (travelTrailVfx != null || PoolManager.Instance == null ||
            string.IsNullOrWhiteSpace(travelTrailPoolId))
            return;

        travelTrailReleaseTween?.Kill();
        travelTrailReleaseTween = null;

        var trailObject = PoolManager.Instance.Spawn(
            travelTrailPoolId,
            transform.position,
            transform.rotation,
            transform);
        if (trailObject == null)
            return;

        travelTrailVfx = trailObject.GetComponent<StickerTravelTrailVfx>();
        if (travelTrailVfx == null)
        {
            PoolManager.Instance.Despawn(trailObject);
            return;
        }

        travelTrailVfx.AttachTo(transform);
        var stickerRenderer = GetComponent<SpriteRenderer>();
        travelTrailVfx.ConfigureSorting(
            stickerRenderer.sortingLayerID,
            stickerRenderer.sortingOrder);
    }

    private void StartTravelTrail()
    {
        AcquireTravelTrail();
        travelTrailVfx?.Play();

        StartTravelStarParticles();
    }

    private void StopTravelTrail(bool clearImmediately)
    {
        if (travelTrailVfx != null)
        {
            travelTrailVfx.Stop(clearImmediately);
            if (clearImmediately)
            {
                ReleaseTravelTrail();
            }
            else if (travelTrailReleaseTween == null || !travelTrailReleaseTween.IsActive())
            {
                travelTrailReleaseTween = DOVirtual.DelayedCall(
                    travelTrailVfx.ReleaseDelay,
                    ReleaseTravelTrail);
            }
        }

        StopTravelStarParticles(clearImmediately);
    }

    private void ReleaseTravelTrail()
    {
        travelTrailReleaseTween?.Kill();
        travelTrailReleaseTween = null;

        if (travelTrailVfx == null)
            return;

        var instance = travelTrailVfx.gameObject;
        travelTrailVfx = null;
        if (PoolManager.Instance != null)
            PoolManager.Instance.Despawn(instance);
    }

    private void AcquireTravelStarParticles()
    {
        if (travelStarVfx != null || PoolManager.Instance == null ||
            string.IsNullOrWhiteSpace(travelStarPoolId))
            return;

        travelStarReleaseTween?.Kill();
        travelStarReleaseTween = null;

        var starObject = PoolManager.Instance.Spawn(
            travelStarPoolId,
            transform.position,
            transform.rotation,
            transform);
        if (starObject == null)
            return;

        travelStarVfx = starObject.GetComponent<StickerStarTrailVfx>();
        if (travelStarVfx == null)
        {
            PoolManager.Instance.Despawn(starObject);
            return;
        }

        travelStarVfx.AttachTo(transform);
        var stickerRenderer = GetComponent<SpriteRenderer>();
        travelStarVfx.ConfigureSorting(
            stickerRenderer.sortingLayerID,
            stickerRenderer.sortingOrder);
    }

    private void StartTravelStarParticles()
    {
        AcquireTravelStarParticles();
        travelStarVfx?.Play();
    }

    private void StopTravelStarParticles(bool clearImmediately)
    {
        if (travelStarVfx == null)
            return;

        travelStarVfx.Stop(clearImmediately);
        if (clearImmediately)
        {
            ReleaseTravelStarParticles();
        }
        else if (travelStarReleaseTween == null || !travelStarReleaseTween.IsActive())
        {
            travelStarReleaseTween = DOVirtual.DelayedCall(
                travelStarVfx.ReleaseDelay,
                ReleaseTravelStarParticles);
        }
    }

    private void ReleaseTravelStarParticles()
    {
        travelStarReleaseTween?.Kill();
        travelStarReleaseTween = null;

        if (travelStarVfx == null)
            return;

        var instance = travelStarVfx.gameObject;
        travelStarVfx = null;
        if (PoolManager.Instance != null)
            PoolManager.Instance.Despawn(instance);
    }

    private void StopTravelTrailWhenCloseToHolder()
    {
        var trailRenderer = travelTrailVfx != null ? travelTrailVfx.Renderer : null;
        if (trailRenderer == null || !trailRenderer.emitting || targetHolder == null)
            return;

        var stopDistance = Mathf.Max(0f, travelTrailStopDistance);
        if (stopDistance <= 0f)
            return;

        var distanceToHolderSqr = (transform.position - targetHolder.transform.position).sqrMagnitude;
        if (distanceToHolderSqr <= stopDistance * stopDistance)
            StopTravelTrail(false);
    }

    private bool WasThisStickerTapped(Vector2 screenPosition)
    {
        var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
        if (cameraToUse == null || clickCollider == null || !clickCollider.enabled)
            return false;

        var ray = cameraToUse.ScreenPointToRay(screenPosition);
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, clickLayers);
        return hit.collider == clickCollider ||
               (hit.collider != null && hit.collider.transform.IsChildOf(transform));
    }

    private static bool TryGetTapPosition(out Vector2 screenPosition)
    {
        if (Touchscreen.current != null)
        {
            var primaryTouch = Touchscreen.current.primaryTouch;
            if (primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = primaryTouch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }
}

public enum StickerType
{
    Hayvan,
    Meyve,
    Arac,
    Doga
}
