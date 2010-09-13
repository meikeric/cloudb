﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Deveel.Data.Net {
	public class TcpProxyServiceConnector : IServiceConnector {
		public TcpProxyServiceConnector(IPAddress proxyAddress, int proxyPort, string password) {
			this.proxyAddress = proxyAddress;
			this.proxyPort = proxyPort;
			this.password = password;

			Connect();
		}

		private readonly IPAddress proxyAddress;
		private readonly int proxyPort;
		private readonly string password;
		private string initString;

		private BinaryReader pin;
		private BinaryWriter pout;
		private readonly object proxy_lock = new object();

		public int ProxyPort {
			get { return proxyPort; }
		}

		public IPAddress ProxyAddress {
			get { return proxyAddress; }
		}

		private void Connect() {
			Socket socket = new Socket(proxyAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(proxyAddress, proxyPort);
			NetworkStream stream = new NetworkStream(socket);

			pin = new BinaryReader(new BufferedStream(stream), Encoding.Unicode);
			pout = new BinaryWriter(new BufferedStream(stream), Encoding.Unicode);

			try {
				// Perform the handshake,
				long v = pin.ReadInt64();
				pout.Write(v);
				pout.Flush();
				initString = pin.ReadString();
				pout.Write(password);
				pout.Flush();
			} catch (IOException e) {
				throw new Exception("IO Error", e);
			}
		}

		#region Implementation of IDisposable

		public void Dispose() {
			Close();
		}

		#endregion

		#region Implementation of IServiceConnector

		public void Close() {
			try {
				lock (proxy_lock) {
					pout.Write('0');
					pout.Flush();
				}
				pin.Close();
				pout.Close();
			} catch (IOException) {
				//TODO: ERROR log ...
			} finally {
				initString = null;
				pin = null;
				pout = null;
			}
		}

		public IMessageProcessor Connect(IPServiceAddress address, ServiceType type) {
			return new MessageProcessor(this, address, type);
		}
		
		IMessageProcessor IServiceConnector.Connect(IServiceAddress address, ServiceType type) {
			return Connect((IPServiceAddress)address, type);
		}

		#endregion

		#region MessageProcessor

		private class MessageProcessor : IMessageProcessor {
			public MessageProcessor(TcpProxyServiceConnector connector, IPServiceAddress address, ServiceType serviceType) {
				this.connector = connector;
				this.address = address;
				this.serviceType = serviceType;
			}

			private readonly TcpProxyServiceConnector connector;
			private readonly IPServiceAddress address;
			private readonly ServiceType serviceType;

			#region Implementation of IMessageProcessor

			public MessageStream Process(MessageStream messageStream) {
				try {
					lock (connector.proxy_lock) {
						MessageStreamSerializer serializer = new MessageStreamSerializer();

						char code = '\0';
						if (serviceType == ServiceType.Admin)
							code = 'a';
						else if (serviceType == ServiceType.Block)
							code = 'b';
						else if (serviceType == ServiceType.Manager)
							code = 'm';
						else if (serviceType == ServiceType.Root)
							code = 'r';

						// Write the message.
						connector.pout.Write(code);
						IPServiceAddressHandler handler = new IPServiceAddressHandler();
						byte[] addressBytes = handler.ToBytes(address);
						connector.pout.Write(addressBytes);
						serializer.Serialize(messageStream, connector.pout.BaseStream);
						connector.pout.Flush();

						return serializer.Deserialize(connector.pin.BaseStream);
					}
				} catch (IOException e) {
					// Probably caused because the proxy closed the connection when a
					// timeout was reached.
					throw new Exception("IO Error", e);
				}
			}

			#endregion
		}

		#endregion
	}
}