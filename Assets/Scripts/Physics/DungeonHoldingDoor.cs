using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Skybreak.PhysicsInteraction
{
    /// <summary>Animated portcullis. Encounter logic calls Open once agents should be released.</summary>
    public sealed class DungeonHoldingDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform gate;
        [SerializeField] BoxCollider gateCollider;
        [SerializeField] Transform interactionTarget;
        [SerializeField] bool startOpen;
        [SerializeField, Min(.1f)] float openingTime=1.4f;
        [SerializeField, Min(0)] float liftHeight=3.12f;
        [SerializeField] bool allowManualPreview=true;
        [SerializeField, Min(.1f)] float interactionRange=3.8f;
        [SerializeField] UnityEvent fullyOpened=new UnityEvent();
        [SerializeField] UnityEvent fullyClosed=new UnityEvent();
        Vector3 closedPosition;
        float amount;
        bool wantsOpen;
        bool hasCompleted;

        public bool IsOpen=>amount>=.999f;
        public bool IsClosed=>amount<=.001f;
        public bool IsMoving=>amount!=(wantsOpen?1f:0f);
        public UnityEvent FullyOpened=>fullyOpened;
        public UnityEvent FullyClosed=>fullyClosed;

        public string Prompt => "F  /  " + (wantsOpen ? "Close holding gate" : "Open holding gate");
        public bool CanInteract(GameObject actor) => allowManualPreview && !(wantsOpen && ThresholdOccupied());
        public void Interact(GameObject actor) => ManualToggle();
        // The gate's own F check and PlayerInteractor both react to the same key press; toggle once per frame so they don't cancel out.
        int manualToggleFrame=-1;
        void ManualToggle(){if(manualToggleFrame==Time.frameCount)return;manualToggleFrame=Time.frameCount;Toggle();}

        void Awake(){
            if(gate==null||gateCollider==null){Debug.LogError("Assign a gate and gate collider.",this);enabled=false;return;}
            closedPosition=gate.localPosition;amount=startOpen?1:0;wantsOpen=startOpen;ApplyPosition();hasCompleted=true;
        }
        void Update(){
            if(allowManualPreview&&interactionTarget!=null&&Keyboard.current!=null&&Keyboard.current.fKey.wasPressedThisFrame){
                var delta=interactionTarget.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude<=interactionRange*interactionRange)ManualToggle();
            }
            // If a player or physical agent occupies the threshold, reopen instead of crushing it.
            if(!wantsOpen&&amount>0&&ThresholdOccupied()){wantsOpen=true;hasCompleted=false;}
            amount=Mathf.MoveTowards(amount,wantsOpen?1:0,Time.deltaTime/openingTime);ApplyPosition();
            if(!hasCompleted&&!IsMoving){hasCompleted=true;if(wantsOpen)fullyOpened.Invoke();else fullyClosed.Invoke();}
        }
        void ApplyPosition(){gate.localPosition=closedPosition+Vector3.up*(liftHeight*Mathf.SmoothStep(0,1,amount));}
        bool ThresholdOccupied(){
            var centre=transform.TransformPoint(new Vector3(0,1.5f,0));
            foreach(var collider in UnityEngine.Physics.OverlapBox(centre,new Vector3(1.39f,1.42f,.65f),transform.rotation,~0,QueryTriggerInteraction.Ignore)){
                if(collider.transform.IsChildOf(transform))continue;
                if(collider is CharacterController||collider.attachedRigidbody!=null||collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>()!=null)return true;
            }
            return false;
        }
        public void Open()=>SetOpen(true);
        public void Close()=>SetOpen(false);
        public void Toggle()=>SetOpen(!wantsOpen);
        public void SetOpen(bool open){
            if(!enabled||gate==null)return;
            if(!open&&Application.isPlaying&&ThresholdOccupied())return;
            if(wantsOpen==open)return;wantsOpen=open;hasCompleted=false;
        }
        void OnGUI(){
            if(!allowManualPreview||interactionTarget==null)return;var delta=interactionTarget.position-transform.position;delta.y=0;
            if(delta.sqrMagnitude>interactionRange*interactionRange)return;
            var text="F  /  "+(wantsOpen?"Close holding gate":"Open holding gate");
            GUI.Box(new Rect(Screen.width*.5f-145,Screen.height-110,290,32),text);
        }
    }
}
