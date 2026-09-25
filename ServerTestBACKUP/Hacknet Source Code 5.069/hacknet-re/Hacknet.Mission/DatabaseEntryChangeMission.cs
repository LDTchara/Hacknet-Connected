using System;
using System.Collections.Generic;

namespace Hacknet.Mission;

internal class DatabaseEntryChangeMission : MisisonGoal
{
	private Computer c;

	private string Operation;

	private string RecordName;

	private string FieldName;

	private string TargetValue;

	public DatabaseEntryChangeMission(string computerIP, OS os, string operation, string FieldName, string targetValue, string recordName)
	{
		c = Programs.getComputer(os, computerIP);
		Operation = operation;
		this.FieldName = FieldName;
		TargetValue = targetValue;
		RecordName = recordName;
	}

	public override bool isComplete(List<string> additionalDetails = null)
	{
		try
		{
			DatabaseDaemon databaseDaemon = (DatabaseDaemon)c.getDaemon(typeof(DatabaseDaemon));
			object objectForRecordName = databaseDaemon.GetObjectForRecordName(RecordName);
			if (objectForRecordName != null)
			{
				object valueFromObject = ObjectSerializer.GetValueFromObject(objectForRecordName, FieldName);
				try
				{
					double num = Convert.ToDouble(valueFromObject);
					double num2 = Convert.ToDouble(TargetValue);
					switch (Operation)
					{
					case ">":
					case "greater":
						return num > num2;
					case "<":
					case "less":
						return num < num2;
					case "=":
					case "equals":
						return Math.Abs(num - num2) < 0.0001;
					}
				}
				catch (FormatException)
				{
					if (valueFromObject == null)
					{
						return false;
					}
					return valueFromObject.ToString() == TargetValue;
				}
			}
			return false;
		}
		catch (Exception ex2)
		{
			Utils.AppendToErrorFile(Utils.GenerateReportFromException(ex2));
			return true;
		}
	}

	public override string TestCompletable()
	{
		return "";
	}
}
