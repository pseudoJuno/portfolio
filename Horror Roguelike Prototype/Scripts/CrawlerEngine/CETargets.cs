using MoonSharp.Interpreter;
using Pathfinding;
using SubstanceLevel;
using System.Collections.Generic;
using UnityEngine;

namespace CrawlerEngine
{
    public class CETargets : MonoBehaviour
    {
        CEMain main;
        Table targets;
        LayerMask mask;
        public Seeker reachabilitySeeker;

        //public static float aggroDistance = 7f;

        public bool playerReachable = true;
        float reachabilityPathTimer = 0f;

        bool useTargeting = true;

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;
            targets = new Table(main.script.script);
            mask = 1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                1 << LayerMask.NameToLayer("Default") |
                1 << LayerMask.NameToLayer("immovableObject") |
                1 << LayerMask.NameToLayer("object") |
                1 << LayerMask.NameToLayer("door") |
                1 << LayerMask.NameToLayer("VisionBlocker");

            reachabilityPathDelegate = OnReachabilityPathCompleted;
            reachabilitySeeker.pathCallback = reachabilityPathDelegate;
        }

        // Update is called once per frame
        bool seesPlayer = false;
        bool seenByPlayer = false;
        void Update()
        {
            targets.Clear();

            PlayerScript p = LevelManager.objs.localPlayer;
            seesPlayer = (LinecastTarget(p.transform.position + Vector3.up * 0.1f) || LinecastTarget(p.playerCam.transform.position));
            seenByPlayer = (seesPlayer ||
                (!Physics.Linecast(p.playerCam.transform.position, transform.position + Vector3.up * 0.1f, mask) &&
                Mathf.Abs(Vector3.Angle(transform.position + Vector3.up * 0.1f - p.playerCam.transform.position, p.playerCam.transform.forward)) < 45f));

            if (useTargeting)
            {
                if (seesPlayer) targets.Append(DynValue.NewNumber(main.script.player.exposed.AsTarget()));
                foreach (CEScript c in main.script.crawlerManager.crawlers)
                    if (c != main.script &&
                        c.data.alive &&
                        c.data.visible &&
                        c.data.state != CEData.Exposed.States.ABSTRACTED &&
                        c.data.parent == null &&
                        LinecastTarget(c.main.transform.position + Vector3.up * 0.1f))
                    {
                        targets.Append(DynValue.NewNumber(c.main.ID));
                    }
            }
        }
        Pathfinding.OnPathDelegate reachabilityPathDelegate;
        public void OnReachabilityPathCompleted(Pathfinding.Path p)
        {
            playerReachable = !p.error && p.GetTotalLength() < 20f;
        }

        public bool LinecastTarget(Vector3 pos2)
        {
            Vector3 pos1 = ClampPos(main.setup.headBone.TransformPoint(main.setup.headOffset));
            return Vector3.Distance(pos1, pos2) <= main.movement.MovementType().aggroDistance && !Physics.Linecast(pos1, pos2, mask);
        }
        Vector3 ClampPos(Vector3 pos)
        {
            Rect roomRect = main.script.data.room.realRect;
            return new Vector3(
                Mathf.Clamp(pos.x, roomRect.xMin, roomRect.xMax),
                Mathf.Max(0.2f, pos.y),
                Mathf.Clamp(pos.z, roomRect.yMin, roomRect.yMax));
        }

        public bool SeesPlayer()
        {
            return seesPlayer;
        }
        public bool SeenByPlayer()
        {
            return seenByPlayer;
        }

        //copy tables into another script
        public DynValue[] CopyArgs(DynValue[] args, Script s)
        {
            DynValue[] argsCopy = new DynValue[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Type == DataType.Table)
                    argsCopy[i] = CopyTable(args[i].Table, s);
                else
                    argsCopy[i] = args[i];
            }
            return argsCopy;
        }
        public DynValue CopyTable(Table table, Script s)
        {
            DynValue t = DynValue.NewTable(s);
            foreach (DynValue v in table.Values)
            {
                if (v.Type == DataType.Table)
                    t.Table.Append(CopyTable(v.Table, s));
                else
                    t.Table.Append(v);
            }
            return t;
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CETargets This() { return script.main.targets; }
            [MoonSharpHidden] Dictionary<int, SLObject> Target() { return script.lvl.objDict; }

            public enum Types { ANYONE, PLAYER, CRAWLER, OBJECT }

            //public void UseTargeting(bool use)
            //{
            //    This().useTargeting = use;
            //}
            public bool PlayerVisible()
            {
                return This().SeesPlayer();
            }
            public bool PlayerReachable()
            {
                return This().playerReachable;
            }
            public Table TargetList()
            {
                return This().targets;
            }
            public bool Exists(int targetID)
            {
                return Target().ContainsKey(targetID);
            }
            public DynValue Closest(Types type)
            {
                DynValue closest = DynValue.Nil;
                float closestDist = -1f;
                foreach (DynValue value in This().targets.Values)
                {
                    int targetID = (int)value.Number;
                    SLObject target = Target()[targetID];
                    if ((type == Types.ANYONE &&
                        (target.type == SLObject.Type.player || target.type == SLObject.Type.crawler)) ||
                        (type == Types.PLAYER && target.type == SLObject.Type.player) ||
                        (type == Types.CRAWLER && target.type == SLObject.Type.crawler) ||
                        (type == Types.OBJECT && target.type == SLObject.Type.obj))
                    {
                        float dist = Vector3.Distance(script.data.position, target.transform.position);
                        if (closestDist == -1f || dist < closestDist)
                        {
                            closest = DynValue.NewNumber(targetID);
                            closestDist = dist;
                        }
                    }
                }
                return closest;
            }
            public Table InRange(float range, Types type)
            {
                Table table = new Table(script.script);
                foreach (DynValue value in This().targets.Values)
                {
                    int targetID = (int)value.Number;
                    SLObject target = Target()[targetID];
                    if ((type == Types.ANYONE &&
                        (target.type == SLObject.Type.player || target.type == SLObject.Type.crawler)) ||
                        (type == Types.PLAYER && target.type == SLObject.Type.player) ||
                        (type == Types.CRAWLER && target.type == SLObject.Type.crawler) ||
                        (type == Types.OBJECT && target.type == SLObject.Type.obj))
                    {
                        float dist = Vector3.Distance(script.data.position, target.transform.position);
                        if (dist < range + Radius(targetID))
                        {
                            table.Append(DynValue.NewNumber(targetID));
                        }
                    }
                }
                return table;
            }
            public Table Position(int targetID)
            {
                return script.Vector3ToTable(Target()[targetID].transform.position);
            }
            public float Radius(int targetID)
            {
                SLObject target = Target()[targetID];
                if (target.type == SLObject.Type.crawler) return ((CEMain)target).movement.exposed.CharacterRadius();
                if (target.type == SLObject.Type.player) return script.player.exposed.Radius();
                return 1f;
            }
            //public bool Collisions(int targetID)
            //{
            //    return ((CEMain)Target()[targetID]).movement.MovementType().collisions;
            //}
            public Types Type(int targetID)
            {
                SLObject target = Target()[targetID];
                if (target.type == SLObject.Type.crawler) return Types.CRAWLER;
                if (target.type == SLObject.Type.player) return Types.PLAYER;
                return Types.OBJECT;
            }
            public DynValue GetParent(int targetID)
            {
                return ((CEMain)Target()[targetID]).parent.exposed.GetParent();
            }
            public Table GetChildren(int targetID)
            {
                return ((CEMain)Target()[targetID]).parent.exposed.GetChildren();
            }
            public int ChildCount(int targetID)
            {
                return ((CEMain)Target()[targetID]).script.data.children.Count;
            }
            public DynValue GetVar(int targetID, string var)
            {
                CEScript targetScript = ((CEMain)Target()[targetID]).script;
                return targetScript.script.Globals.Get(var);
            }
            public void SetVar(int targetID, string var, DynValue value)
            {
                CEScript targetScript = ((CEMain)Target()[targetID]).script;
                targetScript.script.Globals.Set(var, value);
            }
            public void ChangeVar(int targetID, string var, float amount)
            {
                CEScript targetScript = ((CEMain)Target()[targetID]).script;
                targetScript.script.Globals.Set(var, DynValue.NewNumber(targetScript.script.Globals.Get(var).Number + amount));
            }
            public DynValue Call(int targetID, string func, params DynValue[] args)
            {
                CEScript targetScript = ((CEMain)Target()[targetID]).script;
                DynValue[] argsCopy = This().CopyArgs(args, targetScript.script);
                return targetScript.script.Call(targetScript.script.Globals.Get(func), argsCopy);
            }
            public DynValue Func(int targetID, string className)
            {
                CEScript targetScript = ((CEMain)Target()[targetID]).script;
                return targetScript.script.Globals.Get(className);
            }
        }
    }
}
