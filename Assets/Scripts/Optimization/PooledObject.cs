using UnityEngine;

public class PooledObject : MonoBehaviour
{
    // Property to store the original prefab reference for this pooled object
    public GameObject OriginalPrefab { get; private set; }

    // Method to set the original prefab reference, called when the object is first spawned
    public void SetOriginalPrefab(GameObject prefab)
    {
        OriginalPrefab = prefab;
    }
}