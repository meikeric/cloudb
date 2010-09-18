﻿using System;

namespace Deveel.Data.Net {
	public interface IService : IDisposable {
		ServiceType ServiceType { get; }
		
		IMessageProcessor Processor { get; }
		
		void Init();
	}
}