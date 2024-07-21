using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;

namespace CrawlerEngine
{
    public class CEScriptLoader : ScriptLoaderBase
    {
        public ContentManager content;

        public override object LoadFile(string file, Table globalContext)
        {
            return content.GetCEScript(file);
        }

        public override bool ScriptFileExists(string name)
        {
            return content.GetCEScript(name) != null;
        }
    }
}
