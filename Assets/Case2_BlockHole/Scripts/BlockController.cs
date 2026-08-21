using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlockController : MonoBehaviour
{
    public BlockType blockTypeVariant;

    [Header("Fracture")]
    [Tooltip("Boşsa child hiyerarşisinde Fractured adlı obje aranır.")]
    [SerializeField] private Transform fracturedRoot;
    [SerializeField, Min(0f)] private float fragmentScatterImpulse = 0.12f;
    [SerializeField, Min(0f)] private float fragmentUpwardImpulse = 0.045f;
    [SerializeField, Min(0f)] private float fragmentAngularImpulse = 0.03f;
    [Tooltip("Parçaların önce hafifçe ayrışıp görünür olacağı süre.")]
    [SerializeField, Min(0f)] private float fragmentGravityDelay = 0.3f;
    [Tooltip("Normal yerçekiminin kullanılacak oranı. Düşüşü daha yavaş ve okunur yapar.")]
    [SerializeField, Range(0.01f, 1f)] private float fragmentGravityScale = 0.28f;

    private readonly List<MeshRenderer> solidRenderers = new();
    private readonly List<Collider> solidColliders = new();
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

        if (fracturedRoot == null)
        {
            Debug.LogWarning($"{name} için Fractured child objesi bulunamadı.", this);
            return true;
        }

        fracturedRoot.gameObject.SetActive(true);
        StartCoroutine(ReleaseFragments(hole));
        return true;
    }

    private void CacheReferences()
    {
        if (referencesCached)
            return;

        referencesCached = true;
        if (fracturedRoot == null)
            fracturedRoot = FindChildNamed(transform, "Fractured");

        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (fracturedRoot == null || !renderer.transform.IsChildOf(fracturedRoot))
                solidRenderers.Add(renderer);
        }

        foreach (var collider in GetComponentsInChildren<Collider>(true))
        {
            if (fracturedRoot == null || !collider.transform.IsChildOf(fracturedRoot))
                solidColliders.Add(collider);
        }
    }

    private IEnumerator ReleaseFragments(HoleController hole)
    {
        var fragments = new List<Rigidbody>();
        var center = hole.SnapPosition;

        foreach (var meshRenderer in fracturedRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            var piece = meshRenderer.transform;
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
            fragments.Add(rigidbody);
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

    private static Transform FindChildNamed(Transform root, string childName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
