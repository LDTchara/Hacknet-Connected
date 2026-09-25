using System.Xml;

namespace Hacknet;

public struct UserDetail
{
	public string name;

	public string pass;

	public byte type;

	public bool known;

	public UserDetail(string user, string password, byte accountType)
	{
		name = user;
		pass = password;
		type = accountType;
		known = false;
	}

	public UserDetail(string user)
	{
		name = user;
		pass = PortExploits.getRandomPassword();
		type = 1;
		known = false;
	}

	public string getSaveString()
	{
		return "<user name=\"" + name + "\" pass=\"" + pass + "\" type=\"" + type + "\" known=\"" + known + "\" />";
	}

	public static UserDetail loadUserDetail(XmlReader reader)
	{
		reader.MoveToAttribute("name");
		string user = reader.ReadContentAsString();
		reader.MoveToAttribute("pass");
		string password = reader.ReadContentAsString();
		reader.MoveToAttribute("type");
		byte accountType = (byte)reader.ReadContentAsInt();
		reader.MoveToAttribute("known");
		string text = reader.ReadContentAsString();
		bool flag = text.ToLower().Equals("true");
		UserDetail result = new UserDetail(user, password, accountType);
		result.known = flag;
		return result;
	}

	public override bool Equals(object obj)
	{
		UserDetail? userDetail = obj as UserDetail?;
		if (userDetail.HasValue)
		{
			return name == userDetail.Value.name && pass == userDetail.Value.pass && type == userDetail.Value.type && known == userDetail.Value.known;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
