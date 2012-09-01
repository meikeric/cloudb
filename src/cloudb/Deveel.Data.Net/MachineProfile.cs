﻿using System;

namespace Deveel.Data.Net {
	public class MachineProfile {
		private readonly IServiceAddress address;

		private bool isBlock;
		private bool isRoot;
		private bool isManager;

		private long memoryUsed;
		private long memoryTotal;
		private long diskUsed;
		private long diskTotal;

		private String errorMessage;

		internal MachineProfile(IServiceAddress address) {
			this.address = address;
		}

		public IServiceAddress ServiceAddress {
			get { return address; }
		}

		public bool IsBlock {
			get { return isBlock; }
			internal set { isBlock = value; }
		}

		public bool IsRoot {
			get { return isRoot; }
			internal set { isRoot = value; }
		}

		public bool IsManager {
			get { return isManager; }
			internal set { isManager = value; }
		}

		internal bool IsNotAssigned {
			get { return !isBlock && !isManager && !isRoot; }
		}

		public bool IsError {
			get { return (errorMessage != null); }
		}

		public string ErrorMessage {
			get { return errorMessage; }
			internal set { errorMessage = value; }
		}

		public long MemoryUsed {
			get { return memoryUsed; }
			internal set { memoryUsed = value; }
		}

		public long MemoryTotal {
			get { return memoryTotal; }
			internal set { memoryTotal = value; }
		}

		public long DiskUsed {
			get { return diskUsed; }
			internal set { diskUsed = value; }
		}

		public long DiskTotal {
			get { return diskTotal; }
			internal set { diskTotal = value; }
		}

		public bool IsInRole(ServiceType serviceType) {
			if (serviceType == ServiceType.Manager)
				return isManager;
			if (serviceType == ServiceType.Root)
				return isRoot;
			if (serviceType == ServiceType.Block)
				return isBlock;

			return false;
		}
	}
}