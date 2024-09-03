#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Libplanet.Crypto;

namespace Nekoyume.Delegation
{
    public static class DelegationAddress
    {
        public static Address DelegateeMetadataAddress(
            Address delegateeAddress, Address delegateeAccountAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.DelegateeMetadata);

        public static Address DelegatorMetadataAddress(
            Address delegatorAddress, Address delegatorAccountAddress)
            => DeriveAddress(
                delegatorAddress,
                delegatorAccountAddress,
                DelegationAddressId.DelegatorMetadata);

        public static Address BondAddress(
            Address delegateeAddress, Address delegateeAccountAddress, Address delegatorAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.Bond,
                delegatorAddress.ByteArray);

        public static Address UnbondLockInAddress(
            Address delegateeAddress, Address delegateeAccountAddress, Address delegatorAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.UnbondLockIn,
                delegatorAddress.ByteArray);

        public static Address RebondGraceAddress(
            Address delegateeAddress, Address delegateeAccountAddress, Address delegatorAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.RebondGrace,
                delegatorAddress.ByteArray);

        public static Address CurrentLumpSumRewardsRecordAddress(
            Address delegateeAddress, Address delegateeAccountAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.LumpSumRewardsRecord);

        public static Address LumpSumRewardsRecordAddress(
            Address delegateeAddress, Address delegateeAccountAddress, long height)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.LumpSumRewardsRecord,
                BitConverter.GetBytes(height));

        public static Address RewardCollectorAddress(
            Address delegateeAddress, Address delegateeAccountAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.RewardCollector);

        public static Address RewardDistributorAddress(
            Address delegateeAddress, Address delegateeAccountAddress)
            => DeriveAddress(
                delegateeAddress,
                delegateeAccountAddress,
                DelegationAddressId.RewardDistributor);

        private static Address DeriveAddress(
            Address address,
            Address accountAddress,
            DelegationAddressId identifier,
            IEnumerable<byte>? bytes = null)
        {
            byte[] hashed;
            using (HMACSHA1 hmac = new(
                BitConverter.GetBytes((int)identifier)
                .Concat(accountAddress.ByteArray).ToArray()))
            {
                hashed = hmac.ComputeHash(
                    address.ByteArray.Concat(bytes ?? Array.Empty<byte>()).ToArray());
            }

            return new Address(hashed);
        }

        private enum DelegationAddressId
        {
            DelegateeMetadata,
            DelegatorMetadata,
            Bond,
            UnbondLockIn,
            RebondGrace,
            LumpSumRewardsRecord,
            RewardCollector,
            RewardDistributor,
            DelegationPool,
        }
    }
}
