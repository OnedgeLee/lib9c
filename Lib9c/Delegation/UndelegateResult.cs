namespace Nekoyume.Delegation
{
    public class UndelegateResult<T> : IUndelegateResult
        where T : IDelegatee
    {
        public UndelegateResult(
            T delegatee,
            Bond bond,
            UnbondLockIn unbondLockIn,
            UnbondingSet unbondingSet)
        {
            Delegatee = delegatee;
            Bond = bond;
            UnbondLockIn = unbondLockIn;
            UnbondingSet = unbondingSet;
        }

        IDelegatee IUndelegateResult.Delegatee => Delegatee;

        public T Delegatee { get; }

        public Bond Bond { get; }

        public UnbondLockIn UnbondLockIn { get; }

        public UnbondingSet UnbondingSet { get; }
    }
}
