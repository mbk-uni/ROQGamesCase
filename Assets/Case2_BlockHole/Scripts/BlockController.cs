using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlockController : MonoBehaviour
{
    public BlockType blockTypeVariant;

    [Header("Fracture")]
    [Tooltip("Birden fazla fracture grubu kullanıyorsan hepsini buraya ekle.")]
    [SerializeField] private List<Transform> fracturedRoots = new();
    [Tooltip("Önceki tek-root kurulumlarını korumak için kullanılır.")]
    [SerializeField, HideInInspector] private Transform fracturedRoot;
    [SerializeField, Min(0f)] private float fragmentScatterImpulse = 0.12f;
    [SerializeField, Min(0f)] private float fragmentUpwardImpulse = 0.045f;
    [SerializeField, Min(0f)] private float fragmentAngularImpulse = 0.03f;
    [Tooltip("Parçaların önce hafifçe ayrışıp görünür olacağı süre.")]
    [SerializeField, Min(0f)] private float fragmentGravityDelay = 0.3f;
    [Tooltip("Normal yerçekiminin kullanılacak oranı. Düşüşü daha yavaş ve okunur yapar.")]
    [SerializeField, Range(0.01f, 5f)] private float fragmentGravityScale = 0.28f;
    [Tooltip("Kırılma sonrası tüm parçaların fade-out başlangıcına kadar bekleyeceği süre.")]
    [SerializeField, Min(0f)] private float fragmentFadeDelay = 1f;
    [SerializeField, Min(0.01f)] private float fragmentFadeDuration = 0.35f;
    [Tooltip("Parça bu dünya Y değerine ulaştığında fade-out beklemeden doğrudan kapanır.")]
    [SerializeField] private float fragmentDespawnBelowY = -10f;

    private readonly List<MeshRenderer> solidRenderers = new();
    private readonly List<Collider> solidColliders = new();
    private readonly List<Transform> activeFracturedRoots = new();
    private readonly List<Material> temporarilyLoweredPriorityMaterials = new();
    private bool referencesCached;

    public BlockType BlockTypeVariant => blockTypeVariant;
    public bool IsConsumed { get; private set; }

    private void Awake()
    {
        CacheReferences();
    }

    public bool ConsumeAt(HoleController hole)
    {
        return ConsumeAt(hole, hole != null ? hole.SnapPosition : transform.position);
    }

    public bool ConsumeAt(HoleController hole, Vector3 snapPosition)
    {
        if (IsConsumed || hole == null || !hole.CanAccept(this))
            return false;

        CacheReferences();
        transform.SetPositionAndRotation(snapPosition, hole.SnapRotation);
        hole.Occupy(this);
        IsConsumed = true;

        foreach (var renderer in solidRenderers)
            renderer.enabled = false;

        foreach (var collider in solidColliders)
            collider.enabled = false;

        if (activeFracturedRoots.Count == 0)
        {
            Debug.LogWarning($"{name} için Fractured root objesi bulunamadı.", this);
            return true;
        }

        var isLShapeBreak = blockTypeVariant == BlockType.LShape;
        if (isLShapeBreak)
            BeginLShapeSortingPrioritySequence();

        foreach (var root in activeFracturedRoots)
            root.gameObject.SetActive(true);

        PlayDustPuffs();
        StartCoroutine(ReleaseFragments(hole));
        if (isLShapeBreak)
            FloorTileRebuilder.RestoreFor(hole, RestoreOtherBlocksSortingPriority);
        else
            FloorTileRebuilder.RestoreFor(hole);
        return true;
    }

    private void PlayDustPuffs()
    {
        if (PoolManager.Instance == null)
            return;

        var startColor = TryGetBlockVfxColor(out var blockColor) ? blockColor : Color.white;
        foreach (var fracturedRoot in activeFracturedRoots)
        {
            if (fracturedRoot == null)
                continue;

            var dustPosition = fracturedRoot.position + Vector3.up;
            PoolManager.Instance.PlayVfx("DustPuff", dustPosition, fracturedRoot.rotation, null, startColor);
        }
    }

    private bool TryGetBlockVfxColor(out Color color)
    {
        foreach (var renderer in solidRenderers)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                continue;

            var material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                return true;
            }
        }

        color = Color.white;
        return false;
    }

    public void BeginLShapeSortingPrioritySequence()
    {
        if (blockTypeVariant != BlockType.LShape)
            return;

        SetOtherBlocksSortingPriority(-1);
        Debug.Log($"{name}: L snap sequence started. " +
                  $"{temporarilyLoweredPriorityMaterials.Count} material priority set to -1.", this);
    }

    public void CancelLShapeSortingPrioritySequence()
    {
        if (blockTypeVariant == BlockType.LShape)
            RestoreOtherBlocksSortingPriority();
    }

    public void SetSolidMaterialsOpaque(bool isOpaque)
    {
        CacheReferences();
        foreach (var renderer in solidRenderers)
        {
            if (renderer == null)
                continue;

            foreach (var material in renderer.materials)
            {
                if (material != null)
                    SetMaterialSurfaceType(material, isOpaque);
            }
        }
    }

    private void SetOtherBlocksSortingPriority(int sortingPriority)
    {
        temporarilyLoweredPriorityMaterials.Clear();

        foreach (var otherBlock in FindObjectsByType<BlockController>(FindObjectsSortMode.None))
        {
            if (otherBlock == this || otherBlock.IsConsumed)
                continue;

            otherBlock.CacheReferences();
            foreach (var renderer in otherBlock.solidRenderers)
            {
                if (renderer == null)
                    continue;

                foreach (var material in renderer.materials)
                {
                    if (material == null || temporarilyLoweredPriorityMaterials.Contains(material))
                        continue;

                    temporarilyLoweredPriorityMaterials.Add(material);
                    SetMaterialSortingPriority(material, sortingPriority);
                }
            }
        }
    }

    private void RestoreOtherBlocksSortingPriority()
    {
        var restoredMaterialCount = temporarilyLoweredPriorityMaterials.Count;
        foreach (var material in temporarilyLoweredPriorityMaterials)
        {
            if (material != null)
                SetMaterialSortingPriority(material, 50);
        }

        temporarilyLoweredPriorityMaterials.Clear();
        if (restoredMaterialCount > 0)
            Debug.Log($"{name}: Floor tile restore completed. " +
                      $"{restoredMaterialCount} material priority restored to 50.", this);
    }

    private static void SetMaterialSortingPriority(Material material, int sortingPriority)
    {
        if (material.HasProperty("_QueueOffset"))
            material.SetFloat("_QueueOffset", sortingPriority);

        var baseQueue = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f
            ? 3000
            : 2000;
        material.renderQueue = baseQueue + sortingPriority;

        var queueOffset = material.HasProperty("_QueueOffset")
            ? material.GetFloat("_QueueOffset").ToString()
            : "not supported";
        Debug.Log($"{material.name}: Queue Offset = {queueOffset}, " +
                  $"Render Queue = {material.renderQueue}.");
    }

    private static void SetMaterialSurfaceType(Material material, bool isOpaque)
    {
        var queueOffset = material.HasProperty("_QueueOffset")
            ? Mathf.RoundToInt(material.GetFloat("_QueueOffset"))
            : 0;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", isOpaque ? 0f : 1f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (material.HasProperty("_DstBlend"))
        {
            var destinationBlend = isOpaque
                ? UnityEngine.Rendering.BlendMode.Zero
                : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
            material.SetFloat("_DstBlend", (float)destinationBlend);
        }
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", isOpaque ? 1f : 0f);

        if (isOpaque)
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = 2000 + queueOffset;
            return;
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000 + queueOffset;
    }

    private void CacheReferences()
    {
        if (referencesCached)
            return;

        referencesCached = true;
        CacheFracturedRoots();

        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!IsInFracturedRoot(renderer.transform))
                solidRenderers.Add(renderer);
        }

        foreach (var collider in GetComponentsInChildren<Collider>(true))
        {
            if (!IsInFracturedRoot(collider.transform))
                solidColliders.Add(collider);
        }
    }

    private void CacheFracturedRoots()
    {
        AddFracturedRoot(fracturedRoot);

        foreach (var root in fracturedRoots)
            AddFracturedRoot(root);

        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Fractured")
                AddFracturedRoot(child);
        }
    }

    private void AddFracturedRoot(Transform root)
    {
        if (root != null && root.IsChildOf(transform) && !activeFracturedRoots.Contains(root))
            activeFracturedRoots.Add(root);
    }

    private bool IsInFracturedRoot(Transform candidate)
    {
        foreach (var root in activeFracturedRoots)
        {
            if (candidate.IsChildOf(root))
                return true;
        }

        return false;
    }

    private IEnumerator ReleaseFragments(HoleController hole)
    {
        var fragments = new List<Rigidbody>();
        var center = hole.SnapPosition;

        var processedPieces = new HashSet<Transform>();
        foreach (var fracturedRoot in activeFracturedRoots)
        {
            foreach (var meshRenderer in fracturedRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var piece = meshRenderer.transform;
                if (!processedPieces.Add(piece))
                    continue;

                var rigidbody = piece.GetComponent<Rigidbody>();
                if (rigidbody == null)
                    rigidbody = piece.gameObject.AddComponent<Rigidbody>();

                if (piece.GetComponent<Collider>() == null)
                {
                    var meshFilter = piece.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        var meshCollider = piece.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                        meshCollider.convex = true;
                    }
                }

                rigidbody.isKinematic = false;
                rigidbody.useGravity = false;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;

                var radialDirection = piece.position - center;
                radialDirection.y = 0f;
                if (radialDirection.sqrMagnitude < 0.0001f)
                {
                    var randomPlanarDirection = Random.insideUnitCircle;
                    radialDirection = new Vector3(randomPlanarDirection.x, 0f, randomPlanarDirection.y);
                }

                radialDirection.Normalize();
                var impulse = radialDirection * Random.Range(fragmentScatterImpulse * 0.65f, fragmentScatterImpulse) +
                              Vector3.up * Random.Range(fragmentUpwardImpulse * 0.65f, fragmentUpwardImpulse);
                rigidbody.AddForce(impulse, ForceMode.Impulse);
                rigidbody.AddTorque(Random.insideUnitSphere * fragmentAngularImpulse, ForceMode.Impulse);

                var containment = piece.GetComponent<BlockFragmentContainment>();
                if (containment == null)
                    containment = piece.gameObject.AddComponent<BlockFragmentContainment>();

                containment.Configure(center, hole.FragmentContainmentRadius);

                var fadeOut = piece.GetComponent<BlockFragmentFadeOut>();
                if (fadeOut == null)
                    fadeOut = piece.gameObject.AddComponent<BlockFragmentFadeOut>();

                fadeOut.PlayByFloorState(
                    fragmentFadeDelay,
                    fragmentFadeDuration,
                    fragmentDespawnBelowY);
                fragments.Add(rigidbody);
            }
        }

        yield return new WaitForSeconds(fragmentGravityDelay);

        foreach (var fragment in fragments)
        {
            if (fragment != null)
            {
                var containment = fragment.GetComponent<BlockFragmentContainment>();
                if (containment != null)
                    containment.BeginFalling(fragmentGravityScale);
            }
        }
    }

}
