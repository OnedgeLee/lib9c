namespace Lib9c.PluginBase
{
    public interface IPluginActionEvaluator
    {
        byte[][] Evaluate(byte[] block, byte[] baseStateRootHash);

        bool HasTrie(byte[] hash);
    }
}
