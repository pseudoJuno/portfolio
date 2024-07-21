using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;

namespace CrawlerEngine
{
    public class CETime
    {
        public CETime()
        {
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CETime This() { return script.time; }

            public float FrameDeltaTime()
            {
                return Time.deltaTime;
            }
            public float CombatDeltaTime()
            {
                return script.main.movement.statusTimeScale * Time.deltaTime;
            }
            public float PhysicsDeltaTime()
            {
                return Time.fixedDeltaTime;
            }
            public float StatusTimeScale()
            {
                return script.main.movement.statusTimeScale;
            }
            public float StatusTimeIncrements()
            {
                return (script.player.exposed.Attacked() ? 1f : 0f);
            }
            public float MovementTimeScale()
            {
                return script.main.movement.movementTimeScale;
            }
        }
    }
}
