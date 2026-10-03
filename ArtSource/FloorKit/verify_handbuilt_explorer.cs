var p = UnityEngine.Object.FindFirstObjectByType<Skybreak.Descent.DescentCharacterController>();
if (!UnityEditor.EditorApplication.isPlaying || p == null) throw new System.InvalidOperationException("Explorer checks require Play Mode.");
var capsule = p.GetComponent<UnityEngine.CharacterController>();
System.Action<UnityEngine.Vector3> place = position => { capsule.enabled=false; p.transform.position=position; capsule.enabled=true; for(int i=0;i<20;i++)p.MoveCharacter(UnityEngine.Vector2.zero,false,false,1f/60); };
p.enabled=false;
try
{
    p.ReturnToStart();
    for(int i=0;i<20;i++)p.MoveCharacter(UnityEngine.Vector2.zero,false,false,1f/60);
    bool grounded=p.Grounded; var start=p.transform.position;
    for(int i=0;i<60;i++)p.MoveCharacter(UnityEngine.Vector2.up,false,false,1f/60);
    float walked=p.transform.position.z-start.z;
    float beforeX=p.transform.position.x;
    for(int i=0;i<30;i++)p.MoveCharacter(UnityEngine.Vector2.right,false,false,1f/60);
    float strafed=p.transform.position.x-beforeX;
    for(int i=0;i<200;i++)p.MoveCharacter(UnityEngine.Vector2.left,false,false,1f/60);
    float wallX=p.transform.position.x;
    float floorY=p.transform.position.y,peak=0;
    p.MoveCharacter(UnityEngine.Vector2.zero,true,false,1f/60);
    for(int i=0;i<120;i++){p.MoveCharacter(UnityEngine.Vector2.zero,false,false,1f/60);peak=UnityEngine.Mathf.Max(peak,p.transform.position.y-floorY);}
    bool landed=p.Grounded;
    place(new UnityEngine.Vector3(20,.12f,54));
    for(int i=0;i<80;i++)p.MoveCharacter(UnityEngine.Vector2.right,false,false,1f/60);
    bool westDoor=p.transform.position.x>25.5f;
    place(new UnityEngine.Vector3(59,.12f,46));
    for(int i=0;i<110;i++)p.MoveCharacter(UnityEngine.Vector2.right,false,false,1f/60);
    bool eastDoor=p.transform.position.x>66;
    place(new UnityEngine.Vector3(0,.12f,-4));
    var route=new[]{new UnityEngine.Vector2(0,14),new UnityEngine.Vector2(0,21),new UnityEngine.Vector2(8,21),new UnityEngine.Vector2(21,21),new UnityEngine.Vector2(21,35),new UnityEngine.Vector2(9,35),new UnityEngine.Vector2(9,46),new UnityEngine.Vector2(37,46),new UnityEngine.Vector2(42,46),new UnityEngine.Vector2(42,57),new UnityEngine.Vector2(42,66),new UnityEngine.Vector2(42,84)};
    int reached=0;
    foreach(var goal in route)
    {
        int steps=0;
        while(steps++<250){var difference=goal-new UnityEngine.Vector2(p.transform.position.x,p.transform.position.z);if(difference.magnitude<.08f){reached++;break;}p.MoveCharacter(difference.normalized,false,false,UnityEngine.Mathf.Min(.04f,difference.magnitude/p.walkSpeed));}
    }
    place(new UnityEngine.Vector3(0,.12f,-8.5f));p.UpdateCamera(1);
    bool cameraClear=p.viewCamera.transform.position.z> -8.95f;
    var report=new[]{"Grounded at spawn: "+grounded,"W forward distance: "+walked+"m (expected 4.7m)","D right distance: "+strafed+"m (expected 2.35m)","Blocked by west wall at X: "+wallX,"Jump peak: "+peak+"m / landed: "+landed,"West doorway traversed: "+westDoor,"East doorway traversed: "+eastDoor,"Route waypoints reached using real CharacterController: "+reached+" / "+route.Length,"Camera stayed inside rear wall: "+cameraClear,"Checks use the controller's movement API in Play Mode; no synthetic keyboard or mouse events injected."};
    System.IO.File.WriteAllLines("Docs/GV/LevelDesign/Reports/StoneKit/ExplorerPlayCheck.txt",report);
    if(!grounded||walked<4.5f||strafed<2.2f||wallX< -4.8f||wallX> -4.4f||peak<.8f||!landed||!westDoor||!eastDoor||reached!=route.Length||!cameraClear)throw new System.InvalidOperationException(string.Join("\n",report));
    return string.Join("\n",report);
}
finally {p.ReturnToStart();p.enabled=true;p.UpdateCamera(1);}
