using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using SubstanceLevel;
using MoonSharp.Interpreter;

namespace CrawlerEngine
{
    public class CEMain : SLObject
    {
        [NonSerialized] public CEMovement movement;
        [NonSerialized] public CETargets targets;
        [NonSerialized] public CEActions actions;
        [NonSerialized] public CEAnimation anim;
        [NonSerialized] public CECollision collision;
        [NonSerialized] public CEParent parent;
        [NonSerialized] public CESFX sfx;
        [NonSerialized] public CERender render;

        [NonSerialized] public CEScript script;

        [NonSerialized] public CrawlerSetup setup;

        public void CollectComponents(CEScript script1)
        {
            type = Type.crawler;

            script = script1;
            script.main = this;

            movement = GetComponent<CEMovement>();
            targets = GetComponent<CETargets>();
            actions = GetComponent<CEActions>();
            anim = GetComponent<CEAnimation>();
            collision = GetComponent<CECollision>();
            parent = GetComponent<CEParent>();
            sfx = GetComponent<CESFX>();
            render = GetComponent<CERender>();
        }
        public void InitComponents()
        {
            movement.Init(this);
            targets.Init(this);
            actions.Init(this);
            anim.Init(this);
            collision.Init(this);
            parent.Init(this);
            sfx.Init(this);
            render.Init(this);
        }

        //main variables
        public bool Alive() { return script.data.alive; }
        public bool Fighting()  {return script.data.state == CEData.Exposed.States.COMBAT; }
        public bool Ragdoll() { return script.data.ragdollMode != CEAnimation.RagdollMode.none; }
        public bool DoingAction(bool functional = true)
        {
            return (functional ? actions.actionFunctionallyInProgress : actions.actionInProgress);
        }

        //called after all other CE components, but before the CECrawlerManager
        public void Update()
        {
            if (script.data.alive)
            {
                if (script.data.parent == null)
                    script.Update();
                script.AbstractedUpdate(Time.deltaTime * movement.statusTimeScale);
                //cache info about actions from this frame
                actions.CacheActionInfo();
            }
        }
        public void LateUpdate()
        {
            UpdateSLObject();
        }

        public float despawnTimer = 0f;
        public void DestroySelf()
        {
            Destroy(gameObject);
        }

        //jos poistetaan editorin kautta, putsataan listalta
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (script.data.alive && script.data.state != CEData.Exposed.States.ABSTRACTED)
                script.crawlerManager.RemoveCrawler(script);
        }
    }
}
