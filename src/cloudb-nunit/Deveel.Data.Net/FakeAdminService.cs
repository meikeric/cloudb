﻿using System;
using System.Collections.Generic;

using Deveel.Data.Configuration;
using Deveel.Data.Net.Messaging;

namespace Deveel.Data.Net {
	public sealed class FakeAdminService : AdminService {		
		public FakeAdminService(FakeServiceConnector connector, StoreType storeType)
			: base(FakeServiceAddress.Local, connector, new FakeServiceFactory(storeType)) {
		}
		
		public FakeAdminService(FakeServiceConnector connector)
			: this(connector, StoreType.Memory) {
		}
		
		public FakeAdminService(StoreType storeType)
			: this(null, storeType) {
			Connector = new FakeServiceConnector(ProcessCallback);
		}

		public FakeAdminService()
			: this(StoreType.Memory) {
		}
		
		internal IEnumerable<Message> ProcessCallback(ServiceType serviceType, IEnumerable<Message> inputStream) {
			if (serviceType == ServiceType.Admin)
				return Processor.Process(inputStream);
			if (serviceType == ServiceType.Manager)
				return Manager.Processor.Process(inputStream);
			if (serviceType == ServiceType.Root)
				return Root.Processor.Process(inputStream);
			if (serviceType == ServiceType.Block)
				return Block.Processor.Process(inputStream);

			throw new InvalidOperationException();
		}

		#region FakeServiceFactory

		private class FakeServiceFactory : IServiceFactory {
			private IServiceFactory factory;
			private readonly StoreType storeType;

			public FakeServiceFactory(StoreType storeType) {
				this.storeType = storeType;
			}

			public void Init(AdminService adminService) {
				if (storeType == StoreType.FileSystem) {
					ConfigSource config = adminService.Config;
					string basePath = config.GetString("node_directory", "./base");
					factory = new FileSystemServiceFactory(basePath);
				} else {
					factory = new MemoryServiceFactory();
				}

				factory.Init(adminService);
			}

			public IService CreateService(IServiceAddress serviceAddress, ServiceType serviceType, IServiceConnector connector) {
				return factory.CreateService(serviceAddress, serviceType, connector);
			}
		}

		#endregion
	}
}