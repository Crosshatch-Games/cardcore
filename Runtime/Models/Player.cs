namespace CardCore;

public sealed class Player
{
    public int Id { get; }
    public Hand Hand { get; }

    public Player(int id) : this(id, new Hand()) { }

    [System.Text.Json.Serialization.JsonConstructor]
    internal Player(int id, Hand hand)
    {
        if (id < 0) throw new ArgumentException("Player.Id must be >= 0.", nameof(id));
        Id = id;
        Hand = hand ?? new Hand();
    }
}
