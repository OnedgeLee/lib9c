using System.Reflection;
using Libplanet.Action;
using Libplanet.Store;

namespace Libplanet.Extensions.PluginActionEvaluator
{
    public static class PluginActionEvaluatorLoader
    {
        public static Assembly LoadPlugin(string absolutePath)
        {
            PluginLoadContext loadContext = new PluginLoadContext(absolutePath);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(absolutePath)));
        }

        public static IActionEvaluator? CreateActionEvaluator(string aevTypeName, Assembly assembly, IStateStore stateStore)
        {
            if (assembly.GetType(aevTypeName) is Type type)
            {
                return Activator.CreateInstance(type, args: stateStore) as IActionEvaluator;
            }

            return null;
        }

        public static IActionEvaluator? CreateActionEvaluator(string aevTypeName, string absolutePath, IStateStore stateStore)
            => CreateActionEvaluator(aevTypeName, LoadPlugin(absolutePath), stateStore);
    }
}
