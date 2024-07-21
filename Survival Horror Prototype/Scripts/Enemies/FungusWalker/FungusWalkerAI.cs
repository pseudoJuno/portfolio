using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SymptomAI;
using UnityEngine.InputSystem;

public class FungusWalkerAI : EnemyAIBase
{
    // Start is called before the first frame update
    public override void Init(string name)
    {
        base.Init(name);

        ResetRunAttackTimer();
        attackTimer = Random.Range(3f, 4f);
        ResetReachAttackTimer();
        ResetStumbleTimer();
        strafeTimer = Random.Range(0f, 8f);
    }

    public bool disableActions = false;

    float actionTimer = 0f;
    public void ResetActionTimer() { actionTimer = Random.Range(0.1f, 2f); }

    float attackTimer = 0f;
    public void ResetAttackTimer() { attackTimer = Random.Range(6f, 8f); ResetActionTimer(); }

    float reachAttackTimer = 0f;
    public void ResetReachAttackTimer() { reachAttackTimer = Random.Range(10f, 20f); ResetActionTimer(); }

    float runAttackTimer = 0f;
    public void ResetRunAttackTimer() { runAttackTimer = Random.Range(10f, 20f); ResetActionTimer(); }

    //float lungeTimer = 0f;
    //public void ResetLungeTimer() { lungeTimer = Random.Range(10f, 20f); ResetActionTimer(); }

    float stumbleTimer = 0f;
    public void ResetStumbleTimer() { stumbleTimer = Random.Range(10f, 20f); ResetActionTimer(); }

    float strafeTimer = 0f;
    float strafe = 0f;
    float strafeDir = 0f;
    bool stopStrafeNearWall = false;
    public void ResetStrafeTimer() { strafeTimer = Random.Range(3f, 8f); ResetActionTimer(); }

    float sprint = 0f;
    float sprintSpeed = 0f;

    float attackDist = 2f;
    float stopDist = 1.5f;

    public override void ReactToHit(float side, bool weakSpotHit, bool weakSpotDestroyed)
    {
        if (!weakSpotHit)
            return;
        if (playerDist > attackDist)
        {
            hitSide = side;
            if (!weakSpotDestroyed)
            {
                Invoke("DoRandomAction", 0.25f);
            }
            else
            {
                if (Random.Range(0, 2) == 0)
                    reachAttackTimer = Mathf.Min(reachAttackTimer, Random.Range(1f, 3f));
                else
                    runAttackTimer = Mathf.Min(runAttackTimer, Random.Range(1f, 3f));
            }

        } else
        {
            actionTimer = Mathf.Min(actionTimer, 0.5f);
            attackTimer = Mathf.Min(attackTimer, 0.5f);
            reachAttackTimer = Mathf.Min(reachAttackTimer, 1f);
        }
    }
    float hitSide;
    int preRndAction = -1;
    public void DoRandomAction()
    {
        if (!DoingAnyAction())
        {
            preRndAction = (int)Mathf.Repeat(preRndAction + 1 + Random.Range(0, 2), 3);
            if (preRndAction == 0)
            {
                Sway(hitSide);

                sfx.PlayNoise();
            }
            else if (preRndAction == 1)
            {
                if (Random.Range(0,2) == 0)
                    Lunge();
                else
                    Stumble();
                if (GetHitPlaying())
                    StopGetHit();
            }
            else if (preRndAction == 2)
            {
                Sprint();
            }
        }
    }

    // Update is called once per frame
    float walkSpeed = 1f;
    float playerDist = 0f;
    public override void Update()
    {
        base.Update();

        playerDist = Vector3.Distance(transform.position, player.transform.position);
        bool doingAnyAction = DoingAnyAction();

        if (aggroed)
        {
            SetPathTarget(player.transform.position);

            runAttackTimer = Mathf.Max(0f, runAttackTimer - Time.deltaTime);
            reachAttackTimer = Mathf.Max(0f, reachAttackTimer - Time.deltaTime);
            //lungeTimer = Mathf.Max(0f, lungeTimer - Time.deltaTime);
            if (playerDist > 3f)
                stumbleTimer = Mathf.Max(0f, stumbleTimer - Time.deltaTime);
            strafeTimer = Mathf.Max(0f, strafeTimer - Time.deltaTime);

            if (!doingAnyAction && !disableActions)
            {
                attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);

                actionTimer = Mathf.Max(0f, actionTimer - Time.deltaTime);
                if (actionTimer == 0f)
                {
                    StartNewAction();
                }
            }

            //if too close to player, strafe to avoid blocking the player
            if (playerDist < stopDist)
            {
                if (strafe == 0f)
                    Strafe(false, 3f);
            }
        }
        else if (ReachedDestination())
        {
            Stop();
        }

        //strafe
        strafe = Mathf.Max(0f, strafe - Time.deltaTime);
        if (strafe > 0f)
        {
            if (stopStrafeNearWall && CheckObstacles(Quaternion.Euler(0, adjustDirection, 0) * transform.forward, 0.5f))
                strafe = 0f;
        }
        adjustDirection = Mathf.MoveTowards(adjustDirection, strafe > 0f ? strafeDir : 0f, Time.deltaTime * 90f);
        //sprint
        sprint = Mathf.Max(0f, sprint - Time.deltaTime);
        sprintSpeed = Mathf.MoveTowards(sprintSpeed, sprint > 0f ? 1f : 0f, Time.deltaTime * 4f);

        //walking speed multi
        walkSpeed = Mathf.MoveTowards(walkSpeed,
            (!(seesPlayer && traversableLineOfSightToPlayer) || playerDist > 5f ? 1.5f : 1f) * (strafe > 0f ? 1.25f : 1f),
            Time.deltaTime * 0.25f);
        walkingSpeedMulti = walkSpeed + sprintSpeed;
        //anim speed
        animSpeed = Mathf.MoveTowards(animSpeed, /*strafe > 0f ? 1.5f : */(playerDist < attackDist ? 0.75f : 1f), Time.deltaTime);

        //forward speed
        forwardSpeedMulti = Mathf.MoveTowards(forwardSpeedMulti, (playerDist < stopDist ? 0f : 1f), Time.deltaTime);

        if (doingAnyAction && Swaying())
            StopSway();

        //if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        //    anim.CrossFadeInFixedTime("Additive2.Sway Right", 0.25f);
        //if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        //    anim.CrossFadeInFixedTime("Additive2.Sway Left", 0.25f);

        //if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        //    anim.SetTrigger("Stumble Right");
        //if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        //    anim.SetTrigger("Stumble Left");
        //if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        //    anim.SetTrigger("ReachAttack1");

        //if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        //    GetHit(1);
        //if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        //    GetHit(-1);
        //if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        //    RunAttack(1);
        //if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        //    ReachAttack(1);

        //if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        //    Sprint();

        //float targetDirection = Keyboard.current.leftArrowKey.isPressed ?
        //    90f : (Keyboard.current.rightArrowKey.isPressed ? -90f : 0f);
        //adjustDirection = Mathf.MoveTowards(adjustDirection, targetDirection, Time.deltaTime * 90f);

        //animSpeed = Mathf.MoveTowards(animSpeed,
        //    !ActionInProgress("Stumble Right") && !ActionInProgress("Stumble Left") ? 1f : 0.75f,
        //    Time.deltaTime);
    }

    public void StartNewAction()
    {

        //attack
        if (playerDist < attackDist)
        {
            if (attackTimer == 0f && reachAttackTimer > 0f)
            {
                Attack(1);
                return;
            } else if (attackTimer == 0f && reachAttackTimer == 0f)
            {
                if (Random.Range(0, 2) == 0)
                    Attack(1);
                else
                    ReachAttack(1);
                return;
            }
        }

        //reach attack
        if (playerDist <= 4f)
        {
            if (reachAttackTimer == 0f)
            {
                ReachAttack(1);
                return;
            }
        }

        //run attack
        if (playerDist > 3f)
        {
            if (runAttackTimer == 0f)
            {
                RunAttack(1);
                return;
            }
        }

        ////lunge
        //if (playerDist > attackDist)
        //{
        //    if (lungeTimer == 0f)
        //    {
        //        Lunge();
        //        return;
        //    }
        //}

        //stumble
        if (playerDist > attackDist)
        {
            if (stumbleTimer == 0f)
            {
                if (Random.Range(0, 2) == 0)
                    Stumble();
                else
                    Lunge();
                return;
            }
        }

        //strafe
        if (playerDist < 6f && (player.combat.CasterAimed() || playerDist < 3f))
        {
            if (strafeTimer == 0f)
            {
                Strafe();
                return;
            }
        }
    }

    public bool CheckObstacles(Vector3 dir, float checkDist)
    {
        LayerMask mask = 1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
            1 << LayerMask.NameToLayer("immovableObject") |
            1 << LayerMask.NameToLayer("object") |
            1 << LayerMask.NameToLayer("door");
        return Physics.SphereCast(transform.position + Vector3.up, 0.4f, dir, out RaycastHit hit, checkDist, mask);
    }

    public void Attack(int attack)
    {
        anim.SetTrigger("Attack" + attack);
        ResetAttackTimer();
    }
    public override void DidDamageToPlayer()
    {
        ResetRunAttackTimer();
        ResetReachAttackTimer();
    }
    public void RunAttack(int attack)
    {
        anim.SetTrigger("LungeAttack" + attack);
        ResetAttackTimer();
        ResetRunAttackTimer();
        ResetReachAttackTimer();
    }
    public void ReachAttack(int attack)
    {
        anim.SetTrigger("ReachAttack" + attack);
        ResetAttackTimer();
        ResetRunAttackTimer();
        ResetReachAttackTimer();
    }

    public void Lunge()
    {
        anim.SetTrigger("Lunge");
        ResetStumbleTimer();

        sfx.PlayNoise();
    }

    public void Stumble()
    {
        bool dir = Random.Range(0, 2) == 0;
        anim.SetTrigger(dir ? "Stumble Right" : "Stumble Left");
        ResetStumbleTimer();

        sfx.PlayNoise();
    }

    public void Strafe(bool stopNearWall = true, float duration = -1)
    {
        bool leftClear = !CheckObstacles(-transform.right, 0.5f);
        bool rightClear = !CheckObstacles(transform.right, 0.5f);
        if (leftClear || rightClear)
        {
            strafe = duration == -1 ? Random.Range(2f, 4f) : duration;
            strafeDir = 60f * (leftClear && rightClear ? Mathf.Sign(Random.Range(-1f, 1f)) : (leftClear ? -1f : 1f));
            stopStrafeNearWall = stopNearWall;
            ResetStrafeTimer();
            return;
        }
        else
        {
            ResetStrafeTimer();
        }
    }

    public void Sprint()
    {
        sprint = 2f;

        sfx.PlayNoise();
    }
    
}
