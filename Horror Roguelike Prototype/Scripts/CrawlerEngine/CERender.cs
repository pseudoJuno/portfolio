using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;

namespace CrawlerEngine
{
    public class CERender : MonoBehaviour
    {
        CEMain main;

        public Material crawlerTemplateMat;
        public Material crawlerTemplateMatSpecular;
        public Material crawlerTemplateMatTransparent;
        public Transform targetCircle;
        Material targetCircleMat;
        Color targetCircleColor;
        public Transform shackleEffectPrefab;
        ShackleEffect shackleEffect = null;
        int shackleEffectState = 0;

        Renderer crawlerRend;

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;
            targetCircleMat = targetCircle.GetComponent<Renderer>().material;
            targetCircleColor = targetCircleMat.color;

            crawlerRend = transform.GetChild(4).GetComponentInChildren<Renderer>();
            SetMaterial();
        }
        
        void SetMaterial()
        {
            Material crawlerMat = crawlerRend.material;
            //Debug.Log(crawlerMat.shader.name);
            bool transparentMode = (crawlerMat.GetTag("RenderType", false) == "Transparent");
            bool specularMode = (!transparentMode &&
                crawlerMat.shader.name != "Standard" &&
                crawlerMat.shader.name != "Beautiful Dissolves/Standard Dissolve");

            //copy mat values
            Texture mainTex = crawlerMat.GetTexture("_MainTex");
            Texture specMap = (specularMode ? crawlerMat.GetTexture("_SpecGlossMap") : null);
            Texture normalMap = crawlerMat.GetTexture("_BumpMap");
            Texture emissionMap = crawlerMat.GetTexture("_EmissionMap");
            Vector3 mainOffset = crawlerMat.mainTextureOffset;
            Vector3 mainScale = crawlerMat.mainTextureScale;
            Color color = crawlerMat.color;
            Color specColor = (specularMode ? crawlerMat.GetColor("_SpecColor") : new Color(0.2f, 0.2f, 0.2f, 1f));
            Color emissionColor = crawlerMat.GetColor("_EmissionColor");
            float smoothness = crawlerMat.GetFloat("_Glossiness");
            float smoothnessFactor = crawlerMat.GetFloat("_GlossMapScale");
            float bumpScale = crawlerMat.GetFloat("_BumpScale");
            float alphaCutoff = crawlerMat.GetFloat("_Cutoff");
            //paste values to template mat
            Material newMat = new Material((transparentMode ?
                crawlerTemplateMatTransparent : (specularMode ? crawlerTemplateMatSpecular : crawlerTemplateMat)));
            //if (crawlerMat.IsKeywordEnabled("_EMISSION"))
            newMat.EnableKeyword("_EMISSION");
            newMat.SetTexture("_MainTex", mainTex);
            if (specularMode) newMat.SetTexture("_SpecGlossMap", specMap);
            newMat.SetTexture("_BumpMap", normalMap);
            newMat.SetTexture("_EmissionMap", crawlerMat.GetTexture("_MainTex"));
            newMat.mainTextureOffset = mainOffset;
            newMat.mainTextureScale = mainScale;
            newMat.color = color;
            if (specularMode) newMat.SetColor("_SpecColor", specColor);
            newMat.SetColor("_EmissionColor", Color.black);
            newMat.SetFloat("_Glossiness", smoothness);
            newMat.SetFloat("_GlossMapScale", smoothnessFactor);
            newMat.SetFloat("_BumpScale", bumpScale);
            newMat.SetFloat("_Cutoff", alphaCutoff);
            ////enable normal and spec maps
            //crawlerRend.material.EnableKeyword("_NORMALMAP");
            //if (specularMode)
            //    crawlerRend.material.EnableKeyword("_SPECGLOSSMAP");
            //set the material
            crawlerRend.material = newMat;
        }

        bool showTargetCircleFlag = false;
        public void ShowTargetCircle()
        {
            showTargetCircleFlag = true;
        }

        // Update is called once per frame
        
        float targetCircleAlpha = 0f;
        float emissionValue = 0f;
        void Update()
        {
            //target circle
            targetCircleAlpha = Mathf.Lerp(
                targetCircleAlpha,
                (main.movement.statusTimeScale == 0f && !LevelManager.objs.localPlayer.Attacking() ? 1f : 0f),
                Time.deltaTime * 10f);

            if (showTargetCircleFlag)
                targetCircleMat.color = new Color(
                    targetCircleColor.r,
                    targetCircleColor.g,
                    targetCircleColor.b,
                    targetCircleColor.a * targetCircleAlpha);

            if (showTargetCircleFlag != targetCircle.gameObject.activeSelf)
                targetCircle.gameObject.SetActive(showTargetCircleFlag);
            showTargetCircleFlag = false;

            //freeze highlight effect
            PlayerCombat c = LevelManager.objs.localPlayer.combat;
            float freezeEffect = (main.Fighting() ? c.GetFreezeProgress() * 0.25f +
                Mathf.Pow(1f - Mathf.Clamp(c.GetEnemyFreezeTimescale(), 0f, 1f), 0.33f) * 0.75f : 0f);
            if (freezeEffect != emissionValue)
            {
                emissionValue = freezeEffect;
                crawlerRend.material.SetColor("_EmissionColor", Color.Lerp(Color.black, new Color(0f, 0.2f, 0.53f), emissionValue));
            }
        }
        void LateUpdate()
        {
            if (targetCircle.gameObject.activeSelf)
                targetCircle.rotation = Quaternion.Euler(90f, 0f, 0f);

            //shackle effect
            if ((main.Alive() && main.script.data.Is(CEData.StatusFX.SHACKLED)) ||
                (shackleEffect != null && (shackleEffect.SummedAlpha() > 0f || shackleEffect.shackleDecrementSound.isPlaying)))
            {
                if (shackleEffect == null)
                    shackleEffect = Instantiate(shackleEffectPrefab, transform).GetComponent<ShackleEffect>().Init(0f);
                //Vector3 pos1 = main.setup.UIAnchorBone.TransformPoint(main.setup.UIOffset);
                //Vector3 pos2 = main.setup.root.position;
                shackleEffect.transform.position = main.setup.shackleAnchorBone.TransformPoint(main.setup.shackleOffset);
                shackleEffect.transform.rotation = transform.rotation;
                int state = Mathf.CeilToInt(main.script.data.GetStatusNormalized(CEData.StatusFX.SHACKLED) * 3f);
                for (int i = 0; i < 3; i++)
                    shackleEffect.SetAlpha(i, Mathf.MoveTowards(shackleEffect.GetAlpha(i), (main.Alive() && i < state ? 1f : 0f), Time.deltaTime * (main.Alive() ? 5f : 0.5f)));
                if (state != shackleEffectState)
                {
                    if (main.Alive())
                    {
                        if (state < 3) shackleEffect.PlayShackleDecrementSound();
                        if (state == 0) main.sfx.PlaySoundFromClips(main.setup.aggroSounds, 0, main.setup.aggroSoundsVolume);
                    }
                    shackleEffectState = state;
                }
            }
            else
            {
                if (shackleEffect != null)
                    Destroy(shackleEffect.gameObject);
            }

            //draw prefab
            if (drawInstance != null && !drawInstanceFlag)
                ClearDrawInstance();
            drawInstanceFlag = false;
        }

        string drawingPrefab = "";
        Transform drawInstance;
        Vector3 drawInstanceNormalScale;
        bool drawInstanceFlag = false;
        public void DrawPrefab(string prefabName, Transform tr, Vector3 pos, Quaternion rot, float scale)
        {
            if (drawInstanceFlag)
                return;

            if (prefabName != "" && prefabName != drawingPrefab)
            {
                ClearDrawInstance();
                drawingPrefab = prefabName;
                Transform prefab = null;
                foreach (ContentManager.AssetPackage package in main.lvl.gameMgr.contentMgr.assetPackages)
                    foreach (ObjectScript obj in package.prefabs)
                        if (obj.objName == drawingPrefab)
                        { prefab = obj.transform; break; }
                if (prefab == null) return;

                drawInstance = Instantiate(prefab, pos, rot);
                foreach (Rigidbody rbody in drawInstance.GetComponentsInChildren<Rigidbody>())
                    DestroyImmediate(rbody);
                foreach (Collider collider in drawInstance.GetComponentsInChildren<Collider>())
                    DestroyImmediate(collider);
                foreach (ObjectScript script in drawInstance.GetComponentsInChildren<ObjectScript>())
                    DestroyImmediate(script);
                drawInstanceNormalScale = drawInstance.localScale;
            }

            drawInstance.position = pos;
            drawInstance.rotation = rot;
            drawInstance.localScale = drawInstanceNormalScale * scale;
            drawInstanceFlag = true;
        }
        public void ClearDrawInstance()
        {
            if (drawInstance != null)
                DestroyImmediate(drawInstance.gameObject);
            drawInstance = null;
            drawingPrefab = "";
        }

        public void OnDestroy()
        {
            ClearDrawInstance();
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CERender This() { return script.main.render; }

            public enum Coords { WORLD, SCREEN };

            public Table UIPosition() { return script.Vector3ToTable(script.main.setup.UIAnchorBone.TransformPoint(script.main.setup.UIOffset)); }
            public void DrawUILine(Table from, Table to, Table color, Coords coords)
            {
                DrawUILines.UILine newLine = new DrawUILines.UILine()
                {
                    from = script.TableToVector3(from),
                    to = script.TableToVector3(to),
                    color = script.TableToColor(color)
                };
                if (coords == Coords.WORLD)
                    DrawUILines.worldLineRenderQueue.Add(newLine);
                else if (coords == Coords.SCREEN)
                    DrawUILines.screenLineRenderQueue.Add(newLine);
            }
            public Table WorldToScreenPos(Table worldPos)
            {
                return script.Vector3ToTable(LevelManager.objs.localPlayer.playerCam.WorldToScreenPoint(script.TableToVector3(worldPos)));
            }
            public Table ScreenToWorldPos(Table screenPos)
            {
                return script.Vector3ToTable(LevelManager.objs.localPlayer.playerCam.ScreenToWorldPoint(script.TableToVector3(screenPos)));
            }

            public void CombatText(string text, Table pos, float time)
            {
                LevelManager.objs.localPlayer.interaction.NewWorldPopupText(text, script.TableToVector3(pos), Color.white, time, 0.4f);
            }
            
            public string GetRandomObjectFromCategory(string category)
            {
                List<string> objsInCategory = new List<string>();
                foreach (ContentManager.AssetPackage package in This().main.lvl.gameMgr.contentMgr.assetPackages)
                    foreach (ObjectScript obj in package.prefabs)
                        if (obj.objectCategory == category)
                            objsInCategory.Add(obj.objName);
                if (objsInCategory.Count == 0)
                    return "";
                return objsInCategory[Random.Range(0, objsInCategory.Count)];
            }
            public void RenderObject(string prefabName, Table position, Table direction, float scale)
            {
                This().DrawPrefab(
                    prefabName,
                    null,
                    script.TableToVector3(position),
                    Quaternion.LookRotation(script.TableToVector3(direction)),
                    scale);
            }
            public void SetDissolve(float amount)
            {
                This().crawlerRend.material.SetFloat("_DissolveAmount", amount);
            }
        }
    }
}
