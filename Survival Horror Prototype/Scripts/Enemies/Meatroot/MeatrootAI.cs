using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SymptomAI;
using UnityEngine.InputSystem;

public class MeatrootAI : EnemyAIBase
{
    // Start is called before the first frame update
    public override void Init(string name)
    {
        base.Init(name);

        attackTimer = Random.Range(2f, 3f);
    }

    float pathTargetTimer = 0f;
    public void ResetPathTargetTimer() { pathTargetTimer = Random.Range(1f, 5f); }

    float attackTimer = 0f;
    public void ResetAttackTimer() { attackTimer = Random.Range(3f, 5f); }


    float playerDist = 0f;

    float attackDist = 1.7f;

    float speed = 0f;

    // Update is called once per frame
    public override void Update()
    {
        base.Update();

        playerDist = Vector3.Distance(transform.position, player.transform.position);

        if (aggroed)
        {
            SetPathTarget(player.transform.position);
        }
        else
        {
            if (ReachedDestination())
                pathTargetTimer = Mathf.Max(0f, pathTargetTimer - Time.deltaTime);
            if (pathTargetTimer == 0f)
            {
                ResetPathTargetTimer();

                LayerMask mask = 1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                    1 << LayerMask.NameToLayer("immovableObject") |
                    1 << LayerMask.NameToLayer("door");

                float dist = Random.Range(1f, 3f);
                float radius = 0.5f;
                Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * dist;
                Vector3 pos = transform.position + dir;
                if (Physics.SphereCast(transform.position + Vector3.up, radius, dir, out RaycastHit hit, dist + radius, mask))
                    pos = Vector3.Scale(hit.point + hit.normal * radius, new Vector3(1f, 0f, 1f)) + Vector3.up * transform.position.y;
                SetPathTarget(pos);
            }
        }

        float vel = Mathf.Clamp(
            (intermediaryPath.desiredVelocity.magnitude / intermediaryPath.maxSpeed) + (desiredRotation / 45f),
            0f, 1f);
        speed = Mathf.MoveTowards(speed, vel > 0.2f ? vel : 0f, Time.deltaTime * 2f);
        anim.SetFloat("Speed", speed);
        sfx.SetMovementLoopVolume(speed);

        if (!DoingAnyAction())
        {
            //attack
            if (playerDist < attackDist)
            {
                attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
                if (attackTimer == 0f)
                {
                    Attack(1);
                    ResetAttackTimer();
                    return;
                }
            }
        }
    }

    public void Attack(int attack)
    {
        anim.SetTrigger("Attack" + attack);
    }


}
