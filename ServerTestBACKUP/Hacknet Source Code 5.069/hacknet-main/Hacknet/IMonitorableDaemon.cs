using System;

namespace Hacknet;

internal interface IMonitorableDaemon
{
	void SubscribeToAlertActionFroNewMessage(Action<string, string> act);

	void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act);

	bool ShouldDisplayNotifications();

	string GetName();

	void navigatedTo();
}
