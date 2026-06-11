namespace Core.Shared.UserManagement.Contracts;

public sealed class ExternalIdProvider
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
    
    public required string Authority { get; set; }
    
    public required string ClientId { get; set; }
    
    public string? ClientSecret { get; set; }
}
