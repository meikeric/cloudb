//
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
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using Deveel.Data.Configuration;

namespace Deveel.Data.Diagnostics {
	/// <summary>
	/// A default implementation of <see cref="ILogger"/> that logs 
	/// messages to a <see cref="TextWriter"/> object.
	/// </summary>
	/// <remarks>
	/// This implementation allows for filtering of log messages of particular
	/// depth.  So for example, only message above or equal to level Alert are
	/// shown.
	/// </remarks>
	[LoggerTypeNameAttribute("default")]
	public class DefaultLogger : ILogger {
		/// <summary>
		/// The debug Lock object.
		/// </summary>
		private readonly Object debug_lock = new Object();

		/// <summary>
		/// This variable specifies the level of debugging information that is
		/// output.  Any debugging output above this level is output.
		/// </summary>
		private int debug_level = 0;

		/// <summary>
		/// The string used to format the output message.
		/// </summary>
		private string message_format = "[{Thread}][{Time}] {Source} : {Level:Name} - {Message}";

		/// <summary>
		/// The text writer where the debugging information is output to.
		/// </summary>
		private TextWriter output;

		public void Log(LogEntry entry) {
			lock (output) {
				StringBuilder sb = new StringBuilder();

				if (entry.Level < LogLevel.Message) {
					sb.Append("> ");
				} else {
					sb.Append("% ");
				}

				sb.Append(FormatEntry(entry));
				output.WriteLine(sb.ToString());
				output.Flush();
			}
		}

		protected virtual string FormatEntry(LogEntry entry) {
			if (message_format == null || entry == null)
				return String.Empty;

			StringBuilder message = new StringBuilder();
			StringBuilder field = null;

			for (int i = 0; i < message_format.Length; i++) {
				char c = message_format[i];
				if (c == '{') {
					field = new StringBuilder();
					continue;
				} 
				if (c == '}' && field != null) {
					string fieldValue = field.ToString();
					string result = FormatField(entry, fieldValue);
					message.Append(result);
					field = null;
					continue;
				}

				if (field != null) {
					field.Append(c);
				} else {
					message.Append(c);
				}
			}

			return message.ToString();
		}

		protected virtual string FormatField(LogEntry entry, string fieldName) {
			if (fieldName == null || fieldName.Length == 0)
				return null;

			string format = null;
			int index = fieldName.IndexOf(':');
			if (index != -1) {
				format = fieldName.Substring(index + 1);
				fieldName = fieldName.Substring(0, index);
			}

			if (fieldName.Length == 0)
				return null;

			switch (fieldName.ToLower()) {
				case "message":
					return (format != null ? String.Format(format, entry.Message) : entry.Message);
				case "time":
					return (format != null ? entry.Time.ToString(format, CultureInfo.InvariantCulture) : entry.Time.ToString());
				case "source":
					return (format != null ? String.Format(format, entry.Source) : entry.Source);
				case "level": {
					if (format != null)
						format = format.ToLower();
					if (format == "number" || format == null)
						return entry.Level.Value.ToString();
					if (format == "name")
						return entry.Level.Name;
					return String.Format(format, entry.Level.Value);
				}
				case "thread": {
					string threadName = (entry.Thread == null ? Thread.CurrentThread.ManagedThreadId.ToString() : entry.Thread);
					return (format != null ? String.Format(format, threadName) : threadName);
				}
				default:
					throw new ArgumentException("Unknown field " + fieldName);
			}
		}
		
		/// <summary>
		/// Parses a file string to an absolute position in the file system.
		/// </summary>
		/// <remarks>
		/// We must provide the path to the root directory (eg. the directory 
		/// where the config bundle is located).
		/// </remarks>
		internal static string ParseFileString(string root_path, String root_info, String path_string) {
			string path = Path.GetFullPath(path_string);
			string res;
			// If the path is absolute then return the absoluate reference
			if (Path.IsPathRooted(path_string)) {
				res = path;
			} else {
				// If the root path source is the environment then just return the path.
				if (root_info != null &&
				    root_info.Equals("env")) {
					return path;
				}
					// If the root path source is the configuration file then
					// concat the configuration path with the path string and return it.
				else {
					res = Path.Combine(root_path, path_string);
				}
			}
			return res;
		}

		public void Init(ConfigSource config) {
			string log_path_string = config.GetString("path");
			string root_path_var = config.GetString("rootPath");
			string read_only_access = config.GetString("readOnly");
			string debug_logs = config.GetString("debugLogs");
			bool read_only_bool = false;
			if (read_only_access != null) {
				read_only_bool = String.Compare(read_only_access, "enabled", true) == 0;
			}
			bool debug_logs_bool = true;
			if (debug_logs != null) {
				debug_logs_bool = String.Compare(debug_logs, "enabled", true) == 0;
			}

			// Conditions for not initializing a log directory;
			//  1. Read only access is enabled
			//  2. log_path is empty or not set

			if (debug_logs_bool && !read_only_bool &&
				log_path_string != null && !log_path_string.Equals("")) {
				// First set up the debug information in this VM for the 'Debug' class.
				string log_path = ParseFileString(Environment.CurrentDirectory, root_path_var, log_path_string);
				// If the path doesn't exist the make it.
				if (!Directory.Exists(log_path))
					Directory.CreateDirectory(log_path);

				LogWriter f_writer;
				String dlog_file_name = "";
				try {
					dlog_file_name = config.GetString("file");
					string debug_log_file = Path.Combine(Path.GetFullPath(log_path), dlog_file_name);

					// Allow log size to grow to 512k and allow 12 archives of the log
					//TODO: make it configurable...
					f_writer = new LogWriter(debug_log_file, 512 * 1024, 12);
					f_writer.Write("**** Debug log started: " + DateTime.Now + " ****\n");
					f_writer.Flush();
				} catch (IOException) {
					throw new Exception("Unable to open debug file '" + dlog_file_name + "' in path '" + log_path + "'");
				}
				output = f_writer;
			}

			// If 'debug_logs=disabled', don't Write out any debug logs
			if (!debug_logs_bool) {
				// Otherwise set it up so the output from the logs goes to a PrintWriter
				// that doesn't do anything.  Basically - this means all log information
				// will get sent into a black hole.
				output = new EmptyTextWriter();
			}

			debug_level = config.GetInt32("level", -1);
			if (debug_level == -1)
				// stops all the output
				debug_level = 255;

			string format = config.GetString("format", null);
			if (format != null)
				message_format = format;
		}
		
		public void SetDebugLevel(int value) {
			debug_level = value;
		}

		public bool IsInterestedIn(LogLevel level) {
			return (level >= debug_level);
		}

		public void Log(LogLevel level, object ob, string message) {
			Log(level, ob.GetType().Name, message);
		}

		public void Log(LogLevel level, Type type, string message) {
			Log(level, type.FullName, message);
		}

		public void Log(LogLevel level, string type_string, string message) {
			if (IsInterestedIn(level)) {
				// InternalWrite(output, level, type_string, message);
				Thread thread = Thread.CurrentThread;
				Log(new LogEntry(thread.Name, type_string, level, message, null, DateTime.Now));
			}
		}

		/*
		private void WriteTime() {
			lock (output) {
				output.Write("[ TIME: ");
				output.Write(DateTime.Now.ToString());
				output.WriteLine(" ]");
				output.Flush();
			}
		}
		*/

		public void Dispose() {
			if (output != null)
				output.Dispose();
		}

		private class EmptyTextWriter : TextWriter {
			public override void Write(int c) {
			}

			public override void Write(char[] cbuf, int off, int len) {
			}

			public override void Flush() {
			}

			public override void Close() {
			}

			#region Overrides of TextWriter

			public override Encoding Encoding {
				get { return Encoding.ASCII; }
			}

			#endregion
		}
	}
}