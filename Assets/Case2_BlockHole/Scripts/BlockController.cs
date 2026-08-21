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
    [Tooltip("Floor üzerinde kalan parçanın fade-out başlangıcına kadar bekleyeceği süre.")]
    [SerializeField, Min(0f)] private float fragmentFadeDelay = 1f;
    [Tooltip("Floor'a temas etmeyip hole'dan düşen parçanın fade-out başlangıcına kadar bekleyeceği süre.")]
    [SerializeField, Min(0f)] private float fragmentFallingFadeDelay = 0.25f;
    [SerializeField, Min(0.01f)] private float fragmentFadeDuration = 0.35f;
    [Tooltip("Parça bu dünya Y değerine ulaştığında fade-out beklemeden doğrudan kapanır.")]
    [SerializeField] private float fragmentDespawnBelowY = -10f;

    [Header("Break VFX")]
    [Tooltip("Kırılma başladıktan sonra DebrisBurst'ün oynatılacağı gecikme.")]
    [SerializeField, Min(0f)] private float debrisBurstDelay;

    private readonly List<MeshRenderer> solidRenderers = new();
    private readonly List<Collider> solidColliders = new();
    private readonly List<Transform> activeFracturedRoots = new();
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

        foreach (var root in activeFracturedRoots)
            root.gameObject.SetActive(true);

        PlayDustPuffs();
        PlayBreakVfx(hole);
        StartCoroutine(ReleaseFragments(hole));
        FloorTileRebuilder.RestoreFor(hole);
        return true;
    }

    private void PlayBreakVfx(HoleController hole)
    {
        if (debrisBurstDelay <= 0f)
        {
            PlayDebrisBurst(hole);
            return;
        }

        StartCoroutine(PlayDebrisBurstAfterDelay(hole));
    }

    private IEnumerator PlayDebrisBurstAfterDelay(HoleController hole)
    {
        yield return new WaitForSeconds(debrisBurstDelay);
        PlayDebrisBurst(hole);
    }

    private static void PlayDebrisBurst(HoleController hole)
    {
        if (PoolManager.Instance == null || hole == null)
            return;

        var anchor = hole.VfxAnchor;
        if (anchor == null)
            return;

        var startColor = hole.TryGetVfxColor(out var holeColor) ? holeColor : Color.white;
        PoolManager.Instance.PlayVfx("DebrisBurst", anchor.position, anchor.rotation, null, startColor);
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
                    fragmentFallingFadeDelay,
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
