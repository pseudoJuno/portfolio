using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using UnityStandardAssets.Utility;
using UnityEngine.InputSystem;

public class LSFPSController : MonoBehaviour
{
    GameManager gameMgr;
    PlayerScript player;
    Camera cam;
    CharacterController cc;
    CollisionFlags collisionFlags;

    public CurveControlledBob headBob = new CurveControlledBob();
    public LerpControlledBob jumpBob = new LerpControlledBob();

    public AnimationCurve gamepadAimCurve;

    public AudioSource audioSource;
    public float stepInterval = 2f;
    public float runStepMulti = 0.7f;
    public float stickToGroundForce = 10f;
    public float gravityMultiplier = 1f;
    public float jumpSpeed = 2f;

    public MyMouseLook mouseLook;

    float running = 0f;
    float crouch = 0f;
    float crouchAmount = 0.3f;
    float runningStraight = 0f;
    bool jump = false;
    bool jumping = false;
    float stepCycle;
    float nextStep;
    Quaternion additiveHeadRotation = Quaternion.identity;
    public bool Running() { return running > 0.5f; }
    public float RunningState() { return running; }
    public bool Crouching() { return crouch > 0.2f; }
    public float CrouchState() { return crouch; }
    public Camera GetCam() { return cam; }

    LayerMask mask;

    // Start is called before the first frame update
    public void Init(GameManager gameMgr, PlayerScript player)
    {
        mask = 1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
            1 << LayerMask.NameToLayer("immovableObject") |
            1 << LayerMask.NameToLayer("door");

        this.gameMgr = gameMgr;
        this.player = player;
        cam = player.playerCam;
        cc = player.crController;
        headBob.Setup(cam, stepInterval);
        stepCycle = 0f;
        nextStep = stepInterval / 2f;
        jumping = false;
        startLandingTimer = 1f;

        InitRot();
        mouseLook.UpdateCursorLock(true);
    }

    public void InitRot()
    {
        mouseLook.Init(transform, cam.transform);
    }

    //INPUT
    Vector2 moveInput = Vector2.zero;
    Vector2 adjustedMoveInput = Vector2.zero;
    public Vector2 GetMoveInput() { return adjustedMoveInput; }
    public Vector3 GetMoveVector() { return transform.forward * GetMoveInput().y + transform.right * GetMoveInput().x; }
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    public Vector2 GetLookInput(Vector2 added)
    {
        return (new Vector2(gamepadLookInput.x, gamepadLookInput.y * 0.5f) + added) * 150f * Time.deltaTime + mouseLookInput;
    }
    Vector2 gamepadLookInput = Vector2.zero;
    public Vector2 GetGamepadLookInput() { return gamepadLookInput; }
    void OnGamepadLook(InputValue value)
    {
        Vector2 vector = value.Get<Vector2>();
        float magnitude = vector.magnitude;
        Vector2 normalizedVector = vector.normalized;
        gamepadLookInput = normalizedVector * gamepadAimCurve.Evaluate(magnitude) * 1f * GameManager.joystickSensitivity;
    }
    Vector2 mouseLookInput = Vector2.zero;
    public Vector2 GetMouseLookInput() { return mouseLookInput; }
    void OnMouseLook(InputValue value)
    {
        mouseLookInput = value.Get<Vector2>() * 0.08f * GameManager.mouseSensitivity;
    }
    bool runInput = false;
    void OnRun(InputValue value)
    {
        runInput = value.isPressed;
        runToggled = false;
    }
    bool runToggled = false;
    void OnRunToggle()
    {
        runToggled = !runToggled;
    }
    bool crouchInput = false;
    void OnCrouch(InputValue value)
    {
        crouchInput = value.isPressed;
        crouchToggled = false;
    }
    bool crouchToggled = false;
    void OnCrouchToggle()
    {
        crouchToggled = !crouchToggled;
    }

    public Vector3 DivertDirectionAlongCollisionSurface(Vector3 move)
    {
        // get a normal for the surface that is being touched to move along it
        RaycastHit hitInfo;
        Physics.SphereCast(
            transform.position,
            cc.radius,
            Vector3.down,
            out hitInfo,
            cc.height / 2f,
            mask,
            QueryTriggerInteraction.Ignore);
        return Vector3.ProjectOnPlane(move, hitInfo.normal);
    }

    bool staggering = false;
    public bool Staggering() { return staggering; }
    public float StaggerInfluence(float pow = 4f)
    {
        return !staggering ? 0f : 1f - Mathf.Pow(1f - staggerTimer / staggerDuration, pow);
    }
    public Vector3 StaggerVector() { return staggerDir; }
    Vector3 staggerDir;
    float staggerSpeed = 0f;
    float staggerTimer = 0f;
    float staggerDuration = 0f;
    float staggerBobMulti = 1f;
    float staggerForce = 1f;
    public void Stagger(Vector3 dir, float duration, float force)
    {
        if (player.Dead())
            return;
        staggering = true;
        staggerDir = Vector3.Scale(dir, new Vector3(1f, 0f, 1f)).normalized;
        staggerDuration = duration;
        staggerTimer = duration;
        staggerForce = force;
    }
    void UpdateStagger()
    {
        if (staggering)
        {
            staggerTimer = Mathf.Max(0f, staggerTimer - Time.deltaTime);
            float progress = 1f - staggerTimer / staggerDuration;
            staggerSpeed = (1f - Mathf.Pow(progress, 4f)) * 1.25f;
            ForceCrouch(Mathf.Lerp(0.5f * staggerForce, 0f, progress));
            staggerBobMulti = Mathf.Lerp(3f, 1f, Mathf.Pow(progress, 4f));

            if (staggerTimer == 0f)
                staggering = false;
        } else
        {
            staggerSpeed = 0f;
            staggerBobMulti = 1f;
        }
    }
    public float GetHeadBobPhase()
    {
        return headBob.GetCyclePos();
    }

    // Update is called once per frame
    Vector3 moveVector;
    bool previouslyGrounded = false;
    float startLandingTimer;
    float jumpCooldown;
    float runToggleGraceTimer = 0f;
    void Update()
    {
        //one stick controls
        Vector2 addedLookInput = Vector2.zero;
        if (!gameMgr.oneStickControls)
        {
            adjustedMoveInput = moveInput;
        }
        else
        {
            adjustedMoveInput = new Vector2(0f, moveInput.y);
            if (moveInput != Vector2.zero)
            {
                float yDiff = player.transform.InverseTransformDirection(
                    player.playerCam.transform.rotation * new Vector3(0f, 0.15f, 1f)).y;
                float xLook = Mathf.Sign(moveInput.x) * Mathf.Pow(Mathf.Abs(moveInput.x), 2f);
                addedLookInput = new Vector2(xLook, -yDiff * 0.5f) * 1f * GameManager.joystickSensitivity;
            }
        }

        //looking
        Vector2 lookInput = Vector2.zero;
        if (!player.Dead() &&
            !player.splinterAnim.Visible() &&
            player.physicsInteraction.PhysicsInteractionAllowsTurning() &&
            !player.blockInputUntilInteractButtonReleased)
        {
            lookInput = GetLookInput(addedLookInput);
        }
        mouseLook.LookRotation(transform,
            cam.transform,
            lookInput,
            1f * Mathf.Lerp(1f, 0.25f, (player.combat.CasterAimed() || player.combat.Blocking() ? 1f : player.combat.MeleeAnimFade())),
            additiveHeadRotation);

        //landing a jump
        startLandingTimer = Mathf.Max(0f, startLandingTimer - Time.deltaTime);
        jumpCooldown = Mathf.Max(0f, jumpCooldown - Time.deltaTime);
        if (!previouslyGrounded && cc.isGrounded && startLandingTimer == 0f)
        {
            StartCoroutine(jumpBob.DoBobCycle());
            PlayLanding();
            moveVector.y = 0f;
            jumping = false;
            jumpCooldown = 0.1f;
        }
        if (!cc.isGrounded && !jumping && previouslyGrounded)
        {
            moveVector.y = 0f;
        }
        previouslyGrounded = cc.isGrounded;

        //staggering
        UpdateStagger();

        //moving and jumping
        //adjustedMoveInput = moveInput;// Vector2.MoveTowards(lerpedMoveInput, moveInput, Time.deltaTime * 8f);
        float moveSpeed = Mathf.Min(player.combat.CasterAimed() ?
            player.aimSpeed :
            GetCurrentSpeed(), Crouching() ? player.crouchSpeed : GetCurrentSpeed());

        float speed = Mathf.Lerp((moveSpeed *
            (player.Dead() || player.splinterAnim.Visible() || !player.physicsInteraction.PhysicsInteractionAllowsMovement() ? 0f : 1f) *
            player.getHitEffect.movementSpeedMultiplier *
            player.physicsInteraction.MovementSpeedMulti() *
            player.combat.GetMeleeSlowdown() *
            player.combat.GetBlockingSlowdown() *
            player.combat.GetFreezeTimescale()), staggerSpeed, StaggerInfluence(2f));

        Vector3 move = Vector3.Lerp(GetMoveVector(), StaggerVector(), StaggerInfluence(2f));
        //move = DivertDirectionAlongCollisionSurface(move);
        moveVector.x = move.x * speed;
        moveVector.z = move.z * speed;
        if (cc.isGrounded)
        {
            moveVector.y = -stickToGroundForce;
            if (jump)
            {
                moveVector.y = jumpSpeed;
                PlayJump();
                jump = false;
                jumping = true;
            }
        }
        else
        {
            moveVector += Physics.gravity * gravityMultiplier * Time.deltaTime;
        }
        if (cc.enabled)
        {
            Vector3 moveDelta = moveVector * Time.deltaTime;
            moveDelta += (player.physicsInteraction.PhysicsInteractionMove() +
                player.playerCollision.fixedUpdatePushForce +
                player.GetPushVelocity()) * Time.deltaTime +
                player.GetCollisionPush();

            player.ResetCollisionPush();
            collisionFlags = cc.Move(moveDelta);
        }
        //float runStepMulti = Staggering() ? 1f : Mathf.Lerp(1f, 1.15f, running);
        ProgressStepCycle(speed * /*runStepMulti * */staggerBobMulti);
        UpdateCameraBob(speed * /*runStepMulti * */staggerBobMulti);
        UpdateFootstepPitch();

        //running
        bool run = !player.combat.CasterAimed() && !player.combat.Blocking() && !Crouching() && (runInput || runToggled);
        if (run) player.SetTutorialDone(EventData.TutorialText.run);
        runningStraight = Mathf.Clamp((Vector3.Dot(transform.forward, cc.velocity.normalized) + 0.5f) / 1.5f, 0f, 1f);
        running = Mathf.Clamp(
            Mathf.MoveTowards(running, run ? 1f : 0f, Time.deltaTime / player.runAccelerationTime),
            0f,
            runningStraight);
        //fov effect
        player.playerCam.fieldOfView = Mathf.Lerp(
            player.playerCam.fieldOfView,
            GameManager.fovSetting - (player.combat.CasterAimed() ? 10f : 0f) - (player.combat.Blocking() ? 5f : 0f),
            Time.deltaTime * 10f);

        if (moveInput == Vector2.zero)
            runToggled = false;
        if (runToggled && runningStraight <= 0.33f)
        {
            runToggleGraceTimer = runToggleGraceTimer + Time.deltaTime;
            if (runToggleGraceTimer > 0.5f)
                runToggled = false;
        } else
        {
            runToggleGraceTimer = 0f;
        }

        //crouching
        if (crouchInput || crouchToggled)
        {
            SetCrouch(Mathf.Lerp(crouch, crouchAmount, 1f - Mathf.Pow(0.5f, Time.deltaTime * 15f)));
            player.SetTutorialDone(EventData.TutorialText.crouch);
        }
        else
        {
            SetCrouch(Mathf.Lerp(crouch, 0f, 1f - Mathf.Pow(0.5f, Time.deltaTime * 15f)));
        }
        preForceCrouch = false;
    }

    public float GetCurrentSpeed()
    {
        return Mathf.Lerp(
            Mathf.Lerp(player.backwardsWalkSpeed, player.walkSpeed, runningStraight),
            player.runSpeed,
            running);
    }

    public void SetCCHeight(float height)
    {
        if (cc.height != height)
        {
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
        }
    }
    public float GetCrouch()
    {
        return crouch;
    }
    public void SetCrouch(float c, bool forced = false)
    {
        if (!preForceCrouch || forced)
        {
            crouch = c;
        }
    }
    bool preForceCrouch = false;
    public void ForceCrouch(float c)
    {
        SetCrouch(c, true);
        preForceCrouch = true;
    }

    public void SetMouseSmooth(bool smooth)
    {
        mouseLook.smooth = smooth;
    }

    public void LockCursor(bool doLock = true)
    {
        mouseLook.UpdateCursorLock(doLock);
    }

    public void LookAtPos(Vector3 pos, bool lockY = false, bool instant = false, float speed = 900f)
    {
        mouseLook.LookAtPos(transform, cam.transform, pos, lockY, additiveHeadRotation, instant, speed);
    }
    public void SetAdditiveHeadRotation(Quaternion headRot)
    {
        additiveHeadRotation = headRot;
    }
    public Quaternion GetAdditiveHeadRotation()
    {
        return additiveHeadRotation;
    }

    void ProgressStepCycle(float speed)
    {
        if (cc.velocity.sqrMagnitude > 0 && (moveVector.x != 0 || moveVector.y != 0))
        {
            stepCycle += (cc.velocity.magnitude + (speed * (!Running() ? 1f : runStepMulti))) * Time.deltaTime;
            player.anim.SetViewModelWalk(true);
            player.anim.SetViewModelStepCycle(1f - Mathf.Clamp((nextStep - stepCycle) / stepInterval, 0f, 1f));
        }
        else
        {
            player.anim.SetViewModelWalk(false);
        }

        if (!(stepCycle > nextStep))
        {
            return;
        }

        nextStep = stepCycle + stepInterval;

        PlayFootStep();
    }

    float bobTransition = 0f;
    Vector3 bobPos = Vector3.zero;
    void UpdateCameraBob(float speed)
    {
        bool bob = cc.velocity.magnitude > 0 && cc.isGrounded;
        bobTransition = Mathf.MoveTowards(bobTransition, bob ? 1f : 0f, Time.deltaTime * 4f);
        if (bob)
        {
            bobPos = headBob.DoHeadBob(
                cc.velocity.magnitude + (speed * (!Running() ? 1f : runStepMulti)),
                staggerBobMulti,
                LevelManager.objs.localPlayer.GetCamLocalPos());
        }
        cam.transform.localPosition = Vector3.Lerp(
            LevelManager.objs.localPlayer.GetCamLocalPos(),
            bobPos,
            bobTransition) - Vector3.up * jumpBob.Offset();
    }

    int preStep = -1;
    private void PlayFootStep()
    {
        if (!cc.isGrounded)
        {
            return;
        }
        AudioClip[] footsteps = gameMgr.contentMgr.GetFootsteps(player.walkSurface);
        float vol = player.walkSurface == SubstanceLevel.LevelMaterial.WalkSurface.deepWater ? 0.15f : 0.2f;
        preStep = GetRandomClip(footsteps, preStep);
        AudioClip clip = footsteps[preStep];
        audioSource.PlayOneShot(clip, vol * Mathf.Lerp(Mathf.Lerp(1f, 1.75f, running), 4f, StaggerInfluence()));
    }
    void UpdateFootstepPitch()
    {
        audioSource.pitch = Mathf.Lerp(1f, 1.1f, running);
    }
    int GetRandomClip(AudioClip[] clips, int preClip)
    {
        return (int)Mathf.Repeat(Random.Range(preClip + 1, clips.Length + preClip), clips.Length);
    }
    private void PlayJump()
    {
        AudioClip[] footsteps = gameMgr.contentMgr.GetFootsteps(player.walkSurface);
        preStep = GetRandomClip(footsteps, preStep);
        audioSource.PlayOneShot(footsteps[preStep], 0.2f);
    }
    private void PlayLanding()
    {
        AudioClip[] footsteps = gameMgr.contentMgr.GetFootsteps(player.walkSurface);
        preStep = GetRandomClip(footsteps, preStep);
        audioSource.PlayOneShot(footsteps[preStep], 0.2f);
        nextStep = stepCycle + 0.5f;
    }
}
