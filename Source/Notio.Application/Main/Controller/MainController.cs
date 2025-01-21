using Notio.Http;
using Notio.Http.Attributes;
using Notio.Http.Core;
using System.Net.Http;
using System.Threading.Tasks;


namespace Notio.Application.Main.Controller;

[ApiController]
internal class MainController
{
    [Route("/api", HttpMethodType.GET)]
    public static async Task HelloWord(HttpContext context) 
    {
        object response = new
        {
            StatusCode = 200,
            Message = "Hello, World!"
        };

        await context.Response.WriteJsonResponseAsync(response);
    }
}
