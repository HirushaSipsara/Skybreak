using UnityEngine;
using UnityEngine.InputSystem;
using Skybreak.Combat;

namespace Skybreak.Warden
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WardenLabPlayer : DamageSink
    {
        public Camera eye;
        public WardenNoiseBus noise;
        public WardenObjective objective;
        public bool acceptInput=true;
        public override Allegiance Team=>Allegiance.Player;
        public override float Health=>health;
        public override float MaximumHealth=>100;
        public override Vector3 AimPoint=>transform.position+Vector3.up*1.5f;
        float health=100,yaw,pitch,vertical,fireAt,decoyAt;
        public override void ReceiveDamage(float amount,Transform source)
        {
            if(amount>0&&!float.IsNaN(amount)&&!float.IsInfinity(amount))health=Mathf.Max(0,health-amount);
        }
        public void ResetPlayer(Vector3 position)
        {
            var body=GetComponent<CharacterController>();body.enabled=false;transform.position=position;body.enabled=true;
            health=100;vertical=0;yaw=0;pitch=0;fireAt=0;decoyAt=0;
        }
        void Start(){if(acceptInput){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}}
        void Update()
        {
            if(!acceptInput)return;
            var k=Keyboard.current;var m=Mouse.current;
            if(k==null||m==null||eye==null)return;
            if(k.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            if(m.leftButton.wasPressedThisFrame&&Cursor.lockState!=CursorLockMode.Locked)
            {Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;return;}
            if(!Alive||Cursor.lockState!=CursorLockMode.Locked)return;
            Vector2 look=m.delta.ReadValue();yaw+=look.x*.10f;pitch=Mathf.Clamp(pitch-look.y*.10f,-80,80);
            transform.rotation=Quaternion.Euler(0,yaw,0);eye.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Vector3 move=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),0,(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
            var body=GetComponent<CharacterController>();
            if(body.isGrounded){vertical=-2;if(k.spaceKey.wasPressedThisFrame)vertical=6;}
            else vertical-=18*Time.deltaTime;
            float speed=k.leftShiftKey.isPressed?7:4;
            body.Move((transform.TransformDirection(move.normalized)*speed+Vector3.up*vertical)*Mathf.Min(Time.deltaTime,.05f));
            if(m.leftButton.isPressed&&Time.time>=fireAt)
            {
                fireAt=Time.time+.25f;noise.Emit(transform.position,26,WardenNoiseKind.Gunshot);
                if(Physics.Raycast(eye.transform.position,eye.transform.forward,out var hit,50,~0,QueryTriggerInteraction.Ignore))
                {
                    var receiver=hit.collider.GetComponentInParent<DamageSink>();
                    if(receiver!=null&&receiver!=this)receiver.ReceiveDamage(15,transform);
                }
            }
            if(k.qKey.wasPressedThisFrame&&Time.time>=decoyAt)
            {
                decoyAt=Time.time+3;
                Vector3 destination=eye.transform.position+eye.transform.forward*12;
                if(Physics.Raycast(eye.transform.position,eye.transform.forward,out var hit,12,~0,QueryTriggerInteraction.Ignore))destination=hit.point-eye.transform.forward*.1f;
                noise.Emit(destination,22,WardenNoiseKind.Decoy);
            }
            if(k.fKey.wasPressedThisFrame&&objective!=null&&Vector3.Distance(transform.position,objective.transform.position)<2.5f)objective.Disturb(25);
        }
        void OnApplicationFocus(bool focus){if(!focus){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
        protected override void OnDisable(){base.OnDisable();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
