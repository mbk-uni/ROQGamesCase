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

    [Header("Break VFX")]
    [Tooltip("Boş bırakılırsa Hole altındaki VfxAnchor aranır; o da yoksa Snap Anchor kullanılır.")]
    [SerializeField] private Transform vfxAnchor;
    [Tooltip("Boş bırakılırsa Hole objesindeki Renderer kullanılır.")]
    [SerializeField] private Renderer vfxColorRenderer;

    [Header("Snap Glow")]
    [SerializeField] private ParticleSystem glowVfx;

    public bool IsOccupied { get; private set; }
    public Transform SnapAnchor => snapAnchor != null ? snapAnchor : transform;
    public Vector3 SnapPosition => SnapAnchor.position;
    public Quaternion SnapRotation => SnapAnchor.rotation;
    public Transform VfxAnchor => vfxAnchor != null ? vfxAnchor : FindVfxAnchor();
    public float FragmentContainmentRadius => fragmentScatterRadius;
    public IReadOnlyList<MissingFloorTile> MissingTiles => missingTiles;

    private void Awake()
    {
        SetGlowVfxActive(false);
    }

    /// <summary>
    /// Gets the visible Hole material colour for a break VFX. URP materials use
    /// _BaseColor while legacy/standard materials commonly use _Color.
    /// </summary>
    public bool TryGetVfxColor(out Color color)
    {
        var renderer = vfxColorRenderer != null ? vfxColorRenderer : GetComponent<Renderer>();
        if (renderer == null)
            renderer = GetComponentInChildren<Renderer>(true);

        if (renderer != null)
        {
            var material = renderer.sharedMaterial;
            if (material != null)
            {
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
        }

        color = Color.white;
        return false;
    }

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
            SetGlowVfxActive(false);

            var fadeOut = GetComponent<BlockFragmentFadeOut>();
            if (fadeOut == null)
                fadeOut = gameObject.AddComponent<BlockFragmentFadeOut>();

            fadeOut.Play(fadeDelay, fadeDuration);
        }
    }

    public void SetGlowVfxActive(bool isActive)
    {
        if (glowVfx == null)
            return;

        if (!isActive)
        {
            glowVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            glowVfx.gameObject.SetActive(false);
            return;
        }

        glowVfx.gameObject.SetActive(true);
        glowVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        glowVfx.Play(true);
    }

    private Transform FindVfxAnchor()
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "VfxAnchor")
                return child;
        }

        return SnapAnchor;
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
