using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses GameObject and ParticleSystem instances instead of instantiating them every time.
/// Add one PoolManager to the scene and configure its pools in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class PoolManager : MonoBehaviour
{
    [Serializable]
    public sealed class PoolDefinition
    {
        [Tooltip("Koddan istenen benzersiz havuz adı. Örnek: MiniConfetti")]
        public string id;
        public GameObject prefab;
        [Min(0)] public int initialSize = 4;
    }

    private sealed class Pool
    {
        public PoolDefinition Definition { get; }
        public Queue<GameObject> Available { get; } = new();
        public Transform Container { get; }

        public Pool(PoolDefinition definition, Transform container)
        {
            Definition = definition;
            Container = container;
        }
    }

    public static PoolManager Instance { get; private set; }

    [SerializeField] private PoolDefinition[] pools;

    private readonly Dictionary<string, Pool> poolsById = new();
    private readonly Dictionary<GameObject, Pool> poolByInstance = new();
    private readonly Dictionary<GameObject, int> instanceVersions = new();
    private Transform poolRoot;
    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one PoolManager can exist in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Takes an inactive object from the named pool and activates it at the requested pose.
    /// Call Despawn when a non-particle object is no longer needed.
    /// </summary>
    public GameObject Spawn(string poolId, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!TryGetPool(poolId, out var pool))
            return null;

        var instance = GetOrCreate(pool);
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        instanceVersions[instance] = instanceVersions[instance] + 1;
        return instance;
    }

    /// <summary>
    /// Spawns a pooled ParticleSystem, restarts it, and automatically returns it once all particles are gone.
    /// The particle's Loop setting must be disabled for automatic return to happen.
    /// </summary>
    public GameObject PlayVfx(string poolId, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var instance = Spawn(poolId, position, rotation, parent);
        if (instance == null)
            return null;

        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particleSystem in particleSystems)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }

        if (particleSystems.Length > 0)
            StartCoroutine(ReturnAfterParticlesFinish(instance, particleSystems, instanceVersions[instance]));
        else
            Debug.LogWarning($"Pool '{poolId}' has no ParticleSystem. Return it with Despawn instead.", instance);

        return instance;
    }

    /// <summary>
    /// Stops all particles in an instance and returns it to its original pool immediately.
    /// </summary>
    public void Despawn(GameObject instance)
    {
        if (instance == null || !poolByInstance.TryGetValue(instance, out var pool))
            return;

        foreach (var particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        instance.SetActive(false);
        instance.transform.SetParent(pool.Container, false);
        pool.Available.Enqueue(instance);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        var rootObject = new GameObject("Pooled Objects");
        rootObject.transform.SetParent(transform, false);
        poolRoot = rootObject.transform;

        if (pools == null)
            return;

        foreach (var definition in pools)
        {
            if (!IsValidDefinition(definition) || poolsById.ContainsKey(definition.id))
                continue;

            var containerObject = new GameObject(definition.id);
            containerObject.transform.SetParent(poolRoot, false);
            var pool = new Pool(definition, containerObject.transform);
            poolsById.Add(definition.id, pool);

            for (var index = 0; index < definition.initialSize; index++)
                pool.Available.Enqueue(CreateInstance(pool));
        }
    }

    private bool TryGetPool(string poolId, out Pool pool)
    {
        Initialize();
        if (poolsById.TryGetValue(poolId, out pool))
            return true;

        Debug.LogWarning($"No pool with id '{poolId}' is configured in PoolManager.", this);
        return false;
    }

    private GameObject GetOrCreate(Pool pool)
    {
        while (pool.Available.Count > 0)
        {
            var availableInstance = pool.Available.Dequeue();
            if (availableInstance != null)
                return availableInstance;
        }

        return CreateInstance(pool);
    }

    private GameObject CreateInstance(Pool pool)
    {
        var instance = Instantiate(pool.Definition.prefab, pool.Container);
        instance.name = pool.Definition.prefab.name;
        instance.SetActive(false);
        poolByInstance.Add(instance, pool);
        instanceVersions.Add(instance, 0);
        return instance;
    }

    private static bool IsValidDefinition(PoolDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.id) || definition.prefab == null)
        {
            Debug.LogWarning("PoolManager has an invalid pool entry. Every entry needs an id and a prefab.");
            return false;
        }

        return true;
    }

    private IEnumerator ReturnAfterParticlesFinish(GameObject instance, ParticleSystem[] particleSystems, int version)
    {
        yield return null;

        while (instance != null && instance.activeInHierarchy && AreAnyParticlesAlive(particleSystems))
            yield return null;

        if (instance != null && instance.activeSelf &&
            instanceVersions.TryGetValue(instance, out var currentVersion) && currentVersion == version)
            Despawn(instance);
    }

    private static bool AreAnyParticlesAlive(ParticleSystem[] particleSystems)
    {
        foreach (var particleSystem in particleSystems)
        {
            if (particleSystem != null && particleSystem.IsAlive(true))
                return true;
        }

        return false;
    }
}
