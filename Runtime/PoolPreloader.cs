using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PoolPreloader : MonoBehaviour
{
	[Header("Settings")]
	public List<PreloadObjectList> preloadObjectLists;

	private void Start()
	{
		Debug.Log($"Pool Manager: Preloading {preloadObjectLists.Sum(pol => pol.preloadObjects.Count)} prefabs");

		var preloadCount = 0;

		foreach (var preloadObjectList in preloadObjectLists)
		{
			foreach (var preloadObject in preloadObjectList.preloadObjects)
			{
				SimplePool.Preload(preloadObject.prefab, preloadObject.count, dontDestroyOnLoad: true);

				preloadCount += preloadObject.count;
			}
		}

		Debug.Log($"Pool Manager: Preloaded {preloadCount} objects");
	}
}