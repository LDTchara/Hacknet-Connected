using System;
using System.Collections.Generic;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Daemons.Helpers;

public class BasicMedicalMonitor : IMedicalMonitor
{
	public struct MonitorRecordKeypoint
	{
		public float timeOffset;

		public float value;
	}

	private CLinkBuffer<MonitorRecordKeypoint> data = new CLinkBuffer<MonitorRecordKeypoint>(1024);

	private Func<float, float, List<MonitorRecordKeypoint>> UpdateAction;

	private Func<float, float, List<MonitorRecordKeypoint>> HeartBeatAction;

	public BasicMedicalMonitor(Func<float, float, List<MonitorRecordKeypoint>> updateAction, Func<float, float, List<MonitorRecordKeypoint>> heartbeatAction)
	{
		UpdateAction = updateAction;
		HeartBeatAction = heartbeatAction;
	}

	public float GetCurrentValue(float timeRollback)
	{
		int num = 0;
		Vector2 vector = new Vector2(0f, 0f);
		MonitorRecordKeypoint monitorRecordKeypoint;
		for (float num2 = 0f; num2 < timeRollback; num2 += monitorRecordKeypoint.timeOffset)
		{
			monitorRecordKeypoint = data.Get(num);
			if (monitorRecordKeypoint.timeOffset == 0f)
			{
				break;
			}
			if (num2 + monitorRecordKeypoint.timeOffset >= timeRollback)
			{
				MonitorRecordKeypoint monitorRecordKeypoint2 = data.Get(num - 1);
				float num3 = (timeRollback - num2) / monitorRecordKeypoint.timeOffset;
				Vector2 value = new Vector2(0f, monitorRecordKeypoint2.value * 1f);
				Vector2 value2 = new Vector2(0f, monitorRecordKeypoint.value * 1f);
				vector = Vector2.Lerp(value, value2, 1f - num3);
				num--;
				break;
			}
			num--;
		}
		return vector.Y;
	}

	public void Update(float dt)
	{
		List<MonitorRecordKeypoint> list = UpdateAction(data.Get(0).value, dt);
		for (int i = 0; i < list.Count; i++)
		{
			data.Add(list[i]);
		}
	}

	public void HeartBeat(float beatTime)
	{
		List<MonitorRecordKeypoint> list = HeartBeatAction(data.Get(0).value, beatTime);
		for (int i = 0; i < list.Count; i++)
		{
			data.Add(list[i]);
		}
	}

	public void Draw(Rectangle bounds, SpriteBatch sb, Color c, float timeRollback)
	{
		int num = 0;
		float num2 = 4f;
		float num3 = 100f;
		float num4 = (float)bounds.Height / 3f;
		float num5 = (float)bounds.Width - num2;
		Vector2 vector = new Vector2(num5, 0f);
		bool flag = false;
		Vector2 vector2 = new Vector2(bounds.X, (float)bounds.Y + (float)bounds.Height / 2f);
		if (timeRollback > 0f)
		{
			MonitorRecordKeypoint monitorRecordKeypoint;
			for (float num6 = 0f; num6 < timeRollback; num6 += monitorRecordKeypoint.timeOffset)
			{
				monitorRecordKeypoint = data.Get(num);
				if (monitorRecordKeypoint.timeOffset == 0f)
				{
					break;
				}
				if (num6 + monitorRecordKeypoint.timeOffset >= timeRollback)
				{
					MonitorRecordKeypoint monitorRecordKeypoint2 = data.Get(num - 1);
					float num7 = (timeRollback - num6) / monitorRecordKeypoint.timeOffset;
					Vector2 value = new Vector2(num5, monitorRecordKeypoint2.value * num4);
					Vector2 value2 = new Vector2(num5, monitorRecordKeypoint.value * num4);
					vector = Vector2.Lerp(value, value2, 1f - num7);
					flag = true;
					num5 -= monitorRecordKeypoint.timeOffset * num3 * (1f - num7);
					num--;
					break;
				}
				num--;
			}
		}
		while (num5 >= num2)
		{
			MonitorRecordKeypoint monitorRecordKeypoint = data.Get(num);
			if (monitorRecordKeypoint.timeOffset == 0f)
			{
				break;
			}
			Vector2 value2 = new Vector2(num5, monitorRecordKeypoint.value * num4);
			if (flag)
			{
				Utils.drawLine(sb, vector2 + vector, vector2 + value2, Vector2.Zero, c, 0.56f);
			}
			else
			{
				flag = true;
			}
			num5 -= monitorRecordKeypoint.timeOffset * num3;
			vector = value2;
			num--;
			flag = true;
		}
	}
}
