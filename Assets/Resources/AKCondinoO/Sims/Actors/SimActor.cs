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
        internal PersistentAIMyState persistentAIMyState;
        internal struct PersistentAIMyState{
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
        public const int V_STATE            =15;
        public const int V_PATHFINDING_STATE=16;
        public static int GetV(int V_,(Type simType,ulong number)id){
         if(SimObjectSpawner.Singleton.active.TryGetValue(id,out SimObject sO)&&(sO is SimActor sA)){
          if(V_==V_STATE){
           return(int)sA.MyState;
          }
          if(V_==V_PATHFINDING_STATE){
           return(int)sA.MyPathfindingState;
          }
         }
         return -1;
        }
        internal enum State:int{
         IDLE_ST=0,
        }
        protected State            MyState           =State.IDLE_ST        ;
        protected PathfindingState MyPathfindingState=PathfindingState.IDLE;
        internal virtual void AI(){
         MyPathfindingState=DestinationReached();
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
        internal enum PathfindingState:int{
         IDLE                  =0,
         REACHED               =1,
         PENDING               =2,
         TRAVELING             =3,
         TRAVELING_BUT_NO_SPEED=4,
        }
        internal PathfindingState DestinationReached(){
         if(!navMeshAgent.enabled){
          //  TO DO: fazer outras detecções se não estiver usando navMesh, como por exemplo ao voar
          return PathfindingState.IDLE;
         }else{
          if(navMeshAgent.pathPending){
           return PathfindingState.PENDING;
          }
          if(!navMeshAgent.hasPath){
           return PathfindingState.IDLE;
          }
          if(navMeshAgent.remainingDistance==Mathf.Infinity||navMeshAgent.remainingDistance==float.NaN||navMeshAgent.remainingDistance<0){
           return PathfindingState.IDLE;
          }
          if(navMeshAgent.remainingDistance>navMeshAgent.stoppingDistance){
           if(Mathf.Approximately(navMeshAgent.velocity.sqrMagnitude,0f)){
            return PathfindingState.TRAVELING_BUT_NO_SPEED;
           }
           return PathfindingState.TRAVELING;
          }else{
           return PathfindingState.REACHED;
          }
         }
        }
    }
}