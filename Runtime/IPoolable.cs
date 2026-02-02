// jlink, 10/3/2019


	/// <summary>
	/// Used by SimplePool to spawn/despawn/unload prefabs
	/// </summary>
	public interface IPoolable
	{
		/// <summary>
		/// Called after an object is spawned in the scene. Replaces Start/OnEnable methods.
		/// </summary>
		void Initialize();

		/// <summary>
		/// Called before an object is despawned. Replaces OnDestroy/OnDisable methods.
		/// </summary>
		void Cleanup();

		/// <summary>
		/// Called before an object is removed from the SimplePool Pool and destroyed, usually when a scene ends. If an object has not been despawned and SimplePool.Unload() is called, Cleanup will run first.
		/// </summary>
		void Unload();
	}
