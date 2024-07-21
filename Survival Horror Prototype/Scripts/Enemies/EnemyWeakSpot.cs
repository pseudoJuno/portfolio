using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SymptomAI
{
    public class EnemyWeakSpot : MonoBehaviour
    {
        EnemyAIBase enemy;
        public EnemyAIBase ParentEnemy() { return enemy; }
        Collider coll;
        Renderer rend;
        Outline outline;
        Color defColor;
        Transform model;
        Vector3 originalScale;

        public float hp = 10f;
        public float destroyDmg = 10f;
        public int droppedMana = 3;
        public string hitAnimName = "Get Hit";
        public float pulsateSpeed = 1f;
        public float pulsateScale = 1.1f;

        public void Init()
        {
            enemy = GetComponentInParent<EnemyAIBase>();
            coll = GetComponentInChildren<Collider>();
            rend = GetComponentInChildren<Renderer>();
            outline = GetComponentInChildren<Outline>();
            UpdateOutline();
            defColor = rend.material.color;
            model = transform.GetChild(0);
            originalScale = model.localScale;
            SetPulsateTimer();
        }

        public void TakeDamage(float dmg)
        {
            hp = Mathf.Max(0f, hp - dmg);
            if (hp == 0f)
            {
                LevelManager.objs.localPlayer.weakSpotsDestroyed++;
                if (LevelManager.objs.localPlayer.weakSpotsDestroyed >= 2)
                    LevelManager.objs.localPlayer.SetTutorialDone(EventData.TutorialText.enemyWeakspots, true);

                bool doingAction = enemy.DoingAnyAction();
                DropMana();
                enemy.TakeDamage(destroyDmg, doingAction, transform.position, true, true);
                if (!enemy.Dead() && !doingAction)
                {
                    enemy.PlayGetHitAnim(hitAnimName);
                    enemy.GetSFX().PlayHurt();
                }
                gameObject.SetActive(false);
            } else
            {
                Flash();
                Pulsate();
                enemy.TakeDamage(1f, true, transform.position, true);
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

        public void Update()
        {
            if (enemy.Dead())
            {
                coll.enabled = false;
                enabled = false;
            }
            UpdateFlash();
            UpdatePulsate();
            UpdateOutline();
        }

        float outlineAlpha = 0f;
        void UpdateOutline()
        {
            if (outline != null)
            {
                float distAlpha = Mathf.Clamp(
                    10f - Vector3.Distance(LevelManager.objs.localPlayer.transform.position, transform.position),
                    0f, 1f);

                outlineAlpha = Mathf.Lerp(outlineAlpha,
                    LevelManager.objs.localPlayer.ShowingTutorial(EventData.TutorialText.enemyWeakspots) ? distAlpha : 0f,
                    Time.deltaTime);

                outline.OutlineColor = new Color(
                    outline.OutlineColor.r,
                    outline.OutlineColor.g,
                    outline.OutlineColor.b,
                    outlineAlpha * 0.5f);
            }
        }

        float pulsateProgress = 0f;
        float pulsateAnim = 0f;
        float pulsateTimer = 0f;
        bool pulsate = false;
        float pulsateAnimSpeed = 0.5f;
        float pulsateAnimMulti = 2f;
        void SetPulsateTimer() { pulsateTimer = Random.Range(1f, 3f); }
        public void Pulsate()
        {
            pulsate = true;
            SetPulsateTimer();
            pulsateProgress = 0f;
            pulsateAnim = 0f;
        }
        public void UpdatePulsate()
        {
            if (pulsate)
            {
                pulsateProgress = Mathf.MoveTowards(pulsateProgress, 1f, Time.deltaTime * pulsateAnimSpeed);
                pulsateAnim = Mathf.Sin(Mathf.Clamp(pulsateProgress * pulsateAnimMulti, 0f, 1f) * 180f * Mathf.Deg2Rad);
                model.localScale = originalScale * Mathf.Lerp(1f, pulsateScale, pulsateAnim);
                if (pulsateProgress == 1f)
                    pulsate = false;
            } else
            {
                pulsateTimer = Mathf.Max(0f, pulsateTimer - Time.deltaTime * pulsateSpeed);
                if (pulsateTimer == 0f)
                    Pulsate();
            }
        }

        float flashTimer = 0f;
        float flashValue = 0f;
        public void Flash()
        {
            flashTimer = 0.33f;
        }

        public void UpdateFlash()
        {
            flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
            if (flashTimer > 0f || flashValue > 0.01f)
                flashValue = Mathf.Lerp(flashValue, flashTimer > 0f ? 1f : 0f, Time.deltaTime * 4f);
            else
                flashValue = Mathf.MoveTowards(flashValue, 0f, Time.deltaTime);
            SetEmission(flashValue + pulsateAnim * 0.4f);
            SetColor(flashValue + pulsateAnim * 0.4f);
        }

        float currentEmission = 0f;
        public void SetEmission(float value)
        {
            if (value != currentEmission)
            {
                currentEmission = value;
                rend.material.SetColor("_EmissionColor",
                    Color.Lerp(Color.black, new Color(0.33f, 0f, 0f), currentEmission));
            }
        }
        float currentColor = 0f;
        public void SetColor(float value)
        {
            if (value != currentColor)
            {
                currentColor = value;
                rend.material.color = Color.Lerp(defColor, new Color(0.5f, 0f, 0f), currentColor);
            }
        }
    }
}
