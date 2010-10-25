﻿using System;
using System.Collections.Generic;

namespace Deveel.Data.Net {
	public sealed class MemoryManagerService : ManagerService {
		private MemoryDatabase database;

		public MemoryManagerService(IServiceConnector connector, IServiceAddress address) 
			: base(connector, address) {
		}

		protected override void OnStart() {
			database = new MemoryDatabase(1024);
			database.Start();

			SetBlockDatabase(database);
		}

		protected override void OnStop() {
			base.OnStop();

			database.Stop();
			database = null;
		}

		protected override void PersistBlockServers(IList<BlockServerInfo> servers_list) {
		}

		protected override void PersistRootServers(IList<RootServerInfo> servers_list) {
		}
	}
}