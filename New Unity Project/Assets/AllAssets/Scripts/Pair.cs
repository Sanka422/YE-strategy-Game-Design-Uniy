using System;


public class Pair<S, T> {
	private S first;
	private T second;

	public Pair (S first, T second) {
		this.first = first;
		this.second = second;
	}

	public S First { get { return this.first; } }

	public T Second { get { return this.second; } }
}

