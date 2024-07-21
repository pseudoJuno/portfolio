using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SymptomAI
{
    [System.Serializable]
    public class EnemySave
    {
        public string enemyName;
        public Vector3 pos;
        public Vector3 targetPos;
        public Quaternion rot;

        public float hp;
        public List<float> weakSpotHp;

        public EnemySave() { }
        public EnemySave(EnemyAIBase enemy)
        {
            enemyName = enemy.GetName();
            pos = enemy.transform.position;
            targetPos = enemy.GetPathTarget();
            rot = enemy.transform.rotation;
            hp = enemy.hp;
            weakSpotHp = new List<float>();
            foreach (EnemyWeakSpot weakSpot in enemy.GetWeakSpots())
                weakSpotHp.Add(weakSpot.hp);
        }
        public void LoadEnemy(ContentManager contentMgr, LevelManager lvl)
        {
            EnemyAIBase enemy = lvl.SpawnEnemy(enemyName, pos, rot, true, targetPos);
            enemy.hp = hp;
            EnemyWeakSpot[] weakSpots = enemy.GetWeakSpots();
            for (int i = 0; i < weakSpotHp.Count; i++)
            {
                weakSpots[i].hp = weakSpotHp[i];
                if (weakSpots[i].hp <= 0f)
                    weakSpots[i].gameObject.SetActive(false);
            }
        }
    }
}
