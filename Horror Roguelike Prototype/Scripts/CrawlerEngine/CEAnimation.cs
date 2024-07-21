using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using BeautifulDissolves;

namespace CrawlerEngine
{
    public class CEAnimation : MonoBehaviour
    {
        CEMain main;

        [System.NonSerialized]
        public FootLocking footLock = null;
        [System.NonSerialized]
        public HeadLooking headLook;

        [System.NonSerialized]
        public Animator anim;
        AnimatorOverrideController animatorOverrideController;
        AnimationClipOverrides clipOverrides;

        public Dissolve[] dissolve;

        public Transform stompEffectPrefab;

        public string[] setAnimClips;


        public class AnimationClipOverrides : List<KeyValuePair<AnimationClip, AnimationClip>>
        {
            public AnimationClipOverrides(int capacity) : base(capacity) { }

            public AnimationClip this[string name]
            {
                get { return this.Find(x => x.Key.name.Equals(name)).Value; }
                set
                {
                    int index = this.FindIndex(x => x.Key.name.Equals(name));
                    if (index != -1)
                        this[index] = new KeyValuePair<AnimationClip, AnimationClip>(this[index].Key, value);
                }
            }
        }

        void SetAnimations()
        {
            animatorOverrideController = new AnimatorOverrideController(anim.runtimeAnimatorController);
            anim.runtimeAnimatorController = animatorOverrideController;
            clipOverrides = new AnimationClipOverrides(animatorOverrideController.overridesCount);
            animatorOverrideController.GetOverrides(clipOverrides);
        }

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;

            setAnimClips = new string[System.Enum.GetValues(typeof(Exposed.Animations)).Length];
            for (int i = 0; i < setAnimClips.Length; i++) setAnimClips[i] = "";

            anim = GetComponent<Animator>();
            SetAnimations();
            UpdateMovementType();
        }

        // Update is called once per frame
        int footLockInitDelay = 1;
        void Update()
        {
            //init foot locking with a delay
            if (footLock == null)
            {
                if (footLockInitDelay <= 0)
                    footLock = new FootLocking(main, GetComponentsInChildren<DitzelGames.FastIK.FastIKFabric>());
                footLockInitDelay--;
            }

            if (stoppingAnimation)
                UpdateStopAnim();
            UpdateStompEffect();
        }

        void LateUpdate()
        {
            if (main.OCVisible)
            {
                //foot locking
                if (footLock != null && main.script.data.ragdollMode == RagdollMode.none)
                    footLock.UpdateFootLocking(Mathf.Max(1f, main.movement.moveSpeed));
                ////head look
                if (headLook != null && main.script.data.ragdollMode == RagdollMode.none)
                    headLook.UpdateHeadLook();
            }
        }

        public bool GetStomped(float dmg, bool superStomp, Vector3 force)
        {
            bool killed = main.script.TakeDamage(dmg, force);
            StartStompEffect((int)Mathf.Round(dmg), Color.white);
            return killed;
        }

        public float popupFadeTimer = 0f;
        public void Popup(string text, Color c, float time = 1f)
        {
            LevelManager.objs.localPlayer.interaction.NewWorldPopupText(
                text,
                main.setup.UIAnchorBone.position + transform.rotation * main.setup.UIOffset,
                c,
                time);
            popupFadeTimer = Mathf.Max(popupFadeTimer, time);
        }

        Transform stompEffect = null;
        TextMesh stompEffectText = null;
        Renderer stompEffectRend = null;
        float stompEffectTimer;
        float stompEffectFade;
        float stompEffectTextFade;
        Color stompEffectColor;
        public void StartStompEffect(int dmg, Color c)
        {
            PlayerScript p = LevelManager.objs.localPlayer;
            if (stompEffect == null)
                stompEffect = Instantiate(stompEffectPrefab);

            stompEffectText = stompEffect.GetComponentInChildren<TextMesh>();
            stompEffectText.text = dmg.ToString();
            stompEffectText.color = Color.clear;
            stompEffectRend = stompEffect.GetComponentInChildren<Renderer>();
            stompEffectRend.material.color = Color.clear;
            stompEffectFade = 0.5f;
            stompEffectTextFade = 1f;
            stompEffectTimer = 0f;
            stompEffectColor = c;
        }
        public void UpdateStompEffect()
        {
            PlayerScript p = LevelManager.objs.localPlayer;
            if (stompEffect != null)
            {
                if (stompEffectTimer < 0.1f)
                    stompEffect.rotation = p.transform.rotation * Quaternion.Euler(85f, 0f, 0f);

                stompEffectTimer += Time.deltaTime;
                if (stompEffectTimer > 0f)
                {
                    stompEffectRend.material.color = new Color(
                        stompEffectColor.r,
                        stompEffectColor.g,
                        stompEffectColor.b,
                        stompEffectFade);
                    if (stompEffectTimer > (main.Alive() ? 0.4f : 0.05f))
                        stompEffectFade = Mathf.MoveTowards(stompEffectFade, 0f, Time.deltaTime * 4f);
                }
                if (stompEffectTimer > (main.Alive() ? 0.15f : 0f))
                {
                    stompEffectText.color = new Color(1f, 1f, 1f, stompEffectTextFade);
                    if (stompEffectTimer > (main.Alive() ? 0.5f : 0.3f))
                        stompEffectTextFade = Mathf.MoveTowards(stompEffectTextFade, 0f, Time.deltaTime * 2f);
                }
                if (stompEffectTextFade == 0f)
                    Destroy(stompEffect.gameObject);
            }
        }

        public enum RagdollMode { none, full, legs }
        List<Rigidbody> rbodies;
        public Rigidbody mainRBody = null;
        Vector3 rootBoneRelativePos;
        public void BecomeRagdoll(bool onlyLegs)
        {
            main.script.data.ragdollMode = (onlyLegs ? RagdollMode.legs : RagdollMode.full);
            rootBoneRelativePos = main.setup.root.InverseTransformPoint(main.transform.position);
            //disable animator, character controller, rigidbody and colliders
            anim.Play("Base Layer.Idle");
            anim.enabled = false;

            main.collision.EnableCharacterCollider(false);
            if (main.script.data.ragdollMode == RagdollMode.full)
            {
                main.collision.EnableCrawlerHitCollider(false);
                transform.GetChild(0).GetComponent<Collider>().enabled = false;
            } else
            {
                if (mainRBody == null)
                {
                    mainRBody = gameObject.AddComponent<Rigidbody>();
                    mainRBody.isKinematic = true;
                    mainRBody.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }

            //enable colliders
            foreach (Transform bone in (main.script.data.ragdollMode == RagdollMode.full ? main.setup.bones : main.setup.legs))
            {
                Collider coll = bone.GetComponent<Collider>();
                coll.material = main.collision.physMat;
                coll.enabled = true;
                coll.gameObject.layer = LayerMask.NameToLayer("corpse");
            }
            //create rigidbodies
            rbodies = new List<Rigidbody>();
            foreach (Transform bone in (main.script.data.ragdollMode == RagdollMode.full ? main.setup.bones : main.setup.legs))
            {
                Rigidbody rb = bone.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = bone.gameObject.AddComponent<Rigidbody>();
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    rbodies.Add(rb);
                    if (main.script.data.ragdollMode == RagdollMode.full && bone == main.setup.root)
                        mainRBody = rb;
                }
            }
            //create joints
            foreach (Transform bone in (main.script.data.ragdollMode == RagdollMode.full ? main.setup.bones : main.setup.legs))
            {
                if (bone != main.setup.root)
                {
                    CharacterJoint joint = bone.GetComponent<CharacterJoint>();
                    if (joint == null) joint = bone.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = (main.script.data.ragdollMode == RagdollMode.legs &&
                        !main.setup.legs.Contains(bone.parent) ? mainRBody : bone.parent.GetComponent<Rigidbody>());
                    joint.swingLimitSpring = new SoftJointLimitSpring() { spring = 1000f, damper = 10000f };
                    joint.swing2Limit = new SoftJointLimit() { limit = 20f };
                    joint.twistLimitSpring = new SoftJointLimitSpring() { spring = 1000f, damper = 10000f };
                    joint.highTwistLimit = new SoftJointLimit() { limit = 10f };
                }
            }
            
        }
        public void ApplyForceToRagdoll(Vector3 force)
        {
            //apply force
            foreach (Rigidbody rbody in rbodies)
                rbody.AddForce(force * Random.Range(0.5f, 2f), ForceMode.Impulse);
        }
        public void UndoRagdoll()
        {
            rbodies.Clear();
            //enable animator, character controller, rigidbody and colliders
            Vector3 newPos = main.setup.root.TransformPoint(rootBoneRelativePos);
            transform.position = new Vector3(newPos.x, Mathf.Max(newPos.y, 0.1f), newPos.z);
            transform.rotation = Quaternion.LookRotation(Vector3.Scale(transform.forward, new Vector3(1f, 0f, 1f)));
            anim.enabled = true;

            main.collision.EnableCharacterCollider(true);
            if (main.script.data.ragdollMode == RagdollMode.full)
            {
                main.collision.EnableCrawlerHitCollider(true);
                transform.GetChild(0).GetComponent<Collider>().enabled = true;
            } else
            {
                Destroy(mainRBody);
            }
            mainRBody = null;
            //disable colliders
            foreach (Transform bone in (main.script.data.ragdollMode == RagdollMode.full ? main.setup.bones : main.setup.legs))
            {
                Collider coll = bone.GetComponent<Collider>();
                coll.enabled = false;
            }
            //destroy joints and rigidbodies
            foreach (Transform bone in (main.script.data.ragdollMode == RagdollMode.full ? main.setup.bones : main.setup.legs))
            {
                if (bone != main.setup.root) Destroy(bone.GetComponent<CharacterJoint>());
                Destroy(bone.GetComponent<Rigidbody>());
            }
            main.script.data.ragdollMode = RagdollMode.none;
            //update animator
            anim.Update(Time.deltaTime);
            //reset footlocking
            footLock.ResetTargets();
        }

        public void SetMove(bool move)
        {
            anim.SetBool("Move", move);
        }
        public void SetMoveSpeed(float speed)
        {
            anim.SetFloat("MoveSpeed", speed);
        }

        //stop firing events until completely transitioned to idle
        bool stoppingAnimation = false;
        public void StopAnim()
        {
            anim.fireEvents = false;
            anim.Play("Base Layer.Idle");
            stoppingAnimation = true;
        }
        void UpdateStopAnim()
        {
            if (!InTransition())
            {
                anim.fireEvents = true;
                stoppingAnimation = false;
            }
        }

        public void DeathDissolve()
        {
            dissolve[0].TriggerDissolve();
        }
        public void FleeDissolve()
        {
            dissolve[2].TriggerDissolve();
        }

        AnimationClip GetAnimClip(string clipString)
        {
            string[] clipInfo = clipString.ToLower().Split('#');
            string clipName = clipInfo[0];
            int clipIndex = int.Parse(clipInfo[1]) - 1;
            switch (clipName)
            {
                case "idle":
                    return main.setup.idleAnimations[clipIndex];
                case "move":
                    return main.setup.moveAnimations[clipIndex];
                case "attack":
                    return main.setup.attackAnimations[clipIndex];
                default:
                    return main.setup.miscAnimations[clipIndex];
            }
        }
        public void SetOneAnimClip(Exposed.Animations action, string clipName, bool apply = true)
        {
            string actionName = AnimToStateName(action);
            setAnimClips[(int)action] = clipName;

            clipOverrides[actionName] = GetAnimClip(clipName);
            if (apply)
                animatorOverrideController.ApplyOverrides(clipOverrides);
        }
        public void ApplyAnimationClips() { animatorOverrideController.ApplyOverrides(clipOverrides); }
        
        public void UpdateMovementType()
        {
            //get movement type
            CEData.MovementType movementType = main.movement.MovementType();
            //toggle which movement animation state is used
            bool otherMovement = anim.GetBool("OtherMovement");
            anim.SetBool("OtherMovement", !otherMovement);
            //set animation clip overrides
            setAnimClips[(int)Exposed.Animations.IDLE] = movementType.idleAnim.clip;
            setAnimClips[(int)Exposed.Animations.MOVE] = movementType.moveAnim.clip;
            clipOverrides[(otherMovement ? "Idle" : "OtherIdle")] = GetAnimClip(movementType.idleAnim.clip);
            clipOverrides[(otherMovement ? "Move" : "OtherMove")] = GetAnimClip(movementType.moveAnim.clip);
            animatorOverrideController.ApplyOverrides(clipOverrides);
            //set animation speeds
            anim.SetFloat("IdleSpeed", movementType.idleAnim.speed);
            anim.SetFloat("IdleOffset", Random.value);
        }

        public bool ActionInProgress(string actionName)
        {
            int layer = (actionName == "Attack" || actionName == "GetHit" ? 1 : 0);
            string layerName = (layer == 1 ? "Blends." : "Base Layer.");
            string actionStateName = layerName + actionName;
            string otherActionStateName = layerName +
                (actionName == "Idle" || actionName == "Move" ? "Other" + actionName : actionName);
            bool flagged = ((actionName == "Move"/* || actionName == "Attack"*/) && anim.GetBool(actionName));
            return (flagged ||
                anim.GetNextAnimatorStateInfo(layer).IsName(actionStateName) ||
                anim.GetCurrentAnimatorStateInfo(layer).IsName(actionStateName) ||
                anim.GetNextAnimatorStateInfo(layer).IsName(otherActionStateName) ||
                anim.GetCurrentAnimatorStateInfo(layer).IsName(otherActionStateName));
        }
        public string GetAnimatorState()
        {
            string name = "";
            for (int i = 0; i < System.Enum.GetValues(typeof(Exposed.Animations)).Length; i++)
            {
                name = AnimToStateName((Exposed.Animations)i);
                if (ActionInProgress(name)) return name;
            }
            return "";
        }
        public bool InTransition()
        {
            return (/*anim.GetBool("Attack") || */anim.IsInTransition(0));
        }
        public bool GetActionInProgress(string actionName, out AnimatorStateInfo info)
        {
            int layer = (actionName == "Attack" || actionName == "GetHit" ? 1 : 0);
            string layerName = (layer == 1 ? "Blends." : "Base Layer.");
            string actionStateName = layerName + actionName;
            string otherActionStateName = layerName +
                (actionName == "Idle" || actionName == "Move" ? "Other" + actionName : actionName);
            if (anim.GetCurrentAnimatorStateInfo(layer).IsName(actionStateName) ||
                anim.GetCurrentAnimatorStateInfo(layer).IsName(otherActionStateName))
            {
                info = anim.GetCurrentAnimatorStateInfo(layer);
                return true;
            }
            info = new AnimatorStateInfo();
            return false;
        }
        public string AnimToStateName(Exposed.Animations anim)
        {
            string name = "";
            switch (anim)
            {
                case Exposed.Animations.IDLE: name = "Idle"; break;
                case Exposed.Animations.MOVE: name = "Move"; break;
                //case Exposed.Animations.STOMPED: name = "Stomped"; break;
                case Exposed.Animations.ATTACK: name = "Attack"; break;
                //case Exposed.Animations.DODGE: name = "Dodge"; break;
                case Exposed.Animations.MISC: name = "MiscAnim"; break;
            }
            return name;
        }

        public void JumpFrames(AnimationEvent myEvent)
        {
            anim.Update(1f / 24f *
                (myEvent.floatParameter > 0f ? myEvent.floatParameter : 1f) *
                (myEvent.intParameter > 0 ? myEvent.intParameter : 1f));
        }
        public void ActionEvent(AnimationEvent myEvent)
        {
            main.script.ActionAnimEvent(myEvent);
        }
        public void PlaySound(AnimationEvent myEvent)
        {
            main.script.PlaySoundEvent(myEvent);
        }
        public void GeneralEvent(AnimationEvent myEvent)
        {
            main.script.GeneralAnimEvent(myEvent);
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CEAnimation This() { return script.main.anim; }

            public enum Animations { IDLE, MOVE, STOMPED, ATTACK, DODGE, MISC };
            public enum RagdollModes { NONE, FULL, APPENDAGES };

            public void PlayAttack(Table anim)
            {
                This().SetOneAnimClip(Animations.ATTACK, anim.Get("clip").String);
                This().anim.SetFloat("AttackSpeed", (float)anim.Get("speed").Number);
                This().anim.Play("Blends.Attack");
                This().anim.Update(0.001f);
            }
            public void PlayStomped()
            {
                This().anim.Play("Base Layer.Stomped", -1, 0f);
                This().anim.Update(0.001f);
            }
            public void PlayDodge(Table anim, bool left, float speed)
            {
                string dodgeClip = (left ? "clip_L" : "clip_R");
                This().SetOneAnimClip(Animations.DODGE, anim.Get(dodgeClip).String);
                This().anim.SetFloat("DodgeSpeed", (float)anim.Get("speed").Number * speed);
                This().anim.Play("Base Layer.Dodge");
                This().anim.Update(0.001f);
            }
            public void PlayMisc(Table anim)
            {
                This().SetOneAnimClip(Animations.MISC, anim.Get("clip").String);
                This().anim.SetFloat("MiscAnimSpeed", (float)anim.Get("speed").Number);
                This().anim.Play("Base Layer.MiscAnim");
                This().anim.Update(0.001f);
            }
            public bool Playing(Animations anim)
            {
                return This().ActionInProgress(This().AnimToStateName(anim));
            }
            public float NormalizedTime(Animations anim)
            {
                AnimatorStateInfo info;
                if (This().GetActionInProgress(This().AnimToStateName(anim), out info))
                    return info.normalizedTime;
                else
                    return 0f;
            }
            public void Stop()
            {
                This().StopAnim();
            }
            public void SetRagdollMode(RagdollModes mode)
            {
                if ((int)mode != (int)script.data.ragdollMode)
                {
                    switch (mode) {
                        case RagdollModes.NONE:
                            This().UndoRagdoll();
                            break;
                        case RagdollModes.FULL:
                            This().BecomeRagdoll(false);
                            break;
                        case RagdollModes.APPENDAGES:
                            This().BecomeRagdoll(true);
                            break;
                    }
                }
            }
            public RagdollModes GetRagdollMode()
            {
                return (RagdollModes)(int)script.data.ragdollMode;
            }
            public void ApplyForceToRagdoll(Table force)
            {
                if (script.data.ragdollMode == RagdollMode.full)
                    This().ApplyForceToRagdoll(script.TableToVector3(force));
            }
            public void PlayDamageEffect()
            {
                This().dissolve[1].TriggerDissolve();
            }
            public void LookAt(Table pos)
            {
                This().headLook.LookAt(script.TableToVector3(pos));
            }
            public void LookForward()
            {
                This().headLook.LookForward();
            }
        }
    }
}
