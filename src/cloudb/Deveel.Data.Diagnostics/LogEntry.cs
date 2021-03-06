﻿//
//    This file is part of Deveel in The  Cloud (CloudB).
//
//    CloudB is free software: you can redistribute it and/or modify
//    it under the terms of the GNU Lesser General Public License as 
//    published by the Free Software Foundation, either version 3 of 
//    the License, or (at your option) any later version.
//
//    CloudB is distributed in the hope that it will be useful, but 
//    WITHOUT ANY WARRANTY; without even the implied warranty of 
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//    GNU Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public License
//    along with CloudB. If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Text;

namespace Deveel.Data.Diagnostics {
	/// <summary>
	/// The class <see cref="LogEntry"/> represents a new entry in a log.
	/// </summary>
	/// <remarks>
	/// This is only used in <see cref="DefaultLogger"/> to maintain
	/// information concerning a log entry.
	/// </remarks>
	public sealed class LogEntry {
		private readonly string thread;
		private readonly string message;
		private readonly string source;
		private readonly LogLevel level;
		private readonly Exception error;
		private readonly DateTime time;

		internal LogEntry(string thread, string source, LogLevel level, string message, Exception error, DateTime time) {
			this.thread = thread;
			this.message = message;
			this.time = time;
			this.level = level;
			this.source = source;
			this.error = error;
		}

		/// <summary>
		/// Gets ths identification of the current thread logging.
		/// </summary>
		public string Thread {
			get { return thread; }
		}

		/// <summary>
		/// Gets the time of the event logged.
		/// </summary>
		public DateTime Time {
			get { return time; }
		}

		/// <summary>
		/// Gets the level of the logged entry.
		/// </summary>
		public LogLevel Level {
			get { return level; }
		}

		/// <summary>
		/// Gets the source of the logging.
		/// </summary>
		/// <remarks>
		/// Generally this is the name of the type executing a function
		/// and the method name.
		/// </remarks>
		public string Source {
			get { return source; }
		}

		/// <summary>
		/// Gets the message part of the log.
		/// </summary>
		public string Message {
			get { return message; }
		}

		public Exception Error {
			get { return error; }
		}

		/// <inheritdoc/>
		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			sb.Append("Thread: ");
			sb.Append(thread);
			sb.Append(", ");
			sb.Append("Time: ");
			sb.Append(time);
			sb.Append(", ");
			sb.Append("Level: ");
			sb.Append(level);
			sb.Append(", ");
			sb.Append("Source: ");
			sb.Append(source);
			sb.Append(", ");
			sb.Append("Message: ");
			if (message.Length > 60) {
				sb.Append(message.Substring(0, 57));
				sb.Append("...");
			} else {
				sb.Append(message);
			}

			return sb.ToString();
		}
	}
}