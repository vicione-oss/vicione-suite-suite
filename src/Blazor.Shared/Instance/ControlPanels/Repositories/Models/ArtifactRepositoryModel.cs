using Core.Shared.Instance.Contracts;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Models;

public sealed class ArtifactRepositoryModel
{
    public Guid Id { get; }
    public string Endpoint { get; set; }
    public string? Name { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? TokenEndpoint { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset? Modified { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTimeOffset? TokenValidUntil { get; set; }

    public bool IsTokenInvalid => Enabled && TokenValidUntil.HasValue && TokenValidUntil.Value < DateTimeOffset.UtcNow;

    public bool IsEnabledAndOk => Enabled && (!TokenValidUntil.HasValue || TokenValidUntil.Value > DateTimeOffset.UtcNow);

    public ArtifactRepositoryModel(ArtifactRepository source)
    {
        Id = source.Id;
        Endpoint = source.Endpoint;

        Update(source);
    }

    public ArtifactRepository ToEntity()
    {
        return new()
        {
            Id = Id,
            Enabled = Enabled,
            Modified = Modified,
            ModifiedBy = ModifiedBy,
            Name = Name,
            Endpoint = Endpoint,
            UserName = UserName,
            Password = Password,
            TokenEndpoint = TokenEndpoint,
        };
    }

    public void Update(ArtifactRepository source)
    {
        if (Id != source.Id)
            throw new InvalidOperationException("Cannot update repository source with a source having a different id.");

        Endpoint = source.Endpoint;
        Name = source.Name;
        UserName = source.UserName;
        Password = source.Password;
        Enabled = source.Enabled;
        Modified = source.Modified;
        ModifiedBy = source.ModifiedBy;
        TokenEndpoint = source.TokenEndpoint;
        TokenValidUntil = source.TokenValidUntil;
    }
}
