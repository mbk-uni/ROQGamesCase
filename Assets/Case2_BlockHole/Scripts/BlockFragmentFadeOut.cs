using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlockFragmentFadeOut : MonoBehaviour
{
    private Renderer[] fragmentRenderers;
    private Material[][] instanceMaterials;
    private Color[][] originalColors;
    private bool waitingForFloorContact;
    private float floorFadeDelay;
    private float fadeDuration;
    private float despawnBelowY = float.NegativeInfinity;

    public void Play(float delay, float duration)
    {
        StopAllCoroutines();
        waitingForFloorContact = false;
        despawnBelowY = float.NegativeInfinity;
        CacheMaterials();
        StartCoroutine(FadeAndDisable(delay, duration));
    }

    /// <summary>
    /// Uses a short fade timer for fragments that fall through a hole, while a
    /// fragment that lands on a Floor tile receives the longer floor timer.
    /// </summary>
    public void PlayByFloorState(
        float fallingDelay,
        float newFloorFadeDelay,
        float newFadeDuration,
        float newDespawnBelowY)
    {
        StopAllCoroutines();
        CacheMaterials();
        waitingForFloorContact = true;
        floorFadeDelay = Mathf.Max(0f, newFloorFadeDelay);
        fadeDuration = Mathf.Max(0.01f, newFadeDuration);
        despawnBelowY = newDespawnBelowY;
        StartCoroutine(FadeIfNoFloorContact(Mathf.Max(0f, fallingDelay)));
    }

    private void Update()
    {
        if (transform.position.y <= despawnBelowY)
            gameObject.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!waitingForFloorContact || collision.collider.GetComponentInParent<FloorPhysicsSurface>() == null)
            return;

        waitingForFloorContact = false;
        StopAllCoroutines();
        StartCoroutine(FadeAndDisable(floorFadeDelay, fadeDuration));
    }

    private IEnumerator FadeIfNoFloorContact(float fallingDelay)
    {
        yield return new WaitForSeconds(fallingDelay);
        if (!waitingForFloorContact)
            yield break;

        waitingForFloorContact = false;
        StartCoroutine(FadeAndDisable(0f, fadeDuration));
    }

    private void CacheMaterials()
    {
        fragmentRenderers = GetComponentsInChildren<Renderer>(true);
        instanceMaterials = new Material[fragmentRenderers.Length][];
        originalColors = new Color[fragmentRenderers.Length][];

        for (var rendererIndex = 0; rendererIndex < fragmentRenderers.Length; rendererIndex++)
        {
            var materials = fragmentRenderers[rendererIndex].materials;
            instanceMaterials[rendererIndex] = materials;
            originalColors[rendererIndex] = new Color[materials.Length];

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                originalColors[rendererIndex][materialIndex] = GetMaterialColor(material);
                MakeTransparent(material);
            }
        }
    }

    private IEnumerator FadeAndDisable(float delay, float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));

        var safeDuration = Mathf.Max(0.01f, duration);
        var elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / safeDuration));
            yield return null;
        }

        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    private void SetAlpha(float alphaMultiplier)
    {
        for (var rendererIndex = 0; rendererIndex < instanceMaterials.Length; rendererIndex++)
        {
            for (var materialIndex = 0; materialIndex < instanceMaterials[rendererIndex].Length; materialIndex++)
            {
                var color = originalColors[rendererIndex][materialIndex];
                color.a *= alphaMultiplier;
                SetMaterialColor(instanceMaterials[rendererIndex][materialIndex], color);
            }
        }
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void MakeTransparent(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
