using System.Reflection;
using Lib9c.PluginBase;
using Libplanet.Action;
using Libplanet.Store;

namespace TestPlugin
{
    public static class PluginActionEvaluatorLoader
    {
        public static Assembly LoadPlugin(string absolutePath)
        {
            PluginLoadContext loadContext = new PluginLoadContext(absolutePath);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(absolutePath)));
        }

        public static IPluginActionEvaluator? CreateActionEvaluator(string aevTypeName, Assembly assembly, string storePath)
        {
            if (assembly.GetType(aevTypeName) is Type type)
            {
                return Activator.CreateInstance(type, storePath) as IPluginActionEvaluator;
            }

            return null;
        }

        public static IPluginActionEvaluator? CreateActionEvaluator(string aevTypeName, string absolutePath, string storePath)
            => CreateActionEvaluator(aevTypeName, LoadPlugin(absolutePath), storePath);
    }
}
