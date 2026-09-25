using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hacknet.DLC.MarkovTextGenerator;

public static class CorpusGenerator
{
	public static Corpus GenerateCorpusFromFolder(string name, int maxFilesToRead = -1, Action<float, string> percentCompleteUpdated = null, Action<Corpus> Complete = null)
	{
		try
		{
			Corpus corpus = new Corpus();
			DirectoryInfo directoryInfo = new DirectoryInfo(name + "/");
			string[] extensions = new string[2] { ".txt", ".cs" };
			FileInfo[] array = (from f in directoryInfo.GetFiles()
				where extensions.Contains(f.Extension.ToLower())
				select f).ToArray();
			for (int i = 0; i < array.Length && (maxFilesToRead == -1 || i < maxFilesToRead); i++)
			{
				float num = (float)i / (float)array.Length;
				num *= 100f;
				if (maxFilesToRead != -1)
				{
					num = (float)i / (float)maxFilesToRead;
				}
				string input = File.ReadAllText(name + "/" + array[i].Name);
				percentCompleteUpdated?.Invoke(num, num.ToString("00.00") + "% | Reading " + array[i].Name + "...");
				corpus.LearnText(input);
			}
			Complete?.Invoke(corpus);
			return corpus;
		}
		catch (Exception)
		{
			Complete?.Invoke(null);
			return null;
		}
	}

	public static void GenerateCorpusFromFolderThreaded(string foldername, Action<Corpus> Complete, Action<float, string> percentCompleteUpdated)
	{
		Task.Factory.StartNew(() => GenerateCorpusFromFolder(foldername, -1, percentCompleteUpdated, Complete));
	}
}
