using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using System.Xml.Serialization;
//using MoonSharp.Interpreter.Serialization;

namespace CrawlerEngine
{
    public class CEData
    {
        public string filename;
        public int loadedReferenceID = -1;

        //data variables
        public string name;
        public Transform model;

        public float health;

        public List<MovementType> movementTypes = new List<MovementType>();
        public class MovementType
        {
            public float aggroDistance;
            public float aggroSpeed;
            public float roamSpeed;
            public float turningSpeed;
            public bool meleeImmunity;
            public bool castingImmunity;
            public float characterRadius;

            public Animation idleAnim;
            public Animation moveAnim;

            public MovementType(Table t)
            {
                aggroDistance = (float)t.Get("aggro_distance").Number;
                aggroSpeed = (float)t.Get("aggro_speed").Number;
                roamSpeed = (float)t.Get("roam_speed").Number;
                turningSpeed = (float)t.Get("turning_speed").Number;
                meleeImmunity = t.Get("melee_immunity").Boolean;
                castingImmunity = t.Get("casting_immunity").Boolean;
                characterRadius = (float)t.Get("character_radius").Number;

                idleAnim = new Animation(t.Get("idle_anim").Table);
                moveAnim = new Animation(t.Get("move_anim").Table);
            }
        }

        public class Animation
        {
            public string clip;
            public float speed;
            public bool additiveBlending;

            public Animation(Table t)
            {
                clip = t.Get("clip").String;
                speed = (float)t.Get("speed").Number;
                additiveBlending = (t.Length > 2 ? t.Get("additive_blending").Boolean : false);
            }
        }

        public CEData(string type, Table t, ContentManager content)
        {
            filename = type;
            name = t.Get("name").String;

            string modelName = t.Get("model").String;
            foreach (Transform crawlerModel in content.crawlerModels)
                if (modelName == crawlerModel.name) { model = crawlerModel; break; }

            health = (float)t.Get("health").Number;

            foreach (DynValue v in t.Get("movement_types").Table.Values)
                movementTypes.Add(new MovementType(v.Table));
        }


        //crawler state variables
        public bool alive = true;
        public bool flagKill = false;
        public Vector3 position;
        public Vector3 velocity;
        public SubstanceLevel.Room room = null;
        public int movementType = 1;
        public Exposed.States state = Exposed.States.ROAMING;
        public bool visible = true;

        public enum StatusFX { ZERO, POISONED, STUNNED, CONFUSED, BLINDED, ENRAGED, SLOWED, SHACKLED }
        [System.Serializable]
        public class CrawlerStatusEffect { public int effect; public float time; public float duration; }
        public List<CrawlerStatusEffect> statusFX = new List<CrawlerStatusEffect>();
        CrawlerStatusEffect GetStatusFX(int status)
        {
            foreach (CrawlerStatusEffect fx in statusFX)
            {
                if (fx.effect == status) return fx;
            }
            return null;
        }
        void AddStatusFX(int status, float time, CEScript script)
        {
            CrawlerStatusEffect fx = GetStatusFX(status);
            if (fx != null)
                fx.time = Mathf.Max(fx.time, time);
            else
            {
                statusFX.Add(new CrawlerStatusEffect() { effect = status, time = time, duration = time });
                script.InitStatusEffect(statusFX[statusFX.Count-1].effect);
            }
        }
        public bool Is(StatusFX status) { return GetStatusFX((int)status) != null; }
        public float GetStatusNormalized(StatusFX status)
        {
            CrawlerStatusEffect e = GetStatusFX((int)status);
            return (e != null ? e.time / e.duration : 0f);
        }
        public void Become(StatusFX status, float time, CEScript script) { AddStatusFX((int)status, time - 0.01f, script); }

        public CEScript parent = null;
        public List<CEScript> children = new List<CEScript>();

        public SubstanceLevel.Room spawnRoom = null;
        public Vector3 roamAnchor;

        public CEAnimation.RagdollMode ragdollMode = CEAnimation.RagdollMode.none;

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] public CEData This() { return script.data; }

            public enum States { ROAMING, COMBAT, ABSTRACTED };

            public States GetState() { return This().state; }
            public void SetState(States state) { This().state = state; }
            public Table Position() { return script.Vector3ToTable(This().position); }
            public bool Is(int status) { return This().GetStatusFX(status) != null; }
            public void Become(int status, float time) { This().AddStatusFX(status, time - 0.01f, script); }
            public Table GetStatusFX()
            {
                Table statusFX = new Table(script.script);
                foreach (CrawlerStatusEffect fx in This().statusFX)
                    statusFX.Append(
                        DynValue.NewTable(script.script,
                        DynValue.NewNumber(fx.effect),
                        DynValue.NewNumber(fx.time)));
                return statusFX;
            }
            public void SetStatusFXTime(int index, float time)
            {
                This().statusFX[index - 1].time = time;
                if (time <= 0f) This().statusFX.RemoveAt(index - 1);
            }
            public DynValue AsTarget() { return (script.main != null ? DynValue.NewNumber(script.main.ID) : DynValue.Nil); }
            public Table RoamAnchor() { return script.Vector3ToTable(This().roamAnchor); }
            public void Kill() { This().flagKill = true; }

            public void SetVisible(bool visible)
            {
                This().visible = visible;
            }
        }

        public void LoadSave(CEScript script, CrawlerSave save, LevelManager lvl, CECrawlerManager crawlerMgr)
        {
            alive = save.alive;
            position = save.position;
            velocity = save.velocity;
            room = (save.roomIndex != -1 ? lvl.PHMap.map.indexedRoomList[save.roomIndex] : null);
            movementType = save.movementType;
            state = save.state;
            visible = save.visible;

            if (save.state != Exposed.States.ABSTRACTED)
                loadedReferenceID = save.referenceID;

            statusFX = save.statusFX;

            parent = (save.parentIndex != -1 ? crawlerMgr.crawlers[save.parentIndex] : null);
            foreach (int childIndex in save.childrenIndices)
                children.Add(crawlerMgr.crawlers[childIndex]);

            spawnRoom = (save.spawnRoomIndex != -1 ? lvl.PHMap.map.indexedRoomList[save.spawnRoomIndex] : null);
            roamAnchor = save.roamAnchor;

            ragdollMode = save.ragdollMode;

            //list script metatables
            save.CompileMetaTableList(script);

            //load the script
            foreach (CrawlerSave.ScriptTable table in save.scriptTables)
            {
                if (table.tableIndex > 0)
                {
                    if (table.metaTable != "")
                    {
                        DynValue metaTable = save.GetMetaTableByName(table.metaTable);
                        table.newTable = script.script.Call(metaTable.Table.Get("new"), metaTable);
                    }
                    else
                    {
                        table.newTable = DynValue.NewTable(script.script);
                    }
                }
            }
            foreach (CrawlerSave.ScriptTable table in save.scriptTables)
            {
                foreach (CrawlerSave.ScriptValue value in table.values)
                {
                    DynValue dynValue = null;
                    switch (value.type)
                    {
                        case DataType.Boolean:
                            dynValue = DynValue.NewBoolean(bool.Parse(value.value));
                            break;
                        case DataType.Nil:
                            dynValue = DynValue.Nil;
                            break;
                        case DataType.Number:
                            dynValue = DynValue.NewNumber(double.Parse(value.value));
                            break;
                        case DataType.String:
                            dynValue = DynValue.NewString(value.value.Trim('"'));
                            break;
                        case DataType.Table:
                            dynValue = save.scriptTables[value.tableIndex].newTable;
                            break;
                    }
                    if (table.tableIndex == 0)
                    {
                        script.script.Globals[value.name] = dynValue;
                    }
                    else if (table.metaTable == "")
                    {
                        if (value.name != null)
                            table.newTable.Table.Set(value.name, dynValue);
                        else
                            table.newTable.Table.Append(dynValue);
                    }
                    else if (table.metaTable != "") //päivitetään metatablen instanssiin oikeat arvot
                    {
                        table.newTable.Table[value.name] = dynValue;
                    }
                }
            }
        }
        public void LoadAnimState(CrawlerSave save, CEAnimation anim)
        {
            //clips
            for (int i = 0; i < save.setAnimationClips.Length; i++)
                if (save.setAnimationClips[i] != "")
                    anim.SetOneAnimClip((CEAnimation.Exposed.Animations)i, save.setAnimationClips[i], false);
            anim.ApplyAnimationClips();
            //speeds
            anim.anim.SetFloat("IdleSpeed", save.animationSpeeds[0]);
            anim.anim.SetFloat("MoveSpeed", save.animationSpeeds[1]);
            anim.anim.SetFloat("StompedSpeed", save.animationSpeeds[2]);
            anim.anim.SetFloat("AttackSpeed", save.animationSpeeds[3]);
            anim.anim.SetFloat("DodgeSpeed", save.animationSpeeds[4]);
            anim.anim.SetFloat("MiscAnimSpeed", save.animationSpeeds[4]);
            //state and time
            if (save.animationState != "")
            {
                anim.anim.Play("Base Layer." + save.animationState, 0, save.animationTime);
                anim.anim.Update(0.001f);
            }
        }

        [System.Serializable]
        public class CrawlerSave
        {
            public CrawlerSave() { }
            public CrawlerSave(CEScript script, CEData data, CECrawlerManager crawlerMgr)
            {
                filename = data.filename;
                alive = data.alive;
                position = data.position;
                velocity = data.velocity;
                rotation = (script.main != null ? script.main.transform.rotation : Quaternion.identity);
                roomIndex = (data.room != null ? data.room.listIndex : -1);
                movementType = data.movementType;
                state = data.state;
                visible = data.visible;

                referenceID = (script.main != null ? script.main.ID : -1);

                statusFX = data.statusFX;

                parentIndex = (data.parent != null ? crawlerMgr.crawlers.IndexOf(data.parent) : -1);
                childrenIndices = new List<int>();
                foreach (CEScript child in data.children)
                    childrenIndices.Add(crawlerMgr.crawlers.IndexOf(child));

                spawnRoomIndex = (data.spawnRoom != null ? data.spawnRoom.listIndex : -1);
                roamAnchor = data.roamAnchor;

                ragdollMode = data.ragdollMode;

                //animation state
                setAnimationClips = null; 
                animationSpeeds = null;
                animationState = "";
                animationTime = 0f;
                if (script.main != null)
                {
                    CEAnimation a = script.main.anim;
                    setAnimationClips = a.setAnimClips;
                    animationSpeeds = new float[]
                    {
                        a.anim.GetFloat("IdleSpeed"),
                        a.anim.GetFloat("MoveSpeed"),
                        a.anim.GetFloat("StompedSpeed"),
                        a.anim.GetFloat("AttackSpeed"),
                        a.anim.GetFloat("DodgeSpeed"),
                        a.anim.GetFloat("MiscAnimSpeed")
                    };
                    animationState = a.GetAnimatorState();
                    AnimatorStateInfo info;
                    if (a.GetActionInProgress(animationState, out info))
                        animationTime = info.normalizedTime;
                }

                //list metatables
                CompileMetaTableList(script);
                //save values and tables
                scriptTables = new List<ScriptTable>();
                new ScriptTable(this, script.script.Globals.ReferenceID, "script variables", script.script.Globals);
            }
            public string filename;
            public bool alive;
            public Vector3 position;
            public Vector3 velocity;
            public Quaternion rotation;
            public int roomIndex;
            public int movementType;
            public Exposed.States state;
            public bool enableRoaming;
            public bool visible;

            public int referenceID;

            public List<CrawlerStatusEffect> statusFX;

            public int parentIndex;
            public List<int> childrenIndices;

            public int spawnRoomIndex;
            public Vector3 roamAnchor;

            public CEAnimation.RagdollMode ragdollMode;

            public string[] setAnimationClips;
            public float[] animationSpeeds;
            public string animationState;
            public float animationTime;

            public List<ScriptTable> scriptTables;
            public ScriptTable GetTableByReferenceID(int refID)
            {
                foreach (ScriptTable table in scriptTables)
                {
                    if (table.referenceID == refID)
                        return table;
                }
                return null;
            }
            [System.NonSerialized, XmlIgnore] public List<TablePair> metaTables;
            public string GetMetaTableName(Table metaTable)
            {
                foreach (TablePair pair in metaTables)
                {
                    if (pair.Value.Table == metaTable)
                        return pair.Key.String;
                }
                return "";
            }
            public DynValue GetMetaTableByName(string name)
            {
                foreach (TablePair pair in metaTables)
                {
                    if (pair.Key.String == name)
                        return pair.Value;
                }
                return null;
            }
            public void CompileMetaTableList(CEScript script)
            {
                metaTables = new List<TablePair>();
                foreach (TablePair pair in script.script.Globals.Pairs)
                    if (IsMetaTable(pair.Key.String, pair.Value.Type))
                        metaTables.Add(pair);
            }

            public class ScriptTable
            {
                public ScriptTable() { }
                public ScriptTable(CrawlerSave save, int refID, string name1, Table table)
                {
                    save.scriptTables.Add(this);
                    referenceID = refID;
                    metaTable = (table.MetaTable != null ? save.GetMetaTableName(table.MetaTable) : "");
                    name = name1;
                    tableIndex = save.scriptTables.Count-1;
                    values = new List<ScriptValue>();

                    foreach (TablePair pair in table.Pairs)
                    {
                        if (ShouldBeSaved(pair.Key.String))
                        {
                            if (pair.Value.Type == DataType.Boolean ||
                                pair.Value.Type == DataType.Nil ||
                                pair.Value.Type == DataType.Number ||
                                pair.Value.Type == DataType.String)
                            {
                                values.Add(new ScriptValue()
                                {
                                    tableIndex = -1,
                                    type = pair.Value.Type,
                                    name = pair.Key.String,
                                    value = MoonSharp.Interpreter.Serialization.SerializationExtensions.SerializeValue(pair.Value)
                                });
                            }
                            else if (pair.Value.Type == DataType.Table)
                            {
                                ScriptTable referencedTable = save.GetTableByReferenceID(pair.Value.Table.ReferenceID);
                                if (referencedTable != null)
                                {
                                    values.Add(new ScriptValue()
                                    {
                                        tableIndex = referencedTable.tableIndex,
                                        type = DataType.Table,
                                        name = pair.Key.String,
                                        value = ""
                                    });
                                } else {
                                    values.Add(new ScriptValue()
                                    {
                                        tableIndex = save.scriptTables.Count,
                                        type = DataType.Table,
                                        name = pair.Key.String,
                                        value = ""
                                    });
                                    new ScriptTable(save, pair.Value.Table.ReferenceID, pair.Key.String, pair.Value.Table);
                                }
                            }
                        }
                    }
                }
                [System.NonSerialized, XmlIgnore] public DynValue newTable;
                public int referenceID;
                public string metaTable;
                public string name;
                public int tableIndex;
                public List<ScriptValue> values;
            }
            public class ScriptValue
            {
                public string name;
                public DataType type;
                public string value;
                public int tableIndex;
            }
        }

        public static bool ShouldBeSaved(string name)
        {
            return (name == null || name == "" ||
                (name[0] != '_' &&
                name[0] == name.ToLower()[0] &&
                name != "standard_data" &&
                name != "string" &&
                name != "package" &&
                name != "table" &&
                name != "math" &&
                name != "coroutine" &&
                name != "bit32" &&
                name != "dynamic" &&
                name != "os" &&
                name != "json"));
        }
        public static bool IsMetaTable(string name, DataType type)
        {
            return (type == DataType.Table &&
                name != null &&
                name != "" &&
                name[0] != '_' &&
                name[0] != name.ToLower()[0]);
        }
    }
}
