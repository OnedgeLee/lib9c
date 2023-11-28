using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Serilog;

namespace Nekoyume.Action
{
    [Serializable]
    [ActionType("plugin_aev_action")]
    public class PluginAevAction : GameAction
    {
        protected override IImmutableDictionary<string, IValue> PlainValueInternal
        {
            get
            {
                var dict = new Dictionary<string, IValue>();
                return dict.ToImmutableDictionary();
            }
        }

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
        }

        public override IAccount Execute(IActionContext context)
        {
            context.UseGas(1);
            if (context.Rehearsal)
            {
                return context.PreviousState;
            }

            var random = context.GetRandom();
            return Execute(
                context.PreviousState,
                context.Signer,
                context.BlockIndex,
                random);
        }

        public IAccount Execute(
            IAccount states,
            Address signer,
            long blockIndex,
            IRandom random)
        {
            var targetAddress = signer.Derive("pluginCalled");
            Log.Information(
                "PluginAevAction has been executed : Address {Address} has been set by text 'Activated'.",
                targetAddress.ToHex());

            states = states.SetState(targetAddress, new Text("Activated"));
            return states;
        }

    }
}
