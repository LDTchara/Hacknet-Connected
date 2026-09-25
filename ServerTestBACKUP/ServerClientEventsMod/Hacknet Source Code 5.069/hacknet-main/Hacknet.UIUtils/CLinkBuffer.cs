namespace Hacknet.UIUtils;

public class CLinkBuffer<T>
{
	private T[] data;

	private int currentIndex = 0;

	public CLinkBuffer(int BufferSize = 128)
	{
		data = new T[BufferSize];
	}

	public T Get(int offset)
	{
		int i;
		for (i = currentIndex + offset; i < 0; i += data.Length)
		{
		}
		while (i >= data.Length)
		{
			i -= data.Length;
		}
		return data[i];
	}

	public void Add(T added)
	{
		currentIndex = NextIndex();
		data[currentIndex] = added;
	}

	public void AddOneAhead(T added)
	{
		data[NextIndex()] = added;
	}

	private int NextIndex()
	{
		int num = currentIndex + 1;
		if (num >= data.Length)
		{
			num = 0;
		}
		return num;
	}
}
