using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Preload Object List_ New", menuName = "Data/Preload Object List")]
public class PreloadObjectList : ScriptableObject
{
	[Header("Settings")]
	public List<PreloadObject> preloadObjects = new List<PreloadObject>();


	[Serializable]
	public class PreloadObject
	{
		public GameObject prefab;
		public int count = 1;
	}
}