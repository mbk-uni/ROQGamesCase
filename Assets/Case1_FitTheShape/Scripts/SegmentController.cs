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
    private const int RowsPerColumn = 15;

    [Header("Shape Assignment")]
    [Tooltip("Bu segmentin kabul ettiği şekil türü.")]
    [SerializeField] private ShapeType targetShape = ShapeType.Star;

    [Header("Segment References")]
    [SerializeField] private Transform mainTransform;
    [SerializeField] private Transform holeTransform;
    [SerializeField] private Transform vfxAnchor;

    [Header("Arrival VFX")]
    [Tooltip("PoolManager içindeki VFX havuz adı.")]
    [SerializeField] private string arrivalVfxPoolId = "MiniConfetti";

    [Header("Arrival Audio")]
    [SerializeField] private string arrivalSfxId = "fittheshape_wave";
    [SerializeField, Range(0f, 1f)] private float arrivalSfxVolume = 1f;

    [Header("Arrival Feedback")]
    [Tooltip("Main Transform'un local X eksenindeki en büyük ölçek çarpanı.")]
    [SerializeField, Min(1f)] private float horizontalStretchMultiplier = 1.18f;
    [SerializeField, Min(0.01f)] private float stretchDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.14f;

    [Header("Neighbour Feedback")]
    [Tooltip("Ana segment en geniş haline ulaştığında dört yönde domino stretch dalgası başlatır.")]
    [SerializeField] private bool stretchAdjacentSegments = true;
    [Tooltip("Her yönde merkezden itibaren kaç segmentin tetikleneceği.")]
    [SerializeField, Min(1)] private int dominoReach = 4;

    [Header("Diagonal Ripple")]
    [Tooltip("Önceki domino efektinden bağımsız çapraz dalga efektini etkinleştirir.")]
    [SerializeField] private bool playDiagonalRipple = true;
    [SerializeField, Min(0f)] private float diagonalRippleDelay = 0.25f;
    [SerializeField, Min(1)] private int diagonalRippleReach = 4;

    private Vector3 mainInitialScale;
    private Tween stretchTween;
    private Tween diagonalRippleDelayTween;

    private static readonly Vector2Int[] AdjacentOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

    private static readonly Vector2Int[] DiagonalOffsets =
    {
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(1, 1)
    };

    public ShapeType TargetShape => targetShape;
    public Transform MainTransform => mainTransform;
    public Transform HoleTransform => holeTransform != null ? holeTransform : mainTransform;
    public Transform VfxAnchor => vfxAnchor != null ? vfxAnchor : FindVfxAnchor();

    private void Awake()
    {
        if (mainTransform != null)
            mainInitialScale = mainTransform.localScale;
    }

    private void OnDisable()
    {
        stretchTween?.Kill();
        stretchTween = null;
        diagonalRippleDelayTween?.Kill();
        diagonalRippleDelayTween = null;
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

        PlayArrivalVfx();
        PlayArrivalSfx();
        StartDiagonalRipple();
        stretchTween?.Kill();
        mainTransform.localScale = mainInitialScale;

        var stretchedScale = mainInitialScale;
        stretchedScale.x *= horizontalStretchMultiplier;

        stretchTween = DOTween.Sequence()
            .Append(mainTransform.DOScaleX(stretchedScale.x, stretchDuration).SetEase(Ease.OutQuad))
            .AppendCallback(PlayDominoWave)
            .Append(mainTransform.DOScaleX(mainInitialScale.x, returnDuration).SetEase(Ease.InQuad));
    }

    private void PlayArrivalVfx()
    {
        var spawnAnchor = VfxAnchor;
        if (PoolManager.Instance == null || spawnAnchor == null || string.IsNullOrWhiteSpace(arrivalVfxPoolId))
            return;

        PoolManager.Instance.PlayVfx(arrivalVfxPoolId, spawnAnchor.position, spawnAnchor.rotation);
    }

    private void PlayArrivalSfx()
    {
        var spawnAnchor = VfxAnchor;
        if (AudioManager.Instance == null || spawnAnchor == null || string.IsNullOrWhiteSpace(arrivalSfxId))
            return;

        AudioManager.Instance.PlaySfx(arrivalSfxId, arrivalSfxVolume);
    }

    private void PlayDominoWave()
    {
        if (!stretchAdjacentSegments || !TryGetGridCoordinates(out var column, out var row))
            return;

        foreach (var direction in AdjacentOffsets)
            PlayDominoStep(column, row, direction, 1);
    }

    private void StartDiagonalRipple()
    {
        if (!playDiagonalRipple)
            return;

        diagonalRippleDelayTween?.Kill();
        diagonalRippleDelayTween = DOVirtual.DelayedCall(diagonalRippleDelay, PlayDiagonalRippleWave);
    }

    private void PlayDiagonalRippleWave()
    {
        if (!TryGetGridCoordinates(out var column, out var row))
            return;

        foreach (var direction in DiagonalOffsets)
            PlayDiagonalRippleStep(column, row, direction, 1);
    }

    private void PlayDiagonalRippleStep(int originColumn, int originRow, Vector2Int direction, int distance)
    {
        if (distance > diagonalRippleReach)
            return;

        var column = originColumn + direction.x * distance;
        var row = WrapRow(originRow + direction.y * distance);
        var segment = FindSegmentTransform(column, row);
        if (segment == null)
            return;

        PlayStretch(
            segment,
            segment.localScale,
            () => PlayDiagonalRippleStep(originColumn, originRow, direction, distance + 1));
    }

    private void PlayDominoStep(int originColumn, int originRow, Vector2Int direction, int distance)
    {
        if (distance > dominoReach)
            return;

        var column = originColumn + direction.x * distance;
        var row = direction.y == 0
            ? originRow
            : WrapRow(originRow + direction.y * distance);
        var segment = FindSegmentTransform(column, row);
        if (segment == null)
            return;

        PlayStretch(
            segment,
            segment.localScale,
            () => PlayDominoStep(originColumn, originRow, direction, distance + 1));
    }

    private void PlayStretch(Transform target, Vector3 initialScale, TweenCallback onPeak = null)
    {
        var stretchedScale = initialScale;
        stretchedScale.x *= horizontalStretchMultiplier;

        var sequence = DOTween.Sequence()
            .Append(target.DOScaleX(stretchedScale.x, stretchDuration).SetEase(Ease.OutQuad));

        if (onPeak != null)
            sequence.AppendCallback(onPeak);

        sequence.Append(target.DOScaleX(initialScale.x, returnDuration).SetEase(Ease.InQuad));
    }

    private bool TryGetGridCoordinates(out int column, out int row)
    {
        const string ColumnPrefix = "Segment_c";
        const string RowPrefix = "_r";

        column = 0;
        row = 0;

        var segmentName = mainTransform != null ? mainTransform.name : gameObject.name;
        if (!segmentName.StartsWith(ColumnPrefix))
            return false;

        var rowPrefixIndex = segmentName.IndexOf(RowPrefix, ColumnPrefix.Length);
        if (rowPrefixIndex < 0)
            return false;

        return int.TryParse(
                   segmentName.Substring(ColumnPrefix.Length, rowPrefixIndex - ColumnPrefix.Length),
                   out column) &&
               int.TryParse(segmentName.Substring(rowPrefixIndex + RowPrefix.Length), out row);
    }

    private Transform FindSegmentTransform(int column, int row)
    {
        var segmentContainer = mainTransform != null ? mainTransform.parent : transform.parent;
        if (segmentContainer == null)
            return null;

        var targetName = $"Segment_c{column}_r{row}";
        foreach (var candidate in segmentContainer.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == targetName)
                return candidate;
        }

        return null;
    }

    private static int WrapRow(int row)
    {
        var wrappedRow = row % RowsPerColumn;
        return wrappedRow < 0 ? wrappedRow + RowsPerColumn : wrappedRow;
    }

    private Transform FindVfxAnchor()
    {
        if (mainTransform == null)
            return null;

        foreach (var candidate in mainTransform.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == "VfxAnchor")
                return candidate;
        }

        return mainTransform;
    }
}
