///
/// Simple pooling for Unity. Modified from the original version found here:
///   Author: Martin "quill18" Glaude (quill18@quill18.com)
///   Latest Version: https://gist.github.com/quill18/5a7cfffae68892621267
///   License: CC0 (http://creativecommons.org/publicdomain/zero/1.0/)
///   UPDATES:
/// 	2015-04-16: Changed Pool to use a Stack generic.
/// 
/// Usage:
/// 
///   There's no need to do any special setup of any kind.
/// 
///   Instead of calling Instantiate(), use this:
///       SimplePool.Spawn(somePrefab, somePosition, someRotation);
/// 
///   Instead of destroying an object, use this:
///       SimplePool.Despawn(myGameObject);
/// 
///   If desired, you can preload the pool with a number of instances:
///       SimplePool.Preload(somePrefab, 20);
/// 
/// Remember that Awake and Start will only ever be called on the first instantiation
/// and that member variables won't be reset automatically.  You should reset your
/// object yourself after calling Spawn().  (i.e. You'll have to do things like set
/// the object's HPs to max, reset animation states, etc...)
/// 


using FishNet.Component.Transforming.Beta;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;


public static class SimplePool
{

    // You can avoid resizing of the Stack's internal data by
    // setting this to a number equal to or greater to what you
    // expect most of your pool sizes to be.
    // Note, you can also use Preload() to set the initial size
    // of a pool -- this can be handy if only some of your pools
    // are going to be exceptionally large (for example, your bullets.)
    const int DEFAULT_POOL_SIZE = 300;

    public static bool preloaded = false;

    /// <summary>
    /// The Pool class represents the pool for a particular prefab.
    /// </summary>
    class Pool
    {
        // We append an id to the name of anything we instantiate.
        // This is purely cosmetic.
        int nextId = 1;

        // The structure containing our inactive objects.
        List<GameObject> inactive;

        // The structure containing our active objects.
        List<GameObject> active;

        // The prefab that we are pooling
        GameObject prefab;

        // Constructor
        public Pool(GameObject prefab, int initialQty)
        {
            this.prefab = prefab;

            inactive = new List<GameObject>(initialQty);
            active = new List<GameObject>(initialQty);
        }

        // Spawn an object from our pool
        public GameObject Spawn(Vector3 pos, Quaternion rot, out bool wasInstantiated, bool logToConsole = true, bool initialize = true)
        {
            GameObject obj;
            PoolMember poolMember = null;

            if (inactive.Count == 0)
            {
				// We don't have an object in our pool, so we
				// instantiate a whole new object.      
#if UNITY_EDITOR
				if (logToConsole)
					Debug.LogWarning($"Simple Pool: Pool is empty ({active.Count} active objects) - instantiating new object \"{prefab.name}\" \nIt is very important for performance reasons that all objects are preloaded at the level start.");
#endif

				obj = (GameObject)GameObject.Instantiate(prefab, pos, rot);
#if UNITY_EDITOR
                // Only append the identifier in the editor, to avoid allocing new strings at runtime.
                obj.name = prefab.name + " (" + (nextId++) + ")";
#endif

                // Add a PoolMember component so we know what pool
                // we belong to.
                poolMember = obj.AddComponent<PoolMember>();
                poolMember.myPool = this;
                poolMember.Initialize();

                wasInstantiated = true;
            }
            else
            {
                int lastIndex = inactive.Count - 1;
                obj = inactive[lastIndex];
                inactive.RemoveAt(lastIndex);

                if (obj == null)
                {
                    // The inactive object we expected to find no longer exists.
                    // The most likely causes are:
                    //   - Someone calling Destroy() on our object
                    //   - A scene change (which will destroy all our objects).
                    //     NOTE: This could be prevented with a DontDestroyOnLoad
                    //	   if you really don't want this.
                    // No worries -- we'll just try the next one in our sequence.

                    //if (logToConsole)
                        //Debug.Log($"Simple Pool: Object for prefab \"{prefab.name}\" is missing from pool. \n The most likely causes are: \n - Someone calling Destroy() on our object - A scene change (which will destroy all our objects).");

                    obj = Spawn(pos, rot, out bool _wasInstantiated, logToConsole, initialize);

                    wasInstantiated = _wasInstantiated;
                }
                else
                    wasInstantiated = false;

                if (obj != null)
                    poolMember = obj.GetComponent<PoolMember>();
            }

            obj.transform.position = pos;
            obj.transform.rotation = rot;

            if (poolMember != null)
            {
                foreach (Rigidbody rigidbody in poolMember.listRigidbodies)
                {
                    if (rigidbody == null || rigidbody.isKinematic)
                        continue;

                    rigidbody.linearVelocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }

                foreach (OfflineTickSmoother offlineTickSmoother in poolMember.listTickSmoothers)
                {
                    offlineTickSmoother.SmootherController.UniversalSmoother.Teleport();
                }
            }

            obj.SetActive(true);

            if (poolMember != null && initialize)
            {
                foreach (IPoolable iPoolable in poolMember.listPoolable)
                {
                    iPoolable.Initialize();
                }
            }

            active.Add(obj);

            return obj;
        }

        // Return an object to the inactive pool.
        public void Despawn(PoolMember poolMember, bool cleanup = true)
        {
            GameObject obj = poolMember.gameObject;

            obj.transform.parent = null;

            if (poolMember != null)
            {
                if (cleanup)
                {
                    foreach (IPoolable iPoolable in poolMember.listPoolable)
                    {
                        iPoolable.Cleanup();
                    }
                }
                
                if (poolMember.dontDestroyOnLoad)
                    GameObject.DontDestroyOnLoad(obj);
            }

            obj.transform.localScale = poolMember.initialScale;

            obj.SetActive(false);

            if (inactive.Count == 0 || inactive[inactive.Count - 1] != obj)
                inactive.Add(obj);

            active.Remove(obj);
        }

        // Destroy an object and remove it from the pool.
        public void Unload(GameObject gameObject, bool removeFromPool = true)
        {
            active.Remove(gameObject);

            if (gameObject == null)
                return;

            PoolMember pm = gameObject.GetComponent<PoolMember>();
            Unload(pm, removeFromPool);
        }

        public void Unload(PoolMember poolMember, bool removeFromPool = true)
        {
            GameObject obj = poolMember.gameObject;

            obj.transform.parent = null;

            if (poolMember != null)
            {
                foreach (IPoolable iPoolable in poolMember.listPoolable)
                {
                    iPoolable.Unload();
                }
            }

            if (removeFromPool)
                inactive.Remove(obj);

            if (active.Contains(obj))
                active.Remove(obj);

            obj.SetActive(false);
            GameObject.Destroy(obj);
        }

        public void UnloadAll()
        {
            List<GameObject> newActive = new List<GameObject>();
            newActive.AddRange(active);
            foreach (GameObject g in newActive)
            {
                if (g != null)
                    Despawn(g.GetComponent<PoolMember>());
            }
            active.Clear();

            List<GameObject> newInactive = new List<GameObject>();
            newInactive.AddRange(inactive);
            foreach (GameObject g in newInactive)
            {
                Unload(g);
            }
            inactive.Clear();

            nextId = 1;
        }
    }


    /// <summary>
    /// Added to freshly instantiated objects, so we can link back
    /// to the correct pool on despawn.
    /// </summary>
    class PoolMember : MonoBehaviour
    {
        public Pool myPool;
        public Vector3 initialScale;

        public List<IPoolable> listPoolable;
        public List<Rigidbody> listRigidbodies;
        public List<OfflineTickSmoother> listTickSmoothers;

        public bool dontDestroyOnLoad = false;

        public void Initialize()
        {
            initialScale = transform.localScale;

            listPoolable = CollectionCaches<IPoolable>.RetrieveList();
            listRigidbodies = CollectionCaches<Rigidbody>.RetrieveList();
            listTickSmoothers = CollectionCaches<OfflineTickSmoother>.RetrieveList();

            GetComponentsInChildren(true, listPoolable);
            GetComponentsInChildren(true, listRigidbodies);
            GetComponentsInChildren(true, listTickSmoothers);

#if FEATURE_PREWARM_POOLED_PARTICLESYSTEMS

            var listParticleSystems = CollectionCaches<ParticleSystem>.RetrieveList();
            GetComponentsInChildren(true, listParticleSystems);

            for (int i = 0; i < listParticleSystems.Count; i++)
            {
                ParticleSystem particleSystem = listParticleSystems[i];
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[20];
                particleSystem.SetParticles(particles);
                particleSystem.SetCustomParticleData(new List<Vector4>(20), ParticleSystemCustomData.Custom1);
                particleSystem.SetCustomParticleData(new List<Vector4>(20), ParticleSystemCustomData.Custom2);
            }

            CollectionCaches<ParticleSystem>.Store(listParticleSystems);
#endif
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        private void OnDestroy()
        {
            CollectionCaches<IPoolable>.Store(listPoolable);
            CollectionCaches<Rigidbody>.Store(listRigidbodies);
            CollectionCaches<OfflineTickSmoother>.Store(listTickSmoothers);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (dontDestroyOnLoad)
                Debug.LogWarning($"Simple Pool: Pooled object \"{transform.name}\" marked as dontDestroyOnLoad is getting destroyed!");
#endif
        }

        private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg1 == LoadSceneMode.Single)
                SimplePool.Despawn(gameObject);
        }

        private void SceneManager_sceneUnloaded(Scene arg0)
        {
            if (gameObject.scene == arg0)
                SimplePool.Despawn(gameObject);
        }
    }

    // All of our pools
    static Dictionary<GameObject, Pool> pools;

    /// <summary>
    /// Initialize our dictionary.
    /// </summary>
    static void Init(GameObject prefab = null, int qty = DEFAULT_POOL_SIZE)
    {
        pools ??= new Dictionary<GameObject, Pool>();

        if (prefab != null && pools.ContainsKey(prefab) == false)
        {
            pools[prefab] = new Pool(prefab, qty);
        }
    }

    /// <summary>
    /// If you want to preload a few copies of an object at the start
    /// of a scene, you can use this. Really not needed unless you're
    /// going to go from zero instances to 100+ very quickly.
    /// Could technically be optimized more, but in practice the
    /// Spawn/Despawn sequence is going to be pretty darn quick and
    /// this avoids code duplication.
    /// </summary>
    static public void Preload(GameObject prefab, int qty = 1, bool dontDestroyOnLoad = false)
    {
        Preload(prefab, out _, qty, dontDestroyOnLoad);
    }

    static public void Preload(GameObject prefab, out int instantiatedCount, int qty = 1, bool dontDestroyOnLoad = false)
    {
        Init(prefab, qty);

        instantiatedCount = 0;

        // Make an array to grab the objects we're about to pre-spawn.
        GameObject[] obs = new GameObject[qty];
        for (int i = 0; i < qty; i++)
        {
            obs[i] = Spawn(prefab, Vector3.zero, Quaternion.identity, out bool wasInstantiated, logToConsole: false, initialize: false);

            if (wasInstantiated)
                instantiatedCount++;

            if (dontDestroyOnLoad)
            {
                if (obs[i].TryGetComponent(out PoolMember poolMember))
                    poolMember.dontDestroyOnLoad = true;

                GameObject.DontDestroyOnLoad(obs[i]);
            }
        }

        // Now despawn them all.
        for (int i = 0; i < qty; i++)
        {
            Despawn(obs[i], false);
        }
    }

    static public GameObject Spawn(GameObject prefab)
    {
        return Spawn(prefab, Vector3.zero, Quaternion.identity);
    }

    static public GameObject Spawn(GameObject prefab, Transform parent)
    {
        GameObject g = Spawn(prefab, Vector3.zero, Quaternion.identity);

        if (g != null)
            g.transform.SetParent(parent, false);

        return g;
    }

    /// <summary>
    /// Spawns a copy of the specified prefab (instantiating one if required).
    /// NOTE: Remember that Awake() or Start() will only run on the very first
    /// spawn and that member variables won't get reset.  OnEnable will run
    /// after spawning -- but remember that toggling IsActive will also
    /// call that function.
    /// </summary>
    static public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, bool logToConsole = true, bool initialize = true)
    {
        return Spawn(prefab, pos, rot, out _, logToConsole, initialize);
    }

    static public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, out bool wasInstantiated, bool logToConsole = true, bool initialize = true)
    {
        if (prefab == null)
        {
            wasInstantiated = false;
            return null;
        }

        Init(prefab);

        return pools[prefab].Spawn(pos, rot, out wasInstantiated, logToConsole, initialize);
    }

    /// <summary>
    /// Despawn the specified gameobject back into its pool.
    /// </summary>
    static public void Despawn(GameObject obj, bool cleanup = true)
    {
        PoolMember pm = obj.GetComponent<PoolMember>();
        if (pm == null || pm.myPool == null)
        {
            // If the object is poolable but wasn't spawned from a pool still call Cleanup
            obj.GetComponent<IPoolable>()?.Cleanup();

            //Debug.Log("Object '" + obj.name + "' wasn't spawned from a pool. Destroying it instead.");
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                GameObject.DestroyImmediate(obj);
            else

#endif
                GameObject.Destroy(obj);
        }
        else
        {
            pm.myPool.Despawn(pm, cleanup);
        }
    }

    /// <summary>
    /// Despawn and unload a gameObject
    /// </summary>
    /// <param name="obj"></param>
    static public void Unload(GameObject obj)
    {
        PoolMember pm = obj.GetComponent<PoolMember>();
        if (pm == null)
        {
            //Debug.Log("Object '" + obj.name + "' wasn't spawned from a pool. Destroying it instead.");
            GameObject.Destroy(obj);
        }
        else
        {
            pm.myPool.Unload(pm);
        }
    }

    static public void UnloadPool(GameObject prefab)
    {
        Pool pool;

        if (pools.TryGetValue(prefab, out pool))
        {
            UnloadPool(pool);
        }
        else
        {
            Debug.Log("There is no pool with this prefab.");
        }
    }

    static private void UnloadPool(Pool pool)
    {
        if (pool != null)
        {
            pool.UnloadAll();
        }
    }

    static public void UnloadAll()
    {
        if (pools == null)
            return;

        List<GameObject> keys = pools.Keys.ToList();
        foreach (GameObject prefab in keys)
        {
            UnloadPool(prefab);
        }
    }

    static public List<GameObject> GetAllPrefabs()
    {
        return pools.Keys.ToList();
    }
}
