using System.Threading.Tasks;

namespace PureOrigin.API.Interfaces
{
    public interface IOriginUser
    {
        string Username { get; }
        ulong UserId { get; }
        ulong PersonaId { get; }

        Task<string> GetAvatarUrlAsync(AvatarSizeType sizeType = AvatarSizeType.LARGE);
    }
}
