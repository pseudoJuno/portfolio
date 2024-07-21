using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using SubstanceLevel;

namespace CrawlerEngine
{
    public class CEParent : MonoBehaviour
    {
        CEMain main;
        public Exposed exposed;

        // Start is called before the first frame update
        public void Init(CEMain main1)
        {
            main = main1;
            exposed = new Exposed(main.script);
        }

        int ragdollModeInitDelay = 2;
        public CEAnimation.RagdollMode initRagdollMode = CEAnimation.RagdollMode.none;
        Transform targetBone = null;
        float lerp = 1f;
        void FixedUpdate()
        {
            if (targetBone != null && main.script.data.ragdollMode != CEAnimation.RagdollMode.none)
            {
                Vector3 pos = targetBone.position;
                Quaternion rot = targetBone.rotation;

                Vector3 movePos = pos + (main.anim.mainRBody.position - main.setup.rideBone.TransformPoint(main.setup.rideOffset));
                Vector3 pos2 = new Vector3(movePos.x, Mathf.Max(0.1f, movePos.y), movePos.z);
                main.anim.mainRBody.MovePosition(Vector3.Lerp(main.anim.mainRBody.position, pos2, lerp));
                main.anim.mainRBody.MoveRotation(Quaternion.Lerp(main.anim.mainRBody.rotation, rot, lerp));

                targetBone = null;
            }
        }
        void Update()
        {
            if (targetBone != null && main.script.data.ragdollMode == CEAnimation.RagdollMode.none)
            {
                Vector3 pos = targetBone.position;
                Quaternion rot = targetBone.rotation;

                Vector3 movePos = pos + (transform.position - main.setup.rideBone.TransformPoint(main.setup.rideOffset));
                Vector3 pos2 = new Vector3(movePos.x, Mathf.Max(0.1f, movePos.y), movePos.z);
                transform.position = Vector3.Lerp(transform.position, pos2, lerp);
                transform.rotation = Quaternion.Lerp(transform.rotation, rot, lerp);

                targetBone = null;
            }

            //ragdoll mode is inited with a delay so that parent lerping gets sorted first
            if (ragdollModeInitDelay > 0)
            {
                ragdollModeInitDelay--;
                if (ragdollModeInitDelay == 0 &&
                    initRagdollMode != CEAnimation.RagdollMode.none &&
                    main.script.data.ragdollMode == CEAnimation.RagdollMode.none)
                {
                    main.anim.BecomeRagdoll(initRagdollMode == CEAnimation.RagdollMode.legs);
                    initRagdollMode = CEAnimation.RagdollMode.none;
                }
            }
        }

        public void SetParent(CEScript parent)
        {
            ClearParent();
            main.script.data.parent = parent;
            if (parent != null)
            {
                parent.data.children.Add(main.script);
                //stop combat and reset map target if we become parented
                if (main.script.data.state == CEData.Exposed.States.COMBAT)
                    main.script.data.state = CEData.Exposed.States.ROAMING;
                //main.script.data.ResetMapTarget();
                //main.script.data.ClearGroup();
            }
        }
        public void ClearParent()
        {
            if (main.script.data.parent != null)
                main.script.data.parent.data.children.Remove(main.script);
            main.script.data.parent = null;
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CEParent This() { return script.main.parent; }
            [MoonSharpHidden] CEScript TargetScript(int targetID) { return ((CEMain)script.lvl.objDict[targetID]).script; }

            public enum Bones { PARENT, TRANSITION }

            public void SetParent(int targetID)
            {
                This().SetParent(TargetScript(targetID));
            }
            public DynValue GetParent()
            {
                return (script.data.parent != null && script.data.parent.main != null ?
                    DynValue.NewNumber(script.data.parent.main.ID) : DynValue.Nil);
            }
            public Table GetChildren()
            {
                Table children = new Table(script.script);
                foreach (CEScript child in script.data.children)
                    if (child.main != null)
                        children.Append(DynValue.NewNumber(child.main.ID));
                return children;
            }
            public int GetChild(int childIndex)
            {
                return script.data.children[childIndex].main.ID;
            }
            public int ChildCount()
            {
                return script.data.children.Count;
            }
            public void ClearParent()
            {
                This().ClearParent();
            }
            public void AddChild(int targetID)
            {
                TargetScript(targetID).main.parent.SetParent(script);
            }
            public void RemoveChild(int targetID)
            {
                CEScript child = TargetScript(targetID);
                if (script.data.children.Contains(child))
                {
                    script.data.children.Remove(child);
                    child.data.parent = null;
                }
            }
            public void RemoveChildAtIndex(int index)
            {
                if (script.data.children.Count > index)
                {
                    script.data.children[index].data.parent = null;
                    script.data.children.RemoveAt(index);
                }
            }
            public void ClearChildren()
            {
                foreach (CEScript child in script.data.children)
                    child.data.parent = null;
                script.data.children.Clear();
            }
            public void LerpToBone(Bones boneType, float t)
            {
                This().lerp = t;
                if (boneType == Bones.PARENT)
                    This().targetBone = script.data.parent.main.setup.mountBone;
                else
                    This().targetBone = script.data.parent.main.setup.mountTransitionBone;
            }
            public void LerpChildToBone(int childIndex, Bones boneType, float t)
            {
                script.data.children[childIndex].main.parent.lerp = t;
                if (boneType == Bones.PARENT)
                    script.data.children[childIndex].main.parent.targetBone = script.main.setup.mountBone;
                else
                    script.data.children[childIndex].main.parent.targetBone = script.main.setup.mountTransitionBone;
            }
        }
    }
}
