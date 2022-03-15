using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO{
    internal class SimTime:MonoBehaviour{
     internal const int   _YEAR  =12;
     internal const int   _MONTH =28;
     internal       float _DAY      ;
     internal       float _HOUR     ;
     internal       float _MINUTE   ;
     internal const float _SECOND=1f;
     void Awake(){
      _MINUTE=60*_SECOND;
      _HOUR  =60*_MINUTE;
      _DAY   =24*_HOUR  ;
     }
     /// <summary>
     ///  Quantos minutos dura um ciclo dia-noite no jogo se UseRealTime==false
     /// </summary>
     [SerializeField]internal float SimDayInRealMinutes=13.5f;
     internal uint  SimDay;
     internal uint  SimMonth;
     internal ulong SimYear;
     internal float SimTimeOfDay;//  Contagem de segundos total no dia
     internal float SimDayOfYear;//  Contagem de dias total no ano
     void Update(){        
      SimTimeOfDay+=Time.deltaTime*_DAY/(SimDayInRealMinutes*_MINUTE);
      if(SimTimeOfDay>=_DAY){
      }
     }
    }
}