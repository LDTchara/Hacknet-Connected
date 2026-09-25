using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Hacknet;

public class ExternalCounterpart
{
	private static Dictionary<string, string> networkIPList;

	private static ASCIIEncoding encoder;

	public string idName;

	public string connectionIP;

	public bool isConnected = false;

	private TcpClient connection;

	private byte[] buffer;

	public static string getIPForServerName(string serverName)
	{
		if (networkIPList == null)
		{
			loadNetIPList();
		}
		if (networkIPList.ContainsKey(serverName))
		{
			return networkIPList[serverName];
		}
		throw new InvalidOperationException("Server Name Not Found");
	}

	private static void loadNetIPList()
	{
		networkIPList = new Dictionary<string, string>();
		string[] array = Utils.readEntireFile("Content/Network/NetworkIPList.txt").Split(new string[2] { "\n\r", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(Utils.spaceDelim);
			networkIPList.Add(array2[0], array2[1]);
		}
	}

	public ExternalCounterpart(string idName, string ipEndpoint)
	{
		this.idName = idName;
		connectionIP = ipEndpoint;
		buffer = new byte[4096];
		if (encoder == null)
		{
			encoder = new ASCIIEncoding();
		}
	}

	public void sendMessage(string message)
	{
		if (isConnected)
		{
			writeMessage(message);
		}
	}

	public void disconnect()
	{
		if (isConnected)
		{
			writeMessage("Disconnecting");
			connection.Close();
			isConnected = false;
		}
	}

	public void testConnection()
	{
		establishConnection();
		while (!isConnected)
		{
			Thread.Sleep(5);
		}
		writeMessage("Test Message From " + idName);
	}

	public void establishConnection()
	{
		TcpClient tcpClient = new TcpClient();
		tcpClient.BeginConnect(IPAddress.Parse(connectionIP), Multiplayer.PORT, DoTcpConnectionCallback, tcpClient);
	}

	private void DoTcpConnectionCallback(IAsyncResult ar)
	{
		if (ar.AsyncState is TcpClient { Connected: not false } tcpClient)
		{
			isConnected = true;
			connection = tcpClient;
			tcpClient.EndConnect(ar);
		}
	}

	public void writeMessage(string message)
	{
		if (isConnected)
		{
			NetworkStream stream = connection.GetStream();
			buffer = encoder.GetBytes(message);
			stream.BeginWrite(buffer, 0, buffer.Length, TCPWriteMessageCallback, stream);
		}
		else
		{
			establishConnection();
		}
	}

	private void TCPWriteMessageCallback(IAsyncResult ar)
	{
		if (ar.AsyncState is NetworkStream networkStream)
		{
			try
			{
				networkStream.EndWrite(ar);
			}
			catch (Exception)
			{
			}
		}
	}
}
