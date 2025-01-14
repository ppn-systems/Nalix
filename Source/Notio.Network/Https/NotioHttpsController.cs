using System.Threading.Tasks;

namespace Notio.Network.Https;

public abstract class NotioHttpsController
{
    protected static Task<ApiResponse> Ok(object data)
        => Task.FromResult(new ApiResponse { Data = data });

    protected static Task<ApiResponse> Error(string message)
        => Task.FromResult(new ApiResponse { Error = message });
}