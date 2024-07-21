using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CrawlerEngine;
using SubstanceLevel;
using Pathfinding;
using System.Xml.Serialization;

namespace CrawlerEngine
{
    public class CECrawlerManager : MonoBehaviour
    {
        LevelManager lvl;
        GameManager gameMgr;
        ContentManager contentMgr;
        Seeker seeker;
        CEScript calculatingPathFor = null;

        [System.NonSerialized]
        public List<CEScript> crawlers = new List<CEScript>();
        [System.NonSerialized]
        public List<Projectile> projectiles = new List<Projectile>();
        public class Projectile
        {
            [System.NonSerialized, XmlIgnore] public Transform obj;
            public string projectile;
            public Vector3 pos;
            public Vector3 dir;
            public float speed;
            public float startSpeed;
            public float topSpeed;
            public float dmg;
        }

        LayerMask projectileMask;

        // Start is called before the first frame update
        public void Init(LevelManager lvl1)
        {
            lvl = lvl1;
            gameMgr = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
            contentMgr = gameMgr.contentMgr;
            seeker = GetComponent<Seeker>();

            doorwayMask = 1 << LayerMask.NameToLayer("pathObject");

            projectileMask = 1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                1 << LayerMask.NameToLayer("immovableObject") |
                1 << LayerMask.NameToLayer("object") |
                1 << LayerMask.NameToLayer("door");

        }

        public void Spawn()
        {
            if (gameMgr.enableCrawlers)
            {
                if (gameMgr.SaveOrLevelLoaded())
                {
                    //gameMgr.savegame.LoadCrawlers(this, contentMgr, lvl);
                    //gameMgr.savegame.LoadProjectiles(this);
                }
                else
                {
                    SpawnCrawlers();
                }
            }
        }

        public Room GetRandomRoamableRoom(Room reject)
        {
            Room r;
            do {
                r = lvl.map.indexedRoomList[Random.Range(0, lvl.map.indexedRoomList.Count)];
            } while (r == reject && lvl.map.indexedRoomList.Count > 1);
            return r;
        }

        public uint GetArea(Vector3 pos, int height)
        {
            NNConstraint nn = NNConstraint.Default;
            nn.graphMask = 1 << height;
            return AstarPath.active.GetNearest(pos, nn).node.Area;
        }

        public Vector3 GetNearestPosInArea(Vector3 pos, uint area)
        {
            NNConstraint nn = NNConstraint.Default;
            nn.graphMask = 1 << 1;
            nn.constrainArea = true;
            nn.area = (int)area;
            return (Vector3)AstarPath.active.GetNearest(pos, nn).node.position;
        }

        public float GetPathLength(List<Vector3> vectorPath)
        {
            float tot = 0;
            for (int i = 0; i < vectorPath.Count - 1; i++) tot += Vector3.Distance(vectorPath[i], vectorPath[i + 1]);
            return tot;
        }

        LayerMask doorwayMask;
        public ObjectScript[] GetDoorsOnPath(List<Vector3> path)
        {
            ObjectScript[] pathDoors = new ObjectScript[path.Count];
            for (int n = 0; n < path.Count - 1; n++)
            {
                Vector3 nodePos = path[n];
                Vector3 nextNodePos = path[n + 1];
                RaycastHit hitInfo;
                if (Physics.Linecast(nodePos, nextNodePos, out hitInfo, doorwayMask))
                {
                    pathDoors[n] = hitInfo.transform.parent.GetComponent<ObjectScript>();
                    pathDoors[n + 1] = pathDoors[n];// hitInfo.transform.parent.GetComponent<ObjectScript>();
                }
            }
            return pathDoors;
        }

        public bool GetRandomPointInRoom(Room r, int height, uint area, out Vector3 point)
        {
            NNConstraint nn = NNConstraint.Default;
            nn.graphMask = 1 << height;
            nn.constrainArea = true;
            nn.area = (int)area;

            GraphNode node;
            point = Vector3.zero;

            for (int i = 0; i < 20; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(r.rect.xMin, r.rect.xMax),
                    r.GetRoomPos().y,
                    Random.Range(r.rect.yMin, r.rect.yMax));
                node = AstarPath.active.GetNearest(pos, nn).node;
                if (node != null)
                {
                    point = (Vector3)node.position;
                    point = new Vector3(point.x, r.GetHeight(), point.z);
                    if (point.x >= r.rect.xMin &&
                        point.x <= r.rect.xMax &&
                        point.z >= r.rect.yMin &&
                        point.z <= r.rect.yMax)
                    {
                        return true;
                    }
                    //else
                    //    Debug.Log("node outside of room");
                } else
                {
                    //Debug.DrawRay(r.GetRoomPos(), Vector3.up * 2f, Color.white, 1000f, false);
                }
                //Debug.DrawRay(pos, Vector3.up * 2f, Color.red, 1000f, false);
            }
            return false;
        }

        void SpawnCrawlers()
        {
            if (lvl.eventMgr.loadedGraph.enemyDensity <= 0f)
                return;

            int crawlerCount = (int)Mathf.Ceil(lvl.lootMgr.TotalRoomArea() *
                LootManager.enemiesPerRoomArea *
                lvl.eventMgr.loadedGraph.enemyDensity);
            Debug.Log(crawlerCount);
            List<string> crawlerTypes = new List<string>();
            List<int> weightedSpawnList = lvl.PHMap.loadedGraph.GetWeightedSpawnList();

            if (weightedSpawnList.Count == 0)
                return;

            for (int i = 0; i < crawlerCount; i++)
            {
                string crawler = lvl.PHMap.loadedGraph.GetRndEnemy(weightedSpawnList).name;
                crawlerTypes.Add(crawler);
            }
        }

        public CEScript InstantiateCrawler(string type, Vector3 pos, Quaternion rot, CEScript script = null)
        {
            bool start = false;
            if (script == null)
            {
                script = new CEScript() { crawlerType = type, lvl = lvl, crawlerManager = this };
                script.Init(contentMgr, type);
                script.data.position = pos;
                script.data.room = lvl.GetSubRoom(pos, 0);
                script.data.spawnRoom = script.data.room;
                script.data.roamAnchor = pos;

                crawlers.Add(script);
                start = true;
            }

            CEMain crawler = Instantiate(contentMgr.crawlerTemplate, pos, rot).GetComponent<CEMain>();
            crawler.CollectComponents(script);

            CrawlerSetup crawlerSetup = Instantiate(script.data.model, crawler.transform).GetComponent<CrawlerSetup>();
            CapsuleCollider hitCollSetup = crawlerSetup.transform.GetComponent<CapsuleCollider>();
            if (hitCollSetup != null)
            {
                CapsuleCollider hitColl = crawler.collision.crawlerHitCollider;
                hitColl.center = hitCollSetup.center;
                hitColl.radius = hitCollSetup.radius;
                hitColl.height = hitCollSetup.height;
                hitColl.direction = hitCollSetup.direction;
                Destroy(hitCollSetup);
            }
            LevelManager.SetLayerRecursively(crawlerSetup.gameObject, LayerMask.NameToLayer("corpse"));
            Collider[] colls = crawlerSetup.GetComponentsInChildren<Collider>();
            foreach (Collider coll in colls) coll.enabled = false;
            foreach (FootLockIK footIK in crawler.GetComponentsInChildren<FootLockIK>())
                footIK.gameObject.AddComponent<DitzelGames.FastIK.FastIKFabric>().Setup(footIK);
            crawler.anim.headLook = new HeadLooking(crawler, crawler.GetComponentsInChildren<HeadLook>());
            crawler.setup = crawlerSetup;
            crawler.GetComponent<Animator>().avatar = crawlerSetup.avatar;

            crawler.SLObjectInit(lvl);
            crawler.InitComponents();

            //init ragdoll with delay
            if (script.data.ragdollMode != CEAnimation.RagdollMode.none)
            {
                crawler.parent.initRagdollMode = script.data.ragdollMode;
                script.data.ragdollMode = CEAnimation.RagdollMode.none;
            }

            if (start)
                script.Start();
            return script;
        }

        public void SpawnProjectile(string projectile, Vector3 pos, Vector3 dir, float speed, float topSpeed, float dmg)
        {
            Transform projectilePrefab = contentMgr.GetProjectile(projectile);
            projectiles.Add(new Projectile()
            {
                obj = Instantiate(projectilePrefab,
                pos,
                Quaternion.LookRotation(dir)),
                projectile = projectile,
                speed = speed,
                startSpeed = speed,
                topSpeed = topSpeed,
                dmg = dmg
            });
        }

        
        //called after all other crawler engine scripts (crawlers can get deleted here)
        void Update()
        {
            CEScript c;
            for (int i = 0; i < crawlers.Count; i++)
            {
                c = crawlers[i];
                //Debug.DrawRay(c.data.position, Vector3.up * 2f, Color.white, 0f, false);

                if (c.data.state != CEData.Exposed.States.ABSTRACTED)
                {
                    if (c.data.alive)
                    {
                        if (c.data.parent == null)
                        {
                            if (!c.main.OCVisible && !c.main.Fighting())
                            {
                                c.main.despawnTimer += Time.deltaTime;
                                if (c.main.despawnTimer > 2f)
                                    OffloadCrawler(c.main);
                            }
                            else
                            {
                                c.main.despawnTimer = 0f;
                            }
                        }

                        if (c.IsFlaggedDead())
                        {
                            KillCrawler(c.main);
                        }
                    }
                }
                else
                {
                    if (c.data.parent == null)
                    {
                        if (!LevelManager.objs.localPlayer.Resting() && c.data.room.OCVisible)
                            LoadInCrawler(c, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    }
                }
            }

            //update projectile movement
            PlayerScript p = LevelManager.objs.localPlayer;
            float normalSpeed = p.GetNormalSpeed();
            float statusTimeScale = p.GetPassingTime() + p.combat.GetCastMovement() * 0.75f;
            float movementTimeScale = statusTimeScale + (p.combat.Stomping() ? 0.75f : 0f);
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = projectiles[i];
                float distanceToPlayer = Vector2.Distance(
                    new Vector2(projectile.obj.position.x,projectile.obj.position.z),
                    new Vector2(p.transform.position.x, p.transform.position.z)
                    );
                bool seesPlayer = distanceToPlayer <= 10f &&
                    !Physics.Linecast(projectile.obj.position, p.playerCam.transform.position, projectileMask);
                bool bound = seesPlayer && p.BindingstoneActive();
                float speed = normalSpeed * projectile.speed * (bound ? movementTimeScale : 1f) * Time.deltaTime;
                projectile.obj.Translate(Vector3.forward * speed, Space.Self);
                projectile.speed = Mathf.Min(projectile.topSpeed, projectile.speed + speed);

                bool hitPlayer = distanceToPlayer < 0.35f;
                if (hitPlayer || Physics.CheckSphere(projectile.obj.position, 0.1f, projectileMask))
                {
                    if (hitPlayer)
                    {
                        p.getHitEffect.GetHit(GetHitEffect.GetHitAnim.crawlerHit);
                        p.resources.ChangeHealth(-projectile.dmg);
                        CrawlerProjectile projectileAudio = projectile.obj.GetComponent<CrawlerProjectile>();
                        EnvEffect.EffectAnchor.PlayClipAt(
                            projectileAudio.hitSound,
                            projectile.obj.position,
                            projectileAudio.volume,
                            projectileAudio.output);
                    }
                    projectiles.RemoveAt(i);
                    Destroy(projectile.obj.gameObject);
                }
            }
        }

        public void OffloadCrawler(CEMain crawler)
        {
            foreach (CEScript child in crawler.script.data.children)
                if (child.main != null)
                    OffloadCrawler(child.main);

            crawler.script.data.state = CEData.Exposed.States.ABSTRACTED;
            crawler.DestroySelf();
        }
        public void LoadInCrawler(CEScript crawler, Quaternion rot)
        {
            crawler.data.state = CEData.Exposed.States.ROAMING;
            InstantiateCrawler("", crawler.data.position, rot, crawler);

            foreach (CEScript child in crawler.data.children)
                LoadInCrawler(child, rot);
        }

        public void KillCrawler(CEMain crawler)
        {
            crawler.script.data.alive = false;

            DropMana(crawler);
            crawler.anim.DeathDissolve();
            crawler.sfx.OneShotSound(crawler.sfx.dissolveSound, 0.3f);
            //clear parent relationships
            crawler.parent.exposed.ClearParent();
            crawler.parent.exposed.ClearChildren();
            //remove id from object list and send OnTargetDeleted callbacks
            DelistCrawler(crawler.script);
            //remove script from crawler list
            RemoveCrawler(crawler.script);
        }

        public void DelistCrawler(CEScript crawler)
        {
            int targetID = crawler.main.ID;
            for (int i = 0; i < crawlers.Count; i++)
                crawlers[i].OnTargetDeleted(targetID);
            crawler.main.Delist();
        }

        public void RemoveCrawler(CEScript crawler)
        {
            crawlers.Remove(crawler);
        }

        public void DropMana(CEMain crawler)
        {
            int mana = Mathf.CeilToInt(crawler.script.data.health * LootManager.enemyHPToDroppedManaRatio);
            for (int i = 0; i < mana; i++)
            {
                Vector2 rndPos = Random.insideUnitCircle * 0.5f;
                Instantiate(lvl.gameMgr.contentMgr.lootOrb,
                    new Vector3(crawler.transform.position.x + rndPos.x, 0.2f, crawler.transform.position.z + rndPos.y),
                    Quaternion.identity);
            }
        }

    }
}
