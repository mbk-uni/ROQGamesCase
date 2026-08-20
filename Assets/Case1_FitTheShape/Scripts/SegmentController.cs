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

    [Header("Arrival Feedback")]
    [Tooltip("Main Transform'un local X eksenindeki en büyük ölçek çarpanı.")]
    [SerializeField, Min(1f)] private float horizontalStretchMultiplier = 1.18f;
    [SerializeField, Min(0.01f)] private float stretchDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.14f;

    [Header("Neighbour Feedback")]
    [Tooltip("Ana segment en geniş haline ulaştığında sağ, sol, üst ve alt komşularını da stretch eder.")]
    [SerializeField] private bool stretchAdjacentSegments = true;

    private Vector3 mainInitialScale;
    private Tween stretchTween;

    private static readonly Vector2Int[] AdjacentOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

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
            .AppendCallback(PlayAdjacentStretch)
            .Append(mainTransform.DOScaleX(mainInitialScale.x, returnDuration).SetEase(Ease.InQuad));
    }

    private void PlayAdjacentStretch()
    {
        if (!stretchAdjacentSegments || !TryGetGridCoordinates(out var column, out var row))
            return;

        foreach (var offset in AdjacentOffsets)
        {
            var neighbourColumn = column + offset.x;
            var neighbourRow = offset.y == 0 ? row : WrapRow(row + offset.y);
            var neighbour = FindSegmentTransform(neighbourColumn, neighbourRow);
            if (neighbour == null)
                continue;

            var neighbourController = neighbour.GetComponent<SegmentController>();
            if (neighbourController != null)
            {
                neighbourController.PlayNeighbourStretch();
                continue;
            }

            PlayStretch(neighbour, neighbour.localScale);
        }
    }

    private void PlayNeighbourStretch()
    {
        if (mainTransform == null)
            return;

        PlayStretch(mainTransform, mainInitialScale);
    }

    private void PlayStretch(Transform target, Vector3 initialScale)
    {
        var stretchedScale = initialScale;
        stretchedScale.x *= horizontalStretchMultiplier;

        DOTween.Sequence()
            .Append(target.DOScaleX(stretchedScale.x, stretchDuration).SetEase(Ease.OutQuad))
            .Append(target.DOScaleX(initialScale.x, returnDuration).SetEase(Ease.InQuad));
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
}
