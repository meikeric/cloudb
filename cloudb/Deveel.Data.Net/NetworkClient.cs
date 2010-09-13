using System;
using System.IO;

namespace Deveel.Data.Net {
	public class NetworkClient : IDisposable {
		public NetworkClient(IServiceAddress managerAddress, IServiceConnector connector)
			: this(managerAddress, connector, ManagerCacheState.GetCache(managerAddress)) {
		}

		public NetworkClient(IServiceAddress managerAddress, IServiceConnector connector, INetworkCache cache) {
			this.connector = connector;
			this.managerAddress = managerAddress;
			this.cache = cache;
		}

		private IServiceConnector connector;
		private readonly IServiceAddress managerAddress;
		private NetworkTreeSystem storageSystem;
		private readonly INetworkCache cache;

		private long maxTransactionNodeCacheHeapSize;

		public long MaxTransactionNodeCacheHeapSize {
			get { return maxTransactionNodeCacheHeapSize; }
			set { maxTransactionNodeCacheHeapSize = value; }
		}

		public void Connect() {
			storageSystem = new NetworkTreeSystem(connector, managerAddress, cache);
			storageSystem.SetMaxNodeCacheHeapSize(maxTransactionNodeCacheHeapSize);
		}

		private void OnDisconnected() {
			connector = null;
			storageSystem = null;
		}

		internal void Disconnect() {
			if (connector != null)
				connector.Close();

			OnDisconnected();
		}

		public DataAddress CreateEmptyDatabase() {
			try {
				return storageSystem.CreateEmptyDatabase();
			} catch (IOException e) {
				throw new NetworkWriteException(e.Message, e);
			}
		}

		public ITransaction CreateEmptyTransaction() {
			// Create the transaction object and return it,
			return storageSystem.CreateEmptyTransaction();
		}

		public ITransaction CreateTransaction(DataAddress rootNode) {
			return storageSystem.CreateTransaction(rootNode);
		}

		public DataAddress FlushTransaction(ITransaction transaction) {
			return storageSystem.FlushTransaction(transaction);
		}

		public DataAddress Commit(string pathName, DataAddress proposal) {
			IServiceAddress rootAddress = storageSystem.GetRootServer(pathName);
			return storageSystem.Commit(rootAddress, pathName, proposal);
		}

		public void DisposeTransaction(ITransaction transaction) {
			try {
				storageSystem.DisposeTransaction(transaction);
			} catch (IOException e) {
				throw new Exception("IO Error: " + e.Message);
			}
		}

		public DataAddress[] GetHistoricalSnapshots(string pathName, DateTime timeStart, DateTime timeEnd) {
			IServiceAddress rootAddress = storageSystem.GetRootServer(pathName);
			return storageSystem.GetPathHistorical(rootAddress, pathName, timeStart, timeEnd);
		}

		public DataAddress GetCurrentSnapshot(string pathName) {
			IServiceAddress rootAddress = storageSystem.GetRootServer(pathName);

			if (rootAddress == null)
				throw new Exception("There are no root servers in the network for path '" + pathName + "'");

			return storageSystem.GetPathNow(rootAddress, pathName);
		}

		public string[] GetNetworkPaths() {
			return storageSystem.FindAllPaths();
		}

		public TreeGraph CreateDiagnosticGraph(ITransaction t) {
			return storageSystem.CreateDiagnosticGraph(t);
		}

		public void CreateReachabilityList(TextWriter warning_log, DataAddress root_node, NumberList discovered_node_list) {
			try {
				storageSystem.CreateReachabilityList(warning_log, root_node.Value, discovered_node_list);
			} catch (IOException e) {
				throw new ApplicationException("IO Error: " + e.Message);
			}
		}

		#region Implementation of IDisposable

		public void Dispose() {
			Disconnect();
		}

		#endregion

		/*
		public static NetworkClient ConnectTcp(Properties p) {
			string managerAddress = p.GetProperty("manager_address");
			string networkPassword = p.GetProperty("network_password");

			if (managerAddress == null)
				throw new Exception("'manager_address' property not found.");
			if (networkPassword == null)
				throw new Exception("'network_password' property not found.");

			// Get the type of connection,
			string connectType = p.GetProperty("connect_type", "direct");

			// Direct client connect
			if (connectType.Equals("direct"))
				return ConnectTcp(ServiceAddress.Parse(managerAddress), networkPassword);

			// Connection to network via proxy
			if (connectType.Equals("proxy")) {
				string proxyHost = p.GetProperty("proxy_host");
				string proxyPort = p.GetProperty("proxy_port");

				if (proxyHost == null)
					throw new Exception("'proxy_host' property not found.");
				if (proxyPort == null)
					throw new Exception("'proxy_port' property not found.");

				// Try and parse the port,
				int pport;
				if (!Int32.TryParse(proxyPort, out pport))
					throw new Exception("Unable to parse proxy port property.");

				IPAddress phost = IPAddress.Parse(proxyHost);
				return ConnectProxyTcp(phost, pport, ServiceAddress.Parse(managerAddress), networkPassword);
			}
			
			throw new Exception("Unknown proxy type: " + connectType);
		}

		public static NetworkClient ConnectTcp(ServiceAddress manager_server, String network_password, INetworkCache lnc) {
			TcpNetworkClient client = new TcpNetworkClient(manager_server, network_password, lnc);
			client.Connect();
			return client;
		}

		public static NetworkClient ConnectTcp(ServiceAddress manager_server, string network_password) {
			return ConnectTcp(manager_server, network_password, ManagerCacheState.GetCache(manager_server));
		}

		public static NetworkClient ConnectProxyTcp(IPAddress proxy_address, int proxy_port, ServiceAddress manager_server, String network_password, INetworkCache lnc) {
			TcpProxyNetworkClient proxy_client = new TcpProxyNetworkClient(proxy_address, proxy_port, manager_server, network_password, lnc);
			proxy_client.Connect();
			return proxy_client;
		}

		public static NetworkClient ConnectProxyTcp(IPAddress proxy_address, int proxy_port, ServiceAddress manager_server, String network_password) {
			return ConnectProxyTcp(proxy_address, proxy_port, manager_server, network_password, ManagerCacheState.GetCache(manager_server));
		}
		
#region TcpProxyNetworkClient

sealed class TcpProxyNetworkClient : TcpNetworkClient {
	private TcpProxyServiceConnector proxy_connector;

	private readonly IPAddress proxy_host;
	private readonly int proxy_port;

	public TcpProxyNetworkClient(IPAddress proxy_host, int proxy_port, ServiceAddress manager_server, string network_password, INetworkCache lnc)
		: base(manager_server, network_password, lnc) {
		this.proxy_host = proxy_host;
		this.proxy_port = proxy_port;
	}

	public override void Connect() {
		proxy_connector = new TcpProxyServiceConnector(NetworkPassword);
		proxy_connector.Connect(proxy_host, proxy_port);
		Connect(proxy_connector);
	}
}

#endregion

#region TcpProxyServiceConnector

private class TcpProxyServiceConnector : ProxyServiceConnector {
	public TcpProxyServiceConnector(string net_password) 
		: base(net_password) {
	}

	public void Connect(IPAddress proxy_address, int proxy_port) {
		try {
			Socket socket = new Socket(proxy_address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(proxy_address, proxy_port);
			Connect(new NetworkStream(socket, FileAccess.ReadWrite));
		} catch (IOException e) {
			throw new Exception("IO Error", e);
		}
	}
}

#endregion

*/
	}
}