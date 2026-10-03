var player = UnityEngine.Object.FindFirstObjectByType<Skybreak.Descent.DescentCharacterController>();
var capsule = player.GetComponent<UnityEngine.CharacterController>();
var savedPosition = player.transform.position;
var savedRotation = player.transform.rotation;
var savedCameraPosition = player.viewCamera.transform.position;
var savedCameraRotation = player.viewCamera.transform.rotation;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var yawField = player.GetType().GetField("yaw", flags);
var pitchField = player.GetType().GetField("pitch", flags);
var verticalField = player.GetType().GetField("verticalSpeed", flags);
var savedYaw = (float)yawField.GetValue(player);
var savedPitch = (float)pitchField.GetValue(player);
var savedVertical = (float)verticalField.GetValue(player);
bool wasEnabled = player.enabled;
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool,string> check = (ok,name) => { report.AppendLine((ok ? "PASS " : "FAIL ") + name); if(ok) passed++; };
System.Action<UnityEngine.Vector3> place = position => {
    capsule.enabled = false; player.transform.position = position; capsule.enabled = true;
    yawField.SetValue(player,0f); pitchField.SetValue(player,10f); verticalField.SetValue(player,0f);
    player.transform.rotation = UnityEngine.Quaternion.identity; UnityEngine.Physics.SyncTransforms();
    for(int i=0;i<30;i++) player.MoveCharacter(UnityEngine.Vector2.zero,false,false,1f/60f);
};
player.enabled = false;
try
{
    place(new UnityEngine.Vector3(0,.08f,-4));
    check(player.Grounded && System.Math.Abs(player.transform.position.y) < .12f,"Ground contact at entry");
    for(int i=0;i<60;i++) player.MoveCharacter(UnityEngine.Vector2.up,false,false,1f/60f);
    float walkDistance = player.transform.position.z + 4;
    check(walkDistance > 4.3f && walkDistance < 5f,"W direction and walk speed");
    place(new UnityEngine.Vector3(0,.08f,-4));
    for(int i=0;i<60;i++) player.MoveCharacter(UnityEngine.Vector2.up,false,true,1f/60f);
    check(player.transform.position.z + 4 > walkDistance * 1.5f,"Sprint faster than walk");
    place(new UnityEngine.Vector3(0,.08f,-4));
    for(int i=0;i<180;i++) player.MoveCharacter(UnityEngine.Vector2.left,false,false,1f/60f);
    check(player.transform.position.x > -4.5f && player.transform.position.x < -4.1f,"Wall blocks character");
    place(new UnityEngine.Vector3(-5,.08f,9));
    for(int i=0;i<120;i++) player.MoveCharacter(UnityEngine.Vector2.up,false,false,1f/60f);
    check(player.transform.position.z < 10.95f && player.transform.position.z > 10.4f,"Crate blocks character");
    place(new UnityEngine.Vector3(0,.08f,0));
    float maxY = player.transform.position.y;
    for(int i=0;i<100;i++) { player.MoveCharacter(UnityEngine.Vector2.zero,i==0,false,1f/60f); maxY=UnityEngine.Mathf.Max(maxY,player.transform.position.y); }
    check(maxY > .9f && maxY < 1.4f && player.Grounded,"Jump rises and lands");
    place(new UnityEngine.Vector3(0,.08f,-8.2f)); player.UpdateCamera(1);
    check(player.viewCamera.transform.position.z > -8.7f,"Camera stays inside rear boundary");
    check(!player.visual.GetChild(0).GetComponent<UnityEngine.Renderer>().enabled,"Character hides when camera is very close");
    // Physically traverse the approved route: this also checks narrow joins and raised gates.
    place(new UnityEngine.Vector3(0,.08f,-4));
    var points = new[] { new UnityEngine.Vector2(0,21),new UnityEngine.Vector2(21,21),new UnityEngine.Vector2(21,35),new UnityEngine.Vector2(9,35),new UnityEngine.Vector2(9,46),new UnityEngine.Vector2(42,46),new UnityEngine.Vector2(42,66),new UnityEngine.Vector2(42,84) };
    bool traversed = true;
    foreach(var target in points)
    {
        int steps = 0;
        while(UnityEngine.Vector2.Distance(new UnityEngine.Vector2(player.transform.position.x,player.transform.position.z),target)>.2f && steps++<600)
        {
            var delta=target-new UnityEngine.Vector2(player.transform.position.x,player.transform.position.z);
            player.MoveCharacter(delta.normalized,false,false,1f/60f);
        }
        if(steps>=600 || player.transform.position.y < -.2f) { traversed=false; report.AppendLine("Stopped near "+target+" at "+player.transform.position); break; }
    }
    check(traversed,"Capsule can walk entire entry-to-boss route");
}
finally
{
    capsule.enabled=false; player.transform.SetPositionAndRotation(savedPosition,savedRotation); capsule.enabled=true;
    yawField.SetValue(player,savedYaw);pitchField.SetValue(player,savedPitch);verticalField.SetValue(player,savedVertical);
    UnityEngine.Physics.SyncTransforms();player.UpdateCamera(1);
    player.viewCamera.transform.SetPositionAndRotation(savedCameraPosition,savedCameraRotation);
    player.enabled=wasEnabled;
}
report.AppendLine("Checks passed: "+passed+" / 9");
System.IO.File.WriteAllText("F:/UnityProjects/Skybreak/Docs/GV/LevelDesign/Reports/Dungeon/CharacterValidation.txt",report.ToString());
return report.ToString();
