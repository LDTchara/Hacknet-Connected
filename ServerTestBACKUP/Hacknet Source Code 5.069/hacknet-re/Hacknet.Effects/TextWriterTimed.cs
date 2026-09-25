namespace Hacknet.Effects;

public static class TextWriterTimed
{
	public static int WriteTextToTerminal(string wholeText, object osObj, float timePerChar, float normalLettersDelayForNewline, float normalLetterDelayForWildcard, float elapsedTimeSoFar, int charsRenderedSoFar)
	{
		char c = '%';
		OS oS = (OS)osObj;
		int num = 0;
		float num2 = 0f;
		for (int i = 0; i < wholeText.Length; i++)
		{
			num2 += ((wholeText[i] == '\n') ? (normalLettersDelayForNewline * timePerChar) : ((wholeText[i] == c) ? (normalLetterDelayForWildcard * timePerChar) : timePerChar));
			if (num2 >= elapsedTimeSoFar)
			{
				break;
			}
			num++;
		}
		if (charsRenderedSoFar > num)
		{
			return num;
		}
		for (int i = charsRenderedSoFar; i < num; i++)
		{
			if (wholeText[i] != c)
			{
				if (wholeText[i] == '\n')
				{
					oS.write(" ");
				}
				else
				{
					oS.writeSingle(string.Concat(wholeText[i]));
				}
			}
		}
		return num;
	}
}
