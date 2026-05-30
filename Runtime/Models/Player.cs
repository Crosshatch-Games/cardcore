using System;

namespace CardCore;

public sealed class Player
{
    public int Id { get; }
    public Hand Hand { get; }
    public DiscardPile DiscardPile { get; }

    public Player(int id) : this(id, new Hand(), new DiscardPile()) { }

    [Newtonsoft.Json.JsonConstructor]
    internal Player(int id, Hand hand, DiscardPile? discardPile)
    {
        if (id < 0) throw new ArgumentException("Player.Id must be >= 0.", nameof(id));
        Id = id;
        Hand = hand ?? new Hand();
        DiscardPile = discardPile ?? new DiscardPile();
    }
}
