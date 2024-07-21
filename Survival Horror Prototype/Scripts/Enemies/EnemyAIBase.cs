using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using System.Linq;
using SubstanceLevel;

namespace SymptomAI
{
    public class EnemyAIBase : SLObject
    {
        protected string enemyName;
        public string GetName() { return enemyName; }

        protected EnemySFX sfx;
        public EnemySFX GetSFX() { return sfx; }
        protected Animator anim;
        protected CharacterController cc;
        protected RepelObjects repel;
        protected PlayerScript player;
        protected EnemyWeakSpot[] weakSpots;
        public EnemyWeakSpot[] GetWeakSpots() { return weakSpots; }

        [HideInInspector] public Transform root;
        [HideInInspector] public List<Transform> bones = new List<Transform>();
        [HideInInspector] public Vector3 boneForwardVector = Vector3.down;
        [HideInInspector] public bool flipLBoneVectors = false;
        [HideInInspector] public bool flipRBoneVectors = false;

        public Transform head;
        public float doorwayCheckRate = 0.25f;
        public AIPath intermediaryPath;
        public AIPath targetPath;
        public float forwardSpeedMulti = 1f;
        public float strafeSpeedMulti = 1f;
        public float rotationSpeed = 4f;
        public float aggroDistance = 8f;
        public float hp = 60f;
        public float dmg = 10f;
        public float hitForce = 1f;
        public int droppedMana = 3;
        public bool combatMusic = true;

        public bool Dead() { return hp == 0f; }
        public void TakeDamage(float dmg, bool doAnim, Vector3 pos, bool weakSpotHit, bool weakSpotDestroyed = false)
        {
            if (hp == 0f)
                return;
            if (!aggroed)
                Aggro();
            hp = Mathf.Max(0f, hp - dmg);
            if (hp == 0f)
            {
                if (anim.HasState(0, Animator.StringToHash("Base Layer.Death")))
                    anim.CrossFade("Base Layer.Death", 0.1f);
                else
                    BecomeRagdoll();

                sfx.PlayDeath();

                DropMana();

                intermediaryPath.enabled = false;
                targetPath.enabled = false;
                cc.enabled = false;
                repel.gameObject.SetActive(false);
                player.lvl.enemies.Remove(this);
                enabled = false;
            } else
            {
                float side = transform.InverseTransformPoint(pos).x;
                if (doAnim)
                    GetHit(side);
                    GetHit(side);
                ReactToHit(side, weakSpotHit, weakSpotDestroyed);
            }
        }

        public void DropMana()
        {
            for (int i = 0; i < droppedMana; i++)
            {
                Vector2 rndPos = Random.insideUnitCircle * 0.5f;
                Instantiate(LevelManager.objs.lvl.gameMgr.contentMgr.lootOrb,
                    new Vector3(transform.position.x + rndPos.x, 0.2f, transform.position.z + rndPos.y),
                    Quaternion.identity);
            }
        }

        float hitRadius = 1.2f;
        public void AttackHit(AnimationEvent myEvent)
        {
            if (Vector3.Distance(transform.position + Vector3.up * 1.5f + transform.forward * hitRadius,
                player.transform.position + Vector3.up * 1.5f) < hitRadius)
            {
                float angularDist = Mathf.Abs(Quaternion.Angle(
                        Quaternion.LookRotation(player.playerCam.transform.forward),
                        Quaternion.LookRotation(head.position - player.playerCam.transform.position)));
                bool blockHits = angularDist < 45f && player.combat.Blocking();
                float inflictedDamage = dmg * (blockHits ? (player.combat.BlockParry() ? 0.5f : 0.75f) : 1f);
                if (player.combat.Blocking())
                {
                    if (blockHits)
                        player.combat.BlockHit();
                    else
                        player.combat.EndBlock();
                }
                LevelManager.objs.localPlayer.getHitEffect.GetHit(GetHitEffect.GetHitAnim.hit, hitForce);
                LevelManager.objs.localPlayer.resources.ChangeHealth(-inflictedDamage);
                DidDamageToPlayer();

                if (blockHits)
                    StaggerPlayer(1f, 0.5f);
                else if (myEvent.stringParameter == "stagger")
                    StaggerPlayer(2f, 1f);
            }
        }
        public void StaggerPlayer(float duration, float force)
        {
            if (!player.Dead())
                player.fpsController.Stagger(player.transform.position - transform.position, duration, force);
        }

        public virtual void DidDamageToPlayer() {}

        protected bool aggroed = false;
        public void Aggro() { aggroed = true; }
        protected bool seesPlayer = false;
        public bool SeesPlayer() { return seesPlayer; }
        protected bool lineOfSightToPlayer = false;
        protected bool traversableLineOfSightToPlayer = false;
        public bool LineOfSightToPlayer() { return lineOfSightToPlayer; }
        public bool TraversableLineOfSightToPlayer() { return traversableLineOfSightToPlayer; }

        protected float animSpeed = 1f;
        protected float walkingSpeedMulti = 1f;
        protected float adjustDirection = 0f;
        protected float adjustStrafe = 0f;
        protected float desiredRotation;

        float getHitFadeTime = 0.1f;
        public void GetHit(float side)
        {
            if (DoingAnyAction() || !anim.HasState(1, Animator.StringToHash("Additive.Get Hit Right")))
                return;
            if (side > 0f)
                anim.CrossFade("Additive.Get Hit Right", getHitFadeTime);
            else
                anim.CrossFade("Additive.Get Hit Left", getHitFadeTime);
        }
        public void PlayGetHitAnim(string name)
        {
            anim.CrossFade("Base Layer." + name, getHitFadeTime);
        }

        public void Sway(float side)
        {
            if (DoingAnyAction() || !anim.HasState(2, Animator.StringToHash("Additive2.Sway Right")))
                return;
            if (side > 0f)
                anim.CrossFadeInFixedTime("Additive2.Sway Right", 0.25f);
            else
                anim.CrossFadeInFixedTime("Additive2.Sway Left", 0.25f);
        }

        public virtual void ReactToHit(float side, bool weakSpotHit, bool weakSpotDestroyed) {}

        // Start is called before the first frame update
        public virtual void Init(string name)
        {
            enemyName = name;
            sfx = GetComponent<EnemySFX>();
            anim = GetComponentInChildren<Animator>();
            cc = GetComponent<CharacterController>();
            repel = GetComponentInChildren<RepelObjects>();
            player = LevelManager.objs.localPlayer;
            weakSpots = GetComponentsInChildren<EnemyWeakSpot>();
            foreach (EnemyWeakSpot weakSpot in weakSpots)
                weakSpot.Init();
            SetPathTarget(transform.position);
            //BecomeRagdoll();

            UpdateSLObject();
        }

        // Update is called once per frame
        public virtual void Update()
        {
            //korjataan jos tippuu maan sis‰‰n
            if (transform.position.y < 0f)
                transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);

            //movement
            UpdateMovement();

            //vision
            lineOfSightToPlayer = !Physics.Linecast(head.position, player.playerCam.transform.position, 
                1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                1 << LayerMask.NameToLayer("immovableObject") |
                1 << LayerMask.NameToLayer("door"));
            traversableLineOfSightToPlayer = lineOfSightToPlayer &&
                !Physics.Linecast(head.position, player.playerCam.transform.position, 1 << LayerMask.NameToLayer("object"));
            seesPlayer = lineOfSightToPlayer && Vector3.Distance(transform.position, player.transform.position) <= aggroDistance;
            if (!aggroed)
            {
                if (seesPlayer)
                    Aggro();
            }

            if (aggroed && combatMusic)
                PlayerCombat.SetCombatFlag();
        }

        public bool ReachedDestination()
        {
            return targetPath.hasPath &&
                ((ABPath)targetPath.GetPath()).originalEndPoint == targetPath.destination &&
                targetPath.reachedEndOfPath;
        }

        public bool DestinationVisibleAtDistance(float dist)
        {
            return ReachedDestination() || (Vector3.Distance(transform.position, targetPath.destination) <= dist &&
                !Physics.Linecast(head.position, targetPath.destination + Vector3.up * 0.5f,
                1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                1 << LayerMask.NameToLayer("immovableObject") |
                1 << LayerMask.NameToLayer("door")));
        }

        public float SimpleDistanceToDestination() { return Vector3.Distance(transform.position, targetPath.destination); }

        //public float GetCurrentSpeed() { return animSpeed; }
        public float GetTargetSpeed() { return intermediaryPath.maxSpeed; }
        public bool Aggroed() { return aggroed; }

        List<ObjectScript> doorwayList = new List<ObjectScript>();
        List<Vector3> doorwayDirectionList = new List<Vector3>();
        float doorwayCheckTimer = 0f;
        //float stopThreshold = 0.2f;
        float steeringMulti = 1f;

        public void UpdateMovement()
        {
            if (anim.applyRootMotion)
            {
                intermediaryPath.MovementUpdate(Time.deltaTime, out Vector3 nextPosition, out Quaternion nextRotation);

                var localDesiredVelocity = transform.InverseTransformDirection(intermediaryPath.desiredVelocity);
                localDesiredVelocity = Quaternion.Euler(0, adjustDirection, 0) * localDesiredVelocity * (stopped ? 0f : 1f);

                float speedX = Mathf.Clamp(localDesiredVelocity.x + adjustStrafe, -1f, 1f) * animSpeed * strafeSpeedMulti;
                float speedY = localDesiredVelocity.z * animSpeed * forwardSpeedMulti;
                anim.SetFloat("SpeedX", speedX);
                anim.SetFloat("SpeedY", speedY);

                anim.SetFloat("Walking Speed Multi", walkingSpeedMulti);
            }
            else
            {
                intermediaryPath.canMove = !stopped;
            }

            bool doingAnyAction = DoingAnyAction();
            steeringMulti = Mathf.MoveTowards(steeringMulti, doingAnyAction ? 0f : 1f, Time.deltaTime * (doingAnyAction ? 0.5f : 2f));
            desiredRotation = 0f;
            if (!intermediaryPath.reachedDestination)
            {
                Quaternion newRot;
                if (seesPlayer && traversableLineOfSightToPlayer)
                    newRot = RotateTowardsTarget(player.transform.position, rotationSpeed * GetTargetSpeed() * steeringMulti);
                else
                    newRot = RotateTowardsTarget(intermediaryPath.steeringTarget, rotationSpeed * GetTargetSpeed() * steeringMulti);
                transform.rotation = newRot;
            }
            

            //intermediary path
            doorwayCheckTimer += Time.deltaTime;
            if (doorwayCheckTimer >= doorwayCheckRate && targetPath.hasPath)
            {
                doorwayCheckTimer = 0f;
                doorwayList = GetDoorwaysOnPath(targetPath.GetPath().vectorPath, out doorwayDirectionList);
            }
            int nextClosedDoor = GetNextClosedDoorOnPath();
            if (nextClosedDoor != -1)
            {
                bool locked = doorwayList[nextClosedDoor].BoolState(BoolProperty.Type.locked);
                float approachDistance = locked ? 1.25f : 0.5f;

                Vector3 doorApproachPos = doorwayList[nextClosedDoor].transform.position - doorwayDirectionList[nextClosedDoor] * approachDistance;
                intermediaryPath.destination = doorApproachPos;

                if (Vector3.Distance(transform.position, doorApproachPos) < 0.5f && intermediaryPath.reachedDestination)
                {
                    if (locked)
                        aggroed = false;
                    else
                        doorwayList[nextClosedDoor].GetDoorScript().OpenDoor();
                }
            }
            else
            {
                intermediaryPath.destination = targetPath.destination;
            }
        }

        public void BreakDoor(SLObject SLObj)
        {
            if (!Attacking())
                Attack(SLObj);
        }

        int GetNextClosedDoorOnPath()
        {
            for (int i = 0; i < doorwayList.Count; i++)
                if (!doorwayList[0].DoorFullyOpen())
                    return i;
            return -1;
        }

        Quaternion RotateTowardsTarget(Vector3 target, float speed)
        {
            Vector3 dir = Vector3.Scale(target - transform.position, new Vector3(1f, 0f, 1f));
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                desiredRotation = Mathf.Abs(Quaternion.Angle(transform.rotation, targetRot));
                return Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * speed);
            }
            return transform.rotation;
        }

        public void SetPathTarget(Vector3 position)
        {
            targetPath.destination = position;
            stopped = false;
        }
        public Vector3 GetPathTarget()
        {
            return targetPath.destination;
        }
        public Vector3 GetIntermediaryTarget()
        {
            return intermediaryPath.destination;
        }

        bool stopped = false;
        public void Stop()
        {
            stopped = true;
        }
        public bool Stopped() { return stopped; }

        SLObject attackingDoor = null;
        public void Attack(SLObject attackDoor = null)
        {
            attackingDoor = attackDoor;
            anim.SetTrigger("Attack");
        }
        public bool Attacking()
        {
            return ActionInProgress("Attack");
        }
        public void PlayAggro()
        {
            anim.SetTrigger("Aggro");
        }
        public bool PlayingAggro()
        {
            return ActionInProgress("Aggro");
        }

        public bool ActionInProgress(string actionName)
        {
            string actionStateName = "Base Layer." + actionName;
            bool flagged = anim.GetBool(actionName);
            return (flagged ||
                anim.GetNextAnimatorStateInfo(0).IsName(actionStateName) ||
                anim.GetCurrentAnimatorStateInfo(0).IsName(actionStateName));
        }

        public AnimatorStateInfo GetActionInProgress()
        {
            return anim.GetCurrentAnimatorStateInfo(0);
        }
        public bool DoingAnyAction()
        {
            return !anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Movement") &&
                !anim.GetNextAnimatorStateInfo(0).IsName("Base Layer.Movement");
        }
        public bool Swaying()
        {
            return !anim.GetCurrentAnimatorStateInfo(0).IsName("Additive2.None") &&
                !anim.GetNextAnimatorStateInfo(0).IsName("Additive2.None");
        }
        public void StopSway() { anim.CrossFadeInFixedTime("Additive2.None", 0.5f); }

        public bool GetHitPlaying()
        {
            return !anim.GetCurrentAnimatorStateInfo(0).IsName("Additive.None") &&
                !anim.GetNextAnimatorStateInfo(0).IsName("Additive.None");
        }
        public void StopGetHit() { anim.CrossFadeInFixedTime("Additive.None", 0.25f); }

        public static List<ObjectScript> GetDoorwaysOnPath(List<Vector3> vectorPath, out List<Vector3> dirList)
        {
            dirList = new List<Vector3>();
            List<RaycastHit> hitList = new List<RaycastHit>();
            List<ObjectScript> objList = new List<ObjectScript>();
            //List<Vector3> vectorPath = path.GetVectorPath();
            for (int i = 0; i < vectorPath.Count - 1; i++)
            {
                RaycastHit[] hits;
                Ray r = new Ray(vectorPath[i], vectorPath[i + 1] - vectorPath[i]);
                float rayLength = Vector3.Distance(vectorPath[i + 1], vectorPath[i]);
                hits = Physics.RaycastAll(r, rayLength, 1 << 26).OrderBy(h => h.distance).ToArray();
                foreach (RaycastHit hit in hits)
                {
                    if (hit.transform.tag == "door" && !hitList.Contains(hit))
                    {
                        hitList.Add(hit);
                        objList.Add(hit.transform.parent.GetComponent<ObjectScript>());
                        dirList.Add(-hit.normal);
                    }
                }
            }
            return objList;
        }

        public void BecomeRagdoll()
        {
            intermediaryPath.enabled = false;
            targetPath.enabled = false;
            cc.enabled = false;
            repel.gameObject.SetActive(false);
            anim.enabled = false;
            //create rigidbodies
            foreach (Transform bone in bones)
            {
                Rigidbody rb = bone.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = bone.gameObject.AddComponent<Rigidbody>();
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }
            //create joints
            foreach (Transform bone in bones)
            {
                if (bone != root)
                {
                    CharacterJoint joint = bone.GetComponent<CharacterJoint>();
                    if (joint == null) joint = bone.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = bone.parent.GetComponent<Rigidbody>();
                    joint.swingLimitSpring = new SoftJointLimitSpring() { spring = 1000f, damper = 10000f };
                    joint.swing1Limit = new SoftJointLimit() { limit = 0f, bounciness = 0f };
                    joint.swing2Limit = new SoftJointLimit() { limit = 20f, bounciness = 0f };
                    joint.twistLimitSpring = new SoftJointLimitSpring() { spring = 1000f, damper = 10000f };
                    joint.highTwistLimit = new SoftJointLimit() { limit = 10f, bounciness = 0f };
                    joint.lowTwistLimit = new SoftJointLimit() { limit = -10f, bounciness = 0f };
                }
            }

        }
    }
}