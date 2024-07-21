using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CrawlerEngine
{
    public class CEActions : MonoBehaviour
    {
        CEMain main;

        public bool actionInProgress;
        public bool actionFunctionallyInProgress;
        public bool actionAllowsMovement;

        public void Init(CEMain main1)
        {
            main = main1;
        }

        public void CacheActionInfo()
        {
            actionInProgress = main.script.ActionInProgress(false);
            actionFunctionallyInProgress = main.script.ActionInProgress(true);
            actionAllowsMovement = main.script.ActionAllowsMovement();
        }
    }
}
