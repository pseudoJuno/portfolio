using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;

namespace CrawlerEngine
{
    public class CESFX : MonoBehaviour
    {
        CEMain main;
        CrawlerSetup setup;

        AudioSource[] audioSource;
        public AudioClip dissolveSound;
        public AudioClip missSound;

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;
            setup = main.setup;

            audioSource = GetComponents<AudioSource>();
            //set movement loop sound
            audioSource[1].clip = setup.moveSoundLoop;
            audioSource[1].time = Random.Range(0f, 4f);
        }

        // Update is called once per frame
        void Update()
        {
            //movement sound loop
            float normalizedSpeed = main.movement.pathfinder.maxSpeed / LevelManager.objs.localPlayer.GetNormalSpeed();
            SetMoveLoopVolume((main.movement.pathfinder.canMove && normalizedSpeed > 0.1f && main.anim.anim.GetBool("Move") ? normalizedSpeed : 0f));
        }

        public void PlaySoundFromClips(AudioClip[] sounds, int index, float vol, float pitch = 1f)
        {
            if (index == 0) index = Random.Range(1, sounds.Length + 1);
            OneShotSound(sounds[index - 1], vol, pitch);
        }
        public void OneShotSound(AudioClip clip, float vol, float pitch = 1f)
        {
            if (clip != null && (main.OCVisible || main.Fighting()))
            {
                audioSource[0].pitch = pitch;
                audioSource[0].PlayOneShot(clip, vol);
            }
        }

        public void SetMoveLoopVolume(float vol)
        {
            audioSource[1].volume = setup.moveSoundLoopVolume * vol * (main.OCVisible ? 1f : 0f);
            if (audioSource[1].isPlaying && vol == 0f && !main.Fighting() && !main.OCVisible)
            {
                audioSource[1].Stop();
            }
            else if (!audioSource[1].isPlaying && vol > 0f)
            {
                audioSource[1].time = Random.Range(0f, audioSource[1].clip.length);
                audioSource[1].Play();
            }
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CESFX This() { return script.main.sfx; }

            public enum Sounds { IDLE, MOVE, AGGRO, ATTACK, MISC }

            public void Play(string soundName, int index, float vol)
            {
                Sounds sound = (Sounds)System.Enum.Parse(typeof(Sounds), soundName);
                Play(sound, index, vol);
            }
            public void Play(Sounds sound, int index, float vol)
            {
                switch (sound)
                {
                    case Sounds.IDLE:
                        This().PlaySoundFromClips(This().setup.idleSounds, index, This().setup.idleSoundsVolume * (vol > 0f ? vol : 1f));
                        break;
                    case Sounds.MOVE:
                        This().PlaySoundFromClips(This().setup.moveSounds, index, This().setup.moveSoundsVolume * (vol > 0f ? vol : 1f));
                        break;
                    case Sounds.AGGRO:
                        This().PlaySoundFromClips(This().setup.aggroSounds, index, This().setup.aggroSoundsVolume * (vol > 0f ? vol : 1f));
                        break;
                    case Sounds.ATTACK:
                        This().PlaySoundFromClips(This().setup.attackSounds, index, This().setup.attackSoundsVolume * (vol > 0f ? vol : 1f));
                        break;
                    case Sounds.MISC:
                        This().PlaySoundFromClips(This().setup.miscSounds, index, This().setup.miscSoundsVolume * (vol > 0f ? vol : 1f));
                        break;
                }
            }
        }
    }
}
