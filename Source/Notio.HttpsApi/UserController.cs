using Notio.Network.Http.Attributes;
using Notio.Network.Http;
using System.Threading.Tasks;
using Notio.Common.Model;

namespace Notio.HttpsApi;

[ApiController]
public class UserController : HttpController
{
    [HttpRoute("/api/tets", "GET")]
    public static async Task<HttpResult> GetUsers(HttpContext context)
    {
        var users = new[] { new { Id = 1, Name = "Test" } };
        return await Ok(users);
    }

    [HttpRoute("/api/tets", "POST")]
    public static async Task<HttpResult> CreateUser(HttpContext context)
    {
        // Xử lý tạo user
        return await Ok(new { Id = 1 });
    }
}
