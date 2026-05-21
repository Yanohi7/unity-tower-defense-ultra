using System.Collections;
using UnityEngine;

public class PooledAutoReturn : MonoBehaviour
{
    private Coroutine returnRoutine;

    // Method to start the auto-return process after a specified delay
    public void ReturnAfter(float seconds)
    {
        // If there's already a return routine running, stop it before starting a new one
        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        // Start a new coroutine to return the object to the pool after the specified delay
        returnRoutine = StartCoroutine(ReturnRoutine(seconds));
    }

    // Coroutine to handle the delayed return of the object to the pool
    private IEnumerator ReturnRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        // Return the object to the pool if the ObjectPooler instance exists, otherwise destroy it
        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }

    // Ensure that any running return routine is stopped when the object is disabled to prevent unintended behavior
    private void OnDisable()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
    }
}