using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;

namespace CrawlerEngine
{
    public class CEPlayer
    {
        CEScript script;
        public Exposed exposed;
        PlayerScript player;

        // Start is called before the first frame update
        public CEPlayer(CEScript s)
        {
            script = s;
            exposed = new Exposed(script);
            player = LevelManager.objs.localPlayer;
        }

        public class Exposed
        {
            [MoonSharpHidden] CEScript script;
            [MoonSharpHidden] public Exposed(CEScript p) { script = p; }
            [MoonSharpHidden] CEPlayer This() { return script.player; }
            [MoonSharpHidden] PlayerScript Player() { return script.player.player; }

            public enum Stats { HP, FULL_HP, MANA, FULL_MANA, NUTRITION, FULL_NUTRITION };
            public enum POVAnims { HIT, HARD_HIT, FALL_OVER, MISS };

            public float GetStat(Stats stat)
            {
                switch (stat)
                {
                    case Stats.HP: return Player().GetFloat(FloatProperty.Type.health).State;
                    case Stats.FULL_HP: return Player().GetFloat(FloatProperty.Type.health).MaxState;
                    case Stats.MANA: return Player().GetFloat(FloatProperty.Type.mana).State;
                    case Stats.FULL_MANA: return Player().GetFloat(FloatProperty.Type.mana).MaxState;
                    case Stats.NUTRITION: return Player().GetFloat(FloatProperty.Type.nutrition).State;
                    case Stats.FULL_NUTRITION: return Player().GetFloat(FloatProperty.Type.nutrition).MaxState;
                }
                return 0f;
            }
            public void ChangeStat (Stats stat, float amount)
            {
                switch (stat)
                {
                    case Stats.HP: Player().resources.ChangeHealth(amount); break;
                    case Stats.FULL_HP: Player().GetFloat(FloatProperty.Type.health).MaxState += amount; break;
                    case Stats.MANA: Player().resources.ChangeMana((int)amount); break;
                    case Stats.FULL_MANA: Player().GetFloat(FloatProperty.Type.mana).MaxState += amount; break;
                    case Stats.NUTRITION: Player().resources.ChangeNutrition(amount); break;
                    case Stats.FULL_NUTRITION: Player().GetFloat(FloatProperty.Type.nutrition).MaxState += amount; break;
                }
            }
            public Table Position()
            {
                return script.Vector3ToTable(Player().transform.position);
            }
            public Table CameraPosition()
            {
                return script.Vector3ToTable(Player().playerCam.transform.position);
            }
            public float Radius()
            {
                return PlayerCombat.playerRadius;
            }
            public int AsTarget()
            {
                return Player().ID;
            }
            public bool Attacked()
            {
                return Player().combat.Stomped();
            }
            public bool AttackDodgeCheck()
            {
                return Player().combat.DodgeCheck();
            }
            public DynValue AttackTarget()
            {
                return (Player().combat.StompingCrawler() != null ?
                    DynValue.NewNumber(Player().combat.StompingCrawler().ID) : DynValue.Nil);
            }
            public void SetAttackDodged()
            {
                Player().combat.SetStompDodged();
            }
            public void PlayPOVAnim(POVAnims anim)
            {
                switch (anim)
                {
                    case POVAnims.HIT:
                        Player().getHitEffect.GetHit(GetHitEffect.GetHitAnim.crawlerHit);
                        break;
                    case POVAnims.HARD_HIT:
                        Player().getHitEffect.GetHit(GetHitEffect.GetHitAnim.hit);
                        break;
                    case POVAnims.FALL_OVER:
                        Player().getHitEffect.GetHit(GetHitEffect.GetHitAnim.fallOver);
                        break;
                    case POVAnims.MISS:
                        Player().getHitEffect.GetHit(GetHitEffect.GetHitAnim.dodge);
                        break;
                }
            }
            public bool SeenFromBehindCorner()
            {
                return Physics.Raycast(
                    script.data.position + Vector3.up * 0.2f,
                    Player().transform.position - script.data.position,
                    2f,
                    1 << LayerMask.NameToLayer("staticNavmeshObstacle") |
                    1 << LayerMask.NameToLayer("immovableObject") |
                    1 << LayerMask.NameToLayer("door"));
            }
            public Table CornerTurnDirection()
            {
                Vector3 dir = Player().transform.InverseTransformDirection(script.main.transform.forward);
                return script.Vector3ToTable(Player().transform.right * (dir.x > 0f ? 1f : -1f));
            }
        }
    }
}
