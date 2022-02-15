#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO.Sims.Actors{
    internal class SimActor:SimObject{
        internal PersistentStatsTree persistentStatsTree;
        internal struct PersistentStatsTree{
        }
        internal PersistentSkillTree persistentSkillTree;
        internal struct PersistentSkillTree{
        }
        internal PersistentInventory persistentInventory;
        internal struct PersistentInventory{
        }
        internal PersistentEquipment persistentEquipment;
        internal struct PersistentEquipment{
        }
        internal CharacterController characterController;
        internal NavMeshAgent navMeshAgent;
        internal NavMeshQueryFilter navMeshQueryFilter;
        protected override void Awake(){
         characterController=GetComponentInChildren<CharacterController>();
         localBounds=new Bounds(transform.position,
          new Vector3(
           characterController.radius*2f,
           characterController.height,
           characterController.radius*2f
          )
         );
         base.Awake();
         navMeshAgent=GetComponent<NavMeshAgent>();
         navMeshAgent.enabled=false;
         navMeshQueryFilter=new NavMeshQueryFilter(){
          agentTypeID=navMeshAgent.agentTypeID,
             areaMask=navMeshAgent.areaMask,
         };
         Logger.Debug("navMeshAgent.agentTypeID:"+navMeshAgent.agentTypeID);
        }
        internal bool isUsingAI=true;
        internal override void ManualUpdate(){
         base.ManualUpdate();
         if(!interactionsEnabled){
          DisableNavMeshAgent();
         }else{
          if(!isUsingAI){
           DisableNavMeshAgent();
          }else{
           EnableNavMeshAgent();
           if(!navMeshAgent.isOnNavMesh){
            DisableNavMeshAgent();
           }
           if(navMeshAgent.enabled){
            AI();
           }
          }
         }
        }
        public const int V_STATE=15;
        public static int GetV(int V_,(Type simType,ulong number)id){
         if(SimObjectSpawner.Singleton.active.TryGetValue(id,out SimObject sO)&&(sO is SimActor sA)){
          if(V_==V_STATE){
           return(int)sA.MyState;
          }
         }
         return -1;
        }
        internal enum State:int{
         IDLE_ST=0,
        }
        protected State MyState=State.IDLE_ST;
        internal virtual void AI(){
         if(MyState==State.IDLE_ST){
          OnIDLE_ST();
         }
        }
        internal virtual void OnIDLE_ST(){
        }
        internal void DisableNavMeshAgent(){
         navMeshAgent.enabled=false;
        }
        internal void EnableNavMeshAgent(){
         if(!navMeshAgent.enabled){
          if(NavMesh.SamplePosition(transform.position,out NavMeshHit hitResult,Height,navMeshQueryFilter)&&
           Mathf.Abs(hitResult.position.x-transform.position.x)<Width/2f&&
           Mathf.Abs(hitResult.position.z-transform.position.z)<Depth/2f
          ){
           transform.position=hitResult.position+Vector3.up*navMeshAgent.height/2f;
           navMeshAgent.enabled=true;
           Logger.Debug("navMeshAgent is enabled");
          }
         }
        }
    }
}