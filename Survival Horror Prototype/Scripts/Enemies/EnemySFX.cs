using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SymptomAI
{
    public class EnemySFX : MonoBehaviour
    {
        Animator anim;
        EnemyAIBase enemy;

        public AudioSource movementAudio;
        public AudioSource noiseAudio;
        public AudioSource presenceAudio;
        public AudioSource attackAudio;
        public bool footsteps = true;
        public AudioClip[] footstepSounds;
        public AudioClip[] noiseSounds;
        public Vector2 noiseInterval = new Vector2(3f, 6f);
        public AudioClip[] attackSounds;
        public AudioClip hurtSound;
        public AudioClip deathSound;

        float noiseAudioVolume;
        float presenceAudioVolume;
        float noiseTimer = 0f;
        float movementAudioVolume;
        float movementAudioVolMulti = 1f;

        void Awake()
        {
            enemy = GetComponent<EnemyAIBase>();
            anim = GetComponentInChildren<Animator>();

            noiseAudioVolume = noiseAudio.volume;
            noiseAudio.volume = 0f;
            noiseTimer = Random.Range(0.1f, noiseInterval.x);
            presenceAudioVolume = presenceAudio.volume;
            presenceAudio.volume = 0f;

            movementAudioVolume = movementAudio.volume;
            movementAudio.volume = 0f;
            if (!footsteps && movementAudio.clip != null)
            {
                movementAudio.time = Random.Range(0f, movementAudio.clip.length);
            }
        }

        public void SetMovementLoopVolume(float vol)
        {
            movementAudioVolMulti = vol;
        }

        // Update is called once per frame
        int stepPhase = 0;
        float stepWeightThreshold = 0.1f;
        float lineOfSightFade = 0f;
        float actionFade = 1f;
        void Update()
        {
            noiseTimer = Mathf.Max(0f, noiseTimer - Time.deltaTime);

            bool doingAnyAction = enemy.DoingAnyAction();
            lineOfSightFade = Mathf.MoveTowards(lineOfSightFade,
                !enemy.Dead() ? (enemy.LineOfSightToPlayer() ? 1f : (enemy.Aggroed() ? 0.5f : 0f)) : 0f,
                Time.deltaTime * 4f);
            actionFade = Mathf.MoveTowards(actionFade,
                !enemy.Dead() && !doingAnyAction && noiseTimer == 0f ? 1f : 0f,
                Time.deltaTime * (doingAnyAction ? 2f : 0.5f));
            noiseAudio.volume = lineOfSightFade * actionFade * noiseAudioVolume;
            presenceAudio.volume = lineOfSightFade * presenceAudioVolume;
            movementAudio.volume = lineOfSightFade * movementAudioVolMulti * movementAudioVolume;

            if (!enemy.Dead())
            {

                if (footsteps && anim.velocity.magnitude > 0.01f)
                {
                    bool weightOnLeftFoot = anim.pivotWeight < stepWeightThreshold;
                    bool weightOnRightFoot = anim.pivotWeight > 1f - stepWeightThreshold;

                    switch (stepPhase)
                    {
                        case 0:
                            if (weightOnRightFoot)
                            {
                                PlayFootstep();
                                stepPhase = 1;
                            }
                            break;
                        case 1:
                            if (weightOnLeftFoot)
                            {
                                PlayFootstep();
                                stepPhase = 0;
                            }
                            break;
                    }
                }
            }
        }

        int preFootstepClip = -1;
        int GetRandomClip(AudioClip[] clips, int preClip)
        {
            return (int)Mathf.Repeat(Random.Range(preClip + 1, clips.Length + preClip), clips.Length);
        }
        public void PlayFootstep()
        {
            preFootstepClip = GetRandomClip(footstepSounds, preFootstepClip);
            movementAudio.PlayOneShot(footstepSounds[preFootstepClip]);
        }

        int preNoiseClip = -1;
        public void PlayNoise(float vol = 0.2f)
        {
            preNoiseClip = GetRandomClip(noiseSounds, preNoiseClip);
            attackAudio.PlayOneShot(noiseSounds[preNoiseClip], vol);

            noiseTimer = noiseSounds[preNoiseClip].length;
        }

        public void PlayAttack(AnimationEvent myEvent)
        {
            float vol = myEvent.floatParameter != 0f ? myEvent.floatParameter : 1f;
            attackAudio.PlayOneShot(attackSounds[myEvent.intParameter], vol);
        }

        public void PlayHurt()
        {
            if (hurtSound != null)
                attackAudio.PlayOneShot(hurtSound, 0.4f);
        }

        public void PlayDeath()
        {
            if (deathSound != null)
                attackAudio.PlayOneShot(deathSound, 0.5f);
        }
    }
}
