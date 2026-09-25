using System;
using Microsoft.Xna.Framework;

namespace Hacknet;

public static class NetmapSortingAlgorithms
{
	internal static Vector2 GetNodePosition(NetmapSortingAlgorithm algorithm, float mapWidth, float mapHeight, Computer node, int nodeIndex, int totalNodes, int totalRevealedNodes, OS os)
	{
		switch (algorithm)
		{
		default:
		{
			Vector2 vector = Utils.Clamp(node.location, 0f, 1f);
			return new Vector2(vector.X * mapWidth, vector.Y * mapHeight);
		}
		case NetmapSortingAlgorithm.Grid:
		{
			int num7 = 5;
			int num8 = (int)((float)totalNodes / (float)num7) + 1;
			if (totalRevealedNodes > 60)
			{
				num7 = 7;
				num8 = (int)((float)totalNodes / (float)num7) + 1;
			}
			if (totalRevealedNodes > 150)
			{
				num7 = 9;
				num8 = (int)((float)totalNodes / (float)num7) + 1;
			}
			int num9 = (int)((float)nodeIndex / (float)num7 + 0f);
			int num10 = num7 * num8;
			if (totalNodes > num10 - 10)
			{
				num9 = (int)((float)nodeIndex / (float)num7 + 0.5f);
				num8++;
			}
			int num11 = nodeIndex % num7;
			float xout2 = (float)num9 / ((float)num8 - 2f);
			float yout2 = (float)num11 / ((float)num7 - 1f);
			ClipToMargins(xout2, yout2, totalRevealedNodes, out xout2, out yout2);
			xout2 *= mapWidth;
			yout2 *= mapHeight;
			return new Vector2(xout2, yout2);
		}
		case NetmapSortingAlgorithm.Chaos:
			return new Vector2(Utils.randm(1f) * mapWidth, Utils.randm(1f) * mapHeight);
		case NetmapSortingAlgorithm.LockGrid:
		{
			int num = 5;
			int count = os.netMap.visibleNodes.Count;
			int num2 = count + (int)((float)(totalNodes - count) / 8f);
			int num3 = (int)((float)num2 / (float)num) + 1;
			if (count > 50)
			{
				num = 6;
				num3 = (int)((float)count / (float)num) + 1;
			}
			if (count > 80)
			{
				num = 7;
				num3 = (int)((float)count / (float)num) + 1;
			}
			if (count > 110)
			{
				num = 8;
				num3 = (int)((float)count / (float)num) + 1;
			}
			if (count > 150)
			{
				num = 9;
				num3 = (int)((float)count / (float)num) + 1;
			}
			num3++;
			num3 = Math.Max(num3, 3);
			int num4 = os.netMap.visibleNodes.IndexOf(os.netMap.nodes.IndexOf(node));
			if (num4 == -1)
			{
				num4 = 1;
			}
			int num5 = num4 / num;
			if (count > 150)
			{
				num5 = (int)((float)num4 / (float)num + 0.5f);
				num3++;
			}
			int num6 = num4 % num;
			float xout = (float)num5 / ((float)num3 - 2f);
			float yout = (float)num6 / ((float)num - 1f);
			ClipToMargins(xout, yout, totalRevealedNodes, out xout, out yout);
			xout *= mapWidth;
			yout *= mapHeight;
			return new Vector2(xout, yout);
		}
		}
	}

	private static void ClipToMargins(float x, float y, int revealed, out float xout, out float yout)
	{
		bool flag = revealed > 60;
		xout = Math.Min(flag ? 0.999f : 0.98f, Math.Max(flag ? 0.001f : 0.02f, x));
		yout = Math.Min(flag ? 0.98f : 0.93f, Math.Max(flag ? 0.002f : 0.02f, y));
	}
}
