﻿using System;

namespace Deveel.Data.Net.Client {
	public interface IRequestHandler {
		IPathContext CreateContext(NetworkClient client, string pathName);

		bool CanHandleClientType(string clientType);

		ActionResponse HandleRequest(ActionRequest request);
	}
}