using Notio.Http.Attributes;
using Notio.Http.Core;
using Notio.Http.Enums;
using System.Threading.Tasks;


namespace Notio.Application.Main;

[ApiController]
internal class WebApi : HttpController
{
    [Route("/api", HttpMethod.Get)]
    public static async Task<HttpResponse> GetMessage(HttpContext _)
        => await Ok("Hello, world! con heo");
}