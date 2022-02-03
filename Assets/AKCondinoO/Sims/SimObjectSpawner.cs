using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static AKCondinoO.Sims.SimObject;
using static AKCondinoO.Voxels.VoxelSystem;
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
          string saveFile=string.Format("{0}{1}{2}",Core.savePath,t,".txt");
          Logger.Debug("saveFile:"+saveFile);
          persistentDataSavingBG.data[t]=new ConcurrentDictionary<ulong,PersistentData>();
          FileStream fileStream;
          persistentDataSavingBGThread.fileStream[t]=fileStream=new FileStream(saveFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
          persistentDataSavingBGThread.fileStreamWriter[t]=new StreamWriter(fileStream);
          persistentDataSavingBGThread.fileStreamReader[t]=new StreamReader(fileStream);
         }
         while(SpawnQueue.TryDequeue(out _));
         StartCoroutine(SpawnCoroutine());
        }
        void OnDestroyingCoreEvent(object sender,EventArgs e){
         if(PersistentDataSavingMultithreaded.Clear()!=0){
          Logger.Error("PersistentDataSaving task will stop with pending work");
         }
         PersistentDataSavingMultithreaded.Stop=true;persistentDataSavingBGThread.Wait();
         foreach(var kvp in persistentDataSavingBGThread.fileStream){
          Type t=kvp.Key;
          persistentDataSavingBGThread.fileStreamWriter[t].Dispose();
          persistentDataSavingBGThread.fileStreamReader[t].Dispose();
         }
         if(Singleton==this){Singleton=null;}
        }
        [SerializeField]int       DEBUG_CREATE_SIM_OBJECT_AMOUNT;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_ROTATION;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_POSITION;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_SCALE=Vector3.one;
        [SerializeField]SimObject DEBUG_CREATE_SIM_OBJECT=null;
        [SerializeField]bool      DEBUG_POOL_ALL_SIM_OBJECTS=false;
        [SerializeField]bool      DEBUG_SAVE_PENDING_PERSISTENT_DATA=false;
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
         if(DEBUG_SAVE_PENDING_PERSISTENT_DATA&&OnPendingPersistentDataPushToFile()){
            DEBUG_SAVE_PENDING_PERSISTENT_DATA=false;
         }
         foreach(var a in active){var sO=a.Value;
          sO.ManualUpdate();
         }
         while(DespawnQueue.Count>0){var toDespawn=DespawnQueue.Dequeue();
          OnDeactivate(toDespawn);
         }
        }
        bool OnPendingPersistentDataPushToFile(){
         if(persistentDataSavingBG.IsCompleted(persistentDataSavingBGThread.IsRunning)){
          PersistentDataSavingMultithreaded.Schedule(persistentDataSavingBG);
          return true;
         }
         return false;
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
         readonly Dictionary<Type,Dictionary<int,List<(ulong id,SimObject.PersistentData persistentData)>>>idPersistentDataListBycnkIdxByType=new Dictionary<Type,Dictionary<int,List<(ulong,SimObject.PersistentData)>>>();
          internal static readonly ConcurrentQueue<List<(ulong id,SimObject.PersistentData persistentData)>>idPersistentDataListPool=new ConcurrentQueue<List<(ulong,SimObject.PersistentData)>>();
         protected override void Cleanup(){
          //  pool lists
          foreach(var kvp1 in idPersistentDataListBycnkIdxByType){var idPersistentDataListBycnkIdx=kvp1.Value;
           foreach(var kvp2 in idPersistentDataListBycnkIdx){var idPersistentDataList=kvp2.Value;
            idPersistentDataList.Clear();
            idPersistentDataListPool.Enqueue(idPersistentDataList);
           }
           idPersistentDataListBycnkIdx.Clear();
          }
         }
         protected override void Execute(){


                // TO DO: lock syn, save ids and released ids, make loader thread for each sim object and loader to get ids and released ids and which sim objects to load
                container.data[typeof(SimObject)].TryAdd(0,new PersistentData());//AddOrUpdate
                container.data[typeof(SimObject)].TryAdd(1,new PersistentData());

                
            Logger.Debug("before saving idPersistentDataListPool.Count:"+idPersistentDataListPool.Count);
            foreach(var typePersistentDataToSavePair in container.data){Type t=typePersistentDataToSavePair.Key;var persistentDataToSave=typePersistentDataToSavePair.Value;
             Logger.Debug("before saving type:"+t+", pending PersistentData to save:"+persistentDataToSave.Count);
             foreach(var idPersistentDataPair in persistentDataToSave){ulong id=idPersistentDataPair.Key;
              if(persistentDataToSave.TryRemove(id,out SimObject.PersistentData persistentData)){
               Logger.Debug("saving sim object of type:"+t+", with id:"+id+", at:"+persistentData.position);
               Vector2Int cCoord=vecPosTocCoord(persistentData.position);
               int cnkIdx=GetcnkIdx(cCoord.x,cCoord.y);
               if(!idPersistentDataListBycnkIdxByType.ContainsKey(t)){
                idPersistentDataListBycnkIdxByType.Add(t,new Dictionary<int,List<(ulong,SimObject.PersistentData)>>());
               }
               if(!idPersistentDataListBycnkIdxByType[t].ContainsKey(cnkIdx)){
                if(idPersistentDataListPool.TryDequeue(out List<(ulong,SimObject.PersistentData)>idPersistentDataList)){
                 idPersistentDataListBycnkIdxByType[t].Add(cnkIdx,idPersistentDataList);
                }else{
                 idPersistentDataListBycnkIdxByType[t].Add(cnkIdx,new List<(ulong id,PersistentData persistentData)>());
                }
               }
               idPersistentDataListBycnkIdxByType[t][cnkIdx].Add((id,persistentData));
              }
             }
             Logger.Debug("will now save all of type:"+t+", still pending PersistentData to save for later:"+persistentDataToSave.Count);
            }
            foreach(var kvp1 in idPersistentDataListBycnkIdxByType){Type t=kvp1.Key;var idPersistentDataListBycnkIdx=kvp1.Value;
             FileStream fileStream=this.fileStream[t];
             StreamWriter fileStreamWriter=this.fileStreamWriter[t];
             StreamReader fileStreamReader=this.fileStreamReader[t];
             stringBuilder.Clear();
                    


             foreach(var kvp2 in idPersistentDataListBycnkIdx){int cnkIdx=kvp2.Key;var idPersistentDataList=kvp2.Value;
              stringBuilder.AppendFormat("{{ cnkIdx={0} , {{ ",cnkIdx);
              foreach(var idPersistentData in idPersistentDataList){ulong id=idPersistentData.id;SimObject.PersistentData persistentData=idPersistentData.persistentData;
               stringBuilder.AppendFormat("{{ id={0}, persistentData={{ position={1}, }} }}, ",id,persistentData.position);
              }
              stringBuilder.AppendFormat("}} }}, {0}",Environment.NewLine);
             }
             Debug.Log(stringBuilder.ToString());



            }
            Logger.Debug("after saving idPersistentDataListPool.Count:"+idPersistentDataListPool.Count);
         }
        }
    }
}