using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nethermind.Core2;
using Nethermind.Core2.Crypto;
using Nethermind.Core2.Types;
using Nethermind.Ssz;

[assembly: InternalsVisibleTo("Nethermind.Ssz.Test")]

namespace Nethermind.Merkleization
{
    /// <summary>
    /// This will be moved to Eth2
    /// </summary>
    public abstract class MerkleTree : IMerkleList
    {
        private const int LeafRow = 32;
        private const int LeafLevel = 0;
        public const int TreeHeight = 32;
        private const ulong FirstLeafIndexAsNodeIndex = MaxNodes / 2;
        private const ulong MaxNodes = (1ul << (TreeHeight + 1)) - 1ul;
        private const ulong MaxNodeIndex = MaxNodes - 1;

        private readonly IKeyValueStore<ulong, byte[]> _keyValueStore;

        private static ulong _countKey = ulong.MaxValue;

        public readonly ref struct Index
        {
            public Index(ulong nodeIndex)
            {
                ValidateNodeIndex(nodeIndex);

                Row = CalculateRow(nodeIndex);
                IndexAtRow = CalculateIndexAtRow(Row, nodeIndex);
                NodeIndex = nodeIndex;
            }

            public Index(uint row, ulong nodeIndex)
            {
                ValidateRow(row);
                ValidateNodeIndex(row, nodeIndex);

                Row = row;
                IndexAtRow = CalculateIndexAtRow(row, nodeIndex);
                NodeIndex = nodeIndex;
            }

            public Index(uint row, uint indexAtRow)
            {
                ValidateRow(row);
                ValidateIndexAtRow(row, indexAtRow);

                Row = row;
                NodeIndex = CalculateNodeIndex(row, indexAtRow);
                IndexAtRow = indexAtRow;
            }

            public uint Row { get; }
            public uint IndexAtRow { get; }
            public ulong NodeIndex { get; }

            internal bool IsLeftSibling()
            {
                return IndexAtRow % 2 == 0;
            }

            internal Index Parent()
            {
                if (Row == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Row), "Root node has no parent");
                }

                return new Index(Row - 1, (NodeIndex + 1) / 2 - 1);
            }

            internal Index Sibling()
            {
                if (Row == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Row), "Root node has no siblings.");
                }

                return new Index(Row, IndexAtRow ^ 1);
            }

            private static uint CalculateIndexAtRow(in uint row, in ulong nodeIndex)
            {
                return (uint) (nodeIndex - ((1ul << (int) row) - 1));
            }

            private static ulong CalculateNodeIndex(in uint row, in uint indexAtRow)
            {
                return (1ul << (int) row) - 1u + indexAtRow;
            }

            private static uint CalculateRow(in ulong nodeIndex)
            {
                ValidateNodeIndex(nodeIndex);
                for (uint row = 0; row < LeafRow; row++)
                {
                    if (2ul << (int) row >= nodeIndex + 2)
                    {
                        return row;
                    }
                }

                return LeafRow;
            }

            private static void ValidateRow(in uint row)
            {
                if (row > LeafRow)
                {
                    throw new ArgumentOutOfRangeException($"Tree level should be between 0 and {LeafRow}");
                }
            }

            private static void ValidateIndexAtRow(uint row, uint indexAtRow)
            {
                uint maxIndexAtRow = (uint) ((1ul << (int) row) - 1u);
                if (indexAtRow > maxIndexAtRow)
                {
                    throw new ArgumentOutOfRangeException($"Tree level {row} should only have indices between 0 and {maxIndexAtRow}");
                }
            }

            public override string ToString()
            {
                return $"{NodeIndex} | ({Row},{IndexAtRow})";
            }
        }

        static MerkleTree()
        {
        }

        /// <summary>
        /// Zero hashes will always be stored as 32 bytes (not truncated)
        /// </summary>
        protected abstract byte[][] ZeroHashesInternal { get; }

        public uint Count { get; set; }

        public MerkleTree(IKeyValueStore<ulong, byte[]> keyValueStore)
        {
            _keyValueStore = keyValueStore ?? throw new ArgumentNullException(nameof(keyValueStore));

            byte[]? countBytes = _keyValueStore[_countKey];
            Count = countBytes == null ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(countBytes);
        }

        private void StoreCountInTheDb()
        {
            byte[] countBytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(countBytes, Count);
            _keyValueStore[_countKey] = countBytes;
        }

        private void SaveValue(in Index index, byte[] hashBytes)
        {
            _keyValueStore[index.NodeIndex] = hashBytes;
        }

        private void SaveValue(in Index index, Bytes32 hash)
        {
            SaveValue(index, hash.AsSpan().ToArray());
        }

        private Bytes32 LoadValue(in Index index)
        {
            byte[]? nodeHashBytes = _keyValueStore[index.NodeIndex];
            if (nodeHashBytes == null)
            {
                return Bytes32.Wrap(ZeroHashesInternal[LeafRow - index.Row]);
            }

            return Bytes32.Wrap(nodeHashBytes);
        }

        internal static uint GetLeafIndex(in ulong nodeIndex)
        {
            return new Index(LeafRow, nodeIndex).IndexAtRow;
        }

        internal static ulong GetNodeIndex(in uint row, in uint indexAtRow)
        {
            return new Index(row, indexAtRow).NodeIndex;
        }

        internal static uint GetSiblingIndex(in uint row, in uint indexAtRow)
        {
            return new Index(row, indexAtRow).Sibling().IndexAtRow;
        }

        internal static void ValidateNodeIndex(ulong nodeIndex)
        {
            if (nodeIndex > MaxNodeIndex)
            {
                throw new ArgumentOutOfRangeException($"Node index should be between 0 and {MaxNodeIndex}");
            }
        }

        private static ulong GetMinNodeIndex(in uint row)
        {
            return (1ul << (int) row) - 1;
        }

        private static ulong GetMaxNodeIndex(in uint row)
        {
            return (1ul << (int) (row + 1u)) - 2;
        }

        private static void ValidateNodeIndex(in uint row, in ulong nodeIndex)
        {
            ulong minNodeIndex = GetMinNodeIndex(row);
            ulong maxNodeIndex = GetMaxNodeIndex(row);

            if (nodeIndex < minNodeIndex || nodeIndex > maxNodeIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nodeIndex),
                    $"Node index at row {row} should be in the range of " +
                    $"[{minNodeIndex},{maxNodeIndex}] and was {nodeIndex}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Insert(Bytes32 leaf)
        {
            Index index = new Index(LeafRow, Count);
            Index siblingIndex = index.Sibling();
            Bytes32 hash = leaf;
            Bytes32 siblingHash = LoadValue(siblingIndex);

            SaveValue(index, hash);

            for (int row = LeafRow; row > 0; row--)
            {
                var parentHash = index.IsLeftSibling()
                    ? Hash(hash.AsSpan(), siblingHash.AsSpan())
                    : Hash(siblingHash.AsSpan(), hash.AsSpan());

                Index parentIndex = index.Parent();
                SaveValue(parentIndex, parentHash);

                index = parentIndex;
                if (row != 1)
                {
                    siblingIndex = index.Sibling();
                    hash = Bytes32.Wrap(parentHash);

                    // we can quickly / efficiently find out that it will be a zero hash
                    siblingHash = LoadValue(siblingIndex);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(_countBytes, Count + 1);
                    Root = Root.Wrap(Hash(parentHash, _countBytes));
                }
            }

            Count++;
            StoreCountInTheDb();
        }

        private byte[] _countBytes = new byte[32];

        public IList<Bytes32> GetProof(in uint leafIndex)
        {
            if (leafIndex >= Count)
            {
                throw new InvalidOperationException("Unpexected query for a proof for a value beyond Count");
            }
            
            Index index = new Index(LeafRow, leafIndex);
            List<Bytes32> proof = new List<Bytes32>();

            for (int proofRow = LeafRow; proofRow > 0; proofRow--)
            {
                Index siblingIndex = index.Sibling();
                proof.Add(LoadValue(siblingIndex));
                index = index.Parent();
            }

            return proof;
        }

        public MerkleTreeNode GetLeaf(in uint leafIndex)
        {
            Index index = new Index(LeafRow, leafIndex);
            Bytes32 value = LoadValue(index);
            return new MerkleTreeNode(Bytes32.Wrap(value.AsSpan().ToArray()), index.NodeIndex);
        }

        public MerkleTreeNode[] GetLeaves(params uint[] leafIndexes)
        {
            MerkleTreeNode[] leaves = new MerkleTreeNode[leafIndexes.Length];
            for (int i = 0; i < leafIndexes.Length; i++)
            {
                leaves[i] = GetLeaf(leafIndexes[i]);
            }

            return leaves;
        }

        internal static uint GetIndexAtRow(in uint row, in ulong nodeIndex)
        {
            return new Index(row, nodeIndex).IndexAtRow;
        }

        internal static uint GetRow(in ulong nodeIndex)
        {
            return new Index(nodeIndex).Row;
        }

        public static ulong GetParentIndex(in ulong nodeIndex)
        {
            return new Index(nodeIndex).Parent().NodeIndex;
        }

        public Root Root { get; set; }
        
        protected abstract byte[] Hash(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b);
    }
}