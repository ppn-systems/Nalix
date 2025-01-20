using Notio.Http.Attributes;
using Notio.Http.Core;
using Notio.Http.Enums;
using System.Threading.Tasks;


namespace Notio.Application.Main.Controller;

[ApiController]
internal class MainController : HttpController
{
    [Route("/api", HttpMethod.GET)]
    public static async Task<HttpResponse> HelloWord(HttpContext _) => await Ok("Hello, world!");
}