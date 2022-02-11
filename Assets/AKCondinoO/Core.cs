#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO{
    internal class Core:MonoBehaviour{internal static Core Singleton;
        internal static int ThreadCount;
        internal static readonly string saveLocation=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("\\","/")+"/AbSolitudeV5273OSaneador/";
        internal static string saveName="terra";
        internal static string savePath;
        internal readonly NavMeshBuildSettings[]navMeshBuildSettings=new NavMeshBuildSettings[]{
         new NavMeshBuildSettings{
          agentTypeID=0,//  Medium size agent: 0
          agentHeight=1.75f,
          agentRadius=0.28125f,
          agentClimb=0.75f,
          agentSlope=60f,
          overrideTileSize=true,
                  tileSize=Width*Depth,
          overrideVoxelSize=true,
                  voxelSize=0.09375f,
          minRegionArea=0.28125f,
          debug=new NavMeshBuildDebugSettings{
           flags=NavMeshBuildDebugFlags.None,
          },
          maxJobWorkers=4,
         },
        };
        internal readonly List<Gameplayer>gameplayers=new List<Gameplayer>();
        void Awake(){if(Singleton==null){Singleton=this;}else{DestroyImmediate(this);return;}
         GCSettings.LatencyMode=GCLatencyMode.Batch;
         savePath=string.Format("{0}{1}/",saveLocation,saveName);
         Directory.CreateDirectory(savePath);
         PhysHelper.SetLayerMasks();
        }
        internal event EventHandler OnDestroyingCoreEvent;
        internal class OnDestroyingCoreEventArgs:EventArgs{
        }
        void OnDestroy(){
         try{
          EventHandler handler=OnDestroyingCoreEvent;handler?.Invoke(this,new OnDestroyingCoreEventArgs(){
          });
         }catch(Exception e){
          Logger.Error(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);
         }
         if(ThreadCount>0){
          Logger.Error("ThreadCount>0(ThreadCount=="+ThreadCount+"):one or more threads weren't stopped nor waited for termination");
         }
         if(Singleton==this){Singleton=null;}
        }
    }
    internal static class PhysHelper{
        internal static int VoxelTerrain;
        internal static int      NavMesh;
        internal static void SetLayerMasks(){
         VoxelTerrain=1<<LayerMask.NameToLayer("VoxelTerrain");
              NavMesh=1<<LayerMask.NameToLayer("VoxelTerrain");
        }
    }
    internal static class Logger{
        [Conditional("ENABLE_DEBUG_LOG")]
        internal static void Debug(string logMsg){
            UnityEngine.Debug.Log(logMsg);
        }
        internal static void Error(string logMsg){
            //  Always log errors
            UnityEngine.Debug.LogError(logMsg);
        }
        #if UNITY_EDITOR
            internal static void DrawBounds(Bounds b,Color color,float duration=0){//[https://gist.github.com/unitycoder/58f4b5d80f423d29e35c814a9556f9d9]
             var p1=new Vector3(b.min.x,b.min.y,b.min.z);// bottom
             var p2=new Vector3(b.max.x,b.min.y,b.min.z);
             var p3=new Vector3(b.max.x,b.min.y,b.max.z);
             var p4=new Vector3(b.min.x,b.min.y,b.max.z);
             var p5=new Vector3(b.min.x,b.max.y,b.min.z);// top
             var p6=new Vector3(b.max.x,b.max.y,b.min.z);
             var p7=new Vector3(b.max.x,b.max.y,b.max.z);
             var p8=new Vector3(b.min.x,b.max.y,b.max.z);
             UnityEngine.Debug.DrawLine(p1,p2,color,duration);
             UnityEngine.Debug.DrawLine(p2,p3,color,duration);
             UnityEngine.Debug.DrawLine(p3,p4,color,duration);
             UnityEngine.Debug.DrawLine(p4,p1,color,duration);
             UnityEngine.Debug.DrawLine(p5,p6,color,duration);
             UnityEngine.Debug.DrawLine(p6,p7,color,duration);
             UnityEngine.Debug.DrawLine(p7,p8,color,duration);
             UnityEngine.Debug.DrawLine(p8,p5,color,duration);
             UnityEngine.Debug.DrawLine(p1,p5,color,duration);// sides
             UnityEngine.Debug.DrawLine(p2,p6,color,duration);
             UnityEngine.Debug.DrawLine(p3,p7,color,duration);
             UnityEngine.Debug.DrawLine(p4,p8,color,duration);
            }
            internal static void DrawRotatedBounds(Vector3[]boundsVertices,Color color,float duration=0){
             UnityEngine.Debug.DrawLine(boundsVertices[0],boundsVertices[1],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[1],boundsVertices[2],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[2],boundsVertices[3],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[3],boundsVertices[0],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[4],boundsVertices[5],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[5],boundsVertices[6],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[6],boundsVertices[7],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[7],boundsVertices[4],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[0],boundsVertices[4],color,duration);// sides
             UnityEngine.Debug.DrawLine(boundsVertices[1],boundsVertices[5],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[2],boundsVertices[6],color,duration);
             UnityEngine.Debug.DrawLine(boundsVertices[3],boundsVertices[7],color,duration);
            }
        #endif
    }
    internal abstract class BackgroundContainer{
        internal readonly ManualResetEvent backgroundData=new ManualResetEvent( true);
        internal readonly   AutoResetEvent foregroundData=new   AutoResetEvent(false);
        internal bool IsCompleted(Func<bool>isRunning,int millisecondsTimeout=0){
         if(millisecondsTimeout<0&&isRunning.Invoke()!=true){
          return true;
         }
         return backgroundData.WaitOne(millisecondsTimeout);
        }
    }
    internal abstract class BaseMultithreaded<T>where T:BackgroundContainer{
        internal BaseMultithreaded(){
         Core.ThreadCount++;
         task=Task.Factory.StartNew(BG,TaskCreationOptions.LongRunning);
        }
        protected T container{get;private set;}
        void BG(){Thread.CurrentThread.IsBackground=false;
         ManualResetEvent backgroundData;
           AutoResetEvent foregroundData;
         while(!Stop){enqueued.WaitOne();if(Stop){enqueued.Set();goto _Stop;}
          if(queued.TryDequeue(out T dequeued)){
           container=dequeued;
           foregroundData=container.foregroundData;
           backgroundData=container.backgroundData;
           try{
            Renew(dequeued);
           }catch(Exception e){
            Logger.Error(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);
           }
          }else{
           continue;
          };
          if(queued.Count>0){
           enqueued.Set();
          }
          foregroundData.WaitOne();
          try{
           Execute();
          }catch(Exception e){
           Logger.Error(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);
          }
          try{
           Release();
          }catch(Exception e){
           Logger.Error(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);
          }
          backgroundData.Set();
          container=null;
          try{
           Cleanup();
          }catch(Exception e){
           Logger.Error(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);
          }
         }
         _Stop:{}
         Logger.Debug("Background task ending gracefully!");
        }
        internal static bool Stop{
         get{bool tmp;lock(Stop_syn){tmp=Stop_v;      }return tmp;}
         set{         lock(Stop_syn){    Stop_v=value;}if(value){enqueued.Set();}}
        }static bool Stop_v=false;static readonly object Stop_syn=new object();
        static readonly ConcurrentQueue<T>queued=new ConcurrentQueue<T>();
        static readonly AutoResetEvent enqueued=new AutoResetEvent(false);
        internal static void Schedule(T next){
         next.backgroundData.Reset();
         next.foregroundData.Set();
         queued.Enqueue(next);
         enqueued.Set();
        }
        internal static int Clear(){
         int count=queued.Count;
         while(queued.TryDequeue(out T dequeued)){
          dequeued.foregroundData.WaitOne(0);
          dequeued.backgroundData.Set();
         }
         return count;
        }
        readonly Task task;
        internal bool IsRunning(){
         return Stop==false&&task!=null&&!task.IsCompleted;
        }
        internal void Wait(){
         try{
          task.Wait();
          Core.ThreadCount--;
         }catch(Exception e){
          Logger.Error(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);
         }
        }
        protected virtual void Renew(T next){}
        protected abstract void Execute();
        protected virtual void Release(){}
        protected virtual void Cleanup(){}
    }
    #region[https://answers.unity.com/questions/956047/serialize-quaternion-or-vector3.html]

    /// <summary>
    ///  Since unity doesn't flag the Vector3 as serializable, we
    /// need to create our own version. This one will automatically convert
    /// between Vector3 and SerializableVector3
    /// </summary>
    [Serializable]internal struct SerializableVector3{
    /// <summary>
    /// x component
    /// </summary>
    public float x;
    /// <summary>
    /// y component
    /// </summary>
    public float y;
    /// <summary>
    /// z component
    /// </summary>
    public float z;
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="rX"></param>
    /// <param name="rY"></param>
    /// <param name="rZ"></param>
    public SerializableVector3(float rX,float rY,float rZ){
    x=rX;
    y=rY;
    z=rZ;
    }
    /// <summary>
    /// Returns a string representation of the object
    /// </summary>
    /// <returns></returns>
    public override string ToString(){
    return String.Format("({0},{1},{2})",x,y,z);
    }
    /// <summary>
    /// Automatic conversion from SerializableVector3 to Vector3
    /// </summary>
    /// <param name="rValue"></param>
    /// <returns></returns>
    public static implicit operator Vector3(SerializableVector3 rValue){
    return new Vector3(rValue.x,rValue.y,rValue.z);
    }
    /// <summary>
    /// Automatic conversion from Vector3 to SerializableVector3
    /// </summary>
    /// <param name="rValue"></param>
    /// <returns></returns>
    public static implicit operator SerializableVector3(Vector3 rValue){
    return new SerializableVector3(rValue.x,rValue.y,rValue.z);
    }
    }

    [Serializable]internal struct SerializableVector3Int{
    /// <summary>
    /// x component
    /// </summary>
    public int x;
    /// <summary>
    /// y component
    /// </summary>
    public int y;
    /// <summary>
    /// z component
    /// </summary>
    public int z;
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="rX"></param>
    /// <param name="rY"></param>
    /// <param name="rZ"></param>
    public SerializableVector3Int(int rX,int rY,int rZ){
    x=rX;
    y=rY;
    z=rZ;
    }
    /// <summary>
    /// Returns a string representation of the object
    /// </summary>
    /// <returns></returns>
    public override string ToString(){
    return String.Format("({0},{1},{2})",x,y,z);
    }
    /// <summary>
    /// Automatic conversion from SerializableVector3Int to Vector3Int
    /// </summary>
    /// <param name="rValue"></param>
    /// <returns></returns>
    public static implicit operator Vector3Int(SerializableVector3Int rValue){
    return new Vector3Int(rValue.x,rValue.y,rValue.z);
    }
    /// <summary>
    /// Automatic conversion from Vector3Int to SerializableVector3Int
    /// </summary>
    /// <param name="rValue"></param>
    /// <returns></returns>
    public static implicit operator SerializableVector3Int(Vector3Int rValue){
    return new SerializableVector3Int(rValue.x,rValue.y,rValue.z);
    }
    }

    /// <summary>
    ///  Since unity doesn't flag the Quaternion as serializable, we
    /// need to create our own version. This one will automatically convert
    /// between Quaternion and SerializableQuaternion
    /// </summary>
    [Serializable]internal struct SerializableQuaternion{
    /// <summary>
    /// x component
    /// </summary>
    public float x;     
    /// <summary>
    /// y component
    /// </summary>
    public float y;     
    /// <summary>
    /// z component
    /// </summary>
    public float z;     
    /// <summary>
    /// w component
    /// </summary>
    public float w;     
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="rX"></param>
    /// <param name="rY"></param>
    /// <param name="rZ"></param>
    /// <param name="rW"></param>
    public SerializableQuaternion(float rX,float rY,float rZ,float rW){
    x=rX;
    y=rY;
    z=rZ;
    w=rW;
    }     
    /// <summary>
    /// Returns a string representation of the object
    /// </summary>
    /// <returns></returns>
    public override string ToString(){
    return String.Format("({0},{1},{2},{3})",x,y,z,w);
    }
    /// <summary>
    /// Automatic conversion from SerializableQuaternion to Quaternion
    /// </summary>
    /// <param name="rValue"></param>
    /// <returns></returns>
    public static implicit operator Quaternion(SerializableQuaternion rValue){
    return new Quaternion(rValue.x,rValue.y,rValue.z,rValue.w);
    }     
    /// <summary>
    /// Automatic conversion from Quaternion to SerializableQuaternion
    /// </summary>
    /// <param name="rValue"></param>
    /// <returns></returns>
    public static implicit operator SerializableQuaternion(Quaternion rValue){
    return new SerializableQuaternion(rValue.x,rValue.y,rValue.z,rValue.w);
    }
    }

    #endregion
}