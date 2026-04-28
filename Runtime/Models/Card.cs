namespace CardCore;

public sealed record Card
{
    public int Id { get; }
    public string Name { get; }

    public Card(int Id, string Name)
    {
        if (Id < 0)
            throw new ArgumentException("Card.Id must be >= 0.", nameof(Id));
        if (string.IsNullOrEmpty(Name))
            throw new ArgumentException("Card.Name must be non-empty.", nameof(Name));
        this.Id = Id;
        this.Name = Name;
    }
}
