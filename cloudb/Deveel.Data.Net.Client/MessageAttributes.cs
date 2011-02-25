﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Deveel.Data.Net.Client {
	public sealed class MessageAttributes : IEnumerable<KeyValuePair<string, object>>, ICloneable {
		private readonly IAttributesHandler handler;
		private Dictionary<string, object> values;

		internal MessageAttributes(IAttributesHandler handler) {
			this.handler = handler;
			values = new Dictionary<string, object>();
		}

		private void CheckReadOnly() {
			if (handler.IsReadOnly)
				throw new InvalidOperationException("The container is read-only.");
		}

		public object this[string name] {
			get { return values[name]; }
			set {
				CheckReadOnly();
				if (value != null && value.GetType().IsArray)
					throw new ArgumentException("An attribute cannot be a complex type.");
				values[name] = value;
			}
		}

		public int Count {
			get { return values.Count; }
		}

		public ICollection<string> Keys {
			get { return values.Keys; }
		}

		public bool Contains(string name) {
			return values.ContainsKey(name);
		}

		public void Add(string name, object value) {
			CheckReadOnly();
			if (value != null && value.GetType().IsArray)
				throw new ArgumentException("An attribute cannot be a complex type.");
			values.Add(name, value);
		}

		public void Clear() {
			CheckReadOnly();
			values.Clear();
		}

		public bool Remove(string name) {
			CheckReadOnly();
			return values.Remove(name);
		}

		public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
			return values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public object Clone() {
			MessageAttributes attributes = new MessageAttributes(handler);
			attributes.values = new Dictionary<string, object>(values.Count);
			foreach(KeyValuePair<string, object> pair in values) {
				object value = pair.Value;
				if (value != null && value is ICloneable)
					value = ((ICloneable) value).Clone();

				attributes.values[pair.Key] = value;
			}
			return attributes;
		}
	}
}