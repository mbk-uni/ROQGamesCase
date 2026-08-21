using UnityEngine;
using UnityEngine.Serialization;

public sealed class HoleController : MonoBehaviour
{
    [FormerlySerializedAs("holeVariant")] public BlockType blockTypeVariant;

    [Header("Snap")]
    [Tooltip("Boşsa HoleController objesinin Transform'u kullanılır.")]
    [SerializeField] private Transform snapAnchor;
    [SerializeField, Min(0.01f)] private float snapRadius = 0.65f;

    [Header("Fragment Containment")]
    [Tooltip("Kırık parçaların yatay düzlemde hole dışına çıkabileceği en büyük mesafe.")]
    [SerializeField, Min(0.01f)] private float fragmentContainmentRadius = 0.42f;

    public bool IsOccupied { get; private set; }
    public Transform SnapAnchor => snapAnchor != null ? snapAnchor : transform;
    public Vector3 SnapPosition => SnapAnchor.position;
    public Quaternion SnapRotation => SnapAnchor.rotation;
    public float FragmentContainmentRadius => fragmentContainmentRadius;

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
            IsOccupied = true;
    }
}

public enum BlockType
{
    Single,
    Two,
    LShape
}
