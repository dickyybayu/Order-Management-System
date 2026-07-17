using UserEntity = global::OMS.API.Models.User;
namespace OMS.API.Domain.Auth.Token;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(UserEntity user, string roleName);
}
