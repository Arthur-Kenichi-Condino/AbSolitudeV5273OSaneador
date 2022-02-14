using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace AKCondinoO.Sims{
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
        internal NavMeshAgent navMeshAgent;
        internal CharacterController characterController;
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
        }
    }
}