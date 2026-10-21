"""Generate shared tuning metadata/C# validation, and integrate current agent parameters once."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
GROUPS = {
 'warden': [
 ('armHealth',180,30,1000,10,'Arm health','Applied when the encounter starts or resets.'),
 ('coreHealth',260,30,1500,10,'Core health','Only vulnerable after both arms break and the core opens.'),
 ('decisionInterval',.2,.05,1,.05,'Decision interval · seconds','Lower values react faster and run more decision checks.'),
 ('perceptionInterval',.1,.02,.5,.02,'Sight interval · seconds','Caches the line-of-sight raycast between checks. Lower is more responsive.'),
 ('memorySeconds',4,0,10,.5,'Sight memory · seconds','How long to investigate the last seen player position.'),
 ('cannonCooldown',4,.5,15,.5,'Cannon cooldown · seconds','Starts when the burst begins.'),
 ('bladeCooldown',3,.5,12,.5,'Blade cooldown · seconds','Starts when the slash begins.'),
 ('teleportCooldown',10,2,25,.5,'Rear teleport cooldown · seconds','Starts at the beginning of the two-teleport attack.'),
 ('slamCooldown',7,2,20,.5,'Core attack cooldown · seconds','Time between core attack starts.'),
 ('rearDistance',1.6,1.6,2, .05,'Rear landing distance · metres','Always directly behind current player facing; collision clearance can require more distance.'),
 ('rearDelay',1,.25,2,.05,'Time to turn · seconds','Delay from first successful arrival to the second arrival and instant strike.'),
 ('cannonRange',19,8,24,.5,'Cannon range · metres','A visible target inside this range can receive a cannon score.'),
 ('bladeRange',3,1,5,.1,'Blade selection range · metres','The blade may be chosen below this distance. Damage radius is separate.'),
 ('teleportMinRange',4,2.5,8,.1,'Teleport minimum · metres','Rear teleport is eligible above this distance.'),
 ('teleportMaxRange',17,9,23,.5,'Teleport maximum · metres','Rear teleport is eligible below this distance.'),
 ('cannonBase',.4,0,1,.05,'Cannon base utility','Added to the distance contribution.'),
 ('cannonWeight',.45,0,1,.05,'Cannon distance weight','Multiplies clamped distance / 12.'),
 ('bladeUtility',1,.1,2,.05,'Blade utility','Higher scores beat lower eligible attack scores.'),
 ('teleportUtility',.95,.1,2,.05,'Teleport utility','The core phase overrides these arm-attack scores.'),
 ('cannonDamageFraction',.08,.01,.3,.01,'Cannon damage · health fraction','Each of three shots removes this fraction of victim maximum health.'),
 ('meleeDamageFraction',.3,.05,.75,.05,'Slash / slam damage fraction','Rear strike, normal slash and core slam use this fraction.'),
 ('slamRadius',4.2,2,6,.1,'Core slam radius · metres','Move out of the warning or jump above the low strike.'),
 ('recoverSeconds',1.15,.2,3,.05,'Recovery · seconds','Pause after an attack; a window for the player to retaliate.')],
 'relay': [
 ('maximumHealth',90,20,500,10,'Relay health','Applied at start or support reset.'),
 ('senseInterval',.2,.05,1,.05,'Sense interval · seconds','Throttles threat scans, visibility checks and scoring together.'),
 ('healAmount',18,1,60,1,'Healing per pulse','Health restored when in range and visible.'),
 ('healCooldown',5,.5,20,.5,'Healing cooldown · seconds','Starts only after health was actually restored.'),
 ('attackDamage',10,1,50,1,'Defensive burst damage','Damage to a visible vulnerable hostile target.'),
 ('attackCooldown',1.8,.3,10,.1,'Defensive cooldown · seconds','Starts after a defensive burst.'),
 ('healThreshold',.25,.05,.8,.05,'Missing health to heal','0.25 means healing becomes eligible at 75% player health.'),
 ('retreatThreshold',.3,.1,.8,.05,'Retreat below own health','Requires a threat seen within the memory window.'),
 ('memorySeconds',3,0,10,.5,'Threat memory · seconds','Only a recent visible threat activates low-health retreat.'),
 ('senseRange',16,6,25,.5,'Threat sensing range · metres','Ignores hidden, dead and protected boss parts.'),
 ('healRange',4,1,8,.25,'Healing range · metres','Relay approaches if healing wins but player is out of reach.'),
 ('attackRange',12,3,20,.5,'Defensive range · metres','Approaches the target when farther away.'),
 ('followDistance',3.3,1.5,8,.1,'Follow threshold · metres','Approach the player-side formation spot beyond this distance.'),
 ('followBase',.15,0,1,.05,'Follow base utility','Follow score adds clamped player distance / 9 times follow weight.'),
 ('followWeight',.5,0,1,.05,'Follow distance weight','More weight makes staying close more important.'),
 ('healBase',.45,0,1,.05,'Heal base utility','Added when health need and cooldown conditions pass.'),
 ('healWeight',.55,0,1,.05,'Heal need weight','Multiplies the fraction of missing player health.'),
 ('defendBase',.35,0,1,.05,'Defend base utility','Added when a visible target and ready burst are available.'),
 ('defendWeight',.35,0,1,.05,'Defend danger weight','Danger increases as the hostile approaches the player.'),
 ('retreatUtility',1,.1,3,.05,'Retreat utility','Raise to prioritise survival; lowering may let healing win.')],
 'movement': [
 ('replanInterval',.4,.1,2,.1,'Path replan interval · seconds','Minimum interval for similar destinations while moving. Applies to both agents.'),
 ('stuckSeconds',.8,.2,3,.1,'Stuck detection · seconds','Requests a new route when commanded movement stalls.')]
}
schema = {g:[dict(zip(('key','default','min','max','step','label','help'), row)) for row in rows] for g,rows in GROUPS.items()}
defaults = {'schemaVersion':1, **{g:{f['key']:f['default'] for f in fields} for g,fields in schema.items()}}
(HERE/'schema.json').write_text(json.dumps(schema,indent=2)+'\n')
(HERE/'defaults.json').write_text(json.dumps(defaults,indent=2)+'\n')
(HERE/'data.js').write_text('window.AI_SCHEMA='+json.dumps(schema)+';\nwindow.AI_DEFAULTS='+json.dumps(defaults)+';\n')
config=ROOT/'Assets/Resources/AI/AgentTuning.json'
if not config.exists(): config.write_text(json.dumps(defaults,indent=2)+'\n')
code='''// Parameter metadata is generated by Tools/AIWorkbench/build_tuning.py.
using System;
using UnityEngine;
namespace Skybreak.AI {
 [Serializable] public sealed class AgentTuningData {
  public int schemaVersion = 1;
'''
for g in schema: code+=f'  public {g.title()}Tuning {g} = new {g.title()}Tuning();\n'
code+='  public bool Valid() => schemaVersion == 1 && '+ ' && '.join(f'{g} != null && {g}.Valid()' for g in schema)+';\n }\n'
for g,fields in schema.items():
 code+=f' [Serializable] public sealed class {g.title()}Tuning {{\n'
 for f in fields: code+=f'  public float {f["key"]} = {float(f["default"])}f;\n'
 code+='  public bool Valid() => '+ ' &&\n   '.join(f'!float.IsNaN({f["key"]}) && {f["key"]} >= {float(f["min"])}f && {f["key"]} <= {float(f["max"])}f' for f in fields)+';\n }\n'
code+='}\n'
(ROOT/'Assets/Scripts/AI/Shared/AgentTuningData.cs').write_text(code)

def patch(path, replacements):
 p=ROOT/path; old=p.read_text(); backup=HERE/'baseline'/p.name
 backup.parent.mkdir(exist_ok=True)
 if not backup.exists(): backup.write_text(old)
 if 'AgentTuning.Current' in old: return
 for a,b in replacements:
  if a not in old: raise RuntimeError('Patch anchor missing: '+a[:65])
  old=old.replace(a,b)
 p.write_text(old)

patch('Assets/Scripts/AI/RiftWarden/RiftWardenBrain.cs', [
 ('float clock, previous,', 'WardenTuning T => AgentTuning.Current.warden;\n        float nextSight; bool cachedVisible;\n        void ApplyHealthTuning() { armMaximum = T.armHealth; coreMaximum = T.coreHealth; }\n        float clock, previous,'),
 ('LeftHealth = armMaximum; RightHealth = armMaximum; CoreHealth = coreMaximum;', 'ApplyHealthTuning(); LeftHealth = armMaximum; RightHealth = armMaximum; CoreHealth = coreMaximum;'),
 ('bool visible = !Physics.Linecast(transform.position + Vector3.up * 2.6f, player.position + Vector3.up * 1.2f, ~(1 << 8), QueryTriggerInteraction.Ignore);', 'if (Time.time >= nextSight) { nextSight = Time.time + T.perceptionInterval; cachedVisible = !Physics.Linecast(transform.position + Vector3.up * 2.6f, player.position + Vector3.up * 1.2f, ~(1 << 8), QueryTriggerInteraction.Ignore); }\n            bool visible = cachedVisible;'),
 ('nextDecision = Time.time + .2f','nextDecision = Time.time + T.decisionInterval'),
 ('secondRearAt = clock + 1;', 'secondRearAt = clock + T.rearDelay;'),
 ('(clock - (secondRearAt - 1)) * .6f','(clock - (secondRearAt - T.rearDelay)) / T.rearDelay * .6f'),
 ('clock >= 1.15f','clock >= T.recoverSeconds'),
 ('4.2f','T.slamRadius'),
 ('Time.time - seenAt < 4','Time.time - seenAt < T.memorySeconds'),
 ('range < 19 ? .4f + Mathf.Clamp01(range / 12) * .45f','range < T.cannonRange ? T.cannonBase + Mathf.Clamp01(range / 12) * T.cannonWeight'),
 ('range < 3 ? 1 : -1','range < T.bladeRange ? T.bladeUtility : -1'),
 ('range > 4 && range < 17 ? .95f','range > T.teleportMinRange && range < T.teleportMaxRange ? T.teleportUtility'),
 ('cannonReady = Time.time + 4','cannonReady = Time.time + T.cannonCooldown'),
 ('bladeReady = Time.time + 3','bladeReady = Time.time + T.bladeCooldown'),
 ('teleportReady = Time.time + 10; secondRearAt = 1.18f','teleportReady = Time.time + T.teleportCooldown; secondRearAt = .18f + T.rearDelay'),
 ('slamReady = Time.time + 7','slamReady = Time.time + T.slamCooldown'),
 ('target.MaximumHealth * .08f','target.MaximumHealth * T.cannonDamageFraction'),
 ('radius, .3f, true','radius, T.meleeDamageFraction, true'),
 ('T.slamRadius, .3f, false','T.slamRadius, T.meleeDamageFraction, false'),
 ('Mathf.Max(1.6f, motor.Agent.radius','Mathf.Max(T.rearDistance, motor.Agent.radius'),
 ('Action = RiftAction.Dormant; clock = 0;','Action = RiftAction.Dormant; clock = 0; nextSight = 0; seenAt = -100;'),
 ('Close rear arrival / one second to turn','Close rear arrival / turn before second arrival')])
patch('Assets/Scripts/AI/Relay/RelayBrain.cs',[
 ('float health, nextSense,','RelayTuning T => AgentTuning.Current.relay;\n        void ApplyTuning(bool resetHealth) { if (resetHealth) maximumHealth=T.maximumHealth; healAmount=T.healAmount; healCooldown=T.healCooldown; attackCooldown=T.attackCooldown; }\n        float health, nextSense,'),
 ('health = maximumHealth;', 'ApplyTuning(true); health = maximumHealth;'),
 ('nextSense = Time.time + .2f; SenseAndDecide();','nextSense = Time.time + T.senseInterval; SenseAndDecide();'),
 ('threat = null; float bestThreat = -1;', 'ApplyTuning(false); threat = null; float bestThreat = -1;'),
 ('range > 16','range > T.senseRange'),
 ('Time.time - lastSeenAt < 3','Time.time - lastSeenAt < T.memorySeconds'),
 ('FollowScore = .15f + Mathf.Clamp01(distance / 9) * .5f','FollowScore = T.followBase + Mathf.Clamp01(distance / 9) * T.followWeight'),
 ('need >= .25f','need >= T.healThreshold'),
 ('? .45f + need * .55f','? T.healBase + need * T.healWeight'),
 ('? .35f + bestThreat * .35f','? T.defendBase + bestThreat * T.defendWeight'),
 ('health / maximumHealth < .3f && remembersThreat ? 1','health / maximumHealth < T.retreatThreshold && remembersThreat ? T.retreatUtility'),
 ('Self health below 30% + recent threat','Own health below retreat threshold + recent threat'),
 ('Player missing at least 25% health / healing available','Player health need exceeds threshold / healing available'),
 ('distance <= 4','distance <= T.healRange'),
 ('threat.AimPoint) <= 12','threat.AimPoint) <= T.attackRange'),
 ('threat.ReceiveDamage(10, transform)','threat.ReceiveDamage(T.attackDamage, transform)'),
 ('distance > 3.3f','distance > T.followDistance')])
patch('Assets/Scripts/AgentMovement/NavMeshMotor.cs',[
 ('nextPlan = Time.time + .4f','nextPlan = Time.time + Skybreak.AI.AgentTuning.Current.movement.replanInterval'),
 ('stalled > .8f','stalled > Skybreak.AI.AgentTuning.Current.movement.stuckSeconds')])
print('Generated schema, defaults and validated tuning classes; integrated 3 scripts.')
