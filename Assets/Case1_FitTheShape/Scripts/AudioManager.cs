using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight manager for playing multiple one-shot sound effects.
/// Configure sound ids and clips in the Inspector, then play them through AudioManager.Instance.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    [Serializable]
    public sealed class SfxDefinition
    {
        [Tooltip("Koddan çağrılacak benzersiz ses adı. Örnek: SegmentHit")]
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Küçük bir aralık, tekrar eden seslerin daha doğal duyulmasını sağlar.")]
        public Vector2 pitchRange = Vector2.one;
    }

    public static AudioManager Instance { get; private set; }

    [SerializeField, Min(1)] private int initialSourceCount = 8;
    [SerializeField] private SfxDefinition[] sfx;

    private readonly Dictionary<string, SfxDefinition> sfxById = new();
    private readonly List<AudioSource> sources = new();
    private Transform sourceRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one AudioManager can exist in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        InitializeDefinitions();
        CreateSourceRoot();

        for (var index = 0; index < initialSourceCount; index++)
            CreateSource();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Plays a non-positional (2D) sound effect.
    /// </summary>
    public AudioSource PlaySfx(string sfxId, float volumeMultiplier = 1f)
    {
        return Play(sfxId, transform.position, false, volumeMultiplier);
    }

    /// <summary>
    /// Plays a positional (3D) sound effect at the supplied world position.
    /// </summary>
    public AudioSource PlaySfxAtPosition(string sfxId, Vector3 position, float volumeMultiplier = 1f)
    {
        return Play(sfxId, position, true, volumeMultiplier);
    }

    public void StopAllSfx()
    {
        foreach (var source in sources)
        {
            if (source != null)
                source.Stop();
        }
    }

    private AudioSource Play(string sfxId, Vector3 position, bool is3D, float volumeMultiplier)
    {
        if (!sfxById.TryGetValue(sfxId, out var definition))
        {
            Debug.LogWarning($"No SFX with id '{sfxId}' is configured in AudioManager.", this);
            return null;
        }

        var source = GetAvailableSource();
        source.transform.position = position;
        source.clip = definition.clip;
        source.volume = Mathf.Clamp01(definition.volume * volumeMultiplier);
        source.pitch = UnityEngine.Random.Range(
            Mathf.Min(definition.pitchRange.x, definition.pitchRange.y),
            Mathf.Max(definition.pitchRange.x, definition.pitchRange.y));
        source.spatialBlend = is3D ? 1f : 0f;
        source.loop = false;
        source.Play();
        return source;
    }

    private void InitializeDefinitions()
    {
        if (sfx == null)
            return;

        foreach (var definition in sfx)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.id) || definition.clip == null)
            {
                Debug.LogWarning("AudioManager has an invalid SFX entry. Every entry needs an id and an AudioClip.", this);
                continue;
            }

            if (sfxById.ContainsKey(definition.id))
            {
                Debug.LogWarning($"AudioManager contains duplicate SFX id '{definition.id}'.", this);
                continue;
            }

            sfxById.Add(definition.id, definition);
        }
    }

    private void CreateSourceRoot()
    {
        var rootObject = new GameObject("SFX Sources");
        rootObject.transform.SetParent(transform, false);
        sourceRoot = rootObject.transform;
    }

    private AudioSource GetAvailableSource()
    {
        foreach (var source in sources)
        {
            if (source != null && !source.isPlaying)
                return source;
        }

        return CreateSource();
    }

    private AudioSource CreateSource()
    {
        var sourceObject = new GameObject("SFX Source");
        sourceObject.transform.SetParent(sourceRoot, false);
        var source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        sources.Add(source);
        return source;
    }
}
