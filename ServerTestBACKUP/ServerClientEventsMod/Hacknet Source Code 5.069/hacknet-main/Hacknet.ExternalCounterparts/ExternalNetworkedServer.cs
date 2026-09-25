using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Hacknet.ExternalCounterparts;

public class ExternalNetworkedServer
{
	private const int BUFFER_SIZE = 4096;

	public Action<string> messageReceived;

	private static ASCIIEncoding encoder;

	private List<TcpClient> connections;

	private Dictionary<NetworkStream, byte[]> buffers;

	private TcpListener listener;

	public ExternalNetworkedServer()
	{
		connections = new List<TcpClient>();
		buffers = new Dictionary<NetworkStream, byte[]>();
		if (encoder == null)
		{
			encoder = new ASCIIEncoding();
		}
	}

	public void initializeListener()
	{
		listener = new TcpListener(new IPEndPoint(IPAddress.Any, Multiplayer.PORT));
		listener.Start();
		listener.BeginAcceptTcpClient(AcceptTcpConnectionCallback, listener);
	}

	public void closeServer()
	{
		listener.Stop();
		foreach (TcpClient connection in connections)
		{
			connection.GetStream().Close();
			connection.Close();
		}
	}

	private void AcceptTcpConnectionCallback(IAsyncResult ar)
	{
		if (!(ar.AsyncState is TcpListener tcpListener))
		{
			return;
		}
		try
		{
			TcpClient tcpClient = listener.EndAcceptTcpClient(ar);
			if (tcpClient != null)
			{
				connections.Add(tcpClient);
				NetworkStream stream = tcpClient.GetStream();
				buffers.Add(stream, new byte[4096]);
				if (messageReceived != null)
				{
					messageReceived("Connection");
				}
				tcpClient.GetStream().BeginRead(buffers[stream], 0, 4096, TcpReadCallback, stream);
				tcpListener.BeginAcceptTcpClient(AcceptTcpConnectionCallback, tcpListener);
			}
		}
		catch (Exception)
		{
		}
	}

	private void TcpReadCallback(IAsyncResult ar)
	{
		if (!(ar.AsyncState is NetworkStream networkStream))
		{
			return;
		}
		int num = 0;
		try
		{
			num = networkStream.EndRead(ar);
			if (num != 0)
			{
				byte[] bytes = buffers[networkStream];
				string @string = encoder.GetString(bytes, 0, num);
				if (messageReceived != null)
				{
					messageReceived(@string);
				}
				networkStream.BeginRead(buffers[networkStream], 0, 4096, TcpReadCallback, networkStream);
			}
		}
		catch (Exception)
		{
		}
	}
}
