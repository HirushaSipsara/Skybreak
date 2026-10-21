window.AI_SCHEMA={
  "warden": [
    {
      "key": "armHealth",
      "default": 180,
      "min": 30,
      "max": 1000,
      "step": 10,
      "label": "Arm health",
      "help": "Applied when the encounter starts or resets."
    },
    {
      "key": "coreHealth",
      "default": 260,
      "min": 30,
      "max": 1500,
      "step": 10,
      "label": "Core health",
      "help": "Only vulnerable after both arms break and the core opens."
    },
    {
      "key": "decisionInterval",
      "default": 0.2,
      "min": 0.05,
      "max": 1,
      "step": 0.05,
      "label": "Decision interval \u00b7 seconds",
      "help": "Lower values react faster and run more decision checks."
    },
    {
      "key": "perceptionInterval",
      "default": 0.1,
      "min": 0.02,
      "max": 0.5,
      "step": 0.02,
      "label": "Sight interval \u00b7 seconds",
      "help": "Caches the line-of-sight raycast between checks. Lower is more responsive."
    },
    {
      "key": "memorySeconds",
      "default": 4,
      "min": 0,
      "max": 10,
      "step": 0.5,
      "label": "Sight memory \u00b7 seconds",
      "help": "How long to investigate the last seen player position."
    },
    {
      "key": "cannonCooldown",
      "default": 4,
      "min": 0.5,
      "max": 15,
      "step": 0.5,
      "label": "Cannon cooldown \u00b7 seconds",
      "help": "Starts when the burst begins."
    },
    {
      "key": "bladeCooldown",
      "default": 3,
      "min": 0.5,
      "max": 12,
      "step": 0.5,
      "label": "Blade cooldown \u00b7 seconds",
      "help": "Starts when the slash begins."
    },
    {
      "key": "teleportCooldown",
      "default": 10,
      "min": 2,
      "max": 25,
      "step": 0.5,
      "label": "Rear teleport cooldown \u00b7 seconds",
      "help": "Starts at the beginning of the two-teleport attack."
    },
    {
      "key": "slamCooldown",
      "default": 7,
      "min": 2,
      "max": 20,
      "step": 0.5,
      "label": "Core attack cooldown \u00b7 seconds",
      "help": "Time between core attack starts."
    },
    {
      "key": "rearDistance",
      "default": 1.6,
      "min": 1.6,
      "max": 2,
      "step": 0.05,
      "label": "Rear landing distance \u00b7 metres",
      "help": "Always directly behind current player facing; collision clearance can require more distance."
    },
    {
      "key": "rearDelay",
      "default": 1,
      "min": 0.25,
      "max": 2,
      "step": 0.05,
      "label": "Time to turn \u00b7 seconds",
      "help": "Delay from first successful arrival to the second arrival and instant strike."
    },
    {
      "key": "cannonRange",
      "default": 19,
      "min": 8,
      "max": 24,
      "step": 0.5,
      "label": "Cannon range \u00b7 metres",
      "help": "A visible target inside this range can receive a cannon score."
    },
    {
      "key": "bladeRange",
      "default": 3,
      "min": 1,
      "max": 5,
      "step": 0.1,
      "label": "Blade selection range \u00b7 metres",
      "help": "The blade may be chosen below this distance. Damage radius is separate."
    },
    {
      "key": "teleportMinRange",
      "default": 4,
      "min": 2.5,
      "max": 8,
      "step": 0.1,
      "label": "Teleport minimum \u00b7 metres",
      "help": "Rear teleport is eligible above this distance."
    },
    {
      "key": "teleportMaxRange",
      "default": 17,
      "min": 9,
      "max": 23,
      "step": 0.5,
      "label": "Teleport maximum \u00b7 metres",
      "help": "Rear teleport is eligible below this distance."
    },
    {
      "key": "cannonBase",
      "default": 0.4,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Cannon base utility",
      "help": "Added to the distance contribution."
    },
    {
      "key": "cannonWeight",
      "default": 0.45,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Cannon distance weight",
      "help": "Multiplies clamped distance / 12."
    },
    {
      "key": "bladeUtility",
      "default": 1,
      "min": 0.1,
      "max": 2,
      "step": 0.05,
      "label": "Blade utility",
      "help": "Higher scores beat lower eligible attack scores."
    },
    {
      "key": "teleportUtility",
      "default": 0.95,
      "min": 0.1,
      "max": 2,
      "step": 0.05,
      "label": "Teleport utility",
      "help": "The core phase overrides these arm-attack scores."
    },
    {
      "key": "cannonDamageFraction",
      "default": 0.08,
      "min": 0.01,
      "max": 0.3,
      "step": 0.01,
      "label": "Cannon damage \u00b7 health fraction",
      "help": "Each of three shots removes this fraction of victim maximum health."
    },
    {
      "key": "meleeDamageFraction",
      "default": 0.3,
      "min": 0.05,
      "max": 0.75,
      "step": 0.05,
      "label": "Slash / slam damage fraction",
      "help": "Rear strike, normal slash and core slam use this fraction."
    },
    {
      "key": "slamRadius",
      "default": 4.2,
      "min": 2,
      "max": 6,
      "step": 0.1,
      "label": "Core slam radius \u00b7 metres",
      "help": "Move out of the warning or jump above the low strike."
    },
    {
      "key": "recoverSeconds",
      "default": 1.15,
      "min": 0.2,
      "max": 3,
      "step": 0.05,
      "label": "Recovery \u00b7 seconds",
      "help": "Pause after an attack; a window for the player to retaliate."
    }
  ],
  "relay": [
    {
      "key": "maximumHealth",
      "default": 90,
      "min": 20,
      "max": 500,
      "step": 10,
      "label": "Relay health",
      "help": "Applied at start or support reset."
    },
    {
      "key": "senseInterval",
      "default": 0.2,
      "min": 0.05,
      "max": 1,
      "step": 0.05,
      "label": "Sense interval \u00b7 seconds",
      "help": "Throttles threat scans, visibility checks and scoring together."
    },
    {
      "key": "healAmount",
      "default": 18,
      "min": 1,
      "max": 60,
      "step": 1,
      "label": "Healing per pulse",
      "help": "Health restored when in range and visible."
    },
    {
      "key": "healCooldown",
      "default": 5,
      "min": 0.5,
      "max": 20,
      "step": 0.5,
      "label": "Healing cooldown \u00b7 seconds",
      "help": "Starts only after health was actually restored."
    },
    {
      "key": "attackDamage",
      "default": 10,
      "min": 1,
      "max": 50,
      "step": 1,
      "label": "Defensive burst damage",
      "help": "Damage to a visible vulnerable hostile target."
    },
    {
      "key": "attackCooldown",
      "default": 1.8,
      "min": 0.3,
      "max": 10,
      "step": 0.1,
      "label": "Defensive cooldown \u00b7 seconds",
      "help": "Starts after a defensive burst."
    },
    {
      "key": "healThreshold",
      "default": 0.25,
      "min": 0.05,
      "max": 0.8,
      "step": 0.05,
      "label": "Missing health to heal",
      "help": "0.25 means healing becomes eligible at 75% player health."
    },
    {
      "key": "retreatThreshold",
      "default": 0.3,
      "min": 0.1,
      "max": 0.8,
      "step": 0.05,
      "label": "Retreat below own health",
      "help": "Requires a threat seen within the memory window."
    },
    {
      "key": "memorySeconds",
      "default": 3,
      "min": 0,
      "max": 10,
      "step": 0.5,
      "label": "Threat memory \u00b7 seconds",
      "help": "Only a recent visible threat activates low-health retreat."
    },
    {
      "key": "senseRange",
      "default": 16,
      "min": 6,
      "max": 25,
      "step": 0.5,
      "label": "Threat sensing range \u00b7 metres",
      "help": "Ignores hidden, dead and protected boss parts."
    },
    {
      "key": "healRange",
      "default": 4,
      "min": 1,
      "max": 8,
      "step": 0.25,
      "label": "Healing range \u00b7 metres",
      "help": "Relay approaches if healing wins but player is out of reach."
    },
    {
      "key": "attackRange",
      "default": 12,
      "min": 3,
      "max": 20,
      "step": 0.5,
      "label": "Defensive range \u00b7 metres",
      "help": "Approaches the target when farther away."
    },
    {
      "key": "followDistance",
      "default": 3.3,
      "min": 1.5,
      "max": 8,
      "step": 0.1,
      "label": "Follow threshold \u00b7 metres",
      "help": "Approach the player-side formation spot beyond this distance."
    },
    {
      "key": "followBase",
      "default": 0.15,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Follow base utility",
      "help": "Follow score adds clamped player distance / 9 times follow weight."
    },
    {
      "key": "followWeight",
      "default": 0.5,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Follow distance weight",
      "help": "More weight makes staying close more important."
    },
    {
      "key": "healBase",
      "default": 0.45,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Heal base utility",
      "help": "Added when health need and cooldown conditions pass."
    },
    {
      "key": "healWeight",
      "default": 0.55,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Heal need weight",
      "help": "Multiplies the fraction of missing player health."
    },
    {
      "key": "defendBase",
      "default": 0.35,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Defend base utility",
      "help": "Added when a visible target and ready burst are available."
    },
    {
      "key": "defendWeight",
      "default": 0.35,
      "min": 0,
      "max": 1,
      "step": 0.05,
      "label": "Defend danger weight",
      "help": "Danger increases as the hostile approaches the player."
    },
    {
      "key": "retreatUtility",
      "default": 1,
      "min": 0.1,
      "max": 3,
      "step": 0.05,
      "label": "Retreat utility",
      "help": "Raise to prioritise survival; lowering may let healing win."
    }
  ],
  "movement": [
    {
      "key": "replanInterval",
      "default": 0.4,
      "min": 0.1,
      "max": 2,
      "step": 0.1,
      "label": "Path replan interval \u00b7 seconds",
      "help": "Minimum interval for similar destinations while moving. Applies to both agents."
    },
    {
      "key": "stuckSeconds",
      "default": 0.8,
      "min": 0.2,
      "max": 3,
      "step": 0.1,
      "label": "Stuck detection \u00b7 seconds",
      "help": "Requests a new route when commanded movement stalls."
    }
  ]
}
;
window.AI_DEFAULTS={
  "schemaVersion": 1,
  "warden": {
    "armHealth": 180,
    "coreHealth": 260,
    "decisionInterval": 0.2,
    "perceptionInterval": 0.1,
    "memorySeconds": 4,
    "cannonCooldown": 4,
    "bladeCooldown": 3,
    "teleportCooldown": 10,
    "slamCooldown": 7,
    "rearDistance": 1.6,
    "rearDelay": 1,
    "cannonRange": 19,
    "bladeRange": 3,
    "teleportMinRange": 4,
    "teleportMaxRange": 17,
    "cannonBase": 0.4,
    "cannonWeight": 0.45,
    "bladeUtility": 1,
    "teleportUtility": 0.95,
    "cannonDamageFraction": 0.08,
    "meleeDamageFraction": 0.3,
    "slamRadius": 4.2,
    "recoverSeconds": 1.15
  },
  "relay": {
    "maximumHealth": 90,
    "senseInterval": 0.2,
    "healAmount": 18,
    "healCooldown": 5,
    "attackDamage": 10,
    "attackCooldown": 1.8,
    "healThreshold": 0.25,
    "retreatThreshold": 0.3,
    "memorySeconds": 3,
    "senseRange": 16,
    "healRange": 4,
    "attackRange": 12,
    "followDistance": 3.3,
    "followBase": 0.15,
    "followWeight": 0.5,
    "healBase": 0.45,
    "healWeight": 0.55,
    "defendBase": 0.35,
    "defendWeight": 0.35,
    "retreatUtility": 1
  },
  "movement": {
    "replanInterval": 0.4,
    "stuckSeconds": 0.8
  }
}
;
