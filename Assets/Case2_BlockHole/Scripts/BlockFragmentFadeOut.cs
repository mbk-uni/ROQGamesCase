using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlockFragmentFadeOut : MonoBehaviour
{
    private Renderer[] fragmentRenderers;
    private Material[][] instanceMaterials;
    private Color[][] originalColors;

    public void Play(float delay, float duration)
    {
        StopAllCoroutines();
        CacheMaterials();
        StartCoroutine(FadeAndDisable(delay, duration));
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
