using UnityEngine;

/// <summary>
/// Gives every visible Floor tile a static collider at runtime. Keeping the
/// colliders on individual tiles preserves the intentional gaps made by holes.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloorPhysicsSurface : MonoBehaviour
{
    [Tooltip("Inactive tiles should also become solid when they are enabled later.")]
    [SerializeField] private bool includeInactiveTiles = true;

    private void Awake()
    {
        foreach (var tileRenderer in GetComponentsInChildren<Renderer>(includeInactiveTiles))
        {
            if (tileRenderer.GetComponent<Collider>() != null)
                continue;

            tileRenderer.gameObject.AddComponent<BoxCollider>();
        }
    }
}
