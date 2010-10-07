﻿using System;
using System.IO;
using System.Text;

namespace Deveel.Data.Net.Client {
	public abstract class JsonMessageSerializer : ITextMessageSerializer {
		private Encoding encoding;

		protected JsonMessageSerializer(string encoding)
			: this(!String.IsNullOrEmpty(encoding) ? Encoding.GetEncoding(encoding) : Encoding.UTF8) {
		}

		protected JsonMessageSerializer(Encoding encoding) {
			this.encoding = encoding;
		}

		protected JsonMessageSerializer()
			: this(Encoding.UTF8) {
		}

		public Encoding ContentEncoding {
			get {
				if (encoding == null)
					encoding = Encoding.UTF8;
				return encoding;
			}
			set { encoding = value; }
		}

		public void Serialize(Message message, Stream output) {
			if (output == null)
				throw new ArgumentNullException("output");
			if (!output.CanWrite)
				throw new ArgumentException("The output stream is not writeable.");

			StreamWriter writer = new StreamWriter(output, ContentEncoding);
			Serialize(message, writer);
			writer.Flush();
		}

		public abstract void Serialize(Message message, TextWriter writer);

		public void Deserialize(Message message, Stream input) {
			if (input == null)
				throw new ArgumentNullException("input");
			if (!input.CanRead)
				throw new ArgumentException("The input stream is not readable.");

			Deserialize(message, new StreamReader(input, ContentEncoding));
		}

		public abstract void Deserialize(Message message, TextReader reader);

		string ITextMessageSerializer.ContentEncoding {
			get { return ContentEncoding.BodyName; }
		}

		string ITextMessageSerializer.ContentType {
			get { return "application/json"; }
		}
	}
}