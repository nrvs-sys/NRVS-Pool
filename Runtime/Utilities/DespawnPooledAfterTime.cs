using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DespawnPooledAfterTime : MonoBehaviour
{
    [SerializeField] 
    private float timeBeforeDespawn = 5f;

    public UnityEvent onDespawn;

    private float timeElapsed;

    public float maxTimeBeforeDespawn => timeBeforeDespawn;
    public float remainingTimeBeforeDespawn => Mathf.Max(0f, timeBeforeDespawn - timeElapsed);

    private void OnEnable()
    {
        timeElapsed = 0f;
    }

    private void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= timeBeforeDespawn)
        {
            Despawn();
        }
    }

    private void Despawn()
    {
        onDespawn?.Invoke();
        SimplePool.Despawn(gameObject);
    }
}