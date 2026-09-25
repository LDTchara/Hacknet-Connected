using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace Hacknet.Magic;

public class NAT
{
	private static TimeSpan _timeout = new TimeSpan(0, 0, 0, 3);

	private static string _descUrl;

	private static string _serviceUrl;

	private static string _eventUrl;

	public static TimeSpan TimeOut
	{
		get
		{
			return _timeout;
		}
		set
		{
			_timeout = value;
		}
	}

	public static bool Discover()
	{
		Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
		socket.ReceiveTimeout = 5000;
		string s = "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nST:upnp:rootdevice\r\nMAN:\"ssdp:discover\"\r\nMX:3\r\n\r\n";
		DebugLog.add(s);
		byte[] bytes = Encoding.ASCII.GetBytes(s);
		IPEndPoint remoteEP = new IPEndPoint(IPAddress.Broadcast, 1900);
		byte[] array = new byte[4096];
		DateTime now = DateTime.Now;
		do
		{
			socket.SendTo(bytes, remoteEP);
			socket.SendTo(bytes, remoteEP);
			socket.SendTo(bytes, remoteEP);
			int num = 0;
			do
			{
				try
				{
					num = socket.Receive(array);
				}
				catch (Exception value)
				{
					Console.WriteLine(value);
					num = 0;
				}
				string text = Encoding.ASCII.GetString(array, 0, num).ToLower();
				DebugLog.add(text);
				if (text.Contains("upnp:rootdevice"))
				{
					text = text.Substring(text.ToLower().IndexOf("location:") + 9);
					text = text.Substring(0, text.IndexOf("\r")).Trim();
					if (!string.IsNullOrEmpty(_serviceUrl = GetServiceUrl(text)))
					{
						_descUrl = text;
						return true;
					}
				}
			}
			while (num > 0);
		}
		while (now.Subtract(DateTime.Now) < _timeout);
		return false;
	}

	private static string GetServiceUrl(string resp)
	{
		try
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(WebRequest.Create(resp).GetResponse().GetResponseStream());
			XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
			xmlNamespaceManager.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
			XmlNode xmlNode = xmlDocument.SelectSingleNode("//tns:device/tns:deviceType/text()", xmlNamespaceManager);
			if (!xmlNode.Value.Contains("InternetGatewayDevice"))
			{
				return null;
			}
			XmlNode xmlNode2 = xmlDocument.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:controlURL/text()", xmlNamespaceManager);
			XmlNode xmlNode3;
			if (xmlNode2 == null)
			{
				xmlNode2 = xmlDocument.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANPPPConnection:1\"]/tns:controlURL/text()", xmlNamespaceManager);
				if (xmlNode2 == null)
				{
					return null;
				}
				xmlNode3 = xmlDocument.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANPPPConnection:1\"]/tns:eventSubURL/text()", xmlNamespaceManager);
				_eventUrl = CombineUrls(resp, xmlNode3.Value);
				return CombineUrls(resp, xmlNode2.Value);
			}
			xmlNode3 = xmlDocument.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:eventSubURL/text()", xmlNamespaceManager);
			_eventUrl = CombineUrls(resp, xmlNode3.Value);
			return CombineUrls(resp, xmlNode2.Value);
		}
		catch
		{
			return null;
		}
	}

	private static string CombineUrls(string resp, string p)
	{
		int num = resp.IndexOf("://");
		num = resp.IndexOf('/', num + 3);
		return resp.Substring(0, num) + p;
	}

	public static void ForwardPort(int port, ProtocolType protocol, string description)
	{
		if (string.IsNullOrEmpty(_serviceUrl))
		{
			throw new Exception("No UPnP service available or Discover() has not been called");
		}
		XmlDocument xmlDocument = SOAPRequest(_serviceUrl, "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\"><NewRemoteHost></NewRemoteHost><NewExternalPort>" + port + "</NewExternalPort><NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol><NewInternalPort>" + port + "</NewInternalPort><NewInternalClient>" + Dns.GetHostAddresses(Dns.GetHostName())[0].ToString() + "</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>" + description + "</NewPortMappingDescription><NewLeaseDuration>0</NewLeaseDuration></u:AddPortMapping>", "AddPortMapping");
	}

	public static void DeleteForwardingRule(int port, ProtocolType protocol)
	{
		if (string.IsNullOrEmpty(_serviceUrl))
		{
			throw new Exception("No UPnP service available or Discover() has not been called");
		}
		XmlDocument xmlDocument = SOAPRequest(_serviceUrl, "<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\"><NewRemoteHost></NewRemoteHost><NewExternalPort>" + port + "</NewExternalPort><NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol></u:DeletePortMapping>", "DeletePortMapping");
	}

	public static IPAddress GetExternalIP()
	{
		if (string.IsNullOrEmpty(_serviceUrl))
		{
			throw new Exception("No UPnP service available or Discover() has not been called");
		}
		XmlDocument xmlDocument = SOAPRequest(_serviceUrl, "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\"></u:GetExternalIPAddress>", "GetExternalIPAddress");
		XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
		xmlNamespaceManager.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
		string value = xmlDocument.SelectSingleNode("//NewExternalIPAddress/text()", xmlNamespaceManager).Value;
		return IPAddress.Parse(value);
	}

	private static XmlDocument SOAPRequest(string url, string soap, string function)
	{
		string s = "<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>" + soap + "</s:Body></s:Envelope>";
		WebRequest webRequest = WebRequest.Create(url);
		webRequest.Method = "POST";
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		webRequest.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:WANIPConnection:1#" + function + "\"");
		webRequest.ContentType = "text/xml; charset=\"utf-8\"";
		webRequest.ContentLength = bytes.Length;
		webRequest.GetRequestStream().Write(bytes, 0, bytes.Length);
		XmlDocument xmlDocument = new XmlDocument();
		WebResponse response = webRequest.GetResponse();
		Stream responseStream = response.GetResponseStream();
		xmlDocument.Load(responseStream);
		return xmlDocument;
	}
}
