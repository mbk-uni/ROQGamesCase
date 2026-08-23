using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FloorTileRebuilder : MonoBehaviour
{
    [Header("Tile Prefabs")]
    [SerializeField] private GameObject lightFloorPrefab;
    [SerializeField] private GameObject darkFloorPrefab;
    [SerializeField] private Transform tilesParent;

    [Header("Restore Animation")]
    [SerializeField, Min(0f)] private float restoreDelay = 1f;
    [SerializeField, Min(0f)] private float minimumTileDelay = 0.08f;
    [SerializeField, Min(0f)] private float maximumTileDelay = 0.2f;
    [Tooltip("Yükselme animasyonunun tamamlandığı local Y seviyesi.")]
    [SerializeField] private float finalLocalY = -2.468f;
    [SerializeField] private Vector2 spawnBelowRange = new(0.35f, 0.65f);
    [SerializeField, Min(0f)] private float minimumRiseHeight = 0.55f;
    [SerializeField, Min(0f)] private float maximumRiseHeight = 1.1f;
    [SerializeField] private Vector2 riseDurationRange = new(0.2f, 0.35f);
    [SerializeField] private Vector2 settleDurationRange = new(0.25f, 0.45f);

    private readonly Dictionary<Vector2Int, Transform> existingTiles = new();
    private readonly HashSet<Vector2Int> restoredTiles = new();
    private bool gridCached;
    private Vector3 originLocalPosition;
    private Vector3 xStepLocal;
    private Vector3 zStepLocal;
    private Vector3 tileLocalScale;
    private Quaternion tileLocalRotation;

    public static void RestoreFor(HoleController hole, System.Action onRestoreComplete = null)
    {
        if (hole == null || hole.MissingTiles.Count == 0)
        {
            onRestoreComplete?.Invoke();
            return;
        }

        var rebuilder = FindFirstObjectByType<FloorTileRebuilder>();
        if (rebuilder == null)
        {
            Debug.LogWarning("FloorTileRebuilder sahnede bulunamadı.", hole);
            onRestoreComplete?.Invoke();
            return;
        }

        rebuilder.RestoreTilesFor(hole, onRestoreComplete);
    }

    private void Awake()
    {
        CacheGrid();
    }

    private void RestoreTilesFor(HoleController hole, System.Action onRestoreComplete)
    {
        CacheGrid();
        if (!gridCached)
        {
            onRestoreComplete?.Invoke();
            return;
        }

        var delayBeforeTile = restoreDelay;
        var tileRoutines = new List<(Vector2Int coordinate, float delay)>();
        foreach (var tile in hole.MissingTiles)
        {
            var coordinate = new Vector2Int(tile.column, tile.row);
            if (existingTiles.ContainsKey(coordinate) || !restoredTiles.Add(coordinate))
                continue;

            tileRoutines.Add((coordinate, delayBeforeTile));
            delayBeforeTile += Random.Range(
                Mathf.Min(minimumTileDelay, maximumTileDelay),
                Mathf.Max(minimumTileDelay, maximumTileDelay));
        }

        if (tileRoutines.Count == 0)
        {
            onRestoreComplete?.Invoke();
            return;
        }

        var completedTileCount = 0;
        System.Action onTileRestoreComplete = () =>
        {
            completedTileCount++;
            if (completedTileCount == tileRoutines.Count)
                onRestoreComplete?.Invoke();
        };

        foreach (var tileRoutine in tileRoutines)
            StartCoroutine(RestoreTileRoutine(tileRoutine.coordinate, tileRoutine.delay, onTileRestoreComplete));
    }

    private IEnumerator RestoreTileRoutine(Vector2Int coordinate, float delay, System.Action onComplete)
    {
        yield return new WaitForSeconds(delay);

        var prefab = IsLightTile(coordinate) ? lightFloorPrefab : darkFloorPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"Tile_{coordinate.x}_{coordinate.y} için gerekli Floor prefabı atanmadı.", this);
            onComplete?.Invoke();
            yield break;
        }

        var targetLocalPosition = originLocalPosition + xStepLocal * coordinate.x + zStepLocal * coordinate.y;
        targetLocalPosition.y = finalLocalY;
        var tile = Instantiate(prefab, tilesParent != null ? tilesParent : transform).transform;
        tile.name = $"Tile_{coordinate.x}_{coordinate.y}";
        tile.localRotation = tileLocalRotation;
        var prefabScale = tile.localScale;
        tile.localScale = new Vector3(tileLocalScale.x, prefabScale.y, tileLocalScale.z);

        var spawnPosition = targetLocalPosition - Vector3.up * Random.Range(spawnBelowRange.x, spawnBelowRange.y);
        var riseHeight = Random.Range(
            Mathf.Min(minimumRiseHeight, maximumRiseHeight),
            Mathf.Max(minimumRiseHeight, maximumRiseHeight));
        var peakPosition = targetLocalPosition + Vector3.up * riseHeight;
        tile.localPosition = spawnPosition;

        var tileCollider = tile.GetComponent<Collider>();
        if (tileCollider == null)
            tileCollider = tile.gameObject.AddComponent<BoxCollider>();

        tileCollider.enabled = false;
        yield return MoveLocalPosition(tile, spawnPosition, peakPosition, Random.Range(riseDurationRange.x, riseDurationRange.y));
        yield return MoveLocalPosition(tile, peakPosition, targetLocalPosition, Random.Range(settleDurationRange.x, settleDurationRange.y));
        tile.localPosition = targetLocalPosition;
        tileCollider.enabled = true;
        existingTiles[coordinate] = tile;
        onComplete?.Invoke();
    }

    private static IEnumerator MoveLocalPosition(Transform target, Vector3 from, Vector3 to, float duration)
    {
        var safeDuration = Mathf.Max(0.01f, duration);
        var elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            target.localPosition = Vector3.LerpUnclamped(from, to, Mathf.SmoothStep(0f, 1f, elapsed / safeDuration));
            yield return null;
        }
    }

    private void CacheGrid()
    {
        if (gridCached)
            return;

        foreach (Transform child in transform)
        {
            if (TryParseTileName(child.name, out var coordinate))
                existingTiles[coordinate] = child;
        }

        if (!existingTiles.TryGetValue(Vector2Int.zero, out var originTile))
        {
            Debug.LogWarning("Floor gridi için Tile_0_0 bulunamadı.", this);
            return;
        }

        originLocalPosition = originTile.localPosition;
        tileLocalRotation = originTile.localRotation;
        tileLocalScale = originTile.localScale;

        if (!TryGetStep(Vector2Int.right, out xStepLocal) || !TryGetStep(Vector2Int.up, out zStepLocal))
        {
            Debug.LogWarning("Floor grid adımı mevcut tile'lardan hesaplanamadı.", this);
            return;
        }

        gridCached = true;
    }

    private bool TryGetStep(Vector2Int direction, out Vector3 step)
    {
        foreach (var pair in existingTiles)
        {
            if (existingTiles.TryGetValue(pair.Key + direction, out var adjacentTile))
            {
                step = adjacentTile.localPosition - pair.Value.localPosition;
                return true;
            }
        }

        step = default;
        return false;
    }

    private static bool IsLightTile(Vector2Int coordinate)
    {
        return (coordinate.x + coordinate.y) % 2 == 0;
    }

    private static bool TryParseTileName(string tileName, out Vector2Int coordinate)
    {
        coordinate = default;
        var values = tileName.Split('_');
        if (values.Length != 3 || values[0] != "Tile" ||
            !int.TryParse(values[1], out var column) ||
            !int.TryParse(values[2], out var row))
            return false;

        coordinate = new Vector2Int(column, row);
        return true;
    }
}
