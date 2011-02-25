﻿using System;
using System.Text;

using Deveel.Console;
using Deveel.Console.Commands;

namespace Deveel.Data.Net {
	class AddCommand : Command {
		public override string Name {
			get { return "add"; }
		}
		
		public override string[] Synopsis {
			get {
				return new string[] {
				                    	"add path <type> <path-name> to <root>",
				                    	"add base path <path-name> to <root>",
										"add value <value> into <table> with key <key> [to <path>]"
				                    };
			}
		}
		
		public override bool RequiresContext {
			get { return true; }
		}

		private IServiceAddress[] ParseMachineAddressList(string machineList) {
			string[] machines = machineList.Split(',');

			try {
				IServiceAddress[] services = new IServiceAddress[machines.Length];
				for (int i = 0; i < machines.Length; ++i) {
					services[i] = ServiceAddresses.ParseString(machines[i].Trim());
				}
				return services;
			} catch (Exception e) {
				Error.WriteLine("Error parsing machine address: " + e.Message);
				throw;
			}
		}

		private CommandResultCode AddPath(NetworkContext context, string pathType, string pathName, string rootAddress) {
			IServiceAddress[] addresses = ParseMachineAddressList(rootAddress);

			// Check no duplicates in the list,
			bool duplicateFound = false;
			for (int i = 0; i < addresses.Length; ++i) {
				for (int n = i + 1; n < addresses.Length; ++n) {
					if (addresses[i].Equals(addresses[n])) {
						duplicateFound = true;
					}
				}
			}

			if (duplicateFound) {
				Error.WriteLine("Error: Duplicate root server in definition");
				return CommandResultCode.ExecutionFailed;
			}


			Out.Write("Adding path " + pathType + " " + pathName + " to ");
			for (int i = 0; i < addresses.Length; i++) {
				if (i == 0)
					Out.Write("leader ");
				else if (i == 1)
					Out.Write("and replica ");

				Out.Write(addresses[i].ToString());

				if (i < addresses.Length - 1)
					Out.Write(" ");
			}

			Out.WriteLine();

			for (int i = 0; i < addresses.Length; i++) {
				IServiceAddress address = addresses[i];

				MachineProfile p = context.Network.GetMachineProfile(address);
				if (p == null) {
					Error.WriteLine("error: Machine was not found in the network schema.");
					return CommandResultCode.ExecutionFailed;
				}
				if (!p.IsRoot) {
					Error.WriteLine("error: Given machine is not a root.");
					return CommandResultCode.ExecutionFailed;
				}
			}

			// Add the path,
			try {
				context.Network.AddPath(pathName, pathType, addresses[0], addresses);
			} catch (Exception e) {
				Error.WriteLine("cannot add the path: " + e.Message);
				return CommandResultCode.ExecutionFailed;
			}

			Out.WriteLine("done.");
			return CommandResultCode.Success;
		}

		private CommandResultCode AddValue(NetworkContext context, CommandArguments args) {
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;

			string value = args.Current;

			if (String.IsNullOrEmpty(value))
				return CommandResultCode.ExecutionFailed;

			if (value[0] == '\'') {
				bool endFound = false;
				StringBuilder sb = new StringBuilder();
				while (!endFound) {
					for (int i = 0; !String.IsNullOrEmpty(value) && i < value.Length; i++) {
						char c = value[i];
						if (c == '\'' && i > 0) {
							endFound = true;
							break;
						}

						sb.Append(c);
					}

					if (!endFound && args.MoveNext()) {
						sb.Append(' ');
						value = args.Current;
					}
				}

				value = sb.ToString();
			}

			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			if (args.Current != "into")
				return CommandResultCode.SyntaxError;
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;

			string tableName = args.Current;

			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			if (args.Current != "with")
				return CommandResultCode.SyntaxError;
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			if (args.Current != "key")
				return CommandResultCode.SyntaxError;
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;

			string key = args.Current;

			string pathName = null;

			if (args.MoveNext()) {
				if (args.Current != "to")
					return CommandResultCode.SyntaxError;
				if (!args.MoveNext())
					return CommandResultCode.SyntaxError;

				pathName = args.Current;
			}

			try {
				context.AddValueToPath(pathName, tableName, key, value);
			} catch(Exception e) {
				Error.WriteLine("error while adding the value: " + e.Message);
				Error.WriteLine();
				return CommandResultCode.ExecutionFailed;
			}
			
			return CommandResultCode.Success;
		}

		
		public override CommandResultCode Execute(IExecutionContext context, CommandArguments args) {
			NetworkContext networkContext = (NetworkContext)context;
			
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			
			string pathType;
			
			if (args.Current == "path") {
				if (!args.MoveNext())
					return CommandResultCode.SyntaxError;
				
				pathType = args.Current;
			} else if (args.Current == "base") {
				if (!args.MoveNext())
					return CommandResultCode.SyntaxError;
				if (args.Current != "path")
					return CommandResultCode.SyntaxError;

				pathType = "Deveel.Data.BasePath, cloudbase";
			} else if (args.Current == "value") {
				return AddValue(networkContext, args);
			} else {
				return CommandResultCode.SyntaxError;
			}
			
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			
			string pathName = args.Current;
			
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			
			if (args.Current != "to")
				return CommandResultCode.SyntaxError;
			
			if (!args.MoveNext())
				return CommandResultCode.SyntaxError;
			
			string rootAddress = args.Current;
			
			return AddPath(networkContext, pathType, pathName, rootAddress);
		}
	}
}