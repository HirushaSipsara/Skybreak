using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.Warden
{
    public sealed class WardenPresentation : MonoBehaviour
    {
        public WardenBrain brain;
        public Transform visual;
        public Renderer core;
        public LineRenderer beam;
        [Min(0)] public float breakupDelay=.4f;
        [Min(1)] public float debrisLifetime=4;
        MaterialPropertyBlock properties;
        float beamOff;
        float defeatAt=-1,shatteredAt;
        bool shattered;
        GameObject debris;
        Renderer[] parts;
        readonly List<Renderer> hiddenParts=new List<Renderer>();
        readonly List<Mesh> temporaryMeshes=new List<Mesh>();
        readonly List<Transform> fragments=new List<Transform>();
        Light burstLight;
        void Start()
        {
            properties=new MaterialPropertyBlock();
            if(visual!=null)parts=visual.GetComponentsInChildren<Renderer>(true);
            if(brain!=null)brain.Fired+=Shot;
        }
        void Shot(Vector3 from,Vector3 to)
        {
            if(beam==null)return;
            beam.SetPosition(0,from);beam.SetPosition(1,to);beam.enabled=true;beamOff=Time.time+.12f;
        }
        void Update()
        {
            if(brain==null)return;
            // Skeletal clips own the pose until defeat breaks the visible model into debris.
            if(core!=null)
            {
                Color color=!brain.Alive?Color.gray:brain.WindingUp?new Color(1,.35f,.03f):new Color(.02f,.8f,1);
                core.GetPropertyBlock(properties);properties.SetColor("_BaseColor",color);properties.SetColor("_EmissionColor",color*(brain.WindingUp?3:.8f));core.SetPropertyBlock(properties);
            }
            if(beam!=null&&Time.time>=beamOff)beam.enabled=false;
        }
        void LateUpdate()
        {
            if(brain==null||visual==null)return;
            if(brain.Alive)
            {
                if(defeatAt>=0)ResetDefeatFx();
                return;
            }
            if(beam!=null)beam.enabled=false;
            if(defeatAt<0)defeatAt=Time.time;
            if(!shattered&&Time.time-defeatAt>=breakupDelay)BreakApart();
            if(!shattered||debris==null)return;
            float elapsed=Time.time-shatteredAt;
            if(burstLight!=null)burstLight.intensity=7*Mathf.Clamp01(1-elapsed/.25f);
            float size=Mathf.Clamp01((debrisLifetime-elapsed)/.7f);
            foreach(var piece in fragments)if(piece!=null)piece.localScale=Vector3.one*size;
            if(elapsed>=debrisLifetime)ClearDebris();
        }
        void BreakApart()
        {
            shattered=true;shatteredAt=Time.time;
            debris=new GameObject("Crystal Echo Warden broken pieces (runtime)"){hideFlags=HideFlags.DontSave};
            var actorColliders=new List<Collider>();
            foreach(var actor in Skybreak.Combat.DamageSink.Active)
                if(actor!=null)actorColliders.AddRange(actor.GetComponentsInChildren<Collider>());
            var pieceColliders=new List<Collider>();
            Vector3 origin=brain.AimPoint;
            foreach(var source in parts)
            {
                if(source==null||!source.enabled)continue;
                Mesh mesh;Matrix4x4 toWorld;
                if(source is SkinnedMeshRenderer skin)
                {
                    mesh=new Mesh();skin.BakeMesh(mesh,false);
                    // BakeMesh(false) already includes this imported rig's scale.
                    toWorld=Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one);
                }
                else
                {
                    var filter=source.GetComponent<MeshFilter>();
                    if(filter==null||filter.sharedMesh==null)continue;
                    mesh=Instantiate(filter.sharedMesh);toWorld=source.localToWorldMatrix;
                }
                temporaryMeshes.Add(mesh);
                var vertices=mesh.vertices;
                if(vertices.Length==0)continue;
                var bounds=new Bounds(toWorld.MultiplyPoint3x4(vertices[0]),Vector3.zero);
                for(int i=0;i<vertices.Length;i++){vertices[i]=toWorld.MultiplyPoint3x4(vertices[i]);bounds.Encapsulate(vertices[i]);}
                for(int i=0;i<vertices.Length;i++)vertices[i]-=bounds.center;
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
                var piece=new GameObject(source.name+" fragment"){layer=8};
                piece.transform.SetParent(debris.transform,false);piece.transform.position=bounds.center;
                piece.AddComponent<MeshFilter>().sharedMesh=mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
                var collider=piece.AddComponent<BoxCollider>();
                collider.size=Vector3.Max(mesh.bounds.size*.85f,Vector3.one*.025f);
                // Debris hits the dungeon, but cannot block players, agents or other debris.
                foreach(var actor in actorColliders)if(actor!=null)Physics.IgnoreCollision(collider,actor);
                foreach(var other in pieceColliders)Physics.IgnoreCollision(collider,other);
                pieceColliders.Add(collider);
                var body=piece.AddComponent<Rigidbody>();body.mass=.12f;body.linearDamping=.45f;body.angularDamping=.6f;
                Vector3 direction=bounds.center-origin;direction.y=0;
                if(direction.sqrMagnitude<.01f)direction=Random.insideUnitSphere;
                body.AddForce(direction.normalized*Random.Range(1.5f,3.2f)+Vector3.up*Random.Range(1.4f,3),ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere*7,ForceMode.VelocityChange);
                fragments.Add(piece.transform);source.enabled=false;hiddenParts.Add(source);
            }
            var flash=new GameObject("Cyan core flash");flash.transform.SetParent(debris.transform,false);flash.transform.position=origin;
            burstLight=flash.AddComponent<Light>();burstLight.type=LightType.Point;burstLight.color=new Color(.15f,.85f,1);burstLight.range=5;burstLight.intensity=7;burstLight.shadows=LightShadows.None;
            if(core!=null)
            {
                var sparks=new GameObject("Core crystal splinters").AddComponent<ParticleSystem>();
                sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                sparks.transform.SetParent(debris.transform,false);sparks.transform.position=origin;
                var main=sparks.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.maxParticles=24;main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(2,5);main.startSize=new ParticleSystem.MinMaxCurve(.04f,.1f);main.gravityModifier=.7f;
                var emission=sparks.emission;emission.enabled=false;
                var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.15f;
                var shrink=sparks.sizeOverLifetime;shrink.enabled=true;shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
                var renderer=sparks.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.sharedMaterial=core.sharedMaterial;
                var shard=new Mesh();shard.vertices=new[]{Vector3.up,Vector3.right,Vector3.forward,Vector3.left,Vector3.back,Vector3.down};
                shard.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};shard.RecalculateNormals();temporaryMeshes.Add(shard);renderer.mesh=shard;
                sparks.Play();sparks.Emit(24);
            }
        }
        void ClearDebris()
        {
            if(debris!=null){debris.SetActive(false);Destroy(debris);debris=null;}
            foreach(var mesh in temporaryMeshes)if(mesh!=null)Destroy(mesh);
            temporaryMeshes.Clear();fragments.Clear();burstLight=null;
        }
        public void ResetDefeatFx()
        {
            ClearDebris();foreach(var part in hiddenParts)if(part!=null)part.enabled=true;
            hiddenParts.Clear();defeatAt=-1;shattered=false;
            if(beam!=null)beam.enabled=false;
        }
        void OnDisable(){ResetDefeatFx();}
        void OnDestroy(){if(brain!=null)brain.Fired-=Shot;ClearDebris();}
    }
}
