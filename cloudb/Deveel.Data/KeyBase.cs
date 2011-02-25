﻿using System;

namespace Deveel.Data {
	/// <summary>
	/// The base abstract definition of a key.
	/// </summary>
	[Serializable]
	public abstract class KeyBase : IComparable {
		protected KeyBase(short type, int secondary, long primary) {
			this.type = type;
			this.primary = primary;
			this.secondary = secondary;
		}

		internal KeyBase(long encoded_v1, long encoded_v2) {
			type = (short) (encoded_v1 >> 32);
			secondary = (int) (encoded_v1 & 0x0FFFFFFFF);
			primary = encoded_v2;
		}

		private readonly short type;
		private readonly long primary;
		private readonly int secondary;

		/// <summary>
		/// Gets the secondary component of the key.
		/// </summary>
		public int Secondary {
			get { return secondary; }
		}

		/// <summary>
		/// Gets the primary component of the key.
		/// </summary>
		public long Primary {
			get { return primary; }
		}

		/// <summary>
		/// Gets the type of the key.
		/// </summary>
		public short Type {
			get { return type; }
		}

		public override bool Equals(object obj) {
			if (!(obj is KeyBase))
				throw new ArgumentException();

			KeyBase dest_key = (KeyBase)obj;
			return dest_key.type == type &&
			       dest_key.secondary == secondary &&
			       dest_key.primary == primary;
		}

		public override int GetHashCode() {
			return (int)((secondary << 6) + (type << 3) + primary);
		}

		public long GetEncoded(int n) {
			if (n == 1) {
				long v = (type & 0x0FFFFFFFFL) << 32;
				v |= (secondary & 0x0FFFFFFFFL);
				return v;
			} 
			if (n == 2) {
				return primary;
			}

			throw new ArgumentOutOfRangeException("n");
		}

		public virtual int CompareTo(object obj) {
			if (!(obj is KeyBase))
				throw new ArgumentException();

			KeyBase key = (KeyBase)obj;

			// Either this key or the compared key are not special case, so collate
			// on the key values,

			// Compare secondary keys
		    int c = secondary < key.secondary ? -1 : (secondary == key.secondary ? 0 : 1);
			if (c == 0) {
				// Compare types
			    c = type < key.type ? -1 : (type == key.type ? 0 : 1);
				if (c == 0) {
					// Compare primary keys
					if (primary > key.primary)
						return +1;
					if (primary < key.primary)
						return -1;
					return 0;
				}
			}
			return c;
		}
	}
}