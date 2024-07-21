using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using Pathfinding;
using SubstanceLevel;

namespace CrawlerEngine
{
    public class CEMovement : MonoBehaviour
    {
        CEMain main;
        [System.NonSerialized] public Seeker seeker;
        public CEAIPath pathfinder;
        public CharacterController characterController;
        public Exposed exposed;

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;
            seeker = GetComponent<Seeker>();
            characterController = GetComponent<CharacterController>();
            exposed = new Exposed(main.script);

            destination = transform.position + transform.forward * 0.5f;
            destinationRadius = 1f;
        }

        [System.NonSerialized] public Vector3 destination;
        [System.NonSerialized] public float destinationRadius;
        [System.NonSerialized] public float moveSpeed;
        [System.NonSerialized] public float rotationSpeed;
        [System.NonSerialized] public float statusTimeScale;
        [System.NonSerialized] public float movementTimeScale;
        [System.NonSerialized] public bool canMove = true;

        bool stop = false;
        bool overrideRotation = false;

        bool usePathfinding = true;

        // Update is called once per frame
        void Update()
        {
            //set position in crawler data
            main.script.data.position = transform.position;
            if (main.subRoom != null)
                main.script.data.room = main.subRoom;

            //get movespeed
            PlayerScript p = LevelManager.objs.localPlayer;
            float normalSpeed = p.GetNormalSpeed();
            statusTimeScale = (p.combat.MeleeingTarget() == main ? 0f : p.combat.GetEnemyFreezeTimescale());
            movementTimeScale = p.combat.GetEnemyFreezeTimescale();
            moveSpeed = (!main.Fighting() ? MovementType().roamSpeed : MovementType().aggroSpeed) *
                normalSpeed *
                (main.script.data.Is(CEData.StatusFX.SLOWED) ? 0.5f : 1f);
            rotationSpeed = 8f * MovementType().turningSpeed * movementTimeScale;

            //animator speed
            main.anim.anim.speed = movementTimeScale;

            //velocity
            UpdateVelocity();

            if (seeker.enabled != usePathfinding)
                seeker.enabled = usePathfinding;
            if (usePathfinding)
            {
                //set canMove
                if (main.Alive() &&
                    !main.Ragdoll() &&
                    main.script.data.parent == null &&
                    main.actions.actionAllowsMovement)
                {
                    canMove = !main.collision.Blocked(pathfinder.steeringTarget) &&
                        !stop &&
                        !main.script.data.Is(CEData.StatusFX.SHACKLED) &&
                        !main.anim.ActionInProgress("Stomped");
                    //pathfinder.enabled = true;

                    //rotate
                    if (!overrideRotation)
                        RotateTowardsPath(rotationSpeed * (main.Fighting() ? 1f : 0.33f));
                }
                else
                {
                    canMove = false;
                    //pathfinder.enabled = false;
                }
                stop = false;
                overrideRotation = false;

                pathfinder.canMove = !main.Ragdoll() && main.script.data.parent == null;
                pathfinder.enabled = !main.Ragdoll() && main.script.data.parent == null;

                if (!main.Ragdoll())
                {
                    //set pathfinder variables
                    pathfinder.destination = destination;
                    pathfinder.endReachedDistance = destinationRadius;
                    pathfinder.whenCloseToDestination = (main.targets.SeesPlayer() ?
                        CloseToDestinationMode.Stop :
                        CloseToDestinationMode.ContinueToExactDestination);
                    pathfinder.maxSpeed = (canMove ? moveSpeed : 0f);
                    pathfinder.isStopped = (pathfinder.maxSpeed == 0f);

                    //control movement animation
                    main.anim.SetMove(!exposed.DestinationReached() && canMove/* && moveSpeed > 0.001f*/);
                    main.anim.SetMoveSpeed(moveSpeed * MovementType().moveAnim.speed);
                }
            } else
            {
                pathfinder.canMove = false;
                pathfinder.enabled = false;
            }
        }

        public void AddVelocity(Vector3 velocity)
        {
            main.script.data.velocity += velocity;
        }
        void UpdateVelocity()
        {
            main.script.data.velocity = Vector3.MoveTowards(
                main.script.data.velocity,
                Vector3.zero,
                Time.deltaTime * movementTimeScale);
            if (main.script.data.velocity != Vector3.zero)
                pathfinder.Move(main.script.data.velocity * Time.deltaTime * movementTimeScale);
        }

        void RotateTowardsPath(float speed)
        {
            Vector3 target = pathfinder.steeringTarget;
            Vector3 dir = Vector3.Scale(target - transform.position, new Vector3(1f, 0f, 1f));
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * speed);
            }
        }

        void RotateTowardsPosition(Vector3 pos)
        {
            overrideRotation = true;
            Vector3 dir = Vector3.Scale(pos - transform.position, new Vector3(1f, 0f, 1f));
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        public CEData.MovementType MovementType() { return main.script.data.movementTypes[main.script.data.movementType - 1]; }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CEMovement This() { return script.main.movement; }

            public void SetDestination(Table pos, float radius)
            {
                Vector3 vPos = script.TableToVector3(pos);
                vPos = new Vector3(vPos.x, 0.1f, vPos.z);
                This().destination = vPos;
                This().destinationRadius = radius;

                if (This().main.collision.BlockingPath() && !This().main.collision.BlockingConditions())
                    This().main.collision.UnblockPath();
            }
            public Table GetDestination()
            {
                return script.Vector3ToTable(This().destination);
            }
            public bool DestinationReached()
            {
                if (This().pathfinder.calculatedDestination == This().destination && This().pathfinder.reachedEndOfPath)
                    return true;
                else
                    return Vector2.Distance(
                        new Vector2(This().transform.position.x,
                        This().transform.position.z),
                        new Vector2(This().destination.x, This().destination.z)) <= This().destinationRadius;
            }
            public bool DestinationInAttackRange(float reach)
            {
                return Vector2.Distance(new Vector2(
                    This().transform.position.x,
                    This().transform.position.z),
                    new Vector2(This().destination.x, This().destination.z)) <= This().destinationRadius + reach;
            }
            public int GetMovementType() { return script.data.movementType; }
            public void SetMovementType(int index)
            {
                if (index != script.data.movementType)
                {
                    script.data.movementType = index;
                    script.main.anim.UpdateMovementType();

                    //script.data.ResetMapTarget();
                    //script.data.ClearGroup();
                    script.data.roamAnchor = script.data.position;
                }
            }
            public float CharacterRadius()
            {
                return This().MovementType().characterRadius;
            }
            public void Move(Table speed)
            {
                //This().characterController.SimpleMove(script.TableToVector3(speed));
                if (This().usePathfinding)
                    This().pathfinder.Move(script.TableToVector3(speed) * Time.deltaTime);
                else
                    This().characterController.Move(script.TableToVector3(speed) * Time.deltaTime);
            }
            public Table Direction()
            {
                return script.Vector3ToTable(This().transform.forward);
            }
            public bool DirectionOnLeftSide(Table targetDir)
            {
                return This().transform.InverseTransformDirection(script.TableToVector3(targetDir)).x <= 0f;
            }
            public void RotateTowards(Table pos)
            {
                if (!This().main.Ragdoll())
                    This().RotateTowardsPosition(script.TableToVector3(pos));
            }
            public void RotateTowardsPath()
            {
                if (!This().main.Ragdoll())
                {
                    This().overrideRotation = true;
                    This().RotateTowardsPath(This().rotationSpeed);
                }
            }
            public void Stop()
            {
                This().stop = true;
            }
            public Table FindPositionAtDistance(Table from, float distance)
            {
                Vector3 fromPos = script.TableToVector3(from);
                Vector3 findPos = script.data.position;
                return script.Vector3ToTable(findPos);
            }
        }
    }
}
