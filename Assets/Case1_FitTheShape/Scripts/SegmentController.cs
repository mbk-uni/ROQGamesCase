using DG.Tweening;
using UnityEngine;

public enum ShapeType
{
    Star,
    Round,
    Hexagon
}

public sealed class SegmentController : MonoBehaviour
{
    [Header("Shape Assignment")]
    [Tooltip("Bu segmentin kabul ettiği şekil türü.")]
    [SerializeField] private ShapeType targetShape = ShapeType.Star;

    [Header("Segment References")]
    [SerializeField] private Transform mainTransform;
    [SerializeField] private Transform holeTransform;

    [Header("Arrival Feedback")]
    [Tooltip("Main Transform'un local X eksenindeki en büyük ölçek çarpanı.")]
    [SerializeField, Min(1f)] private float horizontalStretchMultiplier = 1.18f;
    [SerializeField, Min(0.01f)] private float stretchDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.14f;

    private Vector3 mainInitialScale;
    private Tween stretchTween;

    public ShapeType TargetShape => targetShape;
    public Transform MainTransform => mainTransform;
    public Transform HoleTransform => holeTransform != null ? holeTransform : mainTransform;

    private void Awake()
    {
        if (mainTransform != null)
            mainInitialScale = mainTransform.localScale;
    }

    private void OnDisable()
    {
        stretchTween?.Kill();
        stretchTween = null;
    }

    /// <summary>
    /// Plays the segment feedback when its matching shape reaches the hole.
    /// </summary>
    public void PlayArrivalFeedback()
    {
        if (holeTransform != null)
            holeTransform.gameObject.SetActive(false);

        if (mainTransform == null)
            return;

        stretchTween?.Kill();
        mainTransform.localScale = mainInitialScale;

        var stretchedScale = mainInitialScale;
        stretchedScale.x *= horizontalStretchMultiplier;

        stretchTween = DOTween.Sequence()
            .Append(mainTransform.DOScaleX(stretchedScale.x, stretchDuration).SetEase(Ease.OutQuad))
            .Append(mainTransform.DOScaleX(mainInitialScale.x, returnDuration).SetEase(Ease.InQuad));
    }
}
