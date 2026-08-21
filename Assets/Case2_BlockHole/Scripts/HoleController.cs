using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class HoleController : MonoBehaviour
{
    [FormerlySerializedAs("holeVariant")] public BlockType blockTypeVariant;

    [Header("Snap")]
    [Tooltip("Boşsa HoleController objesinin Transform'u kullanılır.")]
    [SerializeField] private Transform snapAnchor;
    [SerializeField, Min(0.01f)] private float snapRadius = 0.65f;

    [Header("Fragment Scatter")]
    [Tooltip("Parçaların hole merkezinden çevreye yayılabileceği en büyük mesafe.")]
    [FormerlySerializedAs("fragmentContainmentRadius")]
    [SerializeField, Min(0.01f)] private float fragmentScatterRadius = 0.42f;

    [Header("Missing Floor Tiles")]
    [SerializeField] private List<MissingFloorTile> missingTiles = new();

    [Header("Completion Fade")]
    [SerializeField, Min(0f)] private float fadeDelay = 1f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;

    public bool IsOccupied { get; private set; }
    public Transform SnapAnchor => snapAnchor != null ? snapAnchor : transform;
    public Vector3 SnapPosition => SnapAnchor.position;
    public Quaternion SnapRotation => SnapAnchor.rotation;
    public float FragmentContainmentRadius => fragmentScatterRadius;
    public IReadOnlyList<MissingFloorTile> MissingTiles => missingTiles;

    public bool CanAccept(BlockController block)
    {
        return block != null && !IsOccupied && block.BlockTypeVariant == blockTypeVariant;
    }

    public bool IsInsideSnapRange(Vector3 worldPosition)
    {
        var planarOffset = worldPosition - SnapPosition;
        planarOffset.y = 0f;
        return planarOffset.sqrMagnitude <= snapRadius * snapRadius;
    }

    public void Occupy(BlockController block)
    {
        if (CanAccept(block))
        {
            IsOccupied = true;

            var fadeOut = GetComponent<BlockFragmentFadeOut>();
            if (fadeOut == null)
                fadeOut = gameObject.AddComponent<BlockFragmentFadeOut>();

            fadeOut.Play(fadeDelay, fadeDuration);
        }
    }

}

[Serializable]
public struct MissingFloorTile
{
    [Min(0)] public int column;
    [Min(0)] public int row;
}

public enum BlockType
{
    Single,
    Two,
    LShape
}
