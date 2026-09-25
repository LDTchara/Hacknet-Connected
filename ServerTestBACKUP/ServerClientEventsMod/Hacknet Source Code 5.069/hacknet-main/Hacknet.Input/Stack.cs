using System;

namespace Hacknet.Input;

public class Stack<T>
{
	private T[] stack;

	public int Capacity => stack.Length;

	public int Count { get; private set; }

	public Stack()
		: this(32)
	{
	}

	public Stack(int capacity)
	{
		if (capacity < 0)
		{
			capacity = 0;
		}
		stack = new T[capacity];
	}

	public void Push(ref T item)
	{
		if (Count == stack.Length)
		{
			T[] destinationArray = new T[stack.Length << 1];
			Array.Copy(stack, 0, destinationArray, 0, stack.Length);
			stack = destinationArray;
		}
		stack[Count] = item;
		Count++;
	}

	public void Pop(out T item)
	{
		if (Count <= 0)
		{
			throw new InvalidOperationException();
		}
		item = stack[Count];
		stack[Count] = default(T);
		Count--;
	}

	public void PopSegment(out ArraySegment<T> segment)
	{
		segment = new ArraySegment<T>(stack, 0, Count);
		Count = 0;
	}
}
