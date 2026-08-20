using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("PoolManager içindeki ana VFX havuz adı.")]
    [SerializeField] private string arrivalVfxPoolId = "MiniConfetti";
    [Tooltip("Ana VFX ile aynı anda ve aynı VFX Anchor konumunda oynatılacak impact havuzu.")]
    [SerializeField] private string impactVfxPoolId = "ImpactBurst";
    [Tooltip("Diğer varış VFX'leriyle aynı anda ve aynı VFX Anchor konumunda oynatılacak merkez efekti havuzu.")]
    [SerializeField] private string centerSphereVfxPoolId = "CenterSphere";
    [Tooltip("CenterSphere için VFX Anchor rotasyonuna uygulanacak local Euler offset.")]
    [SerializeField] private Vector3 centerSphereRotationOffset = new Vector3(90f, 0f, 0f);
    [Tooltip("CenterSphere oynadıktan sonra MiniConfetti ve ImpactBurst'ün başlayacağı gecikme.")]
    [SerializeField, Min(0f)] private float secondaryVfxDelay = 0.03f;

    [Header("Arrival Audio")]
    [SerializeField] private string arrivalSfxId = "fittheshape_wave";
    [SerializeField, Range(0f, 1f)] private float arrivalSfxVolume = 1f;

    [Header("Arrival Feedback")]
    [Tooltip("Segmentin tüm eksenlerde ulaşacağı en büyük ölçek çarpanı.")]
    [FormerlySerializedAs("horizontalStretchMultiplier")]
    [SerializeField, Min(1f)] private float scaleMultiplier = 1.18f;
    [SerializeField, Min(0.01f)] private float stretchDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.14f;

    [Header("Wave Falloff")]
    [Tooltip("Dalga her komşu halkasında ne kadar zayıflar. 0.65: ikinci halka merkezdeki ek büyümenin %65'ini, üçüncü halka %42'sini alır.")]
    [SerializeField, Range(0.01f, 0.99f)] private float waveStrengthFalloffPerStep = 0.65f;

    private Vector3 mainInitialScale;
    private Tween stretchTween;
    private Tween secondaryVfxDelayTween;
    private readonly HashSet<Transform> waveVisitedSegments = new();

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
    public Transform VfxAnchor => vfxAnchor != null ? vfxAnchor : FindVfxAnchor();

    private void Awake()
    {
        if (mainTransform != null)
            mainInitialScale = mainTransform.localScale;
    }

    private void OnDisable()
    {
        stretchTween?.Kill();
        secondaryVfxDelayTween?.Kill();
        stretchTween = null;
        secondaryVfxDelayTween = null;
        waveVisitedSegments.Clear();
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
        stretchTween?.Kill();
        mainTransform.localScale = mainInitialScale;

        var stretchedScale = mainInitialScale * scaleMultiplier;

        stretchTween = DOTween.Sequence()
            .Append(mainTransform.DOScale(stretchedScale, stretchDuration).SetEase(Ease.OutQuad))
            .AppendCallback(StartWave)
            .Append(mainTransform.DOScale(mainInitialScale, returnDuration).SetEase(Ease.InQuad));
    }

    private void PlayArrivalVfx()
    {
        var spawnAnchor = VfxAnchor;
        if (PoolManager.Instance == null || spawnAnchor == null)
            return;

        var spawnPosition = spawnAnchor.position;
        var spawnRotation = spawnAnchor.rotation;

        PlayPooledVfx(
            centerSphereVfxPoolId,
            spawnPosition,
            spawnRotation * Quaternion.Euler(centerSphereRotationOffset));

        secondaryVfxDelayTween?.Kill();
        secondaryVfxDelayTween = DOVirtual.DelayedCall(
            secondaryVfxDelay,
            () => PlaySecondaryArrivalVfx(spawnPosition, spawnRotation));
    }

    private void PlaySecondaryArrivalVfx(Vector3 position, Quaternion rotation)
    {
        if (PoolManager.Instance == null)
            return;

        PlayPooledVfx(arrivalVfxPoolId, position, rotation);
        PlayPooledVfx(impactVfxPoolId, position, rotation);
    }

    private static void PlayPooledVfx(string poolId, Transform spawnAnchor)
    {
        if (string.IsNullOrWhiteSpace(poolId))
            return;

        PoolManager.Instance.PlayVfx(poolId, spawnAnchor.position, spawnAnchor.rotation);
    }

    private static void PlayPooledVfx(string poolId, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrWhiteSpace(poolId))
            return;

        PoolManager.Instance.PlayVfx(poolId, position, rotation);
    }

    private void PlayArrivalSfx()
    {
        var spawnAnchor = VfxAnchor;
        if (AudioManager.Instance == null || spawnAnchor == null || string.IsNullOrWhiteSpace(arrivalSfxId))
            return;

        AudioManager.Instance.PlaySfx(arrivalSfxId, arrivalSfxVolume);
    }

    private void StartWave()
    {
        if (!TryGetGridCoordinates(out var column, out var row))
            return;

        waveVisitedSegments.Clear();
        waveVisitedSegments.Add(mainTransform);
        PropagateWaveFrom(column, row, 1);
    }

    private void PropagateWaveFrom(int originColumn, int originRow, int waveDepth)
    {
        foreach (var direction in AdjacentOffsets)
        {
            var column = originColumn + direction.x;
            var row = direction.y == 0 ? originRow : WrapRow(originRow + direction.y);
            var neighbour = FindSegmentTransform(column, row);
            if (neighbour == null || !waveVisitedSegments.Add(neighbour))
                continue;

            var neighbourScaleMultiplier = GetWaveScaleMultiplier(waveDepth);
            PlayStretch(
                neighbour,
                neighbour.localScale,
                neighbourScaleMultiplier,
                () => PropagateWaveFrom(column, row, waveDepth + 1));
        }
    }

    private float GetWaveScaleMultiplier(int waveDepth)
    {
        var centerExtraScale = scaleMultiplier - 1f;
        var waveExtraScale = centerExtraScale * Mathf.Pow(waveStrengthFalloffPerStep, waveDepth);
        return 1f + waveExtraScale;
    }

    private void PlayStretch(
        Transform target,
        Vector3 initialScale,
        float targetScaleMultiplier,
        TweenCallback onPeak = null)
    {
        var stretchedScale = initialScale * targetScaleMultiplier;

        var sequence = DOTween.Sequence()
            .Append(target.DOScale(stretchedScale, stretchDuration).SetEase(Ease.OutQuad));

        if (onPeak != null)
            sequence.AppendCallback(onPeak);

        sequence.Append(target.DOScale(initialScale, returnDuration).SetEase(Ease.InQuad));
    }

    private bool TryGetGridCoordinates(out int column, out int row)
    {
        var segmentName = mainTransform != null ? mainTransform.name : gameObject.name;
        return TryParseGridCoordinates(segmentName, out column, out row);
    }

    private static bool TryParseGridCoordinates(string segmentName, out int column, out int row)
    {
        const string ColumnPrefix = "Segment_c";
        const string RowPrefix = "_r";

        column = 0;
        row = 0;

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
        var segmentContainer = GetSegmentContainer();
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

    private Transform GetSegmentContainer()
    {
        return mainTransform != null ? mainTransform.parent : transform.parent;
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
