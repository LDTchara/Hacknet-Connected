using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hacknet.DLC.MarkovTextGenerator;

public class Corpus
{
	private const string DICT_FILE_TYPE = ".csd";

	private const string BODY_FILE_TYPE = ".csb";

	private const string EMPTY_STRING_KEY_WILDCARD = "\tEMPTY\t";

	public int LearningLength = 3;

	private Dictionary<string, Dictionary<string, int>> data = new Dictionary<string, Dictionary<string, int>>();

	public List<PotentialEntry> GetPotentialEntries(string preceeding)
	{
		List<PotentialEntry> list = new List<PotentialEntry>();
		if (!data.ContainsKey(preceeding))
		{
			return list;
		}
		Dictionary<string, int> dictionary = data[preceeding];
		int num = 0;
		int num2 = 0;
		foreach (KeyValuePair<string, int> item in dictionary)
		{
			list.Add(new PotentialEntry
			{
				word = item.Key,
				weighting = item.Value
			});
			if (item.Value > num)
			{
				num = item.Value;
			}
			num2 += item.Value;
		}
		for (int i = 0; i < list.Count; i++)
		{
			list[i].weighting = list[i].weighting / (double)num2;
		}
		return list;
	}

	public void Serialize(string filename)
	{
		using FileStream stream = File.OpenWrite(filename);
		StreamWriter streamWriter = new StreamWriter(stream);
		foreach (KeyValuePair<string, Dictionary<string, int>> datum in data)
		{
			streamWriter.Write(datum.Key + "##^@%##\r\n");
			try
			{
				foreach (KeyValuePair<string, int> item in datum.Value)
				{
					streamWriter.Write("\t" + item.Key + "\t|\t" + item.Value + "\r\n");
				}
			}
			catch (Exception)
			{
			}
			streamWriter.Write("#*@*@*@*@*@#\r\n\r\n");
		}
		streamWriter.Flush();
		streamWriter.Close();
	}

	public void LearnText(string input)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < LearningLength; i++)
		{
			list.Add(null);
		}
		string[] array = input.Split(Utils.WhitespaceDelim, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string key = ConvertMemoryToString(list);
			Dictionary<string, int> dictionary;
			if (data.ContainsKey(key))
			{
				dictionary = data[key];
			}
			else
			{
				dictionary = new Dictionary<string, int>();
				try
				{
					data.Add(key, dictionary);
				}
				catch (OutOfMemoryException)
				{
					break;
				}
			}
			string text = array[i];
			if (dictionary.ContainsKey(text))
			{
				dictionary[text]++;
			}
			else
			{
				dictionary.Add(text, 1);
			}
			try
			{
				data[key] = dictionary;
			}
			catch (OutOfMemoryException)
			{
				break;
			}
			if (WordEndsWithSentenceEnder(text))
			{
				for (int j = 0; j < LearningLength; j++)
				{
					list[j] = null;
				}
			}
			else
			{
				list.Add(text);
				list.RemoveAt(0);
			}
		}
	}

	private static bool WordEndsWithSentenceEnder(string word)
	{
		string source = ".!?;{}";
		return source.Contains(word[word.Length - 1]);
	}

	private static string ConvertMemoryToString(List<string> memory)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < memory.Count; i++)
		{
			stringBuilder.Append(memory[i]);
			stringBuilder.Append(" ");
		}
		return stringBuilder.ToString().Trim();
	}

	public string GetAnalysisStringFromWordList(List<string> words)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < LearningLength; i++)
		{
			list.Add(words[words.Count - LearningLength + i]);
		}
		return ConvertMemoryToString(list);
	}

	public string GenerateSentence(Action<string> CompleteAction = null)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < LearningLength; i++)
		{
			list.Add(null);
		}
		int num = 0;
		while (num < 1000)
		{
			List<PotentialEntry> potentialEntries = GetPotentialEntries(GetAnalysisStringFromWordList(list));
			double num2 = Utils.random.NextDouble();
			double num3 = 0.0;
			for (int i = 0; i < potentialEntries.Count; i++)
			{
				num3 += potentialEntries[i].weighting;
				if (num2 < num3)
				{
					list.Add(potentialEntries[i].word);
					break;
				}
			}
			num++;
			if (WordEndsWithSentenceEnder(list[list.Count - 1]))
			{
				string text = "";
				for (int i = 0; i < list.Count; i++)
				{
					text = text + list[i] + " ";
				}
				text = text.Trim();
				CompleteAction?.Invoke(text);
				return text;
			}
		}
		CompleteAction?.Invoke(null);
		return null;
	}

	public void GenerateSentenceThreaded(Action<string> complete)
	{
		Task.Factory.StartNew(() => GenerateSentence(complete));
	}
}
