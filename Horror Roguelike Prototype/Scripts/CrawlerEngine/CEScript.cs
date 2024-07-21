using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;

namespace CrawlerEngine
{
    public class CEScript
    {
        public string crawlerType;
        public CEMain main;
        public LevelManager lvl;
        public CECrawlerManager crawlerManager;

        public Script script;
        DynValue startFunction;
        DynValue updateFunction;
        DynValue abstractedUpdateFunction;
        DynValue onTargetDeletedFunction;
        DynValue takeDamageFunction;
        DynValue aggroFunction;
        DynValue actionAnimEvent;
        DynValue playSoundEvent;
        DynValue generalAnimEvent;
        DynValue actionInProgress;
        DynValue actionAllowsMovement;
        DynValue initStatusEffect;
        DynValue attackFunction;
        public CEData data;
        public CETime time;
        public CEVector vector;
        public CEPlayer player;

        public void Init(ContentManager content, string type)
        {
            time = new CETime();
            player = new CEPlayer(this);

            //register proxy classes that give the script access to internal functions
            UserData.RegisterType<CEDebug>();
            UserData.RegisterProxyType<CEVector, CEScript>(r => new CEVector(r));
            UserData.RegisterProxyType<CEGame, CEScript>(r => new CEGame(r));
            UserData.RegisterProxyType<CEData.Exposed, CEScript>(r => new CEData.Exposed(r));
            UserData.RegisterProxyType<CEMovement.Exposed, CEScript>(r => new CEMovement.Exposed(r));
            UserData.RegisterProxyType<CEAnimation.Exposed, CEScript>(r => new CEAnimation.Exposed(r));
            UserData.RegisterProxyType<CETargets.Exposed, CEScript>(r => new CETargets.Exposed(r));
            UserData.RegisterProxyType<CETime.Exposed, CEScript>(r => new CETime.Exposed(r));
            UserData.RegisterProxyType<CEPlayer.Exposed, CEScript>(r => new CEPlayer.Exposed(r));
            UserData.RegisterProxyType<CESFX.Exposed, CEScript>(r => new CESFX.Exposed(r));
            UserData.RegisterProxyType<CECollision.Exposed, CEScript>(r => new CECollision.Exposed(r));
            UserData.RegisterProxyType<CEParent.Exposed, CEScript>(r => new CEParent.Exposed(r));
            UserData.RegisterProxyType<CERender.Exposed, CEScript>(r => new CERender.Exposed(r));

            //setup the script
            script = new Script();
            script.Options.ScriptLoader = new CEScriptLoader()
            {
                content = content,
                ModulePaths = new string[] { "?_module.lua" }
            };
            script.Globals["debug"] = new CEDebug();
            script.Globals["vector"] = new CEVector(this);
            script.Globals["game"] = new CEGame(this);
            script.Globals["crawler"] = new CEData.Exposed(this);
            script.Globals["movement"] = new CEMovement.Exposed(this);
            script.Globals["anim"] = new CEAnimation.Exposed(this);
            script.Globals["targets"] = new CETargets.Exposed(this);
            script.Globals["time"] = new CETime.Exposed(this);
            script.Globals["player"] = new CEPlayer.Exposed(this);
            script.Globals["sfx"] = new CESFX.Exposed(this);
            script.Globals["collision"] = new CECollision.Exposed(this);
            script.Globals["parent"] = new CEParent.Exposed(this);
            script.Globals["render"] = new CERender.Exposed(this);

            //run the script
            string scriptCode = content.GetCEScript(type + ".lua");
            script.DoString(scriptCode);

            //get the data table and update functions from the script
            data = new CEData(type, script.Globals.Get("standard_data").Table, content);
            startFunction = script.Globals.Get("start");
            updateFunction = script.Globals.Get("update");
            abstractedUpdateFunction = script.Globals.Get("abstracted_update");
            //isDeadFunction = script.Globals.Get("is_dead");
            onTargetDeletedFunction = script.Globals.Get("on_target_deleted");
            takeDamageFunction = script.Globals.Get("take_damage");
            aggroFunction = script.Globals.Get("aggro");
            actionAnimEvent = script.Globals.Get("action_event");
            playSoundEvent = script.Globals.Get("play_sound");
            generalAnimEvent = script.Globals.Get("general_event");
            actionInProgress = script.Globals.Get("action_in_progress");
            actionAllowsMovement = script.Globals.Get("action_allows_movement");
            initStatusEffect = script.Globals.Get("init_status_effect");
            attackFunction = script.Globals.Get("attack");

            //Debug.Log(string.Format("{0} {1}", data.name, data.movementTypes.Count));
        }

        public void Start()
        {
            if (startFunction != DynValue.Nil)
                script.Call(startFunction);
        }

        public void Update()
        {
            script.Call(updateFunction);
        }

        public void AbstractedUpdate(float deltaTime)
        {
            script.Call(abstractedUpdateFunction, DynValue.NewNumber(deltaTime));
        }

        public bool IsFlaggedDead()
        {
            return data.flagKill;
        }

        public void OnTargetDeleted(int targetID)
        {
            script.Call(onTargetDeletedFunction, DynValue.NewNumber(targetID));
        }

        public bool TakeDamage(float dmg, Vector3 force, bool playAnim = true)
        {
            return script.Call(takeDamageFunction,
                DynValue.NewNumber(dmg),
                DynValue.NewBoolean(false),
                DynValue.NewBoolean(playAnim),
                DynValue.NewTable(Vector3ToTable(force))).Boolean;
        }

        public void Aggro()
        {
            script.Call(aggroFunction, DynValue.Nil);
            //update cached info about actions
            main.actions.CacheActionInfo();
        }

        public void Attack(int targetID)
        {
            script.Call(attackFunction, targetID);
            //update cached info about actions
            main.actions.CacheActionInfo();
        }

        public void ActionAnimEvent(AnimationEvent myEvent)
        {
            script.Call(actionAnimEvent, new Table(script,
                DynValue.NewNumber(myEvent.floatParameter),
                DynValue.NewNumber(myEvent.intParameter),
                DynValue.NewString(myEvent.stringParameter)));
        }
        public void PlaySoundEvent(AnimationEvent myEvent)
        {
            script.Call(playSoundEvent, new Table(script,
                DynValue.NewNumber(myEvent.floatParameter),
                DynValue.NewNumber(myEvent.intParameter),
                DynValue.NewString(myEvent.stringParameter)));
        }
        public void GeneralAnimEvent(AnimationEvent myEvent)
        {
            script.Call(generalAnimEvent, new Table(script,
                DynValue.NewNumber(myEvent.floatParameter),
                DynValue.NewNumber(myEvent.intParameter),
                DynValue.NewString(myEvent.stringParameter)));
        }

        public bool ActionInProgress(bool functional)
        {
            return script.Call(actionInProgress, DynValue.NewBoolean(functional)).Boolean;
        }
        public bool ActionAllowsMovement()
        {
            return script.Call(actionAllowsMovement).Boolean;
        }

        public void InitStatusEffect(int effect)
        {
            script.Call(initStatusEffect, DynValue.NewNumber(effect));
        }


        public Vector3 TableToVector3(Table table)
        {
            return new Vector3(
                (float)table.Get(1).Number,
                (float)table.Get(2).Number,
                (float)table.Get(3).Number);
        }
        public Table Vector3ToTable(Vector3 vector)
        {
            return new Table(script,
                DynValue.NewNumber(vector.x),
                DynValue.NewNumber(vector.y),
                DynValue.NewNumber(vector.z));
        }
        public Color TableToColor(Table table)
        {
            return new Color(
                (float)table.Get(1).Number,
                (float)table.Get(2).Number,
                (float)table.Get(3).Number,
                (float)table.Get(4).Number);
        }
        public Table ColorToTable(Color color)
        {
            return new Table(script,
                DynValue.NewNumber(color.r),
                DynValue.NewNumber(color.g),
                DynValue.NewNumber(color.b),
                DynValue.NewNumber(color.a));
        }

        //debugging
        public class CEDebug
        {
            public void Log(DynValue text)
            {
                if (text.Type == DataType.String)
                    Debug.Log(text.String);
                else if (text.Type == DataType.Number)
                    Debug.Log(text.Number);
            }
        }

        //vector math
        public class CEVector
        {
            [MoonSharpHidden] public CEScript script;
            [MoonSharpHidden] public CEVector(CEScript p) { script = p; }

            public float Distance(Table pos1, Table pos2)
            {
                return Vector3.Distance(script.TableToVector3(pos1), script.TableToVector3(pos2));
            }
            public Table Direction(Table pos1, Table pos2)
            {
                return script.Vector3ToTable(Vector3.Normalize(script.TableToVector3(pos2) - script.TableToVector3(pos1)));
            }
            public Table Add(Table vector, Table vector2)
            {
                return script.Vector3ToTable(script.TableToVector3(vector) + script.TableToVector3(vector2));
            }
            public Table Subtract(Table vector, Table vector2)
            {
                return script.Vector3ToTable(script.TableToVector3(vector) - script.TableToVector3(vector2));
            }
            public Table Multiply(Table vector, float m)
            {
                return script.Vector3ToTable(script.TableToVector3(vector) * m);
            }
            public Table Perpendicular(Table vector)
            {
                Vector3 v = script.TableToVector3(vector);
                return script.Vector3ToTable(new Vector3(v.z, v.y, -v.x));
            }
            public float VectorToAngle(Table vector)
            {
                return Quaternion.LookRotation(Vector3.Scale(script.TableToVector3(vector), new Vector3(1f, 0f, 1f))).eulerAngles.y;
            }
            public Table AngleToVector(float angle)
            {
                return script.Vector3ToTable(Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
            }
            public Table RndPosInRadius(float radius)
            {
                Vector2 rndPoint = Random.insideUnitCircle * radius;
                return script.Vector3ToTable(new Vector3(rndPoint.x, 0f, rndPoint.y));
            }
            public Table Up(float units = 1f) { return script.Vector3ToTable(new Vector3(0f, units, 0f)); }
        }

        //game
        public class CEGame
        {
            [MoonSharpHidden] public CEScript script;
            [MoonSharpHidden] public CEGame(CEScript p) { script = p; }

            public DynValue SpawnCrawler(string crawler)
            {
                Vector3 pos = (script.main != null && script.main.setup.spawnerBone != null ?
                    script.main.setup.spawnerBone.TransformPoint(script.main.setup.spawnerOffset) : script.data.position);
                CEScript newCrawler = null;
                return DynValue.NewNumber(newCrawler.main.ID);
            }
            public DynValue SpawnCrawler(string crawler, Table direction)
            {
                Vector3 pos = (script.main != null && script.main.setup.spawnerBone != null ?
                    script.main.setup.spawnerBone.TransformPoint(script.main.setup.spawnerOffset) : script.data.position);
                CEScript newCrawler = null;
                return DynValue.NewNumber(newCrawler.main.ID);
            }
            public DynValue SpawnCrawlerToPosition(string crawler, Table spawnPos, Table facePos)
            {
                Vector3 pos = script.TableToVector3(spawnPos);
                Vector3 pos2 = script.TableToVector3(facePos);
                CEScript newCrawler = null;
                return DynValue.NewNumber(newCrawler.main.ID);
            }
            public void SpawnProjectile(string projectile, Table direction, float speed, float topSpeed, float dmg)
            {
                Vector3 pos = (script.main != null && script.main.setup.spawnerBone != null ?
                    script.main.setup.spawnerBone.TransformPoint(script.main.setup.spawnerOffset) : script.data.position);
            }
        }
    }
}
