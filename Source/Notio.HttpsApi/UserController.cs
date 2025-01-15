using Notio.Network.Https.Attributes;
using Notio.Network.Https.Model;
using Notio.Network.Https;
using System.Threading.Tasks;

namespace Notio.HttpsApi;

[ApiController]
public class UserController : NotioHttpsController
{
    [HttpsRoute("/api/tets", "GET")]
    public static async Task<ApiResponse> GetUsers(NotioHttpsContext context)
    {
        var users = new[] { new { Id = 1, Name = "Test" } };
        return await Ok(users);
    }

    [HttpsRoute("/api/tets", "POST")]
    public static async Task<ApiResponse> CreateUser(NotioHttpsContext context)
    {
        // Xử lý tạo user
        return await Ok(new { Id = 1 });
    }
}
