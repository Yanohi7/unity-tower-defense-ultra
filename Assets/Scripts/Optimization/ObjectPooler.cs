using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    // Singleton instance of the ObjectPooler for easy access from other scripts
    public static ObjectPooler Instance { get; private set; }

    // Serializable class to define the configuration for each pool,
    // including the prefab and initial size
    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        public int initialSize = 10;
    }

    // List of pools to prewarm at the start of the game, set in the inspector
    [Header("Prewarm Pools")]
    [SerializeField] private PoolConfig[] poolsToPrewarm;

    // Dictionary to hold the queues of pooled objects for each prefab
    private readonly Dictionary<GameObject, Queue<GameObject>> pools =
        new Dictionary<GameObject, Queue<GameObject>>();

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        PrewarmPools();
    }

    // Method to prewarm the pools based on the configurations defined in the inspector 
    private void PrewarmPools()
    {
        foreach (PoolConfig config in poolsToPrewarm)
        {
            if (config == null || config.prefab == null || config.initialSize <= 0)
                continue;

            // Ensure a pool exists for the prefab before creating objects
            CreatePoolIfNeeded(config.prefab);

            // Create the specified number of objects for the pool and return them to the pool
            for (int i = 0; i < config.initialSize; i++)
            {
                GameObject obj = CreateNewObject(config.prefab);
                ReturnToPool(obj);
            }
        }
    }

    // Method to spawn an object from the pool, or create a new one if the pool is empty
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        // Ensure a pool exists for the prefab before trying to spawn an object
        CreatePoolIfNeeded(prefab);

        GameObject obj;

        // Check if there are available objects in the pool for the prefab, if so, dequeue one
        if (pools[prefab].Count > 0)
        {
            obj = pools[prefab].Dequeue();
        }
        // If the pool is empty, create a new object using the prefab
        else
        {
            obj = CreateNewObject(prefab);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    // Method to return an object to the pool, deactivating it and enqueueing it for reuse
    public void ReturnToPool(GameObject obj)
    {
        if (obj == null)
            return;

        // Try to get the PooledObject component to find out which prefab it belongs to
        PooledObject pooledObject = obj.GetComponent<PooledObject>();

        // If the object doesn't have a PooledObject component or the original 
        // prefab reference is missing, destroy the object instead of pooling it
        // to avoid issues
        if (pooledObject == null || pooledObject.OriginalPrefab == null)
        {
            Destroy(obj);
            return;
        }

        // Get the original prefab reference from the PooledObject component 
        // to determine which pool to return it to
        GameObject prefab = pooledObject.OriginalPrefab;

        CreatePoolIfNeeded(prefab);

        obj.SetActive(false);
        pools[prefab].Enqueue(obj);
    }

    // Method to ensure a pool exists for a given prefab, creating it if necessary
    private void CreatePoolIfNeeded(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
        {
            pools[prefab] = new Queue<GameObject>();
        }
    }

    // Method to create a new object from a prefab,
    // adding a PooledObject component to track its original prefab
    private GameObject CreateNewObject(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, transform);

        PooledObject pooledObject = obj.GetComponent<PooledObject>();

        if (pooledObject == null)
            pooledObject = obj.AddComponent<PooledObject>();

        pooledObject.SetOriginalPrefab(prefab);

        obj.SetActive(false);

        return obj;
    }
}