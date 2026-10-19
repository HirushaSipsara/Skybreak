using UnityEngine;

namespace Skybreak.AI.Relay
{
    public struct RelayScores { public float follow, heal, defend, retreat; }

    // Layer 3 (utility-based, Lecture 03): maps the percept to a desirability score per action and picks the
    // highest. A score of -1 marks an action that is currently impossible (cooldown, no target, no need).
    public static class RelayUtility
    {
        public static RelayScores Score(in RelayPercept percept, RelayTuning tuning, bool playerAlive, bool healReady, bool attackReady)
        {
            RelayScores s;
            s.follow = tuning.followBase + Mathf.Clamp01(percept.distanceToPlayer / 9) * tuning.followWeight;
            s.heal = playerAlive && percept.playerNeed >= tuning.healThreshold && healReady ? tuning.healBase + percept.playerNeed * tuning.healWeight : -1;
            s.defend = percept.threat != null && attackReady ? tuning.defendBase + percept.threatDanger * tuning.defendWeight : -1;
            s.retreat = percept.ownHealth01 < tuning.retreatThreshold && percept.remembersThreat ? tuning.retreatUtility : -1;
            return s;
        }

        // Highest score wins; ties go to the earlier action in the order Retreat, Heal, Defend, Follow.
        public static RelayAction Select(in RelayScores s, bool hasThreat)
        {
            if (s.retreat >= Mathf.Max(s.heal, s.defend, s.follow)) return RelayAction.Retreat;
            if (s.heal >= Mathf.Max(s.defend, s.follow)) return RelayAction.Heal;
            if (s.defend >= s.follow && hasThreat) return RelayAction.Defend;
            return RelayAction.Follow;
        }
    }
}
