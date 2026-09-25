using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Hacknet;

public class Firewall
{
	private const int MIN_SOLUTION_LENGTH = 6;

	private const int OUTPUT_LINE_WIDTH = 20;

	private const int CHARS_SOLVED_PER_PASS = 3;

	private const string SOLVED_CHAR = "0";

	private int solutionLength = 6;

	private string solution;

	public bool solved = false;

	private int complexity = 1;

	private int analysisPasses = 0;

	private float additionalDelay = 0f;

	public Firewall()
	{
		generateRandomSolution();
	}

	public Firewall(int complexity)
	{
		this.complexity = complexity;
		solutionLength = 6 + complexity;
		generateRandomSolution();
	}

	public Firewall(int complexity, string solution)
	{
		this.complexity = complexity;
		this.solution = solution;
		solutionLength = solution.Length;
	}

	public Firewall(int complexity, string solution, float additionalTime)
	{
		this.complexity = complexity;
		this.solution = solution;
		additionalDelay = additionalTime;
		solutionLength = solution.Length;
	}

	private void generateRandomSolution()
	{
		StringBuilder stringBuilder = new StringBuilder(solutionLength);
		for (int i = 0; i < solutionLength; i++)
		{
			stringBuilder.Append(Utils.getRandomChar());
		}
		solution = stringBuilder.ToString().ToUpperInvariant();
	}

	public static Firewall load(XmlReader reader)
	{
		while (reader.Name != "firewall")
		{
			reader.Read();
		}
		int num = 0;
		string text = null;
		float additionalTime = 0f;
		if (reader.MoveToAttribute("complexity"))
		{
			num = reader.ReadContentAsInt();
		}
		if (reader.MoveToAttribute("solution"))
		{
			text = reader.ReadContentAsString();
		}
		if (reader.MoveToAttribute("additionalDelay"))
		{
			additionalTime = reader.ReadContentAsFloat();
		}
		return new Firewall(num, text, additionalTime);
	}

	public string getSaveString()
	{
		return "<firewall complexity=\"" + complexity + "\" solution=\"" + solution + "\" additionalDelay=\"" + additionalDelay.ToString(CultureInfo.InvariantCulture) + "\" />";
	}

	public void resetSolutionProgress()
	{
		analysisPasses = 0;
	}

	public bool attemptSolve(string attempt, object os)
	{
		if (attempt.Length != solution.Length)
		{
			string text = ((attempt.Length < solution.Length) ? LocaleTerms.Loc("Too few characters") : LocaleTerms.Loc("Too many characters"));
			((OS)os).write(LocaleTerms.Loc("Solution Incorrect Length") + " - " + text);
		}
		else if (attempt.ToLower().Equals(solution.ToLower()))
		{
			solved = true;
			return true;
		}
		return false;
	}

	public void writeAnalyzePass(object os_object, object target_object)
	{
		Computer computer = (Computer)target_object;
		OS oS = (OS)os_object;
		if (computer.firewallAnalysisInProgress)
		{
			oS.write("-" + LocaleTerms.Loc("Analysis already in Progress") + "-");
			return;
		}
		oS.delayer.PostAnimation(generateOutputPass(analysisPasses, oS, computer));
		analysisPasses++;
	}

	private IEnumerator<ActionDelayer.Condition> generateOutputPass(int pass, OS os, Computer target)
	{
		target.firewallAnalysisInProgress = true;
		os.write(string.Format(LocaleTerms.Loc("Firewall Analysis Pass {0}"), analysisPasses) + "\n");
		yield return ActionDelayer.Wait(0.03);
		os.write("--------------------");
		yield return ActionDelayer.Wait(0.03);
		string preceedeString = "     ";
		double secondsDelayPerLine = 0.08 + 0.06 * (double)pass + (double)additionalDelay;
		for (int i = 0; i < solutionLength; i++)
		{
			os.write(preceedeString + generateOutputLine(i));
			yield return ActionDelayer.Wait(secondsDelayPerLine);
		}
		os.write("--------------------\n");
		target.firewallAnalysisInProgress = false;
	}

	private string generateOutputLine(int location)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < 20; i++)
		{
			stringBuilder.Append("0");
		}
		int num = 20 - 3 * analysisPasses;
		for (int i = 0; i < num; i++)
		{
			stringBuilder[i] = string.Concat(Utils.getRandomChar()).ToLower()[0];
		}
		int index = Utils.random.Next(stringBuilder.Length);
		if (location < solution.Length)
		{
			stringBuilder[index] = solution[location];
		}
		for (int i = 0; i < stringBuilder.Length; i += 2)
		{
			stringBuilder.Insert(i, " ");
		}
		return stringBuilder.ToString();
	}

	public override bool Equals(object obj)
	{
		if (obj is Firewall firewall)
		{
			return firewall.additionalDelay == additionalDelay && firewall.complexity == complexity && firewall.solution == solution;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}

	public override string ToString()
	{
		return "Firewall: solution\"" + solution + "\" - time:" + additionalDelay + " - complexity:" + complexity;
	}
}
