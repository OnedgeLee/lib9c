using TestPlugin;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.RocksDBStore;

var storePath = "C:\\Users\\onedg\\planet\\forkable\\test";
var store = new TrieStateStore(new RocksDBKeyValueStore(storePath));

string pluginTypeName = "Lib9c.PluginActionEvaluator.PluginActionEvaluator";
string pluginPath = "C:\\Users\\onedg\\planet\\forkable\\ZB\\Lib9c.PluginActionEvaluator.dll";

var aev = PluginActionEvaluatorLoader.CreateActionEvaluator(
    pluginTypeName,
    pluginPath,
    storePath);

var trie = store.GetStateRoot(null);
Console.WriteLine(aev.HasTrie(trie.Hash.ToByteArray()));

var trie2 = trie.Set(new KeyBytes("abc"), (Bencodex.Types.Text)"zxc");
var trie3 = store.Commit(trie2);

Console.WriteLine(aev.HasTrie(trie3.Hash.ToByteArray()));
