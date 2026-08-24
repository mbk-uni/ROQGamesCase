using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class StickerController : MonoBehaviour
{
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

    private Collider2D clickCollider;
    private StickerHolder targetHolder;
    private Sequence placementSequence;
    private bool isPlacing;

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
        if (isPlacing || !TryGetTapPosition(out var screenPosition))
            return;

        if (WasThisStickerTapped(screenPosition))
            PlaceInMatchingHolder();
    }

    private void OnDisable()
    {
        placementSequence?.Kill();
        placementSequence = null;
        isPlacing = false;
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
        clickCollider.enabled = false;
        peelController.ResetPeel();

        var peelTween = DOTween.To(
                () => peelController.Progress,
                value => peelController.Progress = value,
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

        var moveStartTime = Mathf.Max(0f, peelDuration - moveStartOverlap);
        var restoreStartTime = moveStartTime + moveDuration;

        placementSequence?.Kill();
        placementSequence = DOTween.Sequence()
            .Insert(0f, peelTween)
            .Insert(moveStartTime, moveTween)
            .Insert(restoreStartTime, restoreTween)
            .OnComplete(CompletePlacement)
            .OnKill(() => placementSequence = null);
    }

    private void CompletePlacement()
    {
        transform.position = targetHolder.transform.position;
        peelController.Progress = 0f;
        isPlacing = false;
        placementSequence = null;
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
    Arac
}
