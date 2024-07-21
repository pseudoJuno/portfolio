using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using Pathfinding;

namespace CrawlerEngine
{
    public class CECollision : MonoBehaviour
    {
        CEMain main;
        CharacterController characterController;
        public CapsuleCollider crawlerHitCollider;
        public PhysicMaterial physMat;
        RaycastModifier raycastModifier;
        RepelObjects repelObjects;

        LayerMask raycastMask;

        bool blockingPath = false;
        Vector3 blockedPos;
        float blockDiameter = 1f;
        int blockPenalty;
        float radius = 0.5f;

        float stopDistanceWhenBlocked = 1f;

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;
            characterController = GetComponent<CharacterController>();
            raycastModifier = GetComponent<RaycastModifier>();
            repelObjects = GetComponentInChildren<RepelObjects>();

            raycastMask = 1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                1 << LayerMask.NameToLayer("immovableObject") |
                1 << LayerMask.NameToLayer("object") |
                1 << LayerMask.NameToLayer("door");

            blockPenalty = main.movement.seeker.tagPenalties[1];
        }

        public void EnableCharacterCollider(bool enable)
        {
            characterController.enabled = enable;
        }
        bool crawlerHitColliderEnable = true;
        public void EnableCrawlerHitCollider(bool enable)
        {
            crawlerHitColliderEnable = enable;
            crawlerHitCollider.enabled = crawlerHitColliderEnable && !main.movement.MovementType().castingImmunity;
        }

        bool collidedWithMap = false;

        public void GetPushed(Vector3 pos, float fromDistance, float speedMulti = 1f, bool ifFartherFromPlayer = false)
        {
            float dist = Vector3.Distance(pos, main.script.data.position);
            if (dist <= fromDistance)
            {
                if (!ifFartherFromPlayer ||
                    Vector3.Distance(main.script.data.position, LevelManager.objs.localPlayer.transform.position) >=
                    Vector3.Distance(pos, LevelManager.objs.localPlayer.transform.position))
                {
                    Vector3 dir = Vector3.Normalize(main.script.data.position - pos);
                    float speed = (fromDistance - dist) / fromDistance + 0.1f;
                    main.movement.pathfinder.Move(dir * speed * speedMulti * Time.deltaTime * main.movement.movementTimeScale);
                }
            }
        }

        public void PushPlayer()
        {
            PlayerScript p = LevelManager.objs.localPlayer;
            Vector3 pos = p.transform.position;
            float dist = Vector3.Distance(pos, main.script.data.position);
            float fromDistance = radius + PlayerCombat.playerRadius;
            if (dist <= fromDistance)
            {
                Vector3 dir = Vector3.Normalize(pos - main.script.data.position);
                float speed = Mathf.Pow((fromDistance - dist) / fromDistance, 0.5f) * 6f;
                p.AddCollisionPush(dir * speed * Time.deltaTime);
            }
        }

        public void Update()
        {
            crawlerHitCollider.enabled = crawlerHitColliderEnable && !main.movement.MovementType().castingImmunity;

            //set radius
            radius = main.movement.MovementType().characterRadius;
            //main.movement.pathfinder.radius = 0.4f;
            crawlerHitCollider.radius = radius;
            blockDiameter = radius * 2f;// + 1f;
            repelObjects.SetTriggerRadius(radius);
            //raycastModifier.thickRaycastRadius = 0.2f;

            if (main.Alive())
                PushPlayer();
        }
        float blockUpdateTimer = 0f;
        public void LateUpdate()
        {
            if (main.Alive() && !main.Ragdoll() && main.script.data.parent == null)
            {

                if (Blocked(transform.position))
                {
                    //if we've accidentally ended up on a blocked node, we get pushed away by blocking crawlers
                    //foreach (CEScript c in main.script.crawlerManager.crawlers)
                    //{
                    //    if (c.data.state != CEData.Exposed.States.ABSTRACTED &&
                    //        c != main.script &&
                    //        c.data.alive &&
                    //        c.main.collision.BlockingPath())
                    //    {
                    //        GetPushed(c.data.position, blockDiameter);
                    //    }
                    //}
                }
                else
                {
                    //update blocking
                    if (BlockingConditions())
                    {
                        if (!blockingPath) BlockPath();
                    }
                    else
                    {
                        if (blockingPath) UnblockPath();
                    }

                    //update block position
                    if (blockingPath && Vector3.Distance(transform.position, blockedPos) > 0.1f)
                    {
                        blockUpdateTimer = blockUpdateTimer + Time.deltaTime;
                        if (blockUpdateTimer >= 1f)
                        {
                            UnblockPath();
                            BlockPath();
                            blockUpdateTimer = 0f;
                        }
                    }
                    else
                    {
                        blockUpdateTimer = 0f;
                    }
                }
            }
            else
            {
                if (blockingPath)
                    UnblockPath();
                //if (rvo.enabled)
                //    rvo.enabled = false;
            }
            collidedWithMap = false;
        }

        public void OnDestroy()
        {
            if (blockingPath)
                UnblockPath();
        }

        Vector3 playerCombatPos;
        bool fightingWithPlayer = false;
        public bool BlockingConditions()
        {
            return  main.movement.exposed.DestinationReached() ||
                fightingWithPlayer ||
                !main.actions.actionAllowsMovement ||
                main.script.data.Is(CEData.StatusFX.SHACKLED);
        }

        public bool BlockingPath() { return blockingPath; }
        public void BlockPath()
        {
            if (blockingPath || AstarPath.active == null)
                return;

            blockingPath = true;
            Bounds blockedBounds = new Bounds(transform.position, Vector3.one * blockDiameter);
            blockedPos = blockedBounds.center;
            main.lvl.BlockArea(this, blockedBounds, true, blockDiameter);

            main.movement.seeker.tagPenalties[1] = 0;
        }
        public void UnblockPath()
        {
            if (!blockingPath || AstarPath.active == null)
                return;

            blockingPath = false;
            main.lvl.UnblockArea(this);

            main.movement.seeker.tagPenalties[1] = blockPenalty;
        }

        public bool Blocked(Vector3 pos)
        {
            if (BlockingPath() || main.movement.pathfinder.GetPath() == null)
                return false;

            for (int i = 3; i < main.movement.pathfinder.GetPath().path.Count - 3; i++)
            {
                if (Vector3.Distance(transform.position, (Vector3)main.movement.pathfinder.GetPath().path[i].position) > stopDistanceWhenBlocked + radius)
                    return false;
                if (main.movement.pathfinder.GetPath().path[i].Tag == 1)
                    return true;
            }
            return false;
        }

        //getting pushed
        public void UpdatePush()
        {
            foreach (CEScript c in main.script.crawlerManager.crawlers)
            {
                if (c.data.state != CEData.Exposed.States.ABSTRACTED && c != main.script && c.data.alive && c.data.parent == null)
                {
                    float pushRadius = main.movement.MovementType().characterRadius + c.main.movement.MovementType().characterRadius;
                    GetPushed(c.data.position, pushRadius, 2f, false);
                }
            }
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CECollision This() { return script.main.collision; }

            public bool LinecastObstacles(Table pos1, Table pos2, float height)
            {
                Vector3 vpos1 = script.TableToVector3(pos1);
                Vector3 vpos2 = script.TableToVector3(pos2);
                return Physics.Linecast(new Vector3(vpos1.x, height, vpos1.z), new Vector3(vpos2.x, height, vpos2.z), This().raycastMask);
            }
            public bool ThickLinecastObstacles(Table pos1, Table pos2, float height)
            {
                float radius = 0.25f;
                //float height = 0.75f;
                Vector3 vpos1 = script.TableToVector3(pos1);
                vpos1 = new Vector3(vpos1.x, height, vpos1.z);
                Vector3 vpos2 = script.TableToVector3(pos2);
                vpos2 = new Vector3(vpos2.x, height, vpos2.z);
                return Physics.SphereCast(vpos1, radius, vpos2 - vpos1, out RaycastHit hit, Vector3.Distance(vpos1, vpos2), This().raycastMask);
            }
            public Table ThickRaycastDirection(Table pos1, float height, Table dir, float maxDistance)
            {
                float radius = 0.25f;
                Vector3 vpos1 = script.TableToVector3(pos1);
                Vector3 vdir = script.TableToVector3(dir);
                RaycastHit hit;
                if (Physics.SphereCast(
                    new Vector3(vpos1.x, height, vpos1.z),
                    radius, new Vector3(vdir.x, 0f, vdir.z),
                    out hit,
                    maxDistance,
                    This().raycastMask))
                {
                    return script.Vector3ToTable(new Vector3(hit.point.x, 0.1f, hit.point.z));
                }
                else
                {
                    return script.Vector3ToTable(
                        new Vector3(vpos1.x, 0.1f, vpos1.z) +
                        new Vector3(vdir.x, 0f, vdir.z).normalized * maxDistance);
                }
            }
            public void GetPushed()
            {
                This().UpdatePush();
            }
            public bool Collided()
            {
                return This().collidedWithMap;
            }
        }
    }
}
