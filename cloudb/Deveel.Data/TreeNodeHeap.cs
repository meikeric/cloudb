﻿using System;
using System.Collections.Generic;

using Deveel.Data.Store;

namespace Deveel.Data {
	/// <summary>
	/// This is a temporary storage for organizing nodes
	/// during the modification of a tree.
	/// </summary>
	/// <remarks>
	/// <b>Note</b>: This class is not thread-safe, since it is intended
	/// as a temporary cache for nodes during a tree manipulation: it is
	/// responsibility of external threads to enforce cuncurrency locks.
	/// </remarks>
	public sealed class TreeNodeHeap {
		private long nodeIdSeq;
		private readonly IHashNode[] hash;
		private IHashNode hashEnd;
		private IHashNode hashStart;

		private int totalBranchNodeCount;
		private int totalLeafNodeCount;
		private long memoryUsed;

		private long maxMemoryLimit;

		public TreeNodeHeap(int hashSize, long maxMemoryLimit) {
			hash = new IHashNode[hashSize];
			nodeIdSeq = 2;
			this.maxMemoryLimit = maxMemoryLimit;
		}

		private int CalcHashValue(NodeId id) {
			int hc = id.GetHashCode();
			if (hc < 0) {
				hc = -hc;
			}
			return hc % hash.Length;
		}

		private void HashNode(IHashNode node) {
			NodeId nodeId = node.Id;
			int hashIndex = CalcHashValue(nodeId);
			IHashNode oldNode = hash[hashIndex];
			hash[hashIndex] = node;
			node.NextHash = oldNode;

			// Add it to the start of the linked list,
			if (hashStart != null) {
				hashStart.Previous = node;
			} else {
				hashEnd = node;
			}
			node.Next = hashStart;
			node.Previous = null;
			hashStart = node;

			// Update the 'memory_used' variable
			memoryUsed += node.MemoryAmount;
			if (node is TreeBranch) {
				++totalBranchNodeCount;
			} else {
				++totalLeafNodeCount;
			}
		}

		private NodeId NextNodeId() {
			long p = nodeIdSeq;
			++nodeIdSeq;
			// ISSUE: What happens if the node id sequence overflows?
			//   The boundary is large enough that if we were to create a billion
			//   nodes a second continuously, it would take 18 years to overflow.
			nodeIdSeq = nodeIdSeq & 0x0FFFFFFFFFFFFFFFL;

			return NodeId.CreateInMemoryNode(p);
		}

		internal void Flush() {
			List<IHashNode> toFlush = null;
			int all_node_count = 0;
			// If the memory use is above some limit then we need to flush out some
			// of the nodes,
			if (memoryUsed > maxMemoryLimit) {
				all_node_count = totalBranchNodeCount + totalLeafNodeCount;
				// The number to clean,
				int toClean = (int)(all_node_count * 0.30);

				// Make an array of all nodes to flush,
				toFlush = new List<IHashNode>(toClean);
				// Pull them from the back of the list,
				IHashNode node = hashEnd;
				while (toClean > 0 && node != null) {
					toFlush.Add(node);
					node = node.Previous;
					--toClean;
				}
			}

			// If there are nodes to flush,
			if (toFlush != null) {
				// Read each group and call the node flush routine,

				// The mapping of transaction to node list
				Dictionary<ITransaction, List<NodeId>> tran_map = new Dictionary<ITransaction, List<NodeId>>();
				// Read the list backwards,
				for (int i = toFlush.Count - 1; i >= 0; --i) {
					IHashNode node = toFlush[i];
					// Get the transaction of this node,
					ITransaction tran = node.Transaction;
					// Find the list of this transaction,
					List<NodeId> node_list;
					if (!tran_map.TryGetValue(tran, out node_list)) {
						node_list = new List<NodeId>(toFlush.Count);
						tran_map[tran] = node_list;
					}
					// Add to the list
					node_list.Add(node.Id);
				}
				// Now read the key and dispatch the clean up to the transaction objects,
				foreach(KeyValuePair<ITransaction, List<NodeId>> pair in tran_map) {
					ITransaction tran = pair.Key;
					List<NodeId> node_list = pair.Value;
					// Convert to a 'long[]' array,
					int sz = node_list.Count;
					NodeId[] refs = new NodeId[sz];
					for (int i = 0; i < sz; ++i) {
						refs[i] = node_list[i];
					}
					// Sort the references,
					Array.Sort(refs);
					// Tell the transaction to clean up these nodes,
					((TreeSystemTransaction)tran).FlushNodes(refs);
				}

			}
		}

		public ITreeNode FetchNode(NodeId id) {
			// Fetches the node out of the heap hash array.
			int hashIndex = CalcHashValue(id);
			IHashNode hashNode = hash[hashIndex];
			while (hashNode != null && !hashNode.Id.Equals(id)) {
				hashNode = hashNode.NextHash;
			}

			return hashNode;
		}

		public TreeBranch CreateBranch(ITransaction tran, int maxBranchChildren) {
			NodeId p = NextNodeId();
			HeapTreeBranch node = new HeapTreeBranch(tran, p, maxBranchChildren);
			HashNode(node);
			return node;
		}

		public TreeLeaf CreateLeaf(ITransaction tran, Key key, int maxLeafSize) {
			NodeId p = NextNodeId();
			HeapTreeLeaf node = new HeapTreeLeaf(tran, p, maxLeafSize);
			HashNode(node);
			return node;
		}

		public ITreeNode Copy(ITreeNode nodeToCopy, int maxBranchSize, int maxLeafSize, ITransaction tran) {
			// Create a new pointer for the copy
			NodeId p = NextNodeId();
			IHashNode node;
			if (nodeToCopy is TreeLeaf) {
				node = new HeapTreeLeaf(tran, p, (TreeLeaf)nodeToCopy, maxLeafSize);
			} else {
				node = new HeapTreeBranch(tran, p, (TreeBranch)nodeToCopy, maxBranchSize);
			}

			HashNode(node);
			// Return pointer to node
			return node;
		}

		public void Delete(NodeId id) {
			int hash_index = CalcHashValue(id);
			IHashNode hash_node = hash[hash_index];
			IHashNode previous = null;
			while (hash_node != null &&
			       !hash_node.Id.Equals(id)) {
				previous = hash_node;
				hash_node = hash_node.NextHash;
			}
			if (hash_node == null) {
				throw new ApplicationException("Node not found!");
			}
			if (previous == null) {
				hash[hash_index] = hash_node.NextHash;
			} else {
				previous.NextHash = hash_node.NextHash;
			}

			// Remove from the double linked list structure,
			// If removed node at head.
			if (hashStart == hash_node) {
				hashStart = hash_node.Next;
				if (hashStart != null) {
					hashStart.Previous = null;
				} else {
					hashEnd = null;
				}
			}
				// If removed node at end.
			else if (hashEnd == hash_node) {
				hashEnd = hash_node.Previous;
				if (hashEnd != null) {
					hashEnd.Next = null;
				} else {
					hashStart = null;
				}
			} else {
				hash_node.Previous.Next = hash_node.Next;
				hash_node.Next.Previous = hash_node.Previous;
			}

			// Update the 'memory_used' variable
			memoryUsed -= hash_node.MemoryAmount;
			if (hash_node is TreeBranch) {
				--totalBranchNodeCount;
			} else {
				--totalLeafNodeCount;
			}
		}

		private interface IHashNode : ITreeNode {
			IHashNode NextHash { get; set; }

			IHashNode Previous { get; set; }
			IHashNode Next { get; set; }

			ITransaction Transaction { get; }
		}

		private class HeapTreeBranch : TreeBranch, IHashNode {
			private IHashNode nextHash;
			private IHashNode next;
			private IHashNode previous;

			private readonly ITransaction tran;

			internal HeapTreeBranch(ITransaction tran, NodeId nodeId, int maxChildren)
				: base(nodeId, maxChildren) {
				this.tran = tran;
			}

			internal HeapTreeBranch(ITransaction tran, NodeId nodeId, TreeBranch branch, int maxChildren)
				: base(nodeId, branch, maxChildren) {
				this.tran = tran;
			}

			public IHashNode NextHash {
				get { return nextHash; }
				set { nextHash = value; }
			}

			public ITransaction Transaction {
				get { return tran; }
			}

			public IHashNode Previous {
				get { return previous; }
				set { previous = value; }
			}

			public IHashNode Next {
				get { return next; }
				set { next = value; }
			}

			public override long MemoryAmount {
				get { return base.MemoryAmount + (8*4); }
			}
		}

		private class HeapTreeLeaf : TreeLeaf, IHashNode {

			private IHashNode next_hash;
			private IHashNode next_list;
			private IHashNode previous_list;

			private readonly ITransaction tran;

			private readonly byte[] data;

			private NodeId nodeId;
			private int size;


			internal HeapTreeLeaf(ITransaction tran, NodeId nodeId, int maxCapacity) {
				this.tran = tran;
				this.nodeId = nodeId;
				size = 0;
				data = new byte[maxCapacity];
			}

			internal HeapTreeLeaf(ITransaction tran, NodeId nodeId, TreeLeaf toCopy, int maxCapacity) {
				this.tran = tran;
				this.nodeId = nodeId;
				size = toCopy.Length;
				// Copy the data into an array in this leaf.
				data = new byte[maxCapacity];
				toCopy.Read(0, data, 0, size);
			}

			// ---------- Implemented from TreeLeaf ----------

			public override NodeId Id {
				get { return nodeId; }
			}

			public override int Length {
				get { return size; }
			}

			public override int Capacity {
				get { return data.Length; }
			}

			public override void Read(int position, byte[] buffer, int offset, int count) {
				Array.Copy(data, position, buffer, offset, count);
			}

			public override void Shift(int position, int off) {
				if (off != 0) {
					int new_size = Length + off;
					Array.Copy(data, position, data, position + off, Length - position);
					// Set the size
					size = new_size;
				}
			}

			public override void Write(int position, byte[] buffer, int offset, int count) {
				Array.Copy(buffer, offset, data, position, count);
				if (position + count > size) {
					size = position + count;
				}
			}

			public override void WriteTo(IAreaWriter writer) {
				writer.Write(data, 0, Length);
			}

			public override void SetLength(int value) {
				if (value< 0 || value > Capacity)
					throw new ArgumentException("Specified leaf size is out of range.");

				size = value;
			}

			public override long MemoryAmount {
				get { return 8 + 4 + data.Length + 64 + (8 * 4); }
			}

			// ---------- Implemented from HashNode ----------

			public IHashNode NextHash {
				get { return next_hash; }
				set { next_hash = value; }
			}

			public ITransaction Transaction {
				get { return tran; }
			}

			public IHashNode Previous {
				get { return previous_list; }
				set { previous_list = value; }
			}

			public IHashNode Next {
				get { return next_list; }
				set { next_list = value; }
			}
		}

	}
}