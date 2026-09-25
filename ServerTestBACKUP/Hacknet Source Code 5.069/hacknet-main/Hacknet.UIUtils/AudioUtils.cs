using System.IO;

namespace Hacknet.UIUtils;

internal class AudioUtils
{
	public static void openWav(string filename, out double[] left, out double[] right)
	{
		byte[] array = File.ReadAllBytes(filename);
		int num = array[22];
		int num2 = 12;
		while (array[num2] != 100 || array[num2 + 1] != 97 || array[num2 + 2] != 116 || array[num2 + 3] != 97)
		{
			num2 += 4;
			int num3 = array[num2] + array[num2 + 1] * 256 + array[num2 + 2] * 65536 + array[num2 + 3] * 16777216;
			num2 += 4 + num3;
		}
		num2 += 8;
		int num4 = (array.Length - num2) / 2;
		if (num == 2)
		{
			num4 /= 2;
		}
		left = new double[num4];
		if (num == 2)
		{
			right = new double[num4];
		}
		else
		{
			right = null;
		}
		int num5 = 0;
		while (num2 < array.Length)
		{
			left[num5] = bytesToDouble(array[num2], array[num2 + 1]);
			num2 += 2;
			if (num == 2)
			{
				right[num5] = bytesToDouble(array[num2], array[num2 + 1]);
				num2 += 2;
			}
			num5++;
		}
	}

	private static double bytesToDouble(byte firstByte, byte secondByte)
	{
		short num = (short)((secondByte << 8) | firstByte);
		return (double)num / 32768.0;
	}
}
