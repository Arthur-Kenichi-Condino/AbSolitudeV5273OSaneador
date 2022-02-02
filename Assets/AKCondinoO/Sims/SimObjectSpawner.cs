using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static AKCondinoO.Sims.SimObject;
namespace AKCondinoO.Sims{
    internal class SimObjectSpawner:MonoBehaviour{internal static SimObjectSpawner Singleton;
        internal readonly ConcurrentDictionary<(Type simType,ulong number),SimObject.PersistentData>persistentDataCache=new ConcurrentDictionary<(Type,ulong),SimObject.PersistentData>();
         readonly Dictionary<(Type simType,ulong number),float>persistentDataTimeToLive=new Dictionary<(Type,ulong),float>();
          readonly List<(Type simType,ulong number)>persistentDataTimeToLiveIds=new List<(Type,ulong)>();
        internal readonly Dictionary<Type,GameObject>SimObjectPrefabs=new Dictionary<Type,GameObject>();
        void Awake(){if(Singleton==null){Singleton=this;}else{DestroyImmediate(this);return;}
         Core.Singleton.OnDestroyingCoreEvent+=OnDestroyingCoreEvent;
         PersistentDataSavingMultithreaded.Stop=false;persistentDataSavingBGThread=new PersistentDataSavingMultithreaded();
         foreach(var o in Resources.LoadAll("AKCondinoO/",typeof(GameObject))){var gO=(GameObject)o;var sO=gO.GetComponent<SimObject>();if(sO==null)continue;
          Type t=sO.GetType();
          SimObjectPrefabs.Add(t,gO);
          pool.Add(t,new LinkedList<SimObject>());
          Logger.Debug("added Prefab:"+sO.name);
         }
         while(SpawnQueue.TryDequeue(out _));
         StartCoroutine(SpawnCoroutine());
        }
        void OnDestroyingCoreEvent(object sender,EventArgs e){
         PersistentDataSavingMultithreaded.Stop=true;persistentDataSavingBGThread.Wait();
         if(Singleton==this){Singleton=null;}
        }
        [SerializeField]int       DEBUG_CREATE_SIM_OBJECT_AMOUNT;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_ROTATION;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_POSITION;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_SCALE=Vector3.one;
        [SerializeField]SimObject DEBUG_CREATE_SIM_OBJECT=null;
        [SerializeField]bool      DEBUG_POOL_ALL_SIM_OBJECTS=false;
        internal readonly Dictionary<Type,ulong>ids=new Dictionary<Type,ulong>();
        internal readonly Dictionary<Type,List<ulong>>releasedIds=new Dictionary<Type,List<ulong>>();
        internal readonly Dictionary<Type,LinkedList<SimObject>>pool=new Dictionary<Type,LinkedList<SimObject>>();
         internal readonly Dictionary<(Type simType,ulong number),SimObject>active=new Dictionary<(Type,ulong),SimObject>();
        readonly SpawnData spawnData=new SpawnData();
        void Update(){
         if(DEBUG_CREATE_SIM_OBJECT!=null){
          if(spawnData.dequeued){
           Logger.Debug("DEBUG_CREATE_SIM_OBJECT:"+DEBUG_CREATE_SIM_OBJECT+";amount:"+DEBUG_CREATE_SIM_OBJECT_AMOUNT);
           var type=DEBUG_CREATE_SIM_OBJECT.GetType();
           for(int i=0;i<DEBUG_CREATE_SIM_OBJECT_AMOUNT;++i){
            spawnData.at.Add((DEBUG_CREATE_SIM_OBJECT_POSITION,DEBUG_CREATE_SIM_OBJECT_ROTATION,DEBUG_CREATE_SIM_OBJECT_SCALE,type,null));
           }
            DEBUG_CREATE_SIM_OBJECT=null;
           spawnData.dequeued=false;
           SpawnQueue.Enqueue(spawnData);
          }
         }
         if(DEBUG_POOL_ALL_SIM_OBJECTS){
            DEBUG_POOL_ALL_SIM_OBJECTS=false;
          foreach(var a in active){var sO=a.Value;
           sO.OnPoolRequest();
          }
         }
         persistentDataTimeToLiveIds.Clear();
         persistentDataTimeToLiveIds.AddRange(persistentDataTimeToLive.Keys);
         for(int i=0;i<persistentDataTimeToLiveIds.Count;++i){
          var id=persistentDataTimeToLiveIds[i];
          if((persistentDataTimeToLive[id]-=Time.deltaTime)<0f){
           persistentDataCache.TryRemove(id,out SimObject.PersistentData persistentData);
           persistentDataTimeToLive.Remove(id);
          }
         }
         //Debug.Log("persistentDataCache.Count:"+persistentDataCache.Count);
         foreach(var a in active){var sO=a.Value;
          sO.ManualUpdate();
         }
         while(DespawnQueue.Count>0){var toDespawn=DespawnQueue.Dequeue();
          OnDeactivate(toDespawn);
         }
        }
        internal class SpawnData{
         internal bool dequeued=true;
         internal readonly List<(Vector3 position,Vector3 rotation,Vector3 scale,Type type,ulong?id)>at;
         internal SpawnData(){
          at=new List<(Vector3,Vector3,Vector3,Type,ulong?)>(1);
         }
         internal SpawnData(int capacity){
          at=new List<(Vector3,Vector3,Vector3,Type,ulong?)>(capacity);
         }
        }
        internal static readonly ConcurrentQueue<SpawnData>SpawnQueue=new ConcurrentQueue<SpawnData>();
        WaitUntil waitSpawnQueue;
        IEnumerator SpawnCoroutine(){
         waitSpawnQueue=new WaitUntil(()=>{
          return SpawnQueue.Count>0;
         });
         Loop:{
          yield return waitSpawnQueue;
          while(SpawnQueue.TryDequeue(out SpawnData toSpawn)){
           Logger.Debug("toSpawn.at.Count:"+toSpawn.at.Count);
           foreach(var at in toSpawn.at){
            Type simType=at.type;
            ulong number;
            if(at.id==null){
             number=0;
             if(!ids.ContainsKey(simType)){
              ids.Add(simType,1);
             }else{
              number=ids[simType]++;
             }
            }else{
             number=at.id.Value;
            }
            (Type simType,ulong number)id=(simType,number);
            if(active.ContainsKey(id)){
             Logger.Debug("SpawnCoroutine:id already spawned:"+id);
             continue;
            }
            GameObject gO;SimObject sO;
            if(pool[at.type].Count>0){
             //Logger.Debug("SpawnCoroutine:using pooled sim object");
             sO=pool[at.type].First.Value;
             pool[at.type].RemoveFirst();
             sO.pooled=null;
              gO=sO.gameObject;
            }else{
             gO=Instantiate(SimObjectPrefabs[at.type],transform);
              sO=gO.GetComponent<SimObject>();
            }
            persistentDataTimeToLive.Remove(id);
            persistentDataCache.TryAdd(id,new SimObject.PersistentData());
            active.Add(id,sO);
            sO.id=id;
           }
           toSpawn.at.Clear();
           toSpawn.dequeued=true;
          }
         }
         goto Loop;
        }
        [SerializeField]float persistentDataCacheTimeToLive=10f;
        internal readonly Queue<SimObject>DespawnQueue=new Queue<SimObject>();
        void OnDeactivate(SimObject sO){
         active.Remove(sO.id.Value);
         persistentDataTimeToLive[sO.id.Value]=persistentDataCacheTimeToLive;
         sO.pooled=pool[sO.id.Value.simType].AddLast(sO);
         sO.id=null;
        }
        internal readonly PersistentDataSavingBackgroundContainer persistentDataSavingBG=new PersistentDataSavingBackgroundContainer();
        internal class PersistentDataSavingBackgroundContainer:BackgroundContainer{
         internal readonly Dictionary<Type,ConcurrentDictionary<ulong,SimObject.PersistentData>>data=new Dictionary<Type,ConcurrentDictionary<ulong,SimObject.PersistentData>>();
        }
        internal PersistentDataSavingMultithreaded persistentDataSavingBGThread;
        internal class PersistentDataSavingMultithreaded:BaseMultithreaded<PersistentDataSavingBackgroundContainer>{
         internal readonly Dictionary<Type,FileStream>fileStream=new Dictionary<Type,FileStream>();
          internal readonly Dictionary<Type,StreamWriter>fileStreamWriter=new Dictionary<Type,StreamWriter>();
          internal readonly Dictionary<Type,StreamReader>fileStreamReader=new Dictionary<Type,StreamReader>();
           internal readonly StringBuilder stringBuilder=new StringBuilder();
         protected override void Execute(){
         }
        }
    }
}