#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using static AKCondinoO.Sims.SimObject;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO.Sims{
    internal class SimObjectSpawner:MonoBehaviour{internal static SimObjectSpawner Singleton;
        internal readonly Dictionary<Type,GameObject>SimObjectPrefabs=new Dictionary<Type,GameObject>();
        internal static string idsFile;
        internal static string releasedIdsFile;
        void Awake(){if(Singleton==null){Singleton=this;}else{DestroyImmediate(this);return;}
         Core.Singleton.OnDestroyingCoreEvent+=OnDestroyingCoreEvent;
                 idsFile=string.Format("{0}{1}",Core.savePath,        "ids.txt");
         releasedIdsFile=string.Format("{0}{1}",Core.savePath,"releasedIds.txt");
         simObjectSpawnSynchronization.Clear();
         PersistentDataSavingMultithreaded.Stop=false;persistentDataSavingBGThread=new PersistentDataSavingMultithreaded();
         foreach(var o in Resources.LoadAll("AKCondinoO/",typeof(GameObject))){var gO=(GameObject)o;var sO=gO.GetComponent<SimObject>();if(sO==null)continue;
          Type t=sO.GetType();
          SimObjectPrefabs.Add(t,gO);
          pool.Add(t,new LinkedList<SimObject>());
          Logger.Debug("added Prefab:"+sO.name);
          string saveFile=string.Format("{0}{1}{2}",Core.savePath,t,".txt");
          //Logger.Debug("saveFile:"+saveFile);
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
         persistentDataSavingBG.IsCompleted(persistentDataSavingBGThread.IsRunning,-1);
         foreach(var kvp in persistentDataCache){var id=kvp.Key;var persistentData=kvp.Value;
          persistentDataSavingBG.data[id.simType].AddOrUpdate(id.number,persistentData,
           (key,oldValue)=>{
            return persistentData;
           }
          );
         }
           persistentDataCache.Clear();
         persistentDataTimeToLive.Clear();
         foreach(var typeIdCount in ids){Type t=typeIdCount.Key;ulong idCount=typeIdCount.Value;
          persistentDataSavingBG.ids[t]=idCount;
         }
         foreach(var kvp in releasedIds){
          if(persistentDataSavingBG.releasedIds.TryGetValue(kvp.Key,out List<ulong>list)){
           list.Clear();
           list.AddRange(kvp.Value);
          }else{
           persistentDataSavingBG.releasedIds.Add(kvp.Key,new List<ulong>(kvp.Value));
          }
         }
         PersistentDataSavingMultithreaded.Schedule(persistentDataSavingBG);
         persistentDataSavingBG.IsCompleted(persistentDataSavingBGThread.IsRunning,-1);
         if(PersistentDataSavingMultithreaded.Clear()!=0){
          Logger.Error("PersistentDataSaving task will stop with pending work");
         }
         PersistentDataSavingMultithreaded.Stop=true;persistentDataSavingBGThread.Wait();
         foreach(var kvp in persistentDataSavingBGThread.fileStream){
          Type t=kvp.Key;
          persistentDataSavingBGThread.fileStreamWriter[t].Dispose();
          persistentDataSavingBGThread.fileStreamReader[t].Dispose();
         }
         persistentDataSavingBGThread.idsFileStreamWriter.Dispose();
         persistentDataSavingBGThread.idsFileStreamReader.Dispose();
         persistentDataSavingBGThread.releasedIdsFileStreamWriter.Dispose();
         persistentDataSavingBGThread.releasedIdsFileStreamReader.Dispose();
         if(Singleton==this){Singleton=null;}
        }
        [SerializeField]int       DEBUG_CREATE_SIM_OBJECT_AMOUNT;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_ROTATION;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_POSITION;
        [SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_SCALE=Vector3.one;
        [SerializeField]SimObject DEBUG_CREATE_SIM_OBJECT=null;
        [SerializeField]bool      DEBUG_POOL_ALL_SIM_OBJECTS=false;
        [SerializeField]bool      DEBUG_SAVE_PENDING_PERSISTENT_DATA=false;
        internal readonly Dictionary<(Type simType,ulong number),SimObject.PersistentData>persistentDataCache=new Dictionary<(Type,ulong),SimObject.PersistentData>();
         readonly Dictionary<(Type simType,ulong number),float>persistentDataTimeToLive=new Dictionary<(Type,ulong),float>();
          readonly List<(Type simType,ulong number)>persistentDataTimeToLiveIds=new List<(Type,ulong)>();
        internal readonly Dictionary<Type,ulong>ids=new Dictionary<Type,ulong>();
        internal readonly Dictionary<Type,List<ulong>>releasedIds=new Dictionary<Type,List<ulong>>();
        internal readonly Dictionary<Type,LinkedList<SimObject>>pool=new Dictionary<Type,LinkedList<SimObject>>();
         internal readonly Dictionary<(Type simType,ulong number),SimObject>active=new Dictionary<(Type,ulong),SimObject>();
        readonly SpawnData spawnData=new SpawnData();
        bool savingPersistentData;
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
           SimObject.PersistentData persistentData=persistentDataCache[id];
           persistentDataSavingBG.data[id.simType].AddOrUpdate(id.number,persistentData,
            (key,oldValue)=>{
             return persistentData;
            }
           );
             persistentDataCache.Remove(id);
           persistentDataTimeToLive.Remove(id);
          }
         }
         if(savingPersistentData&&OnPendingPersistentDataSaved()){
            savingPersistentData=false;
         }else{
             if(DEBUG_SAVE_PENDING_PERSISTENT_DATA&&OnPendingPersistentDataPushToFile()){
                DEBUG_SAVE_PENDING_PERSISTENT_DATA=false;
                OnPendingPersistentDataPushedToFile();
             }
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
          foreach(var typeIdCount in ids){Type t=typeIdCount.Key;ulong idCount=typeIdCount.Value;
           persistentDataSavingBG.ids[t]=idCount;
          }
          foreach(var kvp in releasedIds){
           if(persistentDataSavingBG.releasedIds.TryGetValue(kvp.Key,out List<ulong>list)){
            list.Clear();
            list.AddRange(kvp.Value);
           }else{
            persistentDataSavingBG.releasedIds.Add(kvp.Key,new List<ulong>(kvp.Value));
           }
          }
          PersistentDataSavingMultithreaded.Schedule(persistentDataSavingBG);
          return true;
         }
         return false;
        }
        void OnPendingPersistentDataPushedToFile(){
         savingPersistentData=true;
        }
        bool OnPendingPersistentDataSaved(){
         if(persistentDataSavingBG.IsCompleted(persistentDataSavingBGThread.IsRunning)){
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
            while(savingPersistentData)yield return null;
            Type simType=at.type;
            ulong number;
            if(at.id==null){
             number=0;
             if(!ids.ContainsKey(simType)){
              ids.Add(simType,1);
             }else{
              if(releasedIds.ContainsKey(simType)&&releasedIds[simType].Count>0){
               List<ulong>simTypeReleasedIds=releasedIds[simType];
               number=simTypeReleasedIds[simTypeReleasedIds.Count-1];
               simTypeReleasedIds.RemoveAt(simTypeReleasedIds.Count-1);
              }else{
               number=ids[simType]++;
              }
             }
            }else{
             number=at.id.Value;
             if(releasedIds.ContainsKey(simType)&&releasedIds[simType].Contains(number)){
              Logger.Debug("SpawnCoroutine:id number is unplaced:"+number);
              continue;
             }
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
             simObjectSpawnSynchronization.Add(sO,sO.synchronizer);
            }
            persistentDataTimeToLive.Remove(id);
            if(!persistentDataCache.ContainsKey(id)){
             persistentDataCache.Add(id,new SimObject.PersistentData());
            }//  TO DO: use loaded data
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
        internal static readonly Dictionary<SimObject,object>simObjectSpawnSynchronization=new Dictionary<SimObject,object>();
        #region loading
        internal class PersistentDataLoadingBackgroundContainer:BackgroundContainer{
        }
        internal class PersistentDataLoadingMultithreaded:BaseMultithreaded<PersistentDataLoadingBackgroundContainer>{
         internal readonly Dictionary<Type,FileStream>fileStream=new Dictionary<Type,FileStream>();
          internal readonly Dictionary<Type,StreamReader>fileStreamReader=new Dictionary<Type,StreamReader>();            
         protected override void Execute(){
          lock(simObjectSpawnSynchronization){
           foreach(var typeFileStreamPair in this.fileStream){Type t=typeFileStreamPair.Key;
            FileStream fileStream=typeFileStreamPair.Value;
           }
          }   
         }
        }
        #endregion
        #region saving
        internal readonly PersistentDataSavingBackgroundContainer persistentDataSavingBG=new PersistentDataSavingBackgroundContainer();
        internal class PersistentDataSavingBackgroundContainer:BackgroundContainer{
         internal readonly Dictionary<Type,ulong>ids=new Dictionary<Type,ulong>();
         internal readonly Dictionary<Type,List<ulong>>releasedIds=new Dictionary<Type,List<ulong>>();
         internal readonly Dictionary<Type,ConcurrentDictionary<ulong,SimObject.PersistentData>>data=new Dictionary<Type,ConcurrentDictionary<ulong,SimObject.PersistentData>>();
        }
        internal PersistentDataSavingMultithreaded persistentDataSavingBGThread;
        internal class PersistentDataSavingMultithreaded:BaseMultithreaded<PersistentDataSavingBackgroundContainer>{
         internal readonly FileStream idsFileStream;
          internal readonly StreamWriter idsFileStreamWriter;
          internal readonly StreamReader idsFileStreamReader;
           internal readonly StringBuilder idsStringBuilder=new StringBuilder();
         internal readonly FileStream releasedIdsFileStream;
          internal readonly StreamWriter releasedIdsFileStreamWriter;
          internal readonly StreamReader releasedIdsFileStreamReader;
           internal readonly StringBuilder releasedIdsStringBuilder=new StringBuilder();
         internal readonly Dictionary<Type,FileStream>fileStream=new Dictionary<Type,FileStream>();
          internal readonly Dictionary<Type,StreamWriter>fileStreamWriter=new Dictionary<Type,StreamWriter>();
          internal readonly Dictionary<Type,StreamReader>fileStreamReader=new Dictionary<Type,StreamReader>();
           internal readonly StringBuilder stringBuilder=new StringBuilder();
            internal readonly StringBuilder lineStringBuilder=new StringBuilder();
         readonly Dictionary<Type,Dictionary<int,List<(ulong id,SimObject.PersistentData persistentData)>>>idPersistentDataListBycnkIdxByType=new Dictionary<Type,Dictionary<int,List<(ulong,SimObject.PersistentData)>>>();
          internal static readonly ConcurrentQueue<List<(ulong id,SimObject.PersistentData persistentData)>>idPersistentDataListPool=new ConcurrentQueue<List<(ulong,SimObject.PersistentData)>>();
         readonly Dictionary<Type,List<ulong>>idListByType=new Dictionary<Type,List<ulong>>();
         readonly List<int>processedcnkIdx=new List<int>();
         internal PersistentDataSavingMultithreaded(){
          idsFileStream=new FileStream(SimObjectSpawner.idsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
           idsFileStreamWriter=new StreamWriter(idsFileStream);
           idsFileStreamReader=new StreamReader(idsFileStream);
          releasedIdsFileStream=new FileStream(SimObjectSpawner.releasedIdsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
           releasedIdsFileStreamWriter=new StreamWriter(releasedIdsFileStream);
           releasedIdsFileStreamReader=new StreamReader(releasedIdsFileStream);
         }
         protected override void Cleanup(){
          //  pool lists
          foreach(var kvp1 in idPersistentDataListBycnkIdxByType){var idPersistentDataListBycnkIdx=kvp1.Value;
           foreach(var kvp2 in idPersistentDataListBycnkIdx){var idPersistentDataList=kvp2.Value;
            idPersistentDataList.Clear();
            idPersistentDataListPool.Enqueue(idPersistentDataList);
           }
           idPersistentDataListBycnkIdx.Clear();
          }
          foreach(var kvp in idListByType){var idList=kvp.Value;
           idList.Clear();
          }
         }
         protected override void Execute(){
// TO DO: save released ids, make loader thread to get ids and released ids and sim objects
          lock(simObjectSpawnSynchronization){    
           idsStringBuilder.Clear();
           foreach(var typeIdCount in container.ids){Type t=typeIdCount.Key;ulong idCount=typeIdCount.Value;
            idsStringBuilder.AppendFormat("{{ type={0}, idCount={1} }}, {2}",t,idCount,Environment.NewLine);
           }
           idsFileStream.SetLength(0L);
           idsFileStreamWriter.Write(idsStringBuilder.ToString());
           idsFileStreamWriter.Flush();
           releasedIdsStringBuilder.Clear();
           foreach(var typeReleasedIdsPair in container.releasedIds){Type t=typeReleasedIdsPair.Key;var list=typeReleasedIdsPair.Value;
            releasedIdsStringBuilder.AppendFormat("{{ type={0}, {{ ",t);
            for(int i=0;i<list.Count;++i){
             releasedIdsStringBuilder.AppendFormat("{0}, ",list[i]);
            }
            releasedIdsStringBuilder.AppendFormat("}}, {0}",Environment.NewLine);
           }
           releasedIdsFileStream.SetLength(0L);
           releasedIdsFileStreamWriter.Write(releasedIdsStringBuilder.ToString());
           releasedIdsFileStreamWriter.Flush();
           foreach(var syn in simObjectSpawnSynchronization)Monitor.Enter(syn.Value);
           try{
            //Logger.Debug("before saving idPersistentDataListPool.Count:"+idPersistentDataListPool.Count);
            foreach(var typePersistentDataToSavePair in container.data){Type t=typePersistentDataToSavePair.Key;var persistentDataToSave=typePersistentDataToSavePair.Value;
             //Logger.Debug("before saving type:"+t+", pending PersistentData to save:"+persistentDataToSave.Count);
             if(!idListByType.ContainsKey(t)){
              idListByType.Add(t,new List<ulong>());
             }
             foreach(var idPersistentDataPair in persistentDataToSave){ulong id=idPersistentDataPair.Key;
              if(persistentDataToSave.TryRemove(id,out SimObject.PersistentData persistentData)){
               //Logger.Debug("saving sim object of type:"+t+", with id:"+id+", at:"+persistentData.position);
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
               idListByType[t].Add(id);
              }
             }
             //Logger.Debug("will now save all of type:"+t+", still pending PersistentData to save for later:"+persistentDataToSave.Count);
            }
            foreach(var kvp1 in idPersistentDataListBycnkIdxByType){Type t=kvp1.Key;var idPersistentDataListBycnkIdx=kvp1.Value;
             processedcnkIdx.Clear();
             FileStream fileStream=this.fileStream[t];
             StreamWriter fileStreamWriter=this.fileStreamWriter[t];
             StreamReader fileStreamReader=this.fileStreamReader[t];
             stringBuilder.Clear();                    
             fileStream.Position=0L;
             fileStreamReader.DiscardBufferedData();
             string line;
             while((line=fileStreamReader.ReadLine())!=null){
              if(string.IsNullOrEmpty(line)){continue;}
              int totalCharactersRemoved=0;
              lineStringBuilder.Clear();
              lineStringBuilder.Append(line);
              int cnkIdxStringStart=line.IndexOf("cnkIdx=")+7;
              int cnkIdxStringEnd=line.IndexOf(" ,",cnkIdxStringStart);
              int cnkIdxStringLength=cnkIdxStringEnd-cnkIdxStringStart;
              int cnkIdx=int.Parse(line.Substring(cnkIdxStringStart,cnkIdxStringLength));
              //Logger.Debug("process file at cnkIdx:"+cnkIdx);
              processedcnkIdx.Add(cnkIdx);
              int simObjectStringStart=cnkIdxStringEnd+2;
              int endOfLineStart=simObjectStringStart;
              while((simObjectStringStart=line.IndexOf("simObject=",simObjectStringStart))>=0){
               int simObjectStringEnd=line.IndexOf("}, ",simObjectStringStart)+3;
               string simObjectString=line.Substring(simObjectStringStart,simObjectStringEnd-simObjectStringStart);
               //Logger.Debug("simObjectString:"+simObjectString);
               int idStringStart=simObjectString.IndexOf("id=")+3;
               int idStringEnd=simObjectString.IndexOf(", ",idStringStart);
               ulong id=ulong.Parse(simObjectString.Substring(idStringStart,idStringEnd-idStringStart));
               //Logger.Debug("id:"+id);
               if(idListByType[t].Contains(id)){
                int toRemoveLength=simObjectStringEnd-totalCharactersRemoved-(simObjectStringStart-totalCharactersRemoved);
                lineStringBuilder.Remove(simObjectStringStart-totalCharactersRemoved,toRemoveLength);
                totalCharactersRemoved+=toRemoveLength;
               }
               simObjectStringStart=simObjectStringEnd;
               endOfLineStart=simObjectStringStart;
              }
              endOfLineStart=line.IndexOf("} }, ",endOfLineStart);
              int endOfLineEnd=line.IndexOf(", ",endOfLineStart)+2;
              lineStringBuilder.Remove(endOfLineStart-totalCharactersRemoved,endOfLineEnd-totalCharactersRemoved-(endOfLineStart-totalCharactersRemoved));
              line=lineStringBuilder.ToString();
              stringBuilder.Append(line);
              if(idPersistentDataListBycnkIdx.ContainsKey(cnkIdx)){
               foreach(var idPersistentData in idPersistentDataListBycnkIdx[cnkIdx]){ulong id=idPersistentData.id;SimObject.PersistentData persistentData=idPersistentData.persistentData;
                stringBuilder.AppendFormat("simObject={{ id={0}, {1} }}, ",id,persistentData.ToString());
               }
              }
              stringBuilder.AppendFormat("}} }}, {0}",Environment.NewLine);
             }
             foreach(var kvp2 in idPersistentDataListBycnkIdx){int cnkIdx=kvp2.Key;var idPersistentDataList=kvp2.Value;
              if(processedcnkIdx.Contains(cnkIdx)){continue;}
              stringBuilder.AppendFormat("{{ cnkIdx={0} , {{ ",cnkIdx);
              foreach(var idPersistentData in idPersistentDataList){ulong id=idPersistentData.id;SimObject.PersistentData persistentData=idPersistentData.persistentData;
               stringBuilder.AppendFormat("simObject={{ id={0}, {1} }}, ",id,persistentData.ToString());
              }
              stringBuilder.AppendFormat("}} }}, {0}",Environment.NewLine);
             }
             fileStream.SetLength(0L);
             fileStreamWriter.Write(stringBuilder.ToString());
             fileStreamWriter.Flush();
            }
            //Logger.Debug("after saving idPersistentDataListPool.Count:"+idPersistentDataListPool.Count);
           }catch{
            throw;
           }finally{
            foreach(var syn in simObjectSpawnSynchronization)Monitor.Exit(syn.Value);
           }
          }
         }
        }
        #endregion
    }
}